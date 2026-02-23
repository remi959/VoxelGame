using System.Collections.Generic;
using Assets.Scripts.Buildings.Construction;
using Assets.Scripts.Core;
using Assets.Scripts.Data;
using Assets.Scripts.NPCs.Units;
using Assets.Scripts.Buildings.Placement;  // For ValidationService.BuildingSpatialHash
using Assets.Scripts.Shared;
using Assets.Scripts.Shared.Enums;
using Assets.Scripts.Shared.Interfaces;
using Assets.Scripts.Shared.Utilities;
using UnityEngine;
using UnityEngine.AI;
using Assets.Scripts.Core.Services;

namespace Assets.Scripts.Buildings
{
    /// <summary>
    /// A building under construction. Shows ghost preview and manages construction progress.
    /// 
    /// Responsibilities:
    /// - Visual ghost rendering for parts
    /// - Part slot management and claiming
    /// - Worker-to-slot assignments
    /// - Completion logic and NavMesh obstacle
    /// - Implements IHoverTarget for contextual UI
    /// 
    /// Note: Worker tracking is unified with BuildingSite when used via ConstructionService.
    /// </summary>
    public class BuildingBlueprint : MonoBehaviour, IInteractable, IHoverTarget
    {
        [Header("Ghost Settings")]
        [SerializeField] private Material ghostMaterial;
        [SerializeField] private Color ghostColor = new(0.5f, 0.5f, 1f, 0.3f);
        
        [Header("Footprint Settings")]
        [Tooltip("Show only a footprint marker instead of ghost building parts")]
        [SerializeField] private bool useFootprintOnly = true;
        [SerializeField] private Color footprintColor = new(0.3f, 0.6f, 1f, 0.4f);
        [SerializeField] private float footprintHeight = 0.05f;
        [SerializeField] private float footprintBorderWidth = 0.1f;
        [SerializeField] private Color footprintBorderColor = new(0.2f, 0.4f, 0.8f, 0.8f);

        [Header("Worker Settings")]
        [SerializeField] private int maxWorkers = 4;

        private BuildingDefinitionSO buildingPrefabSO;
        private List<PartSlot> slots = new();
        private HashSet<int> assignedWorkerIds = new();
        private Dictionary<int, PartSlot> workerSlots = new();
        private Dictionary<PartSlot, GameObject> ghosts = new();

        // Dropped part tracking - parts that were dropped mid-transport can be recovered
        private Dictionary<int, CraftedPart> droppedPartsForSlot = new();

        private int completedCount;
        private bool isComplete;
        private float constructionStartTime;

        // Cached ghost material to avoid per-ghost allocations
        private Material cachedGhostMaterial;
        
        // Footprint visualization
        private GameObject footprintObject;
        private GameObject footprintBorder;
        
        // Reference to BuildingSite data (set by ConstructionService)
        private string linkedSiteId;

        // IHoverTarget constants
        private const string ACTION_CANCEL = "cancel_construction";

        // Cached actions for IHoverTarget
        private readonly List<HoverAction> cachedHoverActions = new();

        public InteractionType InteractionType => InteractionType.Blueprint;
        public BuildingDefinitionSO BuildingPrefabSO => buildingPrefabSO;
        public bool IsComplete => isComplete;
        public int TotalParts => slots.Count;
        public int CompletedParts => completedCount;
        public float Progress => TotalParts > 0 ? (float)completedCount / TotalParts : 0f;
        public bool CanAssignWorker => assignedWorkerIds.Count < maxWorkers && !isComplete;
        public string BuildingName => buildingPrefabSO?.displayName ?? "Building";
        public int AssignedWorkerCount => assignedWorkerIds.Count;
        public IReadOnlyCollection<int> AssignedWorkerIds => assignedWorkerIds;
        public string LinkedSiteId => linkedSiteId;

        #region IHoverTarget Implementation

        /// <inheritdoc />
        public string DisplayName => $"{BuildingName} (Building)";

        /// <inheritdoc />
        public HoverTargetType TargetType => HoverTargetType.BuildingUnderConstruction;

        /// <inheritdoc />
        public IReadOnlyList<HoverAction> GetAvailableActions()
        {
            cachedHoverActions.Clear();

            if (!isComplete)
            {
                // Build tooltip with progress and refund info
                string tooltip = $"Progress: {Progress * 100:F0}%";
                
                cachedHoverActions.Add(new HoverAction(
                    id: ACTION_CANCEL,
                    label: "Cancel Construction",
                    isDestructive: true,
                    isEnabled: true,
                    tooltip: tooltip
                ));
            }

            return cachedHoverActions;
        }

