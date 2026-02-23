// ============================================================================
// BuildingStateBase.cs - Shared behavior for building states
// ============================================================================
using Assets.Scripts.Buildings;
using Assets.Scripts.NPCs.Units;
using Assets.Scripts.Shared.Interfaces;
using UnityEngine;

namespace Assets.Scripts.NPCs.States.Building
{
    /// <summary>
    /// Base class for building construction states.
    /// Provides shared access to worker, state machine, and per-worker building context.
    /// 
    /// Uses BuildingWorkContext (per-worker instance) instead of static context
    /// to support multiple workers building simultaneously without conflicts.
    /// </summary>
    public abstract class BuildingStateBase : IState
    {
        protected readonly Worker worker;
        protected readonly StateMachine stateMachine;

        /// <summary>
        /// All building states are in the Building category.
        /// </summary>
        public StateCategory Category => StateCategory.Building;

        /// <summary>
        /// Per-worker building context. Accessed via Worker.BuildingContext.
        /// This replaces the old static BuildingContext class.
        /// </summary>
        protected BuildingWorkContext Context => worker.BuildingContext;

        protected BuildingStateBase(Worker worker, StateMachine stateMachine)
        {
            this.worker = worker;
            this.stateMachine = stateMachine;
        }

        public abstract void Enter();
        public abstract void Update();
        public abstract void Exit();

        /// <summary>
        /// Clean up building context and return to idle.
        /// Unassigns worker from blueprint and drops any carried part.
        /// The dropped part is registered with the blueprint for recovery by other workers.
        /// </summary>
        protected void AbandonBuilding()
        {
            AbandonBuilding(dropPart: true);
        }

        /// <summary>
        /// Clean up building context and return to idle.
        /// </summary>
        /// <param name="dropPart">If true, drops the part and registers it for recovery. 
        /// If false, the worker keeps carrying the part (for player move commands).</param>
        protected void AbandonBuilding(bool dropPart)
        {
            var blueprint = Context.CurrentBlueprint;
            var part = Context.CurrentPart;
            var slotIndex = Context.CurrentSlotIndex;

            if (blueprint != null)
            {
                blueprint.UnassignWorker(worker);
            }

            if (part != null && dropPart)
            {
                // Register the dropped part with the blueprint so another worker can pick it up
                if (blueprint != null && slotIndex >= 0)
                {
                    blueprint.RegisterDroppedPart(slotIndex, part);
                }
                part.Drop();
            }

            // Clear context (but if not dropping, part stays attached to worker)
            if (dropPart)
            {
                Context.Clear();
            }
            else
            {
                // Keep the part reference but clear blueprint assignment
                Context.CurrentBlueprint = null;
                Context.CurrentSlot = null;
            }
            
            stateMachine.SetState<WorkerIdleState>();
        }

        /// <summary>
        /// Move to position, then transition to next state.
        /// </summary>
        protected void MoveToThen<TNextState>(Vector3 position) where TNextState : IState
        {
            var moveState = stateMachine.GetState<MoveToTargetState>();
            moveState.SetTarget(position, () =>
            {
                stateMachine.SetState<TNextState>();
            });
            stateMachine.SetState<MoveToTargetState>();
        }
    }
}