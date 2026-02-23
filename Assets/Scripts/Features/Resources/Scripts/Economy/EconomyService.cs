using System;
using System.Collections.Generic;
using Assets.Scripts.Buildings;
using Assets.Scripts.Core;
using Assets.Scripts.Core.Events;
using Assets.Scripts.Core.Services;
using Assets.Scripts.Economy.Events;
using Assets.Scripts.Events;
using Assets.Scripts.Shared.Enums;
using UnityEngine;

namespace Assets.Scripts.Economy
{
    /// <summary>
    /// Central service for all resource transactions.
    /// 
    /// IMPORTANT - SOURCE OF TRUTH MODEL:
    /// - StoragePoints are the source of truth for resource amounts
    /// - ResourceStorageAggregator sums all storage points for totals
    /// - This service reads from the aggregator (does NOT store totals directly)
    /// - PlayerResources now ONLY tracks reservations (not actual amounts)
    /// 
    /// Design notes:
    /// - Uses reservation pattern for planned spending
    /// - Fires events for UI updates when aggregator reports changes
    /// - Supports multiple players (for AI/multiplayer)
    /// - Ensures a standard all-types storage point exists
    /// 
    /// PERSISTENCE:
    /// This component should be placed as a child of the [Services] GameObject.
    /// PersistentServices handles DontDestroyOnLoad for the entire hierarchy.
    /// </summary>  
    public class EconomyService : MonoBehaviour
    {
        public static EconomyService Instance { get; private set; }

        [Header("Standard Storage")]
        [Tooltip("Prefab for the standard all-types storage point. If null, a basic one is created.")]
        [SerializeField] private GameObject standardStoragePrefab;

        // Per-player resource tracking (now only for reservations)
        private readonly Dictionary<int, PlayerResources> playerResources = new();

        // Per-player storage aggregators
        private readonly Dictionary<int, ResourceStorageAggregator> aggregators = new();

        // Active reservations (pending transactions)
        private readonly Dictionary<string, ResourceReservation> reservations = new();

        // Cached list for deterministic iteration (avoids allocation per frame)
        private readonly List<string> sortedKeys = new();
        private readonly List<string> expiredKeys = new();

        // Reservation ID generator
        private int nextReservationId = 1;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            ServiceLocator.Register(this);
        }

        private void OnDestroy()
        {
            // Dispose all aggregators
            foreach (var aggregator in aggregators.Values)
            {
                aggregator?.Dispose();
            }
            aggregators.Clear();

            if (Instance == this)
            {
                Instance = null;
                ServiceLocator.Unregister<EconomyService>();
            }
        }

        /// <summary>
        /// Initialize a player's resource pool.
        /// Call this when a player joins or game starts.
        /// 
        /// NOTE: Starting resources are no longer passed here since storage points
        /// are the source of truth. Pre-populate storage points in the scene instead,
        /// or call EnsureStandardStorageExists() after initialization.
        /// </summary>
        public void InitializePlayer(int playerId, Dictionary<EResourceType, int> startingResources = null)
        {
            // Create reservation tracker (no longer stores actual amounts)
            playerResources[playerId] = new PlayerResources(playerId, null);

            // Create aggregator for this player
            var aggregator = new ResourceStorageAggregator(playerId);
            aggregator.SetStandardStoragePrefab(standardStoragePrefab);
            aggregator.OnTotalsChanged += (type, oldTotal, newTotal) =>
            {
                HandleAggregatorTotalsChanged(playerId, type, oldTotal, newTotal);
            };
            aggregators[playerId] = aggregator;

            // Ensure standard storage exists and calculate initial totals
            // Delay this slightly to allow scene storage points to register
            StartCoroutine(DelayedInitialization(playerId, aggregator));
        }

        private System.Collections.IEnumerator DelayedInitialization(int playerId, ResourceStorageAggregator aggregator)
        {
            // Wait one frame for scene storage points to register
            yield return null;

            aggregator.EnsureStandardStorageExists();
            aggregator.RecalculateAllTotals();

            Debug.Log($"EconomyService: Player {playerId} initialized with storage aggregator");
        }

        /// <summary>
        /// Handle totals changing from the aggregator.
        /// Fires the UI update event.
        /// </summary>
        private void HandleAggregatorTotalsChanged(int playerId, EResourceType type, int oldTotal, int newTotal)
        {
            // Publish to EventBus for all listeners
            EventBus.Publish(new ResourceChangedEvent
            {
                PlayerId = playerId,
                ResourceType = type,
                OldAmount = oldTotal,
                NewAmount = newTotal,
                Source = ResourceChangeSource.StorageDeposit 
            });
        }

        /// <summary>
        /// Get current total resources from all storage points.
        /// </summary>
        public int GetTotal(int playerId, EResourceType type)
        {
            if (aggregators.TryGetValue(playerId, out var aggregator))
            {
                return aggregator.GetTotal(type);
            }
            return 0;
        }

        /// <summary>
        /// Get current available resources (total minus reservations).
        /// </summary>
        public int GetAvailable(int playerId, EResourceType type)
        {
            int total = GetTotal(playerId, type);
            int reserved = GetReserved(playerId, type);
            return total - reserved;
        }

