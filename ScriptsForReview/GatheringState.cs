using Assets.Scripts.Core;
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
        private System.Action<ResourceFragment> fragmentCallback;
        private bool isActive;

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
            isActive = true;
            worker.Motor.Stop();

            if (targetResource == null || targetResource.IsDepleted)
            {
                DebugManager.LogState("GatheringState: Resource is null or depleted");
                HandleResourceDepleted();
                return;
            }

            DebugManager.LogState($"GatheringState: Started working on {targetResource.name}");

            // Store callback reference so we can stop our specific work session
            fragmentCallback = OnFragmentReady;

            // Start working - pass worker transform for closest piece calculation
            targetResource.StartWorking(fragmentCallback, worker.transform);
        }

        private void OnFragmentReady(ResourceFragment fragment)
        {
            // Guard: Ignore callback if state is no longer active
            if (!isActive)
            {
                DebugManager.LogState("GatheringState: Ignoring stale callback (state no longer active)");
                return;
            }

            if (fragment != null)
            {
                DebugManager.LogState("GatheringState: Fragment ready, going to pick it up");
                worker.PickUpFragment(fragment, targetResource);
            }
            else
            {
                DebugManager.LogState("GatheringState: No fragment (stage transition or depleted)");

                if (targetResource != null && !targetResource.IsDepleted)
                {
                    DebugManager.LogState("GatheringState: Auto-continuing to next stage");
                    
                    // Check if we need to move to a new position for the next piece
                    if (targetResource.DoesCurrentStageYieldPieces())
                    {
                        MoveToNextPieceAndContinue();
                    }
                    else
                    {
                        // Non-yielding stage, just continue working
                        targetResource.StartWorking(fragmentCallback, worker.transform);
                    }
                }
                else
                {
                    HandleResourceDepleted();
                }
            }
        }

        /// <summary>
        /// Move to the next piece position before continuing to harvest.
        /// </summary>
        private void MoveToNextPieceAndContinue()
        {
            if (targetResource == null || targetResource.IsDepleted)
            {
                HandleResourceDepleted();
                return;
            }

            Vector3 nextWorkPosition = targetResource.GetWorkPositionFor(worker.transform);
            float distanceToWork = Vector3.Distance(worker.transform.position, nextWorkPosition);

            // If already close enough, just continue working
            if (distanceToWork <= 2f) // Use a reasonable interaction distance
            {
                targetResource.StartWorking(fragmentCallback, worker.transform);
                return;
            }

            // Need to move to the next piece
            DebugManager.LogState($"GatheringState: Moving to next piece position");

            var moveState = stateMachine.GetState<MoveToTargetState>();
            moveState.SetTarget(nextWorkPosition, () =>
            {
                // After arriving, continue gathering (re-enter this state)
                if (isActive && targetResource != null && !targetResource.IsDepleted)
                {
                    stateMachine.SetState<GatheringState>();
                }
                else
                {
                    HandleResourceDepleted();
                }
            });

            stateMachine.SetState<MoveToTargetState>();
        }

        private void HandleResourceDepleted()
        {
            if (!isActive) return;

            if (worker.CarriedAmount > 0)
            {
                DebugManager.LogState("GatheringState: Resource depleted, returning to storage");
                worker.ReturnToStorage();
            }
            else
            {
                DebugManager.LogState("GatheringState: Going idle");
                stateMachine.SetState<WorkerIdleState>();
            }
        }

        public void Update()
        {
            if (!isActive) return;

            if (targetResource == null)
            {
                DebugManager.LogState("GatheringState: Resource was destroyed");
                HandleResourceDepleted();
            }
        }

        public void Exit()
        {
            // Mark state as inactive FIRST to prevent stale callbacks
            isActive = false;

            // Stop our specific work session
            if (targetResource != null && fragmentCallback != null)
            {
                targetResource.StopWorking(fragmentCallback);
            }

            fragmentCallback = null;
        }
    }
}