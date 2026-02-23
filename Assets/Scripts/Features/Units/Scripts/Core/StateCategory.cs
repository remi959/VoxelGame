// ============================================================================
// StateCategory.cs - State categorization for efficient state queries
// ============================================================================

namespace Assets.Scripts.NPCs.States
{
    /// <summary>
    /// Categories for state machine states.
    /// 
    /// Why this exists:
    /// - Checking IsInState<T>() for 7 different types is O(7) with typeof() allocations
    /// - Each typeof(T) allocates ~40 bytes and requires dictionary lookup
    /// - With IsBuilding checked multiple times per frame for 500 workers = disaster
    /// 
    /// Usage:
    /// - Each IState implementation returns its Category
    /// - StateMachine.CurrentCategory provides O(1) category check
    /// - Worker.IsBuilding becomes: stateMachine.CurrentCategory == StateCategory.Building
    /// 
    /// Categories are designed for common queries:
    /// - Idle: No active task
    /// - Movement: Moving to target
    /// - Gathering: Resource collection
    /// - Building: Construction-related states
    /// - Combat: Attack/defense states (future)
    /// </summary>
    public enum StateCategory
    {
        /// <summary>
        /// No active task (WorkerIdleState).
        /// </summary>
        Idle,

        /// <summary>
        /// Moving to a destination (MoveToTargetState).
        /// </summary>
        Movement,

        /// <summary>
        /// Resource gathering states (GatheringState, PickUpFragmentState, DepositingState).
        /// </summary>
        Gathering,

        /// <summary>
        /// Building construction states (ClaimSlot, GatherBuildResources, DeliverBuildResources, etc.)
        /// </summary>
        Building,

        /// <summary>
        /// Combat-related states (future expansion).
        /// </summary>
        Combat,

        /// <summary>
        /// Fallback for unknown states.
        /// </summary>
        Other
    }
}
