using Assets.Scripts.Core;
using Assets.Scripts.Core.Services;
using Assets.Scripts.Data;
using Assets.Scripts.NPCs.Units;
using Assets.Scripts.Resources;
using Assets.Scripts.Shared.Interfaces;
using UnityEngine;

namespace Assets.Scripts.NPCs.States
{
    public class PickUpFragmentState : IState
    {
        private readonly Worker worker;
        private readonly StateMachine stateMachine;

        public StateCategory Category => StateCategory.Gathering;

        private ResourceFragment targetFragment;
        private Resource sourceResource;
        private bool hasPickedUp;
        private float postPickupDelay;

        // Destination caching to avoid repeated SetDestination calls
        private bool hasSetDestination = false;
        private Vector3 lastTargetPosition;

        public PickUpFragmentState(Worker worker, StateMachine stateMachine)
        {
            this.worker = worker;
            this.stateMachine = stateMachine;
        }

        public void SetTarget(ResourceFragment fragment, Resource source)
        {
            targetFragment = fragment;
            sourceResource = source;
        }

        public void Enter()
        {
            hasPickedUp = false;
            postPickupDelay = 0f;
            hasSetDestination = false;

            if (targetFragment == null)
            {
                DebugManager.LogWarning("PickUpFragmentState: No fragment to pick up!");
                ContinueGatheringOrFinish();
                return;
            }

            DebugManager.LogState("PickUpFragmentState: Moving to fragment");
        }

        public void Update()
        {
            var settings = NPCSettingsSO.Instance;
            
            if (hasPickedUp)
            {
                postPickupDelay += Time.deltaTime;
                if (postPickupDelay >= settings.PostPickupDelay) DecideNextAction();

                return;
            }

            if (targetFragment == null)
            {
                DebugManager.LogState("PickUpFragmentState: Fragment gone");
                ContinueGatheringOrFinish();
                return;
            }

            // Wait for fragment to settle before moving
            if (!targetFragment.CanBePickedUp)
            {
                worker.Motor.Stop();
                hasSetDestination = false;
                return;
            }

            Vector3 fragmentPosition = targetFragment.transform.position;

            // Check if we need to update the destination (only if target moved significantly)
            float moveThresholdSqr = settings.TargetMoveThreshold * settings.TargetMoveThreshold;
            bool targetMoved = hasSetDestination && 
                              (fragmentPosition - lastTargetPosition).sqrMagnitude > moveThresholdSqr;

            // Use sqrMagnitude for distance check
            float distanceSqr = (worker.transform.position - fragmentPosition).sqrMagnitude;
            float pickupDistSqr = targetFragment.PickupDistance * targetFragment.PickupDistance;

            if (distanceSqr > pickupDistSqr)
            {
                // Only set destination if we haven't already, or if target moved
                if (!hasSetDestination || targetMoved)
                {
                    worker.Motor.SetDestination(fragmentPosition);
                    lastTargetPosition = fragmentPosition;
                    hasSetDestination = true;
                }
            }
            else
            {
                // Within pickup range
                worker.Motor.Stop();
                DebugManager.LogState($"PickUpFragmentState: Picking up fragment worth {targetFragment.Value}");
                worker.AddToInventory(targetFragment);
                targetFragment = null;
                hasPickedUp = true;
                postPickupDelay = 0f;
                hasSetDestination = false;
            }
        }

        private void DecideNextAction()
        {
            // Decide what to do next
            if (worker.IsInventoryFull)
            {
                DebugManager.LogState("PickUpFragmentState: Inventory full, returning to storage");
                worker.ReturnToStorage();
            }
            else ContinueGatheringOrFinish();
        }

        private void ContinueGatheringOrFinish()
        {
            // Use the Worker's helper method for consistent behavior
            worker.HandleResourceDepletedOrGone(sourceResource);
        }

        public void Exit()
        {
            targetFragment = null;
            sourceResource = null;
            hasPickedUp = false;
            hasSetDestination = false;
        }
    }
}