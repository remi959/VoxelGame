using Assets.Scripts.Core;
using Assets.Scripts.Core.Services;
using Assets.Scripts.Data;
using Assets.Scripts.NPCs.Units;
using Assets.Scripts.Resources;
using Assets.Scripts.Shared.Interfaces;
using UnityEngine;

namespace Assets.Scripts.NPCs.States
{
    /// <summary>
    /// State for gathering resources using polling pattern (zero allocations).
    /// 
    /// Flow:
    /// 1. Enter: Call resource.BeginWork(workerId, transform)
    /// 2. Update: Poll resource.CheckWorkProgress(workerId, out fragment)
    /// 3. When FragmentReady: Collect fragment and transition to PickUpFragmentState
    /// 4. When StageComplete: Re-begin work for next stage
    /// 5. Exit: Call resource.CancelWork(workerId)
    /// </summary>
    public class GatheringState : IState
    {
        private readonly Worker worker;
        private readonly StateMachine stateMachine;
        private readonly int workerId;

        public StateCategory Category => StateCategory.Gathering;

        private Resource targetResource;
        private bool isActive;
        private bool workStarted;

        public GatheringState(Worker worker, StateMachine stateMachine)
        {
            this.worker = worker;
            this.stateMachine = stateMachine;
            this.workerId = worker.GetInstanceID();
        }

        public void SetTarget(Resource resource) => targetResource = resource;

        public Resource GetTargetResource() => targetResource;

        public void Enter()
        {
            isActive = true;
            workStarted = false;
            worker.Motor.Stop();

            if (targetResource == null || targetResource.IsDepleted)
            {
                DebugManager.LogState("GatheringState: Resource is null or depleted");
                HandleResourceDepleted();
                return;
            }

            DebugManager.LogState($"GatheringState: Started working on {targetResource.name}");

            // Begin work session (no callback allocation!)
            workStarted = targetResource.BeginWork(workerId, worker.transform);
            
            if (!workStarted)
            {
                DebugManager.LogState("GatheringState: Failed to begin work");
                HandleResourceDepleted();
            }
        }

        public void Update()
        {
            if (!isActive) return;

            // Check if resource was destroyed
            if (targetResource == null)
            {
                DebugManager.LogState("GatheringState: Resource was destroyed");
                HandleResourceDepleted();
                return;
            }

            // Poll for work progress (zero allocations!)
            var result = targetResource.CheckWorkProgress(workerId, out var fragment);

            switch (result)
            {
                case WorkResult.InProgress:
                    // Still working, nothing to do
                    break;

                case WorkResult.Waiting:
                    // Queued behind another worker, just wait
                    break;

                case WorkResult.FragmentReady:
                    // Fragment is ready, go pick it up
                    DebugManager.LogState("GatheringState: Fragment ready, going to pick it up");
                    HandleFragmentReady(fragment);
                    break;

                case WorkResult.StageComplete:
                    // Stage transition complete (e.g., tree fell), continue working
                    DebugManager.LogState("GatheringState: Stage complete, continuing to next stage");
                    HandleStageComplete();
                    break;

                case WorkResult.Depleted:
                    // Resource is depleted
                    DebugManager.LogState("GatheringState: Resource depleted");
                    HandleResourceDepleted();
                    break;

                case WorkResult.Invalid:
                    // Session was lost somehow, try to restart
                    DebugManager.LogState("GatheringState: Invalid session, attempting restart");
                    if (!targetResource.IsDepleted)
                    {
                        workStarted = targetResource.BeginWork(workerId, worker.transform);
                    }
                    else
                    {
                        HandleResourceDepleted();
                    }
                    break;
            }
        }

        private void HandleFragmentReady(ResourceFragment fragment)
        {
            if (fragment != null)
            {
                // Collect the fragment from the resource
                targetResource.CollectFragment(workerId);
                
                // Cancel our work session before transitioning
                targetResource.CancelWork(workerId);
                workStarted = false;

                // Go pick up the fragment
                worker.PickUpFragment(fragment, targetResource);
            }
            else
            {
                // Fragment was null (shouldn't happen but handle gracefully)
                HandleStageComplete();
            }
        }

        private void HandleStageComplete()
        {
            if (targetResource == null || targetResource.IsDepleted)
            {
                HandleResourceDepleted();
                return;
            }

            // Cancel old session
            targetResource.CancelWork(workerId);
            workStarted = false;

            // Check if we need to move to a new position for the next piece
            if (targetResource.DoesCurrentStageYieldPieces())
            {
                MoveToNextPieceAndContinue();
            }
            else
            {
                // Non-yielding stage, just restart work
                workStarted = targetResource.BeginWork(workerId, worker.transform);
                if (!workStarted)
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

            // Use sqrMagnitude for performance - use settings for close enough distance
            var settings = NPCSettingsSO.Instance;
            float distanceSqr = (worker.transform.position - nextWorkPosition).sqrMagnitude;
            float closeEnoughDistSqr = settings.CloseEnoughDistance * settings.CloseEnoughDistance;

            // If already close enough, just continue working
            if (distanceSqr <= closeEnoughDistSqr)
            {
                workStarted = targetResource.BeginWork(workerId, worker.transform);
                if (!workStarted)
                {
                    HandleResourceDepleted();
                }
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

            // Clean up work session
            if (targetResource != null && workStarted)
            {
                targetResource.CancelWork(workerId);
                workStarted = false;
            }

            // Use the Worker's helper method for consistent behavior
            worker.HandleResourceDepletedOrGone(targetResource);
        }

        public void Exit()
        {
            // Mark state as inactive FIRST to prevent stale operations
            isActive = false;

            // Cancel our work session with the resource
            if (targetResource != null && workStarted)
            {
                targetResource.CancelWork(workerId);
                workStarted = false;
            }
        }
    }
}