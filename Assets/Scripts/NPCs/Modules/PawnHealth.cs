using System;
using Assets.Scripts.Events;
using UnityEngine;

namespace Assets.Scripts.NPCs.Modules
{
    /// <summary>
    /// Tracks hit points, applies damage, and fires a death event.
    /// Knows nothing about what "dying" means to the game — it just reports it.
    /// </summary>
    public class PawnHealth : MonoBehaviour
    {
        [SerializeField] private float maxHealth = 100f;

        private float currentHealth;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public bool IsDead => currentHealth <= 0f;

        /// <summary>
        /// Fired once when health reaches zero. Subscribers decide what happens (destroy, ragdoll, etc.).
        /// </summary>
        public event Action OnDeath;

        private void Awake()
        {
            currentHealth = maxHealth;
        }

        public void TakeDamage(float amount)
        {
            if (IsDead) return;

            currentHealth = Mathf.Max(0f, currentHealth - amount);

            if (currentHealth <= 0f)
            {
                OnDeath?.Invoke();
                EventBus.Publish(new NPCDiedEvent { NPC = gameObject });
            }
        }

        public void Heal(float amount)
        {
            if (IsDead) return;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        }
    }
}
