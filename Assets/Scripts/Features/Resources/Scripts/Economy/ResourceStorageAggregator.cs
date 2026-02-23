// ============================================================================
// ResourceStorageAggregator.cs - Aggregates all storage points into player totals
// ============================================================================
using System;
using System.Collections.Generic;
using Assets.Scripts.Core;
using Assets.Scripts.Core.Events;
using Assets.Scripts.Economy.Events;
using Assets.Scripts.Events;
using Assets.Scripts.Resources;
using Assets.Scripts.Shared;
using Assets.Scripts.Shared.Enums;
using UnityEngine;

namespace Assets.Scripts.Economy
{
    /// <summary>
    /// Aggregates resource totals across all storage points for a player.
    /// 
    /// SOURCE OF TRUTH:
    /// - StoragePoints hold the actual resource amounts
    /// - This aggregator calculates totals by summing all storage points
    /// - EconomyService reads from this aggregator for player resource queries
    /// 
    /// RESPONSIBILITIES:
    /// - Listen for StorageChangedEvent from any storage point
    /// - Maintain cached totals per resource type
    /// - Fire StorageTotalsChangedEvent when totals change
    /// - Ensure a "standard" all-types storage point exists
    /// 
    /// LIFECYCLE:
    /// - Created by EconomyService during initialization
    /// - Subscribes to EventBus for storage changes
    /// - Recalculates totals when storage points register/unregister
    /// 
    /// PERSISTENCE:
    /// This is a child component/service of EconomyService.
    /// PersistentServices handles scene persistence for the parent hierarchy.
    /// </summary>
    public class ResourceStorageAggregator : IDisposable
    {
        private readonly int playerId;
        private bool isDisposed;

        // Cached totals per resource type
        private readonly Dictionary<EResourceType, int> cachedTotals = new();

        // Track when we need to recalculate (dirty flag for efficiency)
        private bool needsRecalculation = true;

        // Reference to standard storage prefab (optional - can be set externally)
        private GameObject standardStoragePrefab;

        // The standard storage point instance (if we spawned one)
        private StoragePoint standardStorageInstance;

        /// <summary>
        /// Event fired when aggregated totals change.
        /// EconomyService subscribes to this.
        /// </summary>
        public event Action<EResourceType, int, int> OnTotalsChanged;

        /// <summary>
        /// Creates an aggregator for a specific player.
        /// </summary>
        public ResourceStorageAggregator(int playerId)
        {
            this.playerId = playerId;

            // Initialize cached totals for all resource types
            foreach (EResourceType type in Enum.GetValues(typeof(EResourceType)))
            {
                cachedTotals[type] = 0;
            }

            // Subscribe to storage change events
            EventBus.Subscribe<StorageChangedEvent>(HandleStorageChanged);
            EventBus.Subscribe<StoragePointRegisteredEvent>(HandleStorageRegistered);
            EventBus.Subscribe<StoragePointUnregisteredEvent>(HandleStorageUnregistered);
        }

        /// <summary>
        /// Set the prefab to use when spawning a standard storage point.
        /// If not set, the aggregator will create a basic GameObject with StoragePoint.
        /// </summary>
        public void SetStandardStoragePrefab(GameObject prefab)
        {
            standardStoragePrefab = prefab;
        }

        /// <summary>
        /// Ensures a standard (all-types) storage point exists FOR THIS PLAYER.
        /// Call this after scene load or during initialization.
        /// 
        /// OWNERSHIP: Only checks storage points owned by this player.
        /// </summary>
        public void EnsureStandardStorageExists()
        {
            // Check if any storage point FOR THIS PLAYER accepts all types
            foreach (var storage in EntityRegistry<StoragePoint>.All)
            {
                if (storage != null && storage.OwnerId == playerId && storage.AcceptsAllTypes)
                {
                    Debug.Log($"ResourceStorageAggregator: Found existing standard storage for player {playerId}: {storage.name}");
                    return;
                }
            }

            // No standard storage exists for this player - create one
            SpawnStandardStorage();
        }

        /// <summary>
        /// Spawns a standard storage point that accepts all resource types FOR THIS PLAYER.
        /// </summary>
        private void SpawnStandardStorage()
        {
            if (standardStoragePrefab != null)
            {
                // Use prefab
                var go = UnityEngine.Object.Instantiate(standardStoragePrefab);
                go.name = $"StandardStoragePoint_Player{playerId}";
                standardStorageInstance = go.GetComponent<StoragePoint>();
                
                // Set ownership
                if (standardStorageInstance != null)
                {
                    standardStorageInstance.SetOwner(playerId);
                }
            }
            else
            {
                // Create basic storage point
                var go = new GameObject($"StandardStoragePoint_Player{playerId}");
                standardStorageInstance = go.AddComponent<StoragePoint>();
                standardStorageInstance.Initialize(true, playerId);
            }

            Debug.Log($"ResourceStorageAggregator: Spawned standard storage point for player {playerId}");
        }

