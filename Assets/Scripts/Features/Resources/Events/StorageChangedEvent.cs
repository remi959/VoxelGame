// ============================================================================
// StorageChangedEvent.cs - Events for storage point changes
// ============================================================================
using Assets.Scripts.Resources;
using Assets.Scripts.Shared.Enums;

namespace Assets.Scripts.Economy.Events
{
    /// <summary>
    /// Fired by a StoragePoint when its contents change (deposit or withdrawal).
    /// The ResourceStorageAggregator listens for these to update totals.
    /// 
    /// OWNERSHIP: Includes OwnerId so aggregators can filter by player.
    /// </summary>
    public struct StorageChangedEvent
    {
        /// <summary>
        /// The storage point that changed.
        /// </summary>
        public StoragePoint Storage;

        /// <summary>
        /// The player who owns this storage point.
        /// Used by ResourceStorageAggregator to filter events by player.
        /// </summary>
        public int OwnerId;

        /// <summary>
        /// The type of resource that changed.
        /// </summary>
        public EResourceType ResourceType;

        /// <summary>
        /// Amount before the change.
        /// </summary>
        public int OldAmount;

        /// <summary>
        /// Amount after the change.
        /// </summary>
        public int NewAmount;

        /// <summary>
        /// The difference (positive = added, negative = removed).
        /// </summary>
        public int Delta => NewAmount - OldAmount;
    }

    /// <summary>
    /// Fired by ResourceStorageAggregator when aggregated totals change.
    /// EconomyService listens for these to update its cached totals.
    /// </summary>
    public struct StorageTotalsChangedEvent
    {
        /// <summary>
        /// The player whose totals changed.
        /// </summary>
        public int PlayerId;

        /// <summary>
        /// The type of resource that changed.
        /// </summary>
        public EResourceType ResourceType;

        /// <summary>
        /// Previous total across all storage points.
        /// </summary>
        public int OldTotal;

        /// <summary>
        /// New total across all storage points.
        /// </summary>
        public int NewTotal;

        /// <summary>
        /// The difference (positive = gained, negative = lost).
        /// </summary>
        public int Delta => NewTotal - OldTotal;
    }

    /// <summary>
    /// Fired when a storage point is registered with the aggregator.
    /// Useful for UI that wants to show storage point icons on minimap, etc.
    /// </summary>
    public struct StoragePointRegisteredEvent
    {
        public StoragePoint Storage;
        
        /// <summary>
        /// The player who owns this storage point.
        /// </summary>
        public int OwnerId;
    }

    /// <summary>
    /// Fired when a storage point is unregistered from the aggregator.
    /// </summary>
    public struct StoragePointUnregisteredEvent
    {
        public StoragePoint Storage;
        
        /// <summary>
        /// The player who owns this storage point.
        /// </summary>
        public int OwnerId;
    }

    /// <summary>
    /// Fired when a storage point is enabled or disabled.
    /// Workers should check this to avoid depositing at disabled storage points.
    /// </summary>
    public struct StoragePointStateChangedEvent
    {
        public StoragePoint Storage;
        public int OwnerId;
        public bool IsEnabled;
    }

    /// <summary>
    /// Fired when a storage point is destroyed.
    /// Workers currently targeting this storage should find a new one.
    /// </summary>
    public struct StoragePointDestroyedEvent
    {
        public StoragePoint Storage;
        public int OwnerId;
        public int ResourcesLost;
    }
}
