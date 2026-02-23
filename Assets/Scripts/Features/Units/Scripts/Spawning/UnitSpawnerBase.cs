// ============================================================================
// UnitSpawnerBase.cs - Abstract base class for unit spawners
// ============================================================================
using System;
using System.Collections.Generic;
using Assets.Scripts.Core.Events;
using Assets.Scripts.Economy;
using Assets.Scripts.Economy.Events;
using Assets.Scripts.Events;
using Assets.Scripts.Resources;
using Assets.Scripts.Shared.Enums;
using UnityEngine;
using UnityEngine.AI;

namespace Assets.Scripts.NPCs.Spawning
{
    /// <summary>
    /// Abstract base class for unit spawners.
    /// 
    /// Responsibilities:
    /// - Shared spawn validation logic
    /// - Resource cost checking and deduction
    /// - Spawn position calculation
    /// - Cooldown management
    /// - Event notifications
    /// 
    /// Subclasses define:
    /// - Which unit types can be spawned
    /// - Any additional spawn requirements
    /// 
    /// Design Notes:
    /// - Attach to building prefabs
    /// - Works with EconomyService for resource management
    /// - Uses NavMesh for spawn position finding
    /// - NO UI logic - only spawning mechanics
    /// </summary>
    public abstract class UnitSpawnerBase : MonoBehaviour, IUnitSpawner
    {
        // ========== Serialized Fields ==========

        [Header("Spawner Settings")]
        [Tooltip("Display name for this spawner in UI")]
        [SerializeField] protected string spawnerName = "Unit Training";

        [Tooltip("Owner player ID (set automatically from building)")]
        [SerializeField] protected int ownerPlayerId = 0;

        [Header("Spawn Position")]
        [Tooltip("Local offset from building for spawn position")]
        [SerializeField] protected Vector3 spawnOffset = new(0f, 0f, 2f);

        [Tooltip("Search radius for valid NavMesh position")]
        [SerializeField] protected float navMeshSearchRadius = 5f;

        [Header("Rally Point")]
        [Tooltip("Optional rally point where spawned units will move to")]
        [SerializeField] protected Transform rallyPoint;

        // ========== Runtime State ==========

        /// <summary>
        /// Cooldowns per unit type (unitId → time when cooldown ends).
        /// </summary>
        protected readonly Dictionary<string, float> cooldowns = new();

        /// <summary>
        /// Whether the spawner is currently active.
        /// </summary>
        protected bool isActive = true;

        // ========== Cached References ==========

        protected EconomyService economyService;

        // ========== IUnitSpawner Implementation ==========

        /// <inheritdoc />
        public virtual bool IsSpawnerActive => isActive && enabled && gameObject.activeInHierarchy;

        /// <inheritdoc />
        public string SpawnerDisplayName => spawnerName;

        /// <inheritdoc />
        public int OwnerPlayerId => ownerPlayerId;

        /// <inheritdoc />
        public event Action OnSpawnableUnitsChanged;

        /// <inheritdoc />
        public abstract IReadOnlyList<SpawnableUnitInfo> GetSpawnableUnits();

        /// <inheritdoc />
        public abstract bool CanSpawnUnit(string unitId, out string reason);

        /// <inheritdoc />
        public SpawnResult TrySpawnUnit(string unitId)
        {
            // Validate spawner state
            if (!IsSpawnerActive)
            {
                return SpawnResult.Failed("Spawner is not active");
            }

            // Check if unit can be spawned
            if (!CanSpawnUnit(unitId, out string reason))
            {
                return SpawnResult.Failed(reason);
            }

            // Get unit definition
            var definition = GetUnitDefinition(unitId);
            if (definition == null || !definition.IsValid)
            {
                return SpawnResult.Failed($"Invalid unit definition: {unitId}");
            }

            // Deduct resources
            if (!TryDeductResources(definition))
            {
                return SpawnResult.Failed("Failed to deduct resources");
            }

            // Calculate spawn position
            Vector3 spawnPos = CalculateSpawnPosition();

            // Instantiate the unit
            var unitObject = Instantiate(definition.prefab, spawnPos, CalculateSpawnRotation());
            var npcBase = unitObject.GetComponent<NPCBase>();

            if (npcBase == null)
            {
                Debug.LogError($"UnitSpawner: Prefab {definition.prefab.name} missing NPCBase component!");
                Destroy(unitObject);
                // Refund resources (best effort)
                RefundResources(definition);
                return SpawnResult.Failed("Unit prefab missing NPCBase component");
            }

            // Initialize the unit
            InitializeSpawnedUnit(npcBase, definition);

            // Start cooldown
            StartCooldown(unitId, definition.spawnCooldown);

            // Publish spawn event
            PublishSpawnEvent(npcBase, definition);

            // Notify listeners
            NotifySpawnableUnitsChanged();

            Debug.Log($"UnitSpawner: Spawned {definition.displayName} at {spawnPos}");

            return SpawnResult.Succeeded(npcBase);
        }

