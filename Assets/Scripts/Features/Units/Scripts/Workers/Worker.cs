using Assets.Scripts.Buildings;
using Assets.Scripts.Core;
using Assets.Scripts.Core.Services;
using Assets.Scripts.NPCs.States;
using Assets.Scripts.NPCs.States.Building;
using Assets.Scripts.NPCs.States.Crafting;
using Assets.Scripts.Resources;
using Assets.Scripts.Shared.Enums;
using Assets.Scripts.Shared.Interfaces;
using UnityEngine;

namespace Assets.Scripts.NPCs.Units
{
    /// <summary>
    /// Worker unit capable of gathering resources, depositing them, and constructing buildings.
    /// Uses a state machine with individual states for each behavior.
    /// 
    /// Building Construction Flow:
    /// ClaimSlotState → GatherBuildResourcesState → DeliverBuildResourcesState → 
    /// WaitForCraftState → PickUpCraftedPartState → DeliverPartState → PlacePartState
    /// </summary>
    [RequireComponent(typeof(WorkerInventory))]
    public class Worker : NPCBase
    {
        [Header("Gathering Settings")]
        [SerializeField] private float interactionDistance = 2f;

        private WorkerInventory inventory;
        private Resource currentResource;
        private BuildingBlueprint currentBlueprint;

        /// <summary>
        /// Per-worker building context.
        /// This replaces the static context to support multiple workers building simultaneously.
        /// </summary>
        public BuildingWorkContext BuildingContext { get; } = new();

        /// <summary>
        /// Per-worker crafting context.
        /// Persists across state transitions to prevent duplicate craft requests.
        /// </summary>
        public CraftingWorkContext CraftingContext { get; } = new();

        #region Properties

        public int CarriedAmount => inventory.Amount;
        public int CarryCapacity => inventory.Capacity;
        public EResourceType CarriedType => inventory.ResourceType;
        public bool IsInventoryFull => inventory.IsFull;
        public Transform CarryPoint => inventory.CarryPoint;
        
        /// <summary>
        /// Direct access to the worker's inventory component.
        /// </summary>
        public WorkerInventory Inventory => inventory;

        // Building properties
        public BuildingBlueprint CurrentBlueprint => currentBlueprint;

        #endregion

        #region Lifecycle

        protected override void Awake()
        {
            base.Awake();
            npcName = "Worker";
            inventory = GetComponent<WorkerInventory>();
        }

        protected override void InitializeStateMachine()
        {
            stateMachine = new StateMachine();
            stateMachine.AddState(new WorkerIdleState(this));
            stateMachine.AddState(new MoveToTargetState(this, stateMachine, interactionDistance));
            stateMachine.AddState(new GatheringState(this, stateMachine));
            stateMachine.AddState(new PickUpFragmentState(this, stateMachine));
            stateMachine.AddState(new DepositingState(this, stateMachine));
            stateMachine.AddState(new ClaimSlotState(this, stateMachine));
            stateMachine.AddState(new GatherBuildResourcesState(this, stateMachine));
            stateMachine.AddState(new DeliverBuildResourcesState(this, stateMachine));
            stateMachine.AddState(new WaitForCraftState(this, stateMachine));
            stateMachine.AddState(new PickUpCraftedPartState(this, stateMachine));
            stateMachine.AddState(new DeliverPartState(this, stateMachine));
            stateMachine.AddState(new PlacePartState(this, stateMachine));
            stateMachine.SetState<WorkerIdleState>();
        }

        #endregion

        #region Interaction

        /// <summary>
        /// Handle interaction with a target object.
        /// Delegates to IInteractable interface when available.
        /// </summary>
        public override void InteractWith(GameObject target)
        {
            // Try IInteractable interface first (most efficient)
            if (target.TryGetComponent<IInteractable>(out var interactable))
            {
                if (interactable.CanInteract(this))
                {
                    interactable.OnInteract(this);
                    return;
                }
                else
                {
                    DebugManager.LogInventory($"Worker: Cannot interact with {target.name}");
                    return;
                }
            }

            // Fallback: check parent for Resource (for clicking on child pieces)
            var resource = target.GetComponentInParent<Resource>();
            if (resource != null && !resource.IsDepleted)
            {
                GatherFrom(resource);
                return;
            }

            base.InteractWith(target);
        }

        #endregion

