// ============================================================================
// ClaimSlotState.cs - First phase: claim a build slot
// ============================================================================
using Assets.Scripts.Buildings;
using Assets.Scripts.Core;
using Assets.Scripts.Core.Services;
using Assets.Scripts.NPCs.Units;

namespace Assets.Scripts.NPCs.States.Building
{
    /// <summary>
    /// First building state: claim an available slot on the blueprint.
    /// 
    /// Building Construction Flow:
    /// ClaimSlotState → GatherBuildResourcesState → DeliverBuildResourcesState → 
    /// WaitForCraftState → PickUpCraftedPartState → DeliverPartState → PlacePartState
    /// 
    /// This flow uses the crafting bench to create parts, which are then carried
    /// and placed at the building site with a satisfying scale-up animation.
    /// </summary>
    public class ClaimSlotState : BuildingStateBase
    {
        public ClaimSlotState(Worker worker, StateMachine stateMachine) 
            : base(worker, stateMachine) { }

        public void SetBlueprint(BuildingBlueprint blueprint)
        {
            Context.CurrentBlueprint = blueprint;
        }

        public override void Enter()
        {
            worker.Motor.Stop();

            var blueprint = Context.CurrentBlueprint;
            if (blueprint == null || blueprint.IsComplete)
            {
                DebugManager.LogState("ClaimSlotState: No valid blueprint");
                AbandonBuilding();
                return;
            }

            // Handle worker that's already carrying a crafted part
            if (Context.CurrentPart != null && Context.CurrentPart.CanBePickedUp == false)
            {
                HandleCarriedPart(blueprint);
                return;
            }

            // Claim a slot
            var claimedSlot = blueprint.ClaimSlot(worker);
            if (claimedSlot == null)
            {
                DebugManager.LogState("ClaimSlotState: No slots available");
                AbandonBuilding();
                return;
            }

            Context.CurrentSlot = claimedSlot;
            DebugManager.LogState($"ClaimSlotState: Claimed slot {claimedSlot.Index}");

            // Check if there's a dropped part we can recover
            var droppedPart = blueprint.GetDroppedPartForSlot(claimedSlot.Index);
            if (droppedPart != null)
            {
                DebugManager.LogState($"ClaimSlotState: Found dropped part for slot {claimedSlot.Index}, going to pick it up");
                blueprint.ClearDroppedPart(claimedSlot.Index);
                Context.CurrentPart = droppedPart;
                stateMachine.SetState<PickUpCraftedPartState>();
                return;
            }

            // Start the crafting flow
            StartCraftingFlow();
        }

        /// <summary>
        /// Crafting bench flow: Worker gathers resources, delivers to bench,
        /// waits for crafting, then carries the part to the building site.
        /// </summary>
        private void StartCraftingFlow()
        {
            var claimedSlot = Context.CurrentSlot;
            
            // Find a crafting bench
            var bench = CraftingBench.FindNearest(worker.transform.position);
            if (bench == null)
            {
                DebugManager.LogWarning("ClaimSlotState: No crafting bench found");
                AbandonBuilding();
                return;
            }

            Context.CurrentBench = bench;

            // Check what resources the bench needs
            var missing = bench.GetMissingResources(claimedSlot.SourcePart);
            if (missing.Count == 0)
            {
                // Bench already has resources, start crafting
                stateMachine.SetState<WaitForCraftState>();
            }
            else
            {
                // Need to gather resources
                stateMachine.SetState<GatherBuildResourcesState>();
            }
        }

        /// <summary>
        /// Handle a worker that's already carrying a crafted part.
        /// </summary>
        private void HandleCarriedPart(BuildingBlueprint blueprint)
        {
            var carriedPart = Context.CurrentPart;
            int targetSlot = carriedPart.TargetSlotIndex;
            
            if (targetSlot >= 0 && carriedPart.TargetBlueprint == blueprint)
            {
                // Try to reclaim the original slot for this part
                var slot = blueprint.TryClaimSpecificSlot(worker, targetSlot);
                if (slot != null)
                {
                    Context.CurrentSlot = slot;
                    DebugManager.LogState($"ClaimSlotState: Worker has part for slot {targetSlot}, reclaiming and delivering");
                    stateMachine.SetState<DeliverPartState>();
                    return;
                }
            }
            
            // Couldn't reclaim original slot - drop the part for someone else
            DebugManager.LogState($"ClaimSlotState: Worker has orphaned part, dropping it");
            if (carriedPart.TargetBlueprint != null && carriedPart.TargetSlotIndex >= 0)
            {
                carriedPart.TargetBlueprint.RegisterDroppedPart(carriedPart.TargetSlotIndex, carriedPart);
            }
            carriedPart.Drop();
            Context.CurrentPart = null;
            
            // Re-enter to claim a new slot
            Enter();
        }

        public override void Update() { }
        public override void Exit() { }
    }
}