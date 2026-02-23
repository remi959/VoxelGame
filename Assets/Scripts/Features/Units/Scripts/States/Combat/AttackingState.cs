// ============================================================================
// AttackingState.cs - State for executing attacks on target
// ============================================================================
using Assets.Scripts.Core;
using Assets.Scripts.Core.Services;
using Assets.Scripts.NPCs.Units;
using Assets.Scripts.Shared.Interfaces;
using UnityEngine;

namespace Assets.Scripts.NPCs.States.Combat
{
    /// <summary>
    /// Attack state for offensive units.
    /// Executes attack and handles cooldown transitions.
    /// </summary>
    public class AttackingState : IState
    {
        private readonly OffensiveNPCBase offensiveNPC;
        private readonly StateMachine stateMachine;
        
        private bool attackExecuted;
        private float stateTimer;
        private const float TargetCheckInterval = 0.1f;
        private float targetCheckTimer;

        public StateCategory Category => StateCategory.Combat;

        public AttackingState(OffensiveNPCBase offensiveNPC, StateMachine stateMachine)
        {
            this.offensiveNPC = offensiveNPC;
            this.stateMachine = stateMachine;
        }

        public void Enter()
        {
            attackExecuted = false;
            stateTimer = 0f;
            targetCheckTimer = 0f;
            offensiveNPC.Motor.Stop();
            
            DebugManager.LogState($"{offensiveNPC.name}: Entering Attacking state");

            if (!ValidateTarget())
            {
                ReturnToIdle();
                return;
            }

            // Face the target
            FaceTarget();
        }

        public void Update()
        {
            stateTimer += Time.deltaTime;
            targetCheckTimer += Time.deltaTime;

            // Periodically check target validity
            if (targetCheckTimer >= TargetCheckInterval)
            {
                targetCheckTimer = 0f;

                if (!ValidateTarget())
                {
                    ReturnToIdle();
                    return;
                }

                // Check if target moved out of range
                float distanceToTarget = offensiveNPC.GetDistanceToTarget();
                if (distanceToTarget > offensiveNPC.CombatStats.AttackRange * 1.2f) // Small buffer
                {
                    stateMachine.SetState<ChaseTargetState>();
                    return;
                }
            }

            // Execute attack if ready
            if (!attackExecuted && offensiveNPC.CanAttack())
            {
                ExecuteAttack();
                attackExecuted = true;
            }

            // After attack, go to cooldown
            if (attackExecuted)
            {
                stateMachine.SetState<AttackCooldownState>();
            }
        }

        public void Exit()
        {
        }

        private void ExecuteAttack()
        {
            var target = offensiveNPC.CurrentTarget;
            if (target == null) return;

            DebugManager.LogState($"{offensiveNPC.name}: Attacking {target.Transform.name}");
            
            // Execute the attack (polymorphic - melee vs ranged handled differently)
            offensiveNPC.ExecuteAttack(target);
        }

        private void FaceTarget()
        {
            var target = offensiveNPC.CurrentTarget;
            if (target == null) return;

            Vector3 direction = (target.TargetPosition - offensiveNPC.transform.position).normalized;
            direction.y = 0; // Keep upright
            
            if (direction != Vector3.zero)
            {
                offensiveNPC.transform.rotation = Quaternion.LookRotation(direction);
            }
        }

        private bool ValidateTarget()
        {
            var target = offensiveNPC.CurrentTarget;
            
            if (target == null || !target.IsTargetable)
            {
                return false;
            }

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
