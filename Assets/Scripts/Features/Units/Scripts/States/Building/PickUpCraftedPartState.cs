// ============================================================================
// PickUpCraftedPartState.cs - Pick up the crafted part from bench
// ============================================================================
using Assets.Scripts.Buildings;
using Assets.Scripts.Core;
using Assets.Scripts.Core.Services;
using Assets.Scripts.NPCs.Units;
using UnityEngine;

namespace Assets.Scripts.NPCs.States.Building
{
    /// <summary>
    /// Move to and pick up the crafted part.
    /// Transitions to DeliverPartState when part is picked up.
    /// </summary>
    public class PickUpCraftedPartState : BuildingStateBase
    {
        private bool isMoving;
        private const float PickupDistance = 1.5f;

        public PickUpCraftedPartState(Worker worker, StateMachine stateMachine) 
            : base(worker, stateMachine) { }

        public override void Enter()
        {
            worker.Motor.Stop();
            isMoving = false;

            var part = Context.CurrentPart;

            if (part == null)
            {
                DebugManager.LogWarning($"PickUpCraftedPartState: No part to pick up (slot={Context.CurrentSlot?.Index})");
                AbandonBuilding();
                return;
            }

            // Check if part was already picked up by someone else
            if (!part.CanBePickedUp)
            {
                DebugManager.LogWarning($"PickUpCraftedPartState: Part cannot be picked up (IsPickedUp={part.IsPickedUp}, IsPlaced={part.IsPlaced})");
                Context.CurrentPart = null;
                AbandonBuilding();
                return;
            }

            DebugManager.LogState("PickUpCraftedPartState: Moving to pick up part");

            // Check if already close enough
            float distance = Vector3.Distance(worker.transform.position, part.transform.position);

            if (distance <= PickupDistance)
            {
                DoPickup();
            }
            else
            {
                MoveToPart();
            }
        }

        private void MoveToPart()
        {
            isMoving = true;

            var part = Context.CurrentPart;
            var moveState = stateMachine.GetState<MoveToTargetState>();
            moveState.SetTarget(part.transform.position, OnArrivedAtPart, OnMoveToPartFailed);
            stateMachine.SetState<MoveToTargetState>();
        }

        private void OnMoveToPartFailed()
        {
            DebugManager.LogWarning("PickUpCraftedPartState: Failed to reach part, abandoning building");
            AbandonBuilding();  // This will drop CurrentPart if set
        }

        private void OnArrivedAtPart()
        {
            isMoving = false;
            DoPickup();
        }

        private void DoPickup()
        {
            var part = Context.CurrentPart;

            if (part == null)
            {
                DebugManager.LogWarning("PickUpCraftedPartState: Part disappeared");
                AbandonBuilding();
                return;
            }

            // Pick up the part
            part.PickUp(worker.CarryPoint);

            DebugManager.LogState("PickUpCraftedPartState: Part picked up");

            // Go deliver to blueprint
            stateMachine.SetState<DeliverPartState>();
        }

        public override void Update()
        {
            // Movement handled by MoveToTargetState
        }

        public override void Exit()
        {
            isMoving = false;
        }
    }
}