        /// <summary>
        /// Get the aggregated total for a resource type.
        /// </summary>
        public int GetTotal(EResourceType type)
        {
            if (needsRecalculation)
            {
                RecalculateAllTotals();
            }

            return cachedTotals.TryGetValue(type, out int total) ? total : 0;
        }

        /// <summary>
        /// Get all aggregated totals as a dictionary.
        /// </summary>
        public IReadOnlyDictionary<EResourceType, int> GetAllTotals()
        {
            if (needsRecalculation)
            {
                RecalculateAllTotals();
            }

            return cachedTotals;
        }

        /// <summary>
        /// Force recalculation of all totals from all storage points.
        /// Called when storage points are added/removed.
        /// 
        /// OWNERSHIP: Only counts storage points owned by this aggregator's player.
        /// </summary>
        public void RecalculateAllTotals()
        {
            // Store old totals for comparison
            var oldTotals = new Dictionary<EResourceType, int>(cachedTotals);

            // Reset all to zero
            foreach (EResourceType type in Enum.GetValues(typeof(EResourceType)))
            {
                cachedTotals[type] = 0;
            }

            // Sum across storage points OWNED BY THIS PLAYER ONLY
            foreach (var storage in EntityRegistry<StoragePoint>.All)
            {
                if (storage == null) continue;
                
                // Skip storage points owned by other players
                if (storage.OwnerId != playerId) continue;

                var amounts = storage.GetAllAmounts();
                foreach (var kvp in amounts)
                {
                    cachedTotals[kvp.Key] += kvp.Value;
                }
            }

            needsRecalculation = false;

            // Fire events for changed totals
            foreach (EResourceType type in Enum.GetValues(typeof(EResourceType)))
            {
                int oldTotal = oldTotals.TryGetValue(type, out int o) ? o : 0;
                int newTotal = cachedTotals[type];

                if (oldTotal != newTotal)
                {
                    OnTotalsChanged?.Invoke(type, oldTotal, newTotal);

                    // Also publish to EventBus for global listeners
                    EventBus.Publish(new StorageTotalsChangedEvent
                    {
                        PlayerId = playerId,
                        ResourceType = type,
                        OldTotal = oldTotal,
                        NewTotal = newTotal
                    });
                }
            }
        }

        /// <summary>
        /// Handle a storage point's contents changing.
        /// Updates cached totals incrementally for efficiency.
        /// 
        /// OWNERSHIP: Only processes events from storage points owned by this player.
        /// </summary>
        private void HandleStorageChanged(StorageChangedEvent evt)
        {
            if (evt.Storage == null) return;
            
            // Ignore events from storage points owned by other players
            if (evt.OwnerId != playerId) return;

            // Get old cached total
            int oldTotal = cachedTotals.TryGetValue(evt.ResourceType, out int o) ? o : 0;

            // Update cached total by the delta
            int delta = evt.NewAmount - evt.OldAmount;
            int newTotal = oldTotal + delta;
            cachedTotals[evt.ResourceType] = newTotal;

            // Fire change event
            if (oldTotal != newTotal)
            {
                OnTotalsChanged?.Invoke(evt.ResourceType, oldTotal, newTotal);

                EventBus.Publish(new StorageTotalsChangedEvent
                {
                    PlayerId = playerId,
                    ResourceType = evt.ResourceType,
                    OldTotal = oldTotal,
                    NewTotal = newTotal
                });
            }
        }

        /// <summary>
        /// Handle a new storage point being registered.
        /// Triggers full recalculation to include its contents.
        /// 
        /// OWNERSHIP: Only recalculates if the storage belongs to this player.
        /// </summary>
        private void HandleStorageRegistered(StoragePointRegisteredEvent evt)
        {
            // Only recalculate if this is our player's storage
            if (evt.OwnerId != playerId) return;
            
            needsRecalculation = true;
            RecalculateAllTotals();
        }

        /// <summary>
        /// Handle a storage point being unregistered.
        /// Triggers full recalculation to remove its contents.
        /// 
        /// OWNERSHIP: Only recalculates if the storage belongs to this player.
        /// </summary>
        private void HandleStorageUnregistered(StoragePointUnregisteredEvent evt)
        {
            // Only recalculate if this is our player's storage
            if (evt.OwnerId != playerId) return;
            
            needsRecalculation = true;
            RecalculateAllTotals();
        }

        /// <summary>
        /// Cleanup subscriptions.
        /// </summary>
        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;

            EventBus.Unsubscribe<StorageChangedEvent>(HandleStorageChanged);
            EventBus.Unsubscribe<StoragePointRegisteredEvent>(HandleStorageRegistered);
            EventBus.Unsubscribe<StoragePointUnregisteredEvent>(HandleStorageUnregistered);

            // Clean up spawned storage if we created it
            if (standardStorageInstance != null)
            {
                UnityEngine.Object.Destroy(standardStorageInstance.gameObject);
            }
        }
    }
}