        // ========== Abstract Methods ==========

        /// <summary>
        /// Get the unit definition for a given unit ID.
        /// </summary>
        protected abstract UnitDefinitionSO GetUnitDefinition(string unitId);

        /// <summary>
        /// Get all unit definitions this spawner can produce.
        /// </summary>
        protected abstract IReadOnlyList<UnitDefinitionSO> GetAllUnitDefinitions();

        // ========== Lifecycle ==========

        protected virtual void Awake()
        {
            // Will be looked up on Start
        }

        protected virtual void Start()
        {
            // Get economy service
            economyService = EconomyService.Instance;

            if (economyService == null)
            {
                Debug.LogWarning($"UnitSpawnerBase: No EconomyService found, resource costs will be ignored");
            }
        }

        protected virtual void OnEnable()
        {
            // Subscribe to resource changes to update affordability
            EventBus.Subscribe<ResourceChangedEvent>(HandleResourceChanged);
        }

        protected virtual void OnDisable()
        {
            EventBus.Unsubscribe<ResourceChangedEvent>(HandleResourceChanged);
        }

        protected virtual void Update()
        {
            // Check if any cooldowns have expired
            UpdateCooldowns();
        }

        // ========== Public API ==========

        /// <summary>
        /// Set the owner player ID for this spawner.
        /// </summary>
        public void SetOwner(int playerId)
        {
            ownerPlayerId = playerId;
            NotifySpawnableUnitsChanged();
        }

        /// <summary>
        /// Set whether this spawner is active.
        /// </summary>
        public void SetActive(bool active)
        {
            if (isActive != active)
            {
                isActive = active;
                NotifySpawnableUnitsChanged();
            }
        }

        /// <summary>
        /// Set the rally point for spawned units.
        /// </summary>
        public void SetRallyPoint(Transform point)
        {
            rallyPoint = point;
        }

        // ========== Resource Management ==========

