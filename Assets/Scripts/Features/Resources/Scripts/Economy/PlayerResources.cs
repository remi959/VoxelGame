// ============================================================================
// PlayerResources.cs - Per-player resource reservation tracking
// ============================================================================
using System.Collections.Generic;
using Assets.Scripts.Shared.Enums;

namespace Assets.Scripts.Economy
{
    /// <summary>
    /// Tracks a single player's resource RESERVATIONS only.
    /// 
    /// IMPORTANT - SOURCE OF TRUTH MODEL:
    /// - Actual resource amounts are stored in StoragePoints
    /// - ResourceStorageAggregator calculates totals from all storage points
    /// - This class ONLY tracks reservations (planned spending)
    /// - EconomyService combines aggregator totals with reservations for "available" amounts
    /// 
    /// This separation ensures:
    /// - Clear ownership (storage points own resources)
    /// - No duplicate tracking of amounts
    /// - Reservations work independently of where resources are stored
    /// </summary>
    public class PlayerResources
    {
        public readonly int PlayerId;

        // Resources currently reserved (planned spending)
        // NOTE: We no longer track totals here - that's the aggregator's job
        private readonly Dictionary<EResourceType, int> reserved = new();

        public PlayerResources(int playerId, Dictionary<EResourceType, int> starting = null)
        {
            PlayerId = playerId;

            // Initialize all resource types to 0 reserved
            foreach (EResourceType type in System.Enum.GetValues(typeof(EResourceType)))
            {
                reserved[type] = 0;
            }

            // Starting resources are now handled by pre-populating storage points
            // The 'starting' parameter is kept for API compatibility but ignored
        }

        /// <summary>
        /// [DEPRECATED] Get total resources.
        /// Use EconomyService.GetTotal() instead which reads from aggregator.
        /// </summary>
        [System.Obsolete("Totals are now tracked by ResourceStorageAggregator. Use EconomyService.GetTotal() instead.")]
        public int GetTotal(EResourceType type) => 0;

        /// <summary>
        /// [DEPRECATED] Get available resources.
        /// Use EconomyService.GetAvailable() instead which combines aggregator totals with reservations.
        /// </summary>
        [System.Obsolete("Use EconomyService.GetAvailable() instead.")]
        public int GetAvailable(EResourceType type) => 0;

        /// <summary>
        /// Get the amount currently reserved for planned spending.
        /// </summary>
        public int GetReserved(EResourceType type)
        {
            return reserved.TryGetValue(type, out int r) ? r : 0;
        }

        /// <summary>
        /// [DEPRECATED] Add resources directly.
        /// Resources should be added via StoragePoint.Deposit() instead.
        /// </summary>
        [System.Obsolete("Use StoragePoint.Deposit() instead. Resources flow through storage points.")]
        public void Add(EResourceType type, int amount)
        {
            // No-op - resources are added via storage points
        }

        /// <summary>
        /// Reserve resources for planned spending.
        /// </summary>
        public void Reserve(EResourceType type, int amount)
        {
            reserved[type] = GetReserved(type) + amount;
        }

        /// <summary>
        /// Confirm a reservation - releases the reservation hold.
        /// Actual resource deduction happens when workers withdraw from storage.
        /// </summary>
        public void ConfirmReservation(EResourceType type, int amount)
        {
            reserved[type] = GetReserved(type) - amount;
            // NOTE: We no longer deduct from totals here
            // Resources are deducted from storage points when workers withdraw them
        }

        /// <summary>
        /// Cancel a reservation - release without spending.
        /// </summary>
        public void CancelReservation(EResourceType type, int amount)
        {
            reserved[type] = GetReserved(type) - amount;
        }
    }
}