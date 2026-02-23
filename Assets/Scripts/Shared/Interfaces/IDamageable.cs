// ============================================================================
// IDamageable.cs - Interface for objects that can receive damage
// ============================================================================
using UnityEngine;

namespace Assets.Scripts.Shared.Interfaces
{
    /// <summary>
    /// Interface for any entity that can receive damage.
    /// 
    /// Design Notes:
    /// - Separates damage handling from NPC hierarchy
    /// - Enables damage to work on NPCs, buildings, destructibles, etc.
    /// - Provides consistent damage handling across the game
    /// </summary>
    public interface IDamageable
    {
        /// <summary>
        /// Current health of this entity.
        /// </summary>
        float CurrentHealth { get; }

        /// <summary>
        /// Maximum health of this entity.
        /// </summary>
        float MaxHealth { get; }

        /// <summary>
        /// Whether this entity is still alive (health > 0).
        /// </summary>
        bool IsAlive { get; }

        /// <summary>
        /// The transform of this damageable entity (for distance checks).
        /// </summary>
        Transform Transform { get; }

        /// <summary>
        /// Apply damage to this entity.
        /// </summary>
        /// <param name="amount">Amount of damage to apply.</param>
        /// <param name="source">The GameObject causing the damage (optional).</param>
        void TakeDamage(float amount, GameObject source = null);

        /// <summary>
        /// Heal this entity.
        /// </summary>
        /// <param name="amount">Amount of health to restore.</param>
        void Heal(float amount);
    }
}
