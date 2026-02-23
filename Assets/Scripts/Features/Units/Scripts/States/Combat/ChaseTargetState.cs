// ============================================================================
// ChaseTargetState.cs - State for moving toward a combat target
// ============================================================================
using Assets.Scripts.Core;
using Assets.Scripts.Core.Services;
using Assets.Scripts.NPCs.Units;
using Assets.Scripts.Shared.Interfaces;
using UnityEngine;

namespace Assets.Scripts.NPCs.States.Combat
{
    /// <summary>
    /// Chase state for offensive units.
    /// Moves toward the current target until within attack range.
    /// </summary>
    public class ChaseTargetState : IState
    {
        private readonly OffensiveNPCBase offensiveNPC;
        private readonly StateMachine stateMachine;
        
        private Vector3 chaseOrigin;
        private float targetCheckTimer;
        private const float TargetCheckInterval = 0.2f;

        public StateCategory Category => StateCategory.Combat;

        public ChaseTargetState(OffensiveNPCBase offensiveNPC, StateMachine stateMachine)
        {
            this.offensiveNPC = offensiveNPC;
            this.stateMachine = stateMachine;
        }

        public void Enter()
        {
            chaseOrigin = offensiveNPC.transform.position;
            targetCheckTimer = 0f;
            DebugManager.LogState($"{offensiveNPC.name}: Entering Chase Target state");

            if (!ValidateTarget())
            {
                ReturnToIdle();
                return;
            }

            // Start moving toward target
            MoveTowardTarget();
        }

        public void Update()
        {
            // Periodically validate target and update path
            targetCheckTimer += Time.deltaTime;
            if (targetCheckTimer >= TargetCheckInterval)
            {
                targetCheckTimer = 0f;

                if (!ValidateTarget())
                {
                    ReturnToIdle();
                    return;
                }

                // Check if we've chased too far
                float chaseDistance = Vector3.Distance(chaseOrigin, offensiveNPC.transform.position);
                if (chaseDistance > offensiveNPC.CombatStats.MaxChaseDistance)
                {
                    DebugManager.LogState($"{offensiveNPC.name}: Target too far, returning to idle");
                    ReturnToIdle();
                    return;
                }

                // Check if within attack range
                float distanceToTarget = offensiveNPC.GetDistanceToTarget();
                if (distanceToTarget <= offensiveNPC.CombatStats.AttackRange)
                {
                    stateMachine.SetState<AttackingState>();
                    return;
                }

                // Update path to target (target may have moved)
                MoveTowardTarget();
            }
        }

        public void Exit()
        {
            offensiveNPC.Motor.Stop();
        }

        private void MoveTowardTarget()
        {
            var target = offensiveNPC.CurrentTarget;
            if (target != null)
            {
                offensiveNPC.Motor.SetDestination(target.TargetPosition);
            }
        }

        private bool ValidateTarget()
        {
            var target = offensiveNPC.CurrentTarget;
            
            // Target is gone or invalid
            if (target == null || !target.IsTargetable)
            {
                return false;
            }

            // Target is an IDamageable that is no longer alive
            if (target is IDamageable damageable && !damageable.IsAlive)
            {
                return false;
            }

            return true;
        }

        private void ReturnToIdle()
        {
            offensiveNPC.ClearTarget();
            stateMachine.SetState<CombatIdleState>();
        }
    }
}
