// ============================================================================
// PlacePartState.cs - Place the part into the slot
// ============================================================================
using Assets.Scripts.Buildings;
using Assets.Scripts.Core;
using Assets.Scripts.Core.Services;
using Assets.Scripts.NPCs.Units;
using UnityEngine;

namespace Assets.Scripts.NPCs.States.Building
{
    /// <summary>
    /// Place the carried part into the building slot.
    /// Transitions to ClaimSlotState if more slots available,
    /// or to Idle if building is complete.
    /// </summary>
    public class PlacePartState : BuildingStateBase
    {
        private bool isPlacing;

        public PlacePartState(Worker worker, StateMachine stateMachine) 
            : base(worker, stateMachine) { }

        public override void Enter()
        {
            worker.Motor.Stop();
            isPlacing = false;

            var part = Context.CurrentPart;
            var blueprint = Context.CurrentBlueprint;
            var slot = Context.CurrentSlot;

            if (part == null || blueprint == null || slot == null)
            {
                DebugManager.LogWarning("PlacePartState: Missing part, blueprint, or slot");
                if (part != null) part.Drop();
                AbandonBuilding();
                return;
            }

            // Get placement transform
            Vector3 targetPos = blueprint.GetSlotWorldPosition(slot);
            Quaternion targetRot = blueprint.GetSlotWorldRotation(slot);
            Vector3 targetScale = slot.LocalScale;

            DebugManager.LogState($"PlacePartState: Placing part in slot {slot.Index}");

            isPlacing = true;

            // Place with animation
            part.Place(
                targetPos,
                targetRot,
                targetScale,
                blueprint.transform,
                OnPlaceComplete
            );
        }

        private void OnPlaceComplete()
        {
            isPlacing = false;

            var part = Context.CurrentPart;
            var blueprint = Context.CurrentBlueprint;
            var slot = Context.CurrentSlot;

            if (blueprint == null)
            {
                DebugManager.LogWarning("PlacePartState: Blueprint gone during placement");
                Context.Clear();
                stateMachine.SetState<WorkerIdleState>();
                return;
            }

            // Clear target info since part is successfully placed
            if (part != null)
            {
                part.ClearTarget();
            }

            // Notify blueprint that part is placed
            blueprint.OnPartPlaced(slot, part);

            DebugManager.LogState($"PlacePartState: Part placed! Building progress: {blueprint.Progress:P0}");

            // Clear part reference (it's now owned by blueprint)
            Context.ClearSlotAndPart();

            // Check if building is complete
            if (blueprint.IsComplete)
            {
                DebugManager.LogState("PlacePartState: Building complete!");
                Context.Clear();
                stateMachine.SetState<WorkerIdleState>();
            }
            else
            {
                // More slots available, claim another
                DebugManager.LogState("PlacePartState: Claiming next slot");
                stateMachine.SetState<ClaimSlotState>();
            }
        }

        public override void Update()
        {
            // Placement animation is handled by CraftedPart

            // Safety check
            if (!isPlacing) return;

            if (Context.CurrentBlueprint == null)
            {
                DebugManager.LogState("PlacePartState: Blueprint destroyed during placement");
                isPlacing = false;
                if (Context.CurrentPart != null)
                {
                    Context.CurrentPart.Drop();
                    Context.CurrentPart = null;
                }
                AbandonBuilding();
            }
        }

        public override void Exit()
        {
            isPlacing = false;
        }
    }
}