// ============================================================================
// BuildingWorkContext.cs - Per-worker building context
// ============================================================================
using Assets.Scripts.Buildings;
using Assets.Scripts.Resources;

namespace Assets.Scripts.NPCs.States.Building
{
    /// <summary>
    /// Per-worker context for building construction.
    /// 
    /// This replaces the static BuildingContext class to support
    /// multiple workers building simultaneously without conflicts.
    /// 
    /// Each Worker instance owns its own BuildingWorkContext,
    /// which is passed to building states through the constructor.
    /// 
    /// Building Flow:
    /// 1. Worker claims a slot via ClaimSlotState
    /// 2. Worker gathers resources from storage (GatherBuildResourcesState)
    /// 3. Worker delivers resources to crafting bench (DeliverBuildResourcesState)
    /// 4. Worker waits for crafting (WaitForCraftState)
    /// 5. Worker picks up crafted part (PickUpCraftedPartState)
    /// 6. Worker delivers and places part (DeliverPartState, PlacePartState)
    /// </summary>
    public class BuildingWorkContext
    {
        /// <summary>
        /// The building blueprint this worker is constructing.
        /// </summary>
        public BuildingBlueprint CurrentBlueprint { get; set; }

        /// <summary>
        /// The specific part slot this worker has claimed.
        /// </summary>
        public BuildingBlueprint.PartSlot CurrentSlot { get; set; }

        /// <summary>
        /// The crafting bench being used for the current part.
        /// </summary>
        public CraftingBench CurrentBench { get; set; }

        /// <summary>
        /// The crafted part being carried (if any).
        /// </summary>
        public CraftedPart CurrentPart { get; set; }
        
        /// <summary>
        /// The storage point being targeted for resource gathering.
        /// Persists across state transitions (e.g., when moving to storage).
        /// </summary>
        public StoragePoint TargetStorage { get; set; }

        /// <summary>
        /// Check if the context has a valid blueprint.
        /// </summary>
        public bool HasBlueprint => CurrentBlueprint != null && !CurrentBlueprint.IsComplete;

        /// <summary>
        /// Check if the context has a valid slot.
        /// </summary>
        public bool HasSlot => CurrentSlot != null;

        /// <summary>
        /// Check if the context has a valid bench.
        /// </summary>
        public bool HasBench => CurrentBench != null;

        /// <summary>
        /// Check if the worker is carrying a part.
        /// </summary>
        public bool HasPart => CurrentPart != null;

        /// <summary>
        /// Convenience property to get the current slot index, or -1 if no slot.
        /// </summary>
        public int CurrentSlotIndex => CurrentSlot?.Index ?? -1;

        /// <summary>
        /// Clear all context references.
        /// Call this when abandoning building or completing construction.
        /// </summary>
        public void Clear()
        {
            CurrentBlueprint = null;
            CurrentSlot = null;
            CurrentBench = null;
            CurrentPart = null;
            TargetStorage = null;
        }

        /// <summary>
        /// Clear only the current slot/part for claiming a new slot.
        /// </summary>
        public void ClearSlotAndPart()
        {
            CurrentSlot = null;
            CurrentPart = null;
        }
    }
}