        /// <summary>
        /// Get amount currently reserved for this resource type.
        /// </summary>
        public int GetReserved(int playerId, EResourceType type)
        {
            if (playerResources.TryGetValue(playerId, out var resources))
            {
                return resources.GetReserved(type);
            }
            return 0;
        }

        /// <summary>
        /// Check if player can afford a set of costs.
        /// </summary>
        public bool CanAfford(int playerId, IEnumerable<ResourceCost> costs)
        {
            foreach (var cost in costs)
            {
                if (GetAvailable(playerId, cost.resourceType) < cost.amount)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// [DEPRECATED] Add resources directly - use StoragePoint.Deposit() instead.
        /// This method exists for backwards compatibility but should not be used.
        /// Resources should always be added via storage points.
        /// </summary>
        [Obsolete("Use StoragePoint.Deposit() instead. Resources should flow through storage points.")]
        public void AddResources(int playerId, EResourceType type, int amount)
        {
            Debug.LogWarning($"EconomyService.AddResources() is deprecated. Use StoragePoint.Deposit() instead.");
            
            // For backwards compatibility, find a storage point owned by the player and deposit there
            var storage = Resources.StoragePoint.FindAny(type, playerId);
            if (storage != null)
            {
                storage.Deposit(type, amount);
            }
            else
            {
                Debug.LogError($"EconomyService: No storage point found for {type} owned by player {playerId}. Cannot add resources.");
            }
        }

        /// <summary>
        /// Create a reservation for planned spending (e.g., building placement).
        /// Resources are not deducted but are no longer "available".
        /// </summary>
        public ResourceReservation CreateReservation(int playerId, IEnumerable<ResourceCost> costs, float expirationSeconds = 60f)
        {
            if (!playerResources.TryGetValue(playerId, out var resources))
            {
                Debug.LogWarning($"EconomyService: Cannot create reservation - Player {playerId} not initialized!");
                return null;
            }

            // Check availability against aggregated totals minus existing reservations
            var costList = new List<ResourceCost>(costs);
            foreach (var cost in costList)
            {
                if (GetAvailable(playerId, cost.resourceType) < cost.amount)
                    return null;  // Cannot afford
            }

            // Create reservation
            var reservation = new ResourceReservation(
                $"res_{nextReservationId++}",
                playerId,
                costList,
                Time.time + expirationSeconds
            );

            // Apply reservation (marks resources as reserved)
            foreach (var cost in costList)
            {
                resources.Reserve(cost.resourceType, cost.amount);
            }

            reservations[reservation.Id] = reservation;

            return reservation;
        }

        /// <summary>
        /// Confirm a reservation - resources are now considered spent.
        /// The actual withdrawal happens from storage points when workers deliver materials.
        /// </summary>
        public bool ConfirmReservation(string reservationId)
        {
            if (!reservations.TryGetValue(reservationId, out var reservation))
                return false;

            if (reservation.IsConfirmed || reservation.IsCancelled)
                return false;

            if (!playerResources.TryGetValue(reservation.PlayerId, out var resources))
                return false;

            // Convert reservation to confirmed (releases the reservation hold)
            foreach (var cost in reservation.Costs)
            {
                resources.ConfirmReservation(cost.resourceType, cost.amount);
            }

            reservation.Confirm();

            return true;
        }

        /// <summary>
        /// Cancel a reservation - release held resources.
        /// Call when building is cancelled before construction.
        /// </summary>
        public bool CancelReservation(string reservationId)
        {
            if (!reservations.TryGetValue(reservationId, out var reservation))
                return false;

            if (reservation.IsConfirmed || reservation.IsCancelled)
                return false;

            if (!playerResources.TryGetValue(reservation.PlayerId, out var resources))
                return false;

            // Release reservation
            foreach (var cost in reservation.Costs)
            {
                resources.CancelReservation(cost.resourceType, cost.amount);
            }

            reservation.Cancel();
            reservations.Remove(reservationId);

            return true;
        }

        /// <summary>
        /// Force recalculation of totals for a player.
        /// Call this if storage points were added/removed outside normal flow.
        /// </summary>
        public void RecalculateTotals(int playerId)
        {
            if (aggregators.TryGetValue(playerId, out var aggregator))
            {
                aggregator.RecalculateAllTotals();
            }
        }

        private void Update()
        {
            if (reservations.Count == 0) return;

            // Check for expired reservations
            sortedKeys.Clear();
            expiredKeys.Clear();

            foreach (var kvp in reservations)
            {
                sortedKeys.Add(kvp.Key);
            }

            sortedKeys.Sort(StringComparer.Ordinal);

            foreach (var key in sortedKeys)
            {
                var reservation = reservations[key];
                if (!reservation.IsConfirmed && !reservation.IsCancelled && Time.time >= reservation.ExpiresAt)
                {
                    expiredKeys.Add(key);
                }
            }

            foreach (var id in expiredKeys)
            {
                CancelReservation(id);
                Debug.Log($"EconomyService: Reservation {id} expired");
            }
        }
    }
}