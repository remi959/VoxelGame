// ============================================================================
// AttackCooldownState.cs - State for handling attack cooldown
// ============================================================================
using Assets.Scripts.Core;
using Assets.Scripts.Core.Services;
using Assets.Scripts.NPCs.Units;
using Assets.Scripts.Shared.Interfaces;
using UnityEngine;

namespace Assets.Scripts.NPCs.States.Combat
{
    /// <summary>
    /// Cooldown state after attacking.
    /// Waits for attack cooldown then transitions back to attack or chase.
    /// </summary>
    public class AttackCooldownState : IState
    {
        private readonly OffensiveNPCBase offensiveNPC;
        private readonly StateMachine stateMachine;
        
        private float cooldownTimer;
        private float targetCheckTimer;
        private const float TargetCheckInterval = 0.2f;

        public StateCategory Category => StateCategory.Combat;

        public AttackCooldownState(OffensiveNPCBase offensiveNPC, StateMachine stateMachine)
        {
            this.offensiveNPC = offensiveNPC;
            this.stateMachine = stateMachine;
        }

        public void Enter()
        {
            cooldownTimer = 0f;
            targetCheckTimer = 0f;
            DebugManager.LogState($"{offensiveNPC.name}: Entering Attack Cooldown state (cooldown: {offensiveNPC.CombatStats.AttackCooldown}s)");
        }

        public void Update()
        {
            cooldownTimer += Time.deltaTime;
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
            }

            // Cooldown finished
            if (cooldownTimer >= offensiveNPC.CombatStats.AttackCooldown)
            {
                offensiveNPC.ResetAttackCooldown();
                
                // Check if still in range
                float distanceToTarget = offensiveNPC.GetDistanceToTarget();
                
                if (distanceToTarget <= offensiveNPC.CombatStats.AttackRange)
                {
                    // Still in range, attack again
                    stateMachine.SetState<AttackingState>();
                }
                else
                {
                    // Out of range, chase
                    stateMachine.SetState<ChaseTargetState>();
                }
            }
        }

        public void Exit()
        {
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