        #region Movement Helper

        /// <summary>
        /// Move to a destination and execute callback on arrival.
        /// </summary>
        private void MoveToThen(Vector3 destination, System.Action onArrived)
        {
            var moveState = stateMachine.GetState<MoveToTargetState>();
            moveState.SetTarget(destination, onArrived);
            stateMachine.SetState<MoveToTargetState>();
        }

        #endregion

        #region Gathering

        /// <summary>
        /// Start gathering from a resource.
        /// </summary>
        public void GatherFrom(Resource resource)
        {
            if (resource == null || resource.IsDepleted)
            {
                DebugManager.LogGathering("Worker: Cannot gather from null or depleted resource");
                return;
            }

            // Stop any building activity
            StopBuilding();

            currentResource = resource;

            var gatherState = stateMachine.GetState<GatheringState>();
            gatherState.SetTarget(resource);

            // Get the appropriate work position (piece position for yielding stages)
            Vector3 workPosition = resource.GetWorkPositionFor(transform);

            MoveToThen(workPosition, () => stateMachine.SetState<GatheringState>());
        }

        /// <summary>
        /// Pick up a resource fragment.
        /// </summary>
        public void PickUpFragment(ResourceFragment fragment, Resource sourceResource)
        {
            if (fragment == null) return;

            if (sourceResource == null) sourceResource = currentResource;

            var pickupState = stateMachine.GetState<PickUpFragmentState>();
            pickupState.SetTarget(fragment, sourceResource);

            stateMachine.SetState<PickUpFragmentState>();
        }

        #endregion

        #region Depositing

        /// <summary>
        /// Deposit resources at a specific storage point.
        /// </summary>
        public void DepositAt(StoragePoint storage)
        {
            if (storage == null || inventory.IsEmpty)
            {
                DebugManager.LogInventory("Worker: Cannot deposit - no storage or nothing to deposit");
                return;
            }

            // Clear current resource since we're manually depositing
            currentResource = null;

            var depositState = stateMachine.GetState<DepositingState>();
            depositState.SetLastResource(null); // Don't go back to resource after manual deposit
            depositState.SetStoragePoint(storage);

            MoveToThen(storage.transform.position, () => stateMachine.SetState<DepositingState>());

            DebugManager.LogInventory($"Worker: Moving to deposit {inventory.Amount} {inventory.ResourceType} at {storage.name}");
        }

        /// <summary>
        /// Return to nearest storage point to deposit carried resources.
        /// Uses owner-filtered storage finder to ensure workers deposit to their player's storage.
        /// </summary>
        public void ReturnToStorage()
        {
            // Find storage owned by this worker's player
            StoragePoint storage = StoragePoint.FindNearest(transform.position, inventory.ResourceType, ownerPlayerId);

            if (storage == null)
            {
                DebugManager.LogWarning($"No storage point found for {inventory.ResourceType} owned by player {ownerPlayerId}!");
                stateMachine.SetState<WorkerIdleState>();
                return;
            }

            var depositState = stateMachine.GetState<DepositingState>();
            depositState.SetLastResource(currentResource);
            depositState.SetStoragePoint(storage);

            MoveToThen(storage.transform.position, () => stateMachine.SetState<DepositingState>());
        }

        /// <summary>
        /// Handle resource depletion - either return to storage or go idle.
        /// Called by states when a resource becomes unavailable.
        /// </summary>
        public void HandleResourceDepletedOrGone(Resource resource)
        {
            if (resource != null && !resource.IsDepleted)
            {
                GatherFrom(resource);
            }
            else if (inventory.Amount > 0)
            {
                ReturnToStorage();
            }
            else
            {
                stateMachine.SetState<WorkerIdleState>();
            }
        }

        #endregion

        #region Building

        /// <summary>
        /// Start building at a blueprint.
        /// Uses the new individual building states (ClaimSlotState as entry point).
        /// </summary>
        public void StartBuilding(BuildingBlueprint blueprint)
        {
            if (blueprint == null || blueprint.IsComplete)
            {
                DebugManager.LogWarning("Worker: Cannot build - invalid blueprint");
                return;
            }

            // Clean up any existing building state first
            // This ensures we don't carry over parts or context from a previous interrupted task
            if (IsBuilding || BuildingContext.CurrentPart != null)
            {
                StopBuilding();
            }

            currentBlueprint = blueprint;

            // Get the ClaimSlotState and set the blueprint context
            var claimState = stateMachine.GetState<ClaimSlotState>();
            claimState.SetBlueprint(blueprint);

            // Move to blueprint, then start the building state chain
            var moveState = stateMachine.GetState<MoveToTargetState>();
            moveState.SetTarget(blueprint.transform.position, () =>
            {
                // Entry point to the building state chain
                stateMachine.SetState<ClaimSlotState>();
            });

            stateMachine.SetState<MoveToTargetState>();

            DebugManager.LogState($"Worker: Starting construction of {blueprint.BuildingName}");
        }

        /// <summary>
        /// Stop current building task.
        /// </summary>
        /// <param name="dropPart">If true (default), drops any carried part for recovery by other workers.
        /// If false, the worker keeps the part (useful when player moves worker mid-task).</param>
        public void StopBuilding(bool dropPart = true)
        {
            if (currentBlueprint != null)
            {
                // Register dropped part with blueprint for recovery before unassigning
                if (dropPart && BuildingContext.CurrentPart != null && BuildingContext.CurrentSlotIndex >= 0)
                {
                    currentBlueprint.RegisterDroppedPart(BuildingContext.CurrentSlotIndex, BuildingContext.CurrentPart);
                }
                
                currentBlueprint.UnassignWorker(this);
                currentBlueprint = null;
            }

            // Drop any carried part before clearing context
            if (BuildingContext.CurrentPart != null && dropPart)
            {
                BuildingContext.CurrentPart.Drop();
            }

            // Clear per-worker building and crafting contexts
            if (dropPart)
            {
                BuildingContext.Clear();
            }
            else
            {
                // Keep the part but clear assignment info
                var part = BuildingContext.CurrentPart;
                BuildingContext.Clear();
                BuildingContext.CurrentPart = part; // Restore part reference
            }
            CraftingContext.Clear();

            // Check if we're in any of the building states
            if (IsBuilding)
            {
                stateMachine.SetState<WorkerIdleState>();
            }
        }

        /// <summary>
        /// Explicitly drop any carried part.
        /// Useful for player-commanded drop when they want the worker to leave a part behind.
        /// </summary>
        public void DropCarriedPart()
        {
            if (BuildingContext.CurrentPart != null)
            {
                var part = BuildingContext.CurrentPart;
                var blueprint = BuildingContext.CurrentBlueprint ?? currentBlueprint;
                var slotIndex = BuildingContext.CurrentSlotIndex;

                // Register with blueprint for recovery if we know the target
                if (blueprint != null && slotIndex >= 0)
                {
                    blueprint.RegisterDroppedPart(slotIndex, part);
                }

                part.Drop();
                BuildingContext.CurrentPart = null;
                
                DebugManager.LogState("Worker: Dropped carried part");
            }
        }

        /// <summary>
        /// Returns true if the worker is currently in any building-related state.
        /// Uses O(1) category check instead of O(7) type checks.
        /// </summary>
        public bool IsBuilding => stateMachine.CurrentCategory == StateCategory.Building;

        #endregion

        #region Inventory Management

        /// <summary>
        /// Add a fragment to the worker's inventory with visual attachment.
        /// </summary>
        public void AddToInventory(ResourceFragment fragment)
        {
            inventory.Add(fragment);
        }

        /// <summary>
        /// Add resources to inventory without a physical fragment.
        /// </summary>
        public void AddToInventory(EResourceType type, int amount)
        {
            inventory.Add(type, amount);
        }

        /// <summary>
        /// Clear inventory with drop animation (for depositing).
        /// </summary>
        public void ClearInventory()
        {
            inventory.Clear();
        }

        /// <summary>
        /// Get the number of visual fragments being carried.
        /// </summary>
        public int GetFragmentCount() => inventory.FragmentCount;

        #endregion

        #region Movement

        /// <summary>
        /// Move to a destination, cancelling any current task.
        /// If the worker is carrying a part, they keep it (smart dropping).
        /// </summary>
        public override void MoveTo(Vector3 destination)
        {
            currentResource = null;
            // When player moves worker, keep any carried parts (they can drop explicitly if needed)
            StopBuilding(dropPart: false);
            MoveToThen(destination, () => stateMachine.SetState<WorkerIdleState>());
        }

        #endregion
    }
}