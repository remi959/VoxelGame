using Assets.Scripts.Shared.Interfaces;
using UnityEngine;

namespace Assets.Scripts.NPCs.States
{
    public class MoveToTargetState : IState
    {
        private readonly NPCBase npc;
        private readonly StateMachine stateMachine;
        private readonly float arrivalDistance;

        private Vector3? targetPosition;
        private System.Action onArrived;

        public MoveToTargetState(NPCBase npc, StateMachine stateMachine, float arrivalDistance = 2f)
        {
            this.npc = npc;
            this.stateMachine = stateMachine;
            this.arrivalDistance = arrivalDistance;
        }

        public void SetTarget(Vector3 position, System.Action onArrivedCallback)
        {
            targetPosition = position;
            onArrived = onArrivedCallback;
        }

        public void Enter()
        {
            if (targetPosition.HasValue)
            {
                npc.Motor.SetDestination(targetPosition.Value);
            }
            else
            {
                stateMachine.SetState<WorkerIdleState>();
            }
        }

        public void Update()
        {
            if (!targetPosition.HasValue)
            {
                stateMachine.SetState<WorkerIdleState>();
                return;
            }

            float distance = Vector3.Distance(npc.transform.position, targetPosition.Value);

            if (distance <= arrivalDistance)
            {
                // Store callback before clearing
                var callback = onArrived;

                // Clear state data
                targetPosition = null;
                onArrived = null;

                // Invoke callback (which will transition to next state)
                callback?.Invoke();
            }
        }

        public void Exit()
        {
            // Data is cleared when arriving or overwritten by next SetTarget call
        }
    }
}