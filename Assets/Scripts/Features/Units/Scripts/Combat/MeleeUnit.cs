// ============================================================================
// MeleeUnit.cs - Concrete melee offensive unit implementation
// ============================================================================
using Assets.Scripts.Core;
using Assets.Scripts.Core.Services;
using Assets.Scripts.Data;
using Assets.Scripts.Shared.Interfaces;
using UnityEngine;

namespace Assets.Scripts.NPCs.Units
{
    /// <summary>
    /// Melee combat unit that attacks targets at close range.
    /// 
    /// Features:
    /// - Close-range melee attacks
    /// - Optional attack animation trigger
    /// - Simple damage application
    /// 
    /// This class demonstrates how to create a concrete offensive unit
    /// by inheriting from OffensiveNPCBase and implementing ExecuteAttack.
    /// </summary>
    public class MeleeUnit : OffensiveNPCBase
    {
        [Header("Melee Settings")]
        [Tooltip("Animator parameter name for attack animation")]
        [SerializeField] private string attackAnimTrigger = "Attack";
        
        [Tooltip("Optional: Visual effect prefab to spawn on attack")]
        [SerializeField] private GameObject attackEffectPrefab;
        
        [Tooltip("Optional: Audio clip to play on attack")]
        [SerializeField] private AudioClip attackSound;

        private Animator animator;
        private AudioSource audioSource;

        protected override void Awake()
        {
            base.Awake();
            
            npcName = "Melee Unit";
            
            // Get optional components
            animator = GetComponentInChildren<Animator>();
            audioSource = GetComponent<AudioSource>();
            
            // Set melee-specific defaults if not configured
            if (combatStats.AttackRange <= 0)
            {
                combatStats.AttackRange = CombatSettingsSO.Instance.DefaultMeleeRange;
            }
        }

        /// <summary>
        /// Execute a melee attack on the target.
        /// </summary>
        public override void ExecuteAttack(ITargetable target)
        {
            if (target == null) return;

            DebugManager.LogState($"{npcName}: Executing melee attack on {target.Transform.name}");

            // Play attack animation
            if (animator != null && !string.IsNullOrEmpty(attackAnimTrigger))
            {
                animator.SetTrigger(attackAnimTrigger);
            }

            // Play attack sound
            if (audioSource != null && attackSound != null)
            {
                audioSource.PlayOneShot(attackSound);
            }

            // Spawn attack effect
            if (attackEffectPrefab != null)
            {
                Vector3 effectPos = Vector3.Lerp(transform.position, target.TargetPosition, 0.5f);
                Instantiate(attackEffectPrefab, effectPos, Quaternion.identity);
            }

            // Apply damage
            ApplyDamageToTarget(target, combatStats.AttackDamage);

            // Record attack time
            lastAttackTime = Time.time;
        }

        /// <summary>
        /// Override to add melee-specific death behavior.
        /// </summary>
        protected override void OnDeath()
        {
            // Play death animation if available
            if (animator != null)
            {
                animator.SetTrigger("Death");
                // Delay destruction to allow animation
                Destroy(gameObject, 2f);
            }
            else
            {
                base.OnDeath();
            }
        }

#if UNITY_EDITOR
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            
            // Melee attack range visualization
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f); // Orange
            Gizmos.DrawSphere(transform.position, combatStats.AttackRange);
        }
#endif
    }
}