        /// <inheritdoc />
        public bool ExecuteAction(string actionId)
        {
            if (isComplete) return false;

            switch (actionId)
            {
                case ACTION_CANCEL:
                    return CancelConstruction();

                default:
                    Debug.LogWarning($"BuildingBlueprint: Unknown action '{actionId}'");
                    return false;
            }
        }

        /// <summary>
        /// Cancel construction via ConstructionService.
        /// </summary>
        private bool CancelConstruction()
        {
            if (string.IsNullOrEmpty(linkedSiteId))
            {
                Debug.LogWarning("BuildingBlueprint: Cannot cancel - no linked site ID");
                return false;
            }

            var constructionService = ConstructionService.Instance;
            if (constructionService == null)
            {
                Debug.LogError("BuildingBlueprint: ConstructionService not found");
                return false;
            }

            return constructionService.CancelSiteWithRefund(linkedSiteId);
        }

        #endregion

        public class PartSlot
        {
            public int Index;
            public BuildingPart SourcePart;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
            public Vector3 LocalScale;
            public SlotState State;
            public int AssignedWorkerId;
            public CraftedPart PlacedPart;
        }

        public enum SlotState { Pending, InProgress, Complete }

        #region Lifecycle

        private void OnEnable()
        {
            EntityRegistry<BuildingBlueprint>.Register(this);
            
            // Register with spatial hash for efficient distance queries
            ValidationService.BuildingSpatialHash.Insert(this, transform.position);
        }

        private void OnDisable()
        {
            EntityRegistry<BuildingBlueprint>.Unregister(this);
            
            // Remove from spatial hash
            ValidationService.BuildingSpatialHash.Remove(this);
            
            // Clean up cached material
            if (cachedGhostMaterial != null)
            {
                Destroy(cachedGhostMaterial);
                cachedGhostMaterial = null;
            }
            
            // Clean up footprint objects
            if (footprintObject != null)
            {
                Destroy(footprintObject);
                footprintObject = null;
            }
            if (footprintBorder != null)
            {
                Destroy(footprintBorder);
                footprintBorder = null;
            }
        }

        #endregion

        #region Static

        public static BuildingBlueprint FindNearest(Vector3 position)
        {
            return EntityRegistry<BuildingBlueprint>.FindNearest(position, bp => !bp.IsComplete);
        }

        #endregion

        #region Initialize

        public void Initialize(BuildingDefinitionSO prefabSO) 
        {
            if (prefabSO == null || !prefabSO.IsValid)
            {
                DebugManager.LogWarning("BuildingBlueprint: Invalid prefab!");
                return;
            }

            buildingPrefabSO = prefabSO;
            var sortedParts = prefabSO.GetPartsSorted();

            // Create slots
            for (int i = 0; i < sortedParts.Count; i++)
            {
                var part = sortedParts[i];
                slots.Add(new PartSlot
                {
                    Index = i,
                    SourcePart = part,
                    LocalPosition = part.transform.localPosition,
                    LocalRotation = part.transform.localRotation,
                    LocalScale = part.transform.localScale,
                    State = SlotState.Pending
                });
            }

            CreateGhosts();
            CreateFootprint();
            DebugManager.LogState($"BuildingBlueprint: Initialized {BuildingName} with {slots.Count} parts");
        }

        private void CreateFootprint()
        {
            Vector2Int footprint = buildingPrefabSO.Footprint;
            
            // Create main footprint quad
            footprintObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            footprintObject.name = "Footprint";
            footprintObject.transform.SetParent(transform);
            footprintObject.transform.localPosition = new Vector3(0, footprintHeight, 0);
            footprintObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            footprintObject.transform.localScale = new Vector3(footprint.x, footprint.y, 1f);
            footprintObject.layer = gameObject.layer;
            
            // Remove collider from footprint
            var footprintCollider = footprintObject.GetComponent<Collider>();
            if (footprintCollider != null) Destroy(footprintCollider);
            
            // Apply footprint material
            var footprintMat = GhostMaterialFactory.CreateTransparentMaterial(footprintColor);
            footprintObject.GetComponent<Renderer>().material = footprintMat;
            
            // Create border frame using 4 thin quads
            CreateFootprintBorder(footprint);
        }

