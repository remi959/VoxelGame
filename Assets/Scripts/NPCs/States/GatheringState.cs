using Assets.Scripts.NPCs.Units;
using Assets.Scripts.Resources;
using Assets.Scripts.Shared.Interfaces;
using UnityEngine;

namespace Assets.Scripts.NPCs.States
{
    public class GatheringState : IState
    {
        private readonly Worker worker;
        private readonly StateMachine stateMachine;

        private Resource targetResource;

        public GatheringState(Worker worker, StateMachine stateMachine)
        {
            this.worker = worker;
            this.stateMachine = stateMachine;
        }

        public void SetTarget(Resource resource)
        {
            targetResource = resource;
        }

        public Resource GetTargetResource() => targetResource;

        public void Enter()
        {
            worker.Motor.Stop();

            if (targetResource == null || targetResource.IsDepleted)
            {
                Debug.Log("GatheringState: Resource is null or depleted");
                HandleResourceDepleted();
                return;
            }

            Debug.Log($"GatheringState: Started working on {targetResource.name}");

            // Start working - callback will fire when fragment is ready
            targetResource.StartWorking(OnFragmentReady);
        }

        private void OnFragmentReady(ResourceFragment fragment)
        {
            if (fragment != null)
            {
                Debug.Log("GatheringState: Fragment ready, going to pick it up");
                worker.PickUpFragment(fragment, targetResource);
            }
            else
            {
                // No fragment - either stage transition without pieces, or depleted
                Debug.Log("GatheringState: No fragment (stage transition or depleted)");

                // Check if resource still has more to give (auto-continue to next stage)
                if (targetResource != null && !targetResource.IsDepleted)
                {
                    Debug.Log("GatheringState: Auto-continuing to next stage");
                    // Continue working the resource (now on new stage)
                    targetResource.StartWorking(OnFragmentReady);
                }
                else
                {
                    HandleResourceDepleted();
                }
            }
        }

        private void HandleResourceDepleted()
        {
            if (worker.CarriedAmount > 0)
            {
                Debug.Log("GatheringState: Resource depleted, returning to storage");
                worker.ReturnToStorage();
            }
            else
            {
                Debug.Log("GatheringState: Going idle");
                stateMachine.SetState<WorkerIdleState>();
            }
        }

        public void Update()
        {
            // Check if resource was destroyed while working
            if (targetResource == null)
            {
                Debug.Log("GatheringState: Resource was destroyed");
                HandleResourceDepleted();
            }
        }

        public void Exit()
        {
            if (targetResource != null)
            {
                targetResource.StopWorking();
            }
        }
    }
}