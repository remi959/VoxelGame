// ============================================================================
// TargetDummy.cs - Non-combat NPC for testing offensive units
// ============================================================================
using Assets.Scripts.Core.Events;
using Assets.Scripts.Events;
using Assets.Scripts.NPCs.States;
using Assets.Scripts.Shared.Interfaces;
using UnityEngine;

namespace Assets.Scripts.NPCs.Units
{
    /// <summary>
    /// A non-combat NPC that can be targeted and damaged by offensive units.
    /// Used for testing the combat system.
    /// 
    /// Features:
    /// - Can be targeted (ITargetable)
    /// - Can be damaged (IDamageable)
    /// - Visual feedback when hit
    /// - Does NOT fight back (non-offensive)
    /// 
    /// This demonstrates proper separation between combat and non-combat NPCs.
    /// </summary>
    public class TargetDummy : NPCBase, IDamageable, ITargetable
    {
        // ========== Configuration ==========

        [Header("Target Dummy Settings")]
        [SerializeField] private float maxHealth = 100f;
        
        [Tooltip("Faction ID - set different from attackers to be targetable")]
        [SerializeField] private int factionId = 1;
        
        [Tooltip("Target priority (higher = targeted first)")]
        [SerializeField] private int targetPriority = 0;

        [Header("Visual Feedback")]
        [Tooltip("Color to flash when hit")]
        [SerializeField] private Color hitFlashColor = Color.red;
        
        [Tooltip("Duration of hit flash")]
        [SerializeField] private float hitFlashDuration = 0.2f;

        [Header("Debug")]
        [SerializeField] private bool logDamage = true;

        // ========== Runtime State ==========

        private float currentHealth;
        private bool isDead;
        private Renderer[] renderers;
        private Color[] originalColors;
        private float hitFlashTimer;

        // ========== IDamageable Implementation ==========

        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public bool IsAlive => !isDead && currentHealth > 0;
        public Transform Transform => transform;

        // ========== ITargetable Implementation ==========

        public int FactionId => factionId;
        public bool IsTargetable => IsAlive && gameObject.activeInHierarchy;
        public int TargetPriority => targetPriority;
        public Vector3 TargetPosition => transform.position + Vector3.up;

        // ========== Lifecycle ==========

        protected override void Awake()
        {
            base.Awake();
            npcName = "Target Dummy";
            currentHealth = maxHealth;

            // Get renderers for visual feedback
            renderers = GetComponentsInChildren<Renderer>();
            originalColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i].material.HasProperty("_Color"))
                {
                    originalColors[i] = renderers[i].material.color;
                }
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            
            // Register as targetable in global registry
            TargetRegistry.Register(this);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            TargetRegistry.Unregister(this);
        }

        protected override void InitializeStateMachine()
        {
            // Simple state machine - just idle
            stateMachine = new StateMachine();
            stateMachine.AddState(new TargetDummyIdleState(this));
            stateMachine.SetState<TargetDummyIdleState>();
        }

        public override void UpdateNPC()
        {
            base.UpdateNPC();
            
            // Handle hit flash fade
            if (hitFlashTimer > 0)
            {
                hitFlashTimer -= Time.deltaTime;
                if (hitFlashTimer <= 0)
                {
                    ResetColors();
                }
            }
        }

        // ========== Damage Handling ==========

        public void TakeDamage(float amount, GameObject source = null)
        {
            if (isDead || amount <= 0) return;

            currentHealth = Mathf.Max(0, currentHealth - amount);

            if (logDamage)
            {
                string sourceName = source != null ? source.name : "Unknown";
                Debug.Log($"[TargetDummy] {npcName}: Took {amount} damage from {sourceName}. Health: {currentHealth}/{maxHealth}");
            }

            // Visual feedback
            FlashColor();

            // Publish damage event
            EventBus.Publish(new DamageEvent(gameObject, source, amount, currentHealth));

            // Check for death
            if (currentHealth <= 0)
            {
                Die(source);
            }
        }

        public void Heal(float amount)
        {
            if (isDead || amount <= 0) return;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        }

        /// <summary>
        /// Reset health to maximum.
        /// </summary>
        public void ResetHealth()
        {
            currentHealth = maxHealth;
            isDead = false;
            gameObject.SetActive(true);
            Debug.Log($"[TargetDummy] {npcName}: Health reset to {maxHealth}");
        }

        // ========== Visual Feedback ==========

        private void FlashColor()
        {
            hitFlashTimer = hitFlashDuration;
            
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].material.HasProperty("_Color"))
                {
                    renderers[i].material.color = hitFlashColor;
                }
            }
        }

        private void ResetColors()
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].material.HasProperty("_Color"))
                {
                    renderers[i].material.color = originalColors[i];
                }
            }
        }

        // ========== Death ==========

        private void Die(GameObject killer)
        {
            if (isDead) return;
            isDead = true;

            string killerName = killer != null ? killer.name : "Unknown";
            Debug.Log($"[TargetDummy] {npcName}: Destroyed by {killerName}!");

            // Publish death event
            EventBus.Publish(new EntityDeathEvent(gameObject, killer, transform.position));

            // Visual feedback - could spawn particles, play sound, etc.
            // For testing, just deactivate
            gameObject.SetActive(false);
        }

        // ========== Debug ==========

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Health bar visualization
            float healthPercent = Application.isPlaying ? currentHealth / maxHealth : 1f;
            
            Vector3 barPos = transform.position + Vector3.up * 2.5f;
            Vector3 barSize = new Vector3(1f, 0.1f, 0.1f);
            
            // Background
            Gizmos.color = Color.black;
            Gizmos.DrawCube(barPos, barSize);
            
            // Health
            Gizmos.color = Color.Lerp(Color.red, Color.green, healthPercent);
            Vector3 healthSize = new Vector3(healthPercent, 0.08f, 0.08f);
            Vector3 healthPos = barPos - Vector3.right * (1f - healthPercent) * 0.5f;
            Gizmos.DrawCube(healthPos, healthSize);

            // Faction indicator
            Gizmos.color = factionId == 0 ? Color.blue : Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
#endif
    }

    /// <summary>
    /// Simple idle state for TargetDummy.
    /// </summary>
    public class TargetDummyIdleState : IState
    {
        private readonly NPCBase npc;

        public StateCategory Category => StateCategory.Idle;

        public TargetDummyIdleState(NPCBase npc)
        {
            this.npc = npc;
        }

        public void Enter()
        {
            npc.Motor.Stop();
        }

        public void Update() { }
        public void Exit() { }
    }
}
