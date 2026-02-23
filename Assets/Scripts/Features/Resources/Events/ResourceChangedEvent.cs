// ============================================================================
// Events/ResourceChangedEvent.cs - Event fired when resources change
// ============================================================================
using Assets.Scripts.Shared.Enums;

namespace Assets.Scripts.Economy.Events
{
    /// <summary>
    /// Fired whenever a player's resource amount changes.
    /// Subscribe to this for UI updates, achievements, tutorials, etc.
    /// </summary>
    public struct ResourceChangedEvent
    {
        /// <summary>
        /// The player whose resources changed.
        /// </summary>
        public int PlayerId;

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
        /// The difference (positive = gained, negative = spent).
        /// </summary>
        public int Delta => NewAmount - OldAmount;

        /// <summary>
        /// Source of the change for UI/logging purposes.
        /// </summary>
        public ResourceChangeSource Source;
    }

    /// <summary>
    /// Identifies why resources changed (for UI feedback).
    /// </summary>
    public enum ResourceChangeSource
    {
        Unknown,
        Gathering,          // Worker deposited resources (legacy)
        StorageDeposit,     // Resources deposited to storage point
        StorageWithdraw,    // Resources withdrawn from storage point
        BuildingCost,       // Building construction consumed resources
        BuildingRefund,     // Cancelled building returned resources
        UnitTraining,       // Unit was trained
        Research,           // Technology was researched
        Trade,              // Player traded resources
        Cheat,              // Debug/cheat command
        InitialGrant        // Starting resources
    }
}