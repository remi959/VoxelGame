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

        private ResourceFragment targetFragment;
        private Resource sourceResource;
        private bool hasPickedUp;
        private float postPickupDelay;
        private const float POST_PICKUP_WAIT = 0.1f; // Small delay after pickup before continuing

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
            
            if (targetFragment == null)
            {
                Debug.LogWarning("PickUpFragmentState: No fragment to pick up!");
                ContinueGatheringOrFinish();
                return;
            }

            Debug.Log("PickUpFragmentState: Moving to fragment");
        }

        public void Update()
        {
            // If we've picked up, wait a tiny bit before continuing
            if (hasPickedUp)
            {
                postPickupDelay += Time.deltaTime;
                if (postPickupDelay >= POST_PICKUP_WAIT)
                {
                    DecideNextAction();
                }
                return;
            }
            
            if (targetFragment == null)
            {
                Debug.Log("PickUpFragmentState: Fragment gone");
                ContinueGatheringOrFinish();
                return;
            }

            // Keep following fragment until it settles
            worker.Motor.SetDestination(targetFragment.transform.position);

            if (!targetFragment.CanBePickedUp)
            {
                return;
            }

            float distance = Vector3.Distance(worker.transform.position, targetFragment.transform.position);

            if (distance <= targetFragment.PickupDistance)
            {
                Debug.Log($"PickUpFragmentState: Picking up fragment worth {targetFragment.Value}");

                // Add fragment with visual attachment
                worker.AddToInventory(targetFragment);
                targetFragment = null;
                hasPickedUp = true;
                postPickupDelay = 0f;
            }
        }

        private void DecideNextAction()
        {
            // Decide what to do next
            if (worker.IsInventoryFull)
            {
                Debug.Log("PickUpFragmentState: Inventory full, returning to storage");
                worker.ReturnToStorage();
            }
            else
            {
                ContinueGatheringOrFinish();
            }
        }

        private void ContinueGatheringOrFinish()
        {
            // Check if we should continue gathering from the same resource
            if (sourceResource != null && !sourceResource.IsDepleted)
            {
                Debug.Log("PickUpFragmentState: Continuing to gather");
                worker.GatherFrom(sourceResource);
            }
            else
            {
                // Resource depleted
                if (worker.CarriedAmount > 0)
                {
                    Debug.Log("PickUpFragmentState: Resource depleted, returning carried resources");
                    worker.ReturnToStorage();
                }
                else
                {
                    Debug.Log("PickUpFragmentState: Going idle");
                    stateMachine.SetState<WorkerIdleState>();
                }
            }
        }

        public void Exit()
        {
            targetFragment = null;
            sourceResource = null;
            hasPickedUp = false;
        }
    }
}