        /// <summary>
        /// Check if the player can afford to spawn a unit.
        /// </summary>
        protected bool CanAffordUnit(UnitDefinitionSO definition)
        {
            if (economyService == null || !definition.HasCosts)
                return true;

            foreach (var cost in definition.spawnCosts)
            {
                int available = economyService.GetAvailable(ownerPlayerId, cost.ResourceType);
                if (available < cost.Amount)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Try to deduct resources for spawning a unit.
        /// Uses the storage point system for proper resource management.
        /// </summary>
        protected bool TryDeductResources(UnitDefinitionSO definition)
        {
            if (economyService == null || !definition.HasCosts)
                return true;

            // First verify we can afford it
            if (!CanAffordUnit(definition))
                return false;

            // Withdraw from storage points
            foreach (var cost in definition.spawnCosts)
            {
                int remaining = cost.Amount;

                // Find storage points with this resource
                var storagePoints = StoragePoint.FindAll(cost.ResourceType, ownerPlayerId);
                
                foreach (var storage in storagePoints)
                {
                    if (remaining <= 0) break;

                    int withdrawn = storage.Withdraw(cost.ResourceType, remaining);
                    remaining -= withdrawn;
                }

                if (remaining > 0)
                {
                    Debug.LogError($"UnitSpawner: Failed to withdraw all {cost.ResourceType}. Needed {cost.Amount}, short by {remaining}");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Refund resources (called if spawn fails after deduction).
        /// Deposits back to available storage points.
        /// </summary>
        protected void RefundResources(UnitDefinitionSO definition)
        {
            if (economyService == null || !definition.HasCosts)
                return;

            foreach (var cost in definition.spawnCosts)
            {
                var storage = StoragePoint.FindAny(cost.ResourceType, ownerPlayerId);
                if (storage != null)
                {
                    storage.Deposit(cost.ResourceType, cost.Amount);
                }
                else
                {
                    Debug.LogWarning($"UnitSpawner: Could not refund {cost.Amount} {cost.ResourceType} - no storage found");
                }
            }
        }

        /// <summary>
        /// Build cost info list with affordability status.
        /// </summary>
        protected List<ResourceCostInfo> BuildCostInfoList(UnitDefinitionSO definition)
        {
            var costs = new List<ResourceCostInfo>();

            foreach (var cost in definition.spawnCosts)
            {
                int currentAmount = economyService?.GetTotal(ownerPlayerId, cost.ResourceType) ?? int.MaxValue;
                bool affordable = currentAmount >= cost.Amount;
                costs.Add(new ResourceCostInfo(cost.ResourceType, cost.Amount, affordable, currentAmount));
            }

            return costs;
        }

        // ========== Cooldown Management ==========

        /// <summary>
        /// Check if a unit type is on cooldown.
        /// </summary>
        protected bool IsOnCooldown(string unitId)
        {
            return cooldowns.TryGetValue(unitId, out float endTime) && Time.time < endTime;
        }

        /// <summary>
        /// Get remaining cooldown time for a unit type.
        /// </summary>
        protected float GetCooldownRemaining(string unitId)
        {
            if (cooldowns.TryGetValue(unitId, out float endTime))
            {
                return Mathf.Max(0f, endTime - Time.time);
            }
            return 0f;
        }

        /// <summary>
        /// Start cooldown for a unit type.
        /// </summary>
        protected void StartCooldown(string unitId, float duration)
        {
            if (duration > 0f)
            {
                cooldowns[unitId] = Time.time + duration;
            }
        }

        /// <summary>
        /// Update cooldowns and notify if any expired.
        /// </summary>
        private void UpdateCooldowns()
        {
            if (cooldowns.Count == 0) return;

            bool anyExpired = false;
            var expiredKeys = new List<string>();

            foreach (var kvp in cooldowns)
            {
                if (Time.time >= kvp.Value)
                {
                    expiredKeys.Add(kvp.Key);
                    anyExpired = true;
                }
            }

            foreach (var key in expiredKeys)
            {
                cooldowns.Remove(key);
            }

            if (anyExpired)
            {
                NotifySpawnableUnitsChanged();
            }
        }

        // ========== Spawn Position ==========

        /// <summary>
        /// Calculate the spawn position for a new unit.
        /// </summary>
        protected virtual Vector3 CalculateSpawnPosition()
        {
            // Start with offset from building
            Vector3 worldOffset = transform.TransformDirection(spawnOffset);
            Vector3 targetPos = transform.position + worldOffset;

            // Find valid NavMesh position
            if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, navMeshSearchRadius, NavMesh.AllAreas))
            {
                return hit.position;
            }

            // Fallback to original position
            Debug.LogWarning($"UnitSpawner: Could not find NavMesh position near {targetPos}, using raw position");
            return targetPos;
        }

        /// <summary>
        /// Calculate the spawn rotation for a new unit.
        /// </summary>
        protected virtual Quaternion CalculateSpawnRotation()
        {
            // Face away from the building by default
            Vector3 awayFromBuilding = (CalculateSpawnPosition() - transform.position).normalized;
            if (awayFromBuilding.sqrMagnitude > 0.01f)
            {
                return Quaternion.LookRotation(awayFromBuilding, Vector3.up);
            }
            return transform.rotation;
        }

        // ========== Unit Initialization ==========

        /// <summary>
        /// Initialize a newly spawned unit.
        /// </summary>
        protected virtual void InitializeSpawnedUnit(NPCBase unit, UnitDefinitionSO definition)
        {
            // Set owner
            unit.SetOwner(ownerPlayerId);

            // If there's a rally point, send unit there
            if (rallyPoint != null)
            {
                unit.Motor?.SetDestination(rallyPoint.position);
            }
        }

        // ========== Event Handling ==========

        private void HandleResourceChanged(ResourceChangedEvent e)
        {
            if (e.PlayerId == ownerPlayerId)
            {
                NotifySpawnableUnitsChanged();
            }
        }

        protected void NotifySpawnableUnitsChanged()
        {
            OnSpawnableUnitsChanged?.Invoke();
        }

        /// <summary>
        /// Publish an event when a unit is spawned.
        /// </summary>
        protected virtual void PublishSpawnEvent(NPCBase unit, UnitDefinitionSO definition)
        {
            EventBus.Publish(new UnitSpawnedEvent
            {
                SpawnedUnit = unit,
                UnitDefinition = definition,
                SpawnerBuilding = gameObject,
                OwnerId = ownerPlayerId,
                SpawnPosition = unit.transform.position
            });
        }
    }
}
