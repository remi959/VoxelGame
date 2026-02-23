// ============================================================================
// CombatEvents.cs - Events related to combat system
// ============================================================================
using UnityEngine;

namespace Assets.Scripts.Events
{
    /// <summary>
    /// Event fired when an entity takes damage.
    /// </summary>
    public readonly struct DamageEvent
    {
        /// <summary>
        /// The entity that received damage.
        /// </summary>
        public readonly GameObject Target;

        /// <summary>
        /// The entity that dealt the damage (null if environmental).
        /// </summary>
        public readonly GameObject Source;

        /// <summary>
        /// Amount of damage dealt.
        /// </summary>
        public readonly float Amount;

        /// <summary>
        /// The target's remaining health after damage.
        /// </summary>
        public readonly float RemainingHealth;

        public DamageEvent(GameObject target, GameObject source, float amount, float remainingHealth)
        {
            Target = target;
            Source = source;
            Amount = amount;
            RemainingHealth = remainingHealth;
        }
    }

    /// <summary>
    /// Event fired when an entity dies.
    /// </summary>
    public readonly struct EntityDeathEvent
    {
        /// <summary>
        /// The entity that died.
        /// </summary>
        public readonly GameObject Entity;

        /// <summary>
        /// The entity that dealt the killing blow (null if environmental).
        /// </summary>
        public readonly GameObject Killer;

        /// <summary>
        /// Position where the death occurred.
        /// </summary>
        public readonly Vector3 Position;

        public EntityDeathEvent(GameObject entity, GameObject killer, Vector3 position)
        {
            Entity = entity;
            Killer = killer;
            Position = position;
        }
    }

    /// <summary>
    /// Event fired when a combat unit acquires a new target.
    /// </summary>
    public readonly struct TargetAcquiredEvent
    {
        /// <summary>
        /// The unit that acquired a target.
        /// </summary>
        public readonly GameObject Attacker;

        /// <summary>
        /// The target that was acquired.
        /// </summary>
        public readonly GameObject Target;

        public TargetAcquiredEvent(GameObject attacker, GameObject target)
        {
            Attacker = attacker;
            Target = target;
        }
    }

    /// <summary>
    /// Event fired when a combat unit loses its target.
    /// </summary>
    public readonly struct TargetLostEvent
    {
        /// <summary>
        /// The unit that lost its target.
        /// </summary>
        public readonly GameObject Attacker;

        /// <summary>
        /// Reason the target was lost.
        /// </summary>
        public readonly TargetLostReason Reason;

        public TargetLostEvent(GameObject attacker, TargetLostReason reason)
        {
            Attacker = attacker;
            Reason = reason;
        }
    }

    /// <summary>
    /// Reasons why a target was lost.
    /// </summary>
    public enum TargetLostReason
    {
        TargetDied,
        OutOfRange,
        TargetUntargetable,
        ManualClear
    }
}
