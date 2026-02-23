// ============================================================================
// DeliverPartState.cs - Carry part to the building slot
// ============================================================================
using Assets.Scripts.Buildings;
using Assets.Scripts.Core;
using Assets.Scripts.Core.Services;
using Assets.Scripts.NPCs.Units;
using UnityEngine;

namespace Assets.Scripts.NPCs.States.Building
{
    /// <summary>
    /// Carry the crafted part to the claimed slot position.
    /// Transitions to PlacePartState when arrived.
    /// </summary>
    public class DeliverPartState : BuildingStateBase
    {
        private bool isMoving;

        public DeliverPartState(Worker worker, StateMachine stateMachine) 
            : base(worker, stateMachine) { }

        public override void Enter()
        {
            worker.Motor.Stop();
            isMoving = false;

            var part = Context.CurrentPart;
            var blueprint = Context.CurrentBlueprint;
            var slot = Context.CurrentSlot;

            if (part == null)
            {
                DebugManager.LogWarning("DeliverPartState: No part to deliver");
                AbandonBuilding();
                return;
            }

            if (blueprint == null || slot == null)
            {
                DebugManager.LogWarning("DeliverPartState: Blueprint or slot gone");
                // Drop the part since we can't place it
                part.Drop();
                Context.CurrentPart = null;
                AbandonBuilding();
                return;
            }

            // Get target position
            Vector3 slotPosition = blueprint.GetSlotWorldPosition(slot);

            DebugManager.LogState($"DeliverPartState: Moving to slot {slot.Index}");

            MoveToSlot(slotPosition);
        }

        private void MoveToSlot(Vector3 position)
        {
            isMoving = true;

            var moveState = stateMachine.GetState<MoveToTargetState>();
            moveState.SetTarget(position, OnArrivedAtSlot, OnMoveFailed);
            stateMachine.SetState<MoveToTargetState>();
        }

        private void OnMoveFailed()
        {
            DebugManager.LogWarning("DeliverPartState: Failed to reach slot, abandoning building");
            AbandonBuilding();  // This drops the part and cleans up
        }

        private void OnArrivedAtSlot()
        {
            isMoving = false;

            // Final check
            if (Context.CurrentBlueprint == null || 
                Context.CurrentSlot == null)
            {
                DebugManager.LogWarning("DeliverPartState: Blueprint/slot gone on arrival");
                if (Context.CurrentPart != null)
                {
                    Context.CurrentPart.Drop();
                    Context.CurrentPart = null;
                }
                AbandonBuilding();
                return;
            }

            DebugManager.LogState("DeliverPartState: Arrived at slot");
            stateMachine.SetState<PlacePartState>();
        }

        public override void Update()
        {
            // Movement handled by MoveToTargetState

            // Safety: check if blueprint destroyed while carrying
            if (Context.CurrentBlueprint == null)
            {
                DebugManager.LogState("DeliverPartState: Blueprint destroyed while delivering");
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
            isMoving = false;
        }
    }
}