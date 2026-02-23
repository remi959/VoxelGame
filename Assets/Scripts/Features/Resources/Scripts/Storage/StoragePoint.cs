using System.Collections.Generic;
using Assets.Scripts.Core.Events;
using Assets.Scripts.Economy.Events;
using Assets.Scripts.Events;
using Assets.Scripts.NPCs.Units;
using Assets.Scripts.Shared;
using Assets.Scripts.Shared.Enums;
using Assets.Scripts.Shared.Interfaces;
using UnityEngine;

namespace Assets.Scripts.Resources
{
    /// <summary>
    /// StoragePoint holds actual resource amounts that workers can deposit INTO and withdraw FROM.
    /// 
    /// OWNERSHIP MODEL:
    /// - Each storage point belongs to a specific player (ownerId)
    /// - Default ownerId = 0 for backwards compatibility (single-player)
    /// - Workers should only deposit to storage points owned by their player
    /// - ResourceStorageAggregator filters storage points by owner
    /// 
    /// ENABLE/DISABLE:
    /// - Storage points can be disabled to prevent deposits
    /// - Disabled storage retains its resources but workers won't use it
    /// </summary>
    public class StoragePoint : MonoBehaviour, IInteractable, IHoverTarget
    {
        // ========== IHoverTarget Constants ==========
        private const string ACTION_DESTROY = "destroy";
        private const string ACTION_TOGGLE_ENABLED = "toggle_enabled";

        [Header("Ownership")]
        [Tooltip("Player who owns this storage point. Default 0 for single-player.")]
        [SerializeField] private int ownerId = 0;

        [Header("Storage Settings")]
        [SerializeField] private bool acceptAllTypes = false;
        [SerializeField] private bool preAllocateResourceAmounts = false;
        [SerializeField] private int preAllocateAmount = 500;
        [SerializeField] private EResourceType acceptedResourceType;

        [Header("Capacity")]
        [SerializeField] private int maxStoragePerType = 999;

        [Header("State")]
        [SerializeField] private bool isEnabled = true;

        // Actual stored amounts per resource
        private readonly Dictionary<EResourceType, int> stored = new();

        // Cached actions for IHoverTarget
        private readonly List<HoverAction> cachedHoverActions = new();

        /// <summary>
        /// The player who owns this storage point.
        /// Resources deposited here belong to this player.
        /// </summary>
        public int OwnerId => ownerId;

        public bool AcceptsAllTypes => acceptAllTypes;
        public EResourceType AcceptedType => acceptedResourceType;

        /// <summary>
        /// Whether this storage point is enabled for deposits.
        /// </summary>
        public bool IsEnabled => isEnabled;

        #region IHoverTarget Implementation

        /// <inheritdoc />
        public string DisplayName => acceptAllTypes ? "Storage (All Types)" : $"Storage ({acceptedResourceType})";

        /// <inheritdoc />
        public HoverTargetType TargetType => HoverTargetType.StoragePoint;

        /// <inheritdoc />
        public IReadOnlyList<HoverAction> GetAvailableActions()
        {
            cachedHoverActions.Clear();

            // Toggle enabled/disabled
            cachedHoverActions.Add(new HoverAction(
                id: ACTION_TOGGLE_ENABLED,
                label: isEnabled ? "Disable Intake" : "Enable Intake",
                isDestructive: false,
                isEnabled: true,
                tooltip: isEnabled ? "Workers will stop depositing here" : "Workers will deposit here again"
            ));

            // Get total stored for tooltip
            int totalStored = GetTotalStoredAmount();

            // Destroy storage point
            cachedHoverActions.Add(new HoverAction(
                id: ACTION_DESTROY,
                label: "Destroy Storage",
                isDestructive: true,
                isEnabled: true,
                tooltip: totalStored > 0 ? $"Warning: {totalStored} resources will be lost!" : "Remove this storage point"
            ));

            return cachedHoverActions;
        }

        /// <inheritdoc />
        public bool ExecuteAction(string actionId)
        {
            switch (actionId)
            {
                case ACTION_TOGGLE_ENABLED:
                    SetEnabled(!isEnabled);
                    return true;

                case ACTION_DESTROY:
                    DestroyStoragePoint();
                    return true;

                default:
                    Debug.LogWarning($"StoragePoint: Unknown action '{actionId}'");
                    return false;
            }
        }

        #endregion

        #region IInteractable

        public InteractionType InteractionType => InteractionType.Storage;

        public Vector3 GetInteractionPosition(Transform workerTransform)
        {
            return transform.position;
        }

        public bool CanInteract(Worker worker)
        {
            if (worker == null) return false;

            // Only allow interaction when worker is carrying something to deposit
            return worker.CarriedAmount > 0 && AcceptsType(worker.CarriedType);
        }

        public void OnInteract(Worker worker)
        {
            worker.DepositAt(this);
        }

        #endregion

        #region Lifecycle

        private void Awake()
        {
            // Initialize storage dictionary
            foreach (EResourceType type in System.Enum.GetValues(typeof(EResourceType)))
            {
                // Only pre-allocate for resource types this storage actually accepts
                if (preAllocateResourceAmounts && AcceptsType(type))
                    stored[type] = preAllocateAmount;
                else
                    stored[type] = 0;
            }
        }

        private void OnEnable()
        {
            EntityRegistry<StoragePoint>.Register(this);
            
            // Notify aggregator that a new storage point is available (with owner context)
            EventBus.Publish(new StoragePointRegisteredEvent { Storage = this, OwnerId = ownerId });
        }

        private void OnDisable()
        {
            // Notify aggregator before unregistering (with owner context)
            EventBus.Publish(new StoragePointUnregisteredEvent { Storage = this, OwnerId = ownerId });
            
            EntityRegistry<StoragePoint>.Unregister(this);
        }

        #endregion

        #region Static Finders

        /// <summary>
        /// Find the nearest storage point that accepts the given type.
        /// </summary>
        /// <param name="position">World position to search from</param>
        /// <param name="type">Resource type the storage must accept</param>
        /// <param name="ownerId">Optional owner filter. If null, searches all storage points.</param>
        public static StoragePoint FindNearest(Vector3 position, EResourceType type, int? ownerId = null)
        {
            return EntityRegistry<StoragePoint>.FindNearest(position, s => 
                s.AcceptsType(type) && (!ownerId.HasValue || s.OwnerId == ownerId.Value));
        }

        /// <summary>
        /// Find the nearest storage point with distance output.
        /// </summary>
        /// <param name="position">World position to search from</param>
        /// <param name="type">Resource type the storage must accept</param>
        /// <param name="distance">Output: distance to the found storage</param>
        /// <param name="ownerId">Optional owner filter. If null, searches all storage points.</param>
        public static StoragePoint FindNearest(Vector3 position, EResourceType type, out float distance, int? ownerId = null)
        {
            return EntityRegistry<StoragePoint>.FindNearest(position, out distance, s => 
                s.AcceptsType(type) && (!ownerId.HasValue || s.OwnerId == ownerId.Value));
        }

        /// <summary>
        /// Find any storage point that accepts the given type.
        /// </summary>
        /// <param name="type">Resource type the storage must accept</param>
        /// <param name="ownerId">Optional owner filter. If null, searches all storage points.</param>
        public static StoragePoint FindAny(EResourceType type, int? ownerId = null)
        {
            return EntityRegistry<StoragePoint>.FindAny(s => 
                s.AcceptsType(type) && (!ownerId.HasValue || s.OwnerId == ownerId.Value));
        }

        /// <summary>
        /// Find all storage points that accept the given type.
        /// </summary>
        /// <param name="type">Resource type the storage must accept</param>
        /// <param name="ownerId">Optional owner filter. If null, returns all storage points.</param>
        public static List<StoragePoint> FindAll(EResourceType type, int? ownerId = null)
        {
            var list = new List<StoragePoint>();
            foreach (var s in EntityRegistry<StoragePoint>.All)
                if (s != null && s.AcceptsType(type) && (!ownerId.HasValue || s.OwnerId == ownerId.Value))
                    list.Add(s);
            return list;
        }

        /// <summary>
        /// Find all storage points owned by a specific player.
        /// </summary>
        public static List<StoragePoint> FindAllForOwner(int ownerId)
        {
            var list = new List<StoragePoint>();
            foreach (var s in EntityRegistry<StoragePoint>.All)
                if (s != null && s.OwnerId == ownerId)
                    list.Add(s);
            return list;
        }

        #endregion

        #region Storage Logic

        /// <summary>
        /// Check if this storage accepts this resource type.
        /// Also checks if storage is enabled.
        /// </summary>
        public bool AcceptsType(EResourceType type)
        {
            // Don't accept anything if disabled
            if (!isEnabled) return false;
            
            return acceptAllTypes || type == acceptedResourceType;
        }

        /// <summary>
        /// Get the amount currently stored of a resource.
        /// </summary>
        public int GetAmount(EResourceType type)
        {
            return stored.TryGetValue(type, out int amount) ? amount : 0;
        }

        /// <summary>
        /// Add resources to this storage.
        /// Fires StorageChangedEvent for aggregator synchronization.
        /// </summary>
        public void Deposit(EResourceType type, int amount)
        {
            if (!AcceptsType(type))
            {
                Debug.LogWarning($"StoragePoint: Cannot deposit {type}, accepts only {acceptedResourceType}");
                return;
            }

            int oldAmount = GetAmount(type);
            int newAmount = Mathf.Min(oldAmount + amount, maxStoragePerType);
            stored[type] = newAmount;

            Debug.Log($"StoragePoint: Deposited {amount} {type}. Total now: {stored[type]}");

            // Fire the storage changed event for aggregator (includes owner context)
            EventBus.Publish(new StorageChangedEvent
            {
                Storage = this,
                OwnerId = ownerId,
                ResourceType = type,
                OldAmount = oldAmount,
                NewAmount = newAmount
            });

            // Keep legacy event for backwards compatibility (deprecated)
            EventBus.Publish(new ResourceDepositedEvent
            {
                Amount = amount,
                ResourceType = (int)type
            });
        }

        /// <summary>
        /// Withdraw up to 'amount' of the resource from storage.
        /// Worker building logic will use this.
        /// Fires StorageChangedEvent for aggregator synchronization.
        /// </summary>
        public int Withdraw(EResourceType type, int amount)
        {
            if (!stored.TryGetValue(type, out int available))
                return 0;

            int taken = Mathf.Min(available, amount);
            int oldAmount = available;
            int newAmount = available - taken;
            stored[type] = newAmount;

            Debug.Log($"StoragePoint: Worker withdrew {taken} {type}. Remaining: {stored[type]}");

            // Fire storage changed event for aggregator (includes owner context)
            if (taken > 0)
            {
                EventBus.Publish(new StorageChangedEvent
                {
                    Storage = this,
                    OwnerId = ownerId,
                    ResourceType = type,
                    OldAmount = oldAmount,
                    NewAmount = newAmount
                });
            }

            return taken;
        }

        /// <summary>
        /// Get all stored amounts as a dictionary.
        /// Used by aggregator during registration to sync initial values.
        /// </summary>
        public IReadOnlyDictionary<EResourceType, int> GetAllAmounts()
        {
            return stored;
        }

        /// <summary>
        /// Configure this storage before OnEnable fires (for programmatic creation via AddComponent).
        /// Call immediately after AddComponent, before the first frame.
        /// </summary>
        public void Initialize(bool acceptsAllTypes, int ownerIdValue)
        {
            acceptAllTypes = acceptsAllTypes;
            ownerId = ownerIdValue;
        }

        /// <summary>
        /// Set the owner of this storage point at runtime.
        /// Use sparingly - typically ownership is set in the Inspector.
        /// </summary>
        public void SetOwner(int newOwnerId)
        {
            if (ownerId == newOwnerId) return;
            
            int oldOwnerId = ownerId;
            ownerId = newOwnerId;
            
            // Re-publish registration events so aggregators update
            EventBus.Publish(new StoragePointUnregisteredEvent { Storage = this, OwnerId = oldOwnerId });
            EventBus.Publish(new StoragePointRegisteredEvent { Storage = this, OwnerId = newOwnerId });
        }

        /// <summary>
        /// Enable or disable this storage point.
        /// When disabled, workers will not deposit resources here.
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            if (isEnabled == enabled) return;

            isEnabled = enabled;

            Debug.Log($"StoragePoint: {(enabled ? "Enabled" : "Disabled")} {DisplayName}");

            // Notify listeners of state change
            EventBus.Publish(new StoragePointStateChangedEvent
            {
                Storage = this,
                OwnerId = ownerId,
                IsEnabled = enabled
            });
        }

        /// <summary>
        /// Get the total amount of all resources stored.
        /// </summary>
        public int GetTotalStoredAmount()
        {
            int total = 0;
            foreach (var kvp in stored)
            {
                total += kvp.Value;
            }
            return total;
        }

        /// <summary>
        /// Destroy this storage point.
        /// Warning: All stored resources will be lost!
        /// </summary>
        public void DestroyStoragePoint()
        {
            int totalLost = GetTotalStoredAmount();
            
            Debug.Log($"StoragePoint: Destroying {DisplayName} (losing {totalLost} resources)");

            // Publish destruction event before destroying
            EventBus.Publish(new StoragePointDestroyedEvent
            {
                Storage = this,
                OwnerId = ownerId,
                ResourcesLost = totalLost
            });

            // Destroy the game object
            Destroy(gameObject);
        }

        #endregion
    }
}
