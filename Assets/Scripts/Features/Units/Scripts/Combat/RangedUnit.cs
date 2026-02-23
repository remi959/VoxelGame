// ============================================================================
// RangedUnit.cs - Concrete ranged offensive unit implementation
// ============================================================================
using Assets.Scripts.Core;
using Assets.Scripts.Core.Services;
using Assets.Scripts.Data;
using Assets.Scripts.Shared.Interfaces;
using UnityEngine;

namespace Assets.Scripts.NPCs.Units
{
    /// <summary>
    /// Ranged combat unit that attacks targets from a distance.
    /// 
    /// Features:
    /// - Long-range projectile/hitscan attacks
    /// - Optional projectile spawning
    /// - Configurable attack pattern
    /// 
    /// This class demonstrates how to create a ranged offensive unit
    /// with different attack behavior from MeleeUnit.
    /// </summary>
    public class RangedUnit : OffensiveNPCBase
    {
        [Header("Ranged Settings")]
        [Tooltip("Projectile prefab to spawn (optional - if null, uses hitscan)")]
        [SerializeField] private GameObject projectilePrefab;
        
        [Tooltip("Transform where projectiles spawn from")]
        [SerializeField] private Transform projectileSpawnPoint;
        
        [Tooltip("Projectile speed (if using projectiles)")]
        [SerializeField] private float projectileSpeed = 20f;
        
        [Tooltip("Animator parameter name for attack animation")]
        [SerializeField] private string attackAnimTrigger = "Attack";
        
        [Tooltip("Optional: Audio clip to play on attack")]
        [SerializeField] private AudioClip attackSound;
        
        [Tooltip("Optional: Muzzle flash effect prefab")]
        [SerializeField] private GameObject muzzleFlashPrefab;

        private Animator animator;
        private AudioSource audioSource;

        protected override void Awake()
        {
            base.Awake();
            
            npcName = "Ranged Unit";
            
            // Get optional components
            animator = GetComponentInChildren<Animator>();
            audioSource = GetComponent<AudioSource>();
            
            // Set ranged-specific defaults if not configured
            if (combatStats.AttackRange <= 0)
            {
                combatStats.AttackRange = CombatSettingsSO.Instance.DefaultRangedRange;
            }

            // Default spawn point to transform if not set
            if (projectileSpawnPoint == null)
            {
                projectileSpawnPoint = transform;
            }
        }

        /// <summary>
        /// Execute a ranged attack on the target.
        /// </summary>
        public override void ExecuteAttack(ITargetable target)
        {
            if (target == null) return;

            DebugManager.LogState($"{npcName}: Executing ranged attack on {target.Transform.name}");

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

            // Spawn muzzle flash
            if (muzzleFlashPrefab != null)
            {
                var flash = Instantiate(muzzleFlashPrefab, projectileSpawnPoint.position, projectileSpawnPoint.rotation);
                Destroy(flash, 0.5f);
            }

            // Either spawn projectile or do hitscan
            if (projectilePrefab != null)
            {
                SpawnProjectile(target);
            }
            else
            {
                // Hitscan attack - instant damage
                ApplyDamageToTarget(target, combatStats.AttackDamage);
            }

            // Record attack time
            lastAttackTime = Time.time;
        }

        /// <summary>
        /// Spawn a projectile toward the target.
        /// </summary>
        private void SpawnProjectile(ITargetable target)
        {
            Vector3 spawnPos = projectileSpawnPoint.position;
            Vector3 direction = (target.TargetPosition - spawnPos).normalized;
            
            Quaternion rotation = Quaternion.LookRotation(direction);
            
            var projectileObj = Instantiate(projectilePrefab, spawnPos, rotation);
            
            // Try to initialize projectile if it has our interface
            var projectile = projectileObj.GetComponent<IProjectile>();
            if (projectile != null)
            {
                projectile.Initialize(gameObject, combatStats.AttackDamage, projectileSpeed, target);
            }
            else
            {
                // Fallback: Just apply velocity if it has a Rigidbody
                var rb = projectileObj.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = direction * projectileSpeed;
                }
                
                // Auto-destroy projectile after some time
                Destroy(projectileObj, 5f);
            }
        }

        /// <summary>
        /// Override to add ranged-specific death behavior.
        /// </summary>
        protected override void OnDeath()
        {
            // Play death animation if available
            if (animator != null)
            {
                animator.SetTrigger("Death");
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
            
            // Show projectile spawn point
            if (projectileSpawnPoint != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(projectileSpawnPoint.position, 0.2f);
                Gizmos.DrawRay(projectileSpawnPoint.position, projectileSpawnPoint.forward * 2f);
            }
        }
#endif
    }

    /// <summary>
    /// Interface for projectiles spawned by ranged units.
    /// </summary>
    public interface IProjectile
    {
        /// <summary>
        /// Initialize the projectile with attack data.
        /// </summary>
        void Initialize(GameObject owner, float damage, float speed, ITargetable target);
    }
}