        private void CreateFootprintBorder(Vector2Int footprint)
        {
            footprintBorder = new GameObject("FootprintBorder");
            footprintBorder.transform.SetParent(transform);
            footprintBorder.transform.localPosition = Vector3.zero;
            footprintBorder.transform.localRotation = Quaternion.identity;
            
            var borderMat = GhostMaterialFactory.CreateTransparentMaterial(footprintBorderColor);
            float halfX = footprint.x / 2f;
            float halfZ = footprint.y / 2f;
            float borderHeight = 0.3f;
            
            // Create 4 border walls
            CreateBorderWall("BorderNorth", new Vector3(0, borderHeight / 2f, halfZ), new Vector3(footprint.x, borderHeight, footprintBorderWidth), borderMat);
            CreateBorderWall("BorderSouth", new Vector3(0, borderHeight / 2f, -halfZ), new Vector3(footprint.x, borderHeight, footprintBorderWidth), borderMat);
            CreateBorderWall("BorderEast", new Vector3(halfX, borderHeight / 2f, 0), new Vector3(footprintBorderWidth, borderHeight, footprint.y), borderMat);
            CreateBorderWall("BorderWest", new Vector3(-halfX, borderHeight / 2f, 0), new Vector3(footprintBorderWidth, borderHeight, footprint.y), borderMat);
        }

        private void CreateBorderWall(string name, Vector3 localPos, Vector3 scale, Material mat)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(footprintBorder.transform);
            wall.transform.localPosition = localPos;
            wall.transform.localScale = scale;
            wall.layer = gameObject.layer;
            
            var collider = wall.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            
            wall.GetComponent<Renderer>().material = mat;
        }

        private void CreateGhosts()
        {
            // Skip ghost creation if using footprint only mode
            if (useFootprintOnly) return;
            
            // Use GhostMaterialFactory for consistent material creation
            if (cachedGhostMaterial == null)
            {
                cachedGhostMaterial = GhostMaterialFactory.CreateGhostMaterial(ghostColor, ghostMaterial);
            }

            foreach (var slot in slots)
            {
                GameObject ghost = Instantiate(slot.SourcePart.gameObject, transform);
                ghost.name = $"Ghost_{slot.Index}";
                ghost.transform.localPosition = slot.LocalPosition;
                ghost.transform.localRotation = slot.LocalRotation;
                ghost.transform.localScale = slot.LocalScale;
                ghost.layer = gameObject.layer;

                // Remove BuildingPart
                var bp = ghost.GetComponent<BuildingPart>();
                if (bp != null) Destroy(bp);

                // Apply shared ghost material using factory utility
                GhostMaterialFactory.ApplyToAllRenderers(ghost, cachedGhostMaterial);

                // Disable colliders
                foreach (var col in ghost.GetComponentsInChildren<Collider>())
                    col.enabled = false;

                ghosts[slot] = ghost;
            }
        }

        #endregion

        #region Site Linking

        /// <summary>
        /// Link this blueprint to a BuildingSite for unified tracking.
        /// Called by ConstructionService when creating the blueprint.
        /// </summary>
        public void LinkToSite(string siteId)
        {
            linkedSiteId = siteId;
        }

        #endregion

        #region Workers

        /// <summary>
        /// Assign a worker to this blueprint.
        /// Uses worker instance ID for tracking to match BuildingSite pattern.
        /// </summary>
        public bool AssignWorker(Worker worker)
        {
            if (worker == null) return false;
            
            int workerId = worker.GetInstanceID();
            
            if (isComplete || assignedWorkerIds.Contains(workerId) || !CanAssignWorker)
                return false;

            assignedWorkerIds.Add(workerId);
            
            // Track construction start time on first worker
            if (assignedWorkerIds.Count == 1)
            {
                constructionStartTime = Time.time;
            }
            
            DebugManager.LogState($"BuildingBlueprint: Worker {workerId} assigned. Total: {assignedWorkerIds.Count}");
            return true;
        }

        /// <summary>
        /// Unassign a worker from this blueprint.
        /// </summary>
        public void UnassignWorker(Worker worker)
        {
            if (worker == null) return;
            
            int workerId = worker.GetInstanceID();
            assignedWorkerIds.Remove(workerId);

            if (workerSlots.TryGetValue(workerId, out var slot))
            {
                if (slot.State == SlotState.InProgress)
                    slot.State = SlotState.Pending;
                slot.AssignedWorkerId = 0;
                workerSlots.Remove(workerId);
            }
        }

        #endregion

        #region Construction

