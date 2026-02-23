// ============================================================================
// CombatIdleState.cs - Idle state for offensive units
// ============================================================================
using Assets.Scripts.Core;
using Assets.Scripts.Core.Services;
using Assets.Scripts.NPCs.Units;
using Assets.Scripts.Shared.Interfaces;
using UnityEngine;

namespace Assets.Scripts.NPCs.States.Combat
{
    /// <summary>
    /// Idle state for offensive units.
    /// Periodically searches for targets within detection range.
    /// </summary>
    public class CombatIdleState : IState
    {
        private readonly OffensiveNPCBase offensiveNPC;
        private readonly StateMachine stateMachine;
        private float searchTimer;

        public StateCategory Category => StateCategory.Idle;

        public CombatIdleState(OffensiveNPCBase offensiveNPC, StateMachine stateMachine)
        {
            this.offensiveNPC = offensiveNPC;
            this.stateMachine = stateMachine;
        }

        public void Enter()
        {
            offensiveNPC.Motor.Stop();
            offensiveNPC.ClearTarget();
            searchTimer = 0f;
            DebugManager.LogState($"{offensiveNPC.name}: Entered Combat Idle state");
        }

        public void Update()
        {
            searchTimer += Time.deltaTime;

            // Periodically search for targets
            if (searchTimer >= offensiveNPC.CombatStats.TargetSearchInterval)
            {
                searchTimer = 0f;

                // Search for a target
                var target = offensiveNPC.FindNearestTarget();
                if (target != null)
                {
                    offensiveNPC.SetTarget(target);
                    stateMachine.SetState<ChaseTargetState>();
                }
            }
        }

        public void Exit()
        {
            searchTimer = 0f;
        }
    }
}