        public PartSlot GetNextAvailableSlot()
        {
            // First, prioritize slots that have dropped parts that can be recovered
            // This ensures workers pick up existing parts instead of crafting duplicates
            foreach (var slot in slots)
            {
                if (slot.State == SlotState.Pending && HasDroppedPartForSlot(slot.Index))
                    return slot;
            }

            // Otherwise return the first pending slot
            foreach (var slot in slots)
            {
                if (slot.State == SlotState.Pending)
                    return slot;
            }
            
            return null;
        }

        public PartSlot ClaimSlot(Worker worker)
        {
            if (worker == null) return null;
            
            int workerId = worker.GetInstanceID();
            
            if (workerSlots.TryGetValue(workerId, out var existing))
                return existing;

            var slot = GetNextAvailableSlot();
            if (slot == null) return null;

            slot.State = SlotState.InProgress;
            slot.AssignedWorkerId = workerId;
            workerSlots[workerId] = slot;

            DebugManager.LogState($"BuildingBlueprint: Slot {slot.Index} claimed by worker {workerId}");
            return slot;
        }

        /// <summary>
        /// Try to claim a specific slot (used when worker returns with a part for that slot).
        /// Returns null if the slot is not available.
        /// </summary>
        public PartSlot TryClaimSpecificSlot(Worker worker, int slotIndex)
        {
            if (worker == null || slotIndex < 0 || slotIndex >= slots.Count)
                return null;
            
            int workerId = worker.GetInstanceID();
            
            // Check if already assigned to a slot
            if (workerSlots.TryGetValue(workerId, out var existing))
            {
                // If it's the same slot, return it
                if (existing.Index == slotIndex)
                    return existing;
                // Otherwise, can't claim a different slot while holding one
                return null;
            }

            var slot = slots[slotIndex];
            
            // Can only claim if pending (not in progress by someone else, not complete)
            if (slot.State != SlotState.Pending)
                return null;

            slot.State = SlotState.InProgress;
            slot.AssignedWorkerId = workerId;
            workerSlots[workerId] = slot;

            DebugManager.LogState($"BuildingBlueprint: Slot {slotIndex} specifically claimed by worker {workerId}");
            return slot;
        }

        public Vector3 GetSlotWorldPosition(PartSlot slot)
        {
            return transform.TransformPoint(slot.LocalPosition);
        }

        public Quaternion GetSlotWorldRotation(PartSlot slot)
        {
            return transform.rotation * slot.LocalRotation;
        }

        public void OnPartPlaced(PartSlot slot, CraftedPart part)
        {
            if (slot.State == SlotState.Complete) return;

            slot.State = SlotState.Complete;
            slot.PlacedPart = part;

            // Hide ghost
            if (ghosts.TryGetValue(slot, out var ghost))
                ghost.SetActive(false);

            // Clear worker assignment
            if (slot.AssignedWorkerId != 0)
            {
                workerSlots.Remove(slot.AssignedWorkerId);
                slot.AssignedWorkerId = 0;
            }

            // Update count
            completedCount = 0;
            foreach (var s in slots)
                if (s.State == SlotState.Complete)
                    completedCount++;

            DebugManager.LogState($"BuildingBlueprint: {completedCount}/{slots.Count} complete");
            
            // Notify ConstructionService of progress if linked
            if (!string.IsNullOrEmpty(linkedSiteId))
            {
                ConstructionService.Instance?.OnPartPlaced(linkedSiteId);
            }

            if (completedCount >= slots.Count)
                CompleteBuilding();
        }

        private void CompleteBuilding()
        {
            isComplete = true;

            // Clean up ghosts
            foreach (var ghostObj in ghosts.Values)
                if (ghostObj != null) Destroy(ghostObj);
            ghosts.Clear();

            // Parent placed parts
            foreach (var slot in slots)
                if (slot.PlacedPart != null)
                    slot.PlacedPart.transform.SetParent(transform);

            // Clear all worker assignments
            assignedWorkerIds.Clear();
            workerSlots.Clear();

            // Add NavMesh obstacle for pathfinding
            AddNavMeshObstacle();

            DebugManager.LogState($"BuildingBlueprint: {BuildingName} complete!");
        }

        /// <summary>
        /// Add NavMeshObstacle to block pathfinding through completed building.
        /// </summary>
        private void AddNavMeshObstacle()
        {
            var settings = BuildingSettingsSO.Instance;
            
            var obstacle = gameObject.AddComponent<NavMeshObstacle>();
            obstacle.carving = true;
            obstacle.carvingMoveThreshold = settings.CarvingMoveThreshold;
            obstacle.carvingTimeToStationary = settings.CarvingTimeToStationary;
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = Vector3.up * (settings.NavMeshObstacleHeight / 2f);
            obstacle.size = new Vector3(
                buildingPrefabSO.Footprint.x,
                settings.NavMeshObstacleHeight,
                buildingPrefabSO.Footprint.y
            );
        }

        #endregion

        #region Dropped Part Tracking

        /// <summary>
        /// Register a dropped part for recovery by another worker.
        /// Called when a worker abandons a task while carrying a part.
        /// </summary>
        public void RegisterDroppedPart(int slotIndex, CraftedPart part)
        {
            if (part == null || slotIndex < 0 || slotIndex >= slots.Count)
            {
                DebugManager.LogWarning($"BuildingBlueprint: Cannot register dropped part - part={part}, slotIndex={slotIndex}");
                return;
            }

            // If there's already a dropped part for this slot, destroy the old one
            // (shouldn't happen in normal flow, but handle it gracefully)
            if (droppedPartsForSlot.TryGetValue(slotIndex, out var existingPart))
            {
                if (existingPart != null && existingPart != part)
                {
                    DebugManager.LogWarning($"BuildingBlueprint: Replacing dropped part for slot {slotIndex}");
                    Destroy(existingPart.gameObject);
                }
            }

            droppedPartsForSlot[slotIndex] = part;
            DebugManager.LogState($"BuildingBlueprint: Registered dropped part for slot {slotIndex} (part IsPickedUp={part.IsPickedUp}, IsPlaced={part.IsPlaced})");
        }

        /// <summary>
        /// Get a dropped part for a specific slot (if one exists and is still valid).
        /// Returns null if no dropped part exists or if the part has been destroyed/placed.
        /// </summary>
        public CraftedPart GetDroppedPartForSlot(int slotIndex)
        {
            if (!droppedPartsForSlot.TryGetValue(slotIndex, out var part))
                return null;

            // Validate the part is still usable
            if (part == null || part.IsPlaced || !part.CanBePickedUp)
            {
                DebugManager.LogState($"BuildingBlueprint: Dropped part for slot {slotIndex} is not usable (null={part == null}, IsPlaced={part?.IsPlaced}, CanBePickedUp={part?.CanBePickedUp})");
                droppedPartsForSlot.Remove(slotIndex);
                return null;
            }

            DebugManager.LogState($"BuildingBlueprint: Found valid dropped part for slot {slotIndex}");
            return part;
        }

        /// <summary>
        /// Clear the dropped part reference for a slot (called when part is picked up).
        /// </summary>
        public void ClearDroppedPart(int slotIndex) 
        {
            droppedPartsForSlot.Remove(slotIndex);
            DebugManager.LogState($"BuildingBlueprint: Cleared dropped part for slot {slotIndex}");
        }

        /// <summary>
        /// Check if there's a recoverable dropped part for a slot.
        /// </summary>
        public bool HasDroppedPartForSlot(int slotIndex)
        {
            return GetDroppedPartForSlot(slotIndex) != null;
        }

        #endregion

        #region Queries

        public Dictionary<EResourceType, int> GetResourcesForNextPart()
        {
            var slot = GetNextAvailableSlot();
            if (slot?.SourcePart == null) return new Dictionary<EResourceType, int>();

            var costs = new Dictionary<EResourceType, int>();
            foreach (var cost in slot.SourcePart.ResourceCosts)
                costs[cost.resourceType] = cost.amount;
            return costs;
        }

        #endregion

        #region IInteractable

        public bool CanInteract(Worker worker)
        {
            // No building when complete
            if (isComplete)
                return false;

            // No assigned worker overflow
            return CanAssignWorker;
        }

        public void OnInteract(Worker worker)
        {
            if (!CanAssignWorker)
            {
                DebugManager.LogState("Blueprint: Cannot assign more workers.");
                return;
            }

            // Assign worker to this blueprint
            if (AssignWorker(worker))
            {
                // Notify ConstructionService so the BuildingSite data model is updated
                // (confirms resource reservation, assigns worker in site, transitions state)
                if (!string.IsNullOrEmpty(linkedSiteId))
                {
                    ConstructionService.Instance?.OnWorkerStartedConstruction(
                        linkedSiteId, worker.GetInstanceID());
                }

                worker.StartBuilding(this);
                DebugManager.LogState($"Blueprint: Worker {worker.name} is now building.");
            }
        }

        public Vector3 GetInteractionPosition(Transform workerTransform)
        {
            // Workers should walk to the closest point around the blueprint
            // You can refine this later (building-specific spots)
            return transform.position;
        }

        #endregion
    }
}