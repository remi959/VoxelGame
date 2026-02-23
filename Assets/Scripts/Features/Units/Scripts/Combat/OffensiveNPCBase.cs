// ============================================================================
// OffensiveNPCBase.cs - Base class for all combat-capable NPCs
// ============================================================================
using Assets.Scripts.Core.Events;
using Assets.Scripts.Core.Services;
using Assets.Scripts.Events;
using Assets.Scripts.NPCs.States;
using Assets.Scripts.NPCs.States.Combat;
using Assets.Scripts.Shared.Interfaces;
using UnityEngine;

namespace Assets.Scripts.NPCs.Units
{
    /// <summary>
    /// Base class for all offensive/combat-capable NPCs.
    /// 
    /// Provides:
    /// - Health and damage handling (IDamageable)
    /// - Targeting capability (ITargetable)
    /// - Target acquisition and tracking
    /// - Combat state machine integration
    /// - Attack execution framework
    /// 
    /// Subclasses (e.g., MeleeUnit, RangedUnit) implement specific attack behavior.
    /// 
    /// Design Principles:
    /// - Inherits from NPCBase for movement, selection, update registration
    /// - Composition over inheritance for combat stats
    /// - Virtual methods for customizable behavior
    /// - Abstract methods for required overrides
    /// </summary>
    public abstract class OffensiveNPCBase : NPCBase, IDamageable, ITargetable
    {
        // ========== Combat Components ==========

        [Header("Combat Stats")]
        [SerializeField] protected CombatStats combatStats = new();

        [Header("Faction")]
        [Tooltip("Faction ID for friend/foe identification")]
        [SerializeField] protected int factionId = 0;

        [Tooltip("Priority when being targeted (higher = targeted first)")]
        [SerializeField] protected int targetPriority = 1;

        // ========== Runtime State ==========

        protected float currentHealth;
        protected ITargetable currentTarget;
        protected float lastAttackTime;
        protected bool isDead;

        // ========== Properties ==========

        /// <summary>
        /// The offensive unit's combat statistics.
        /// </summary>
        public CombatStats CombatStats => combatStats;

        /// <summary>
        /// Current combat target.
        /// </summary>
        public ITargetable CurrentTarget => currentTarget;

        /// <summary>
        /// Whether this unit currently has a target.
        /// </summary>
        public bool HasTarget => currentTarget != null && currentTarget.IsTargetable;

        // ========== IDamageable Implementation ==========

        public float CurrentHealth => currentHealth;
        public float MaxHealth => combatStats.MaxHealth;
        public bool IsAlive => !isDead && currentHealth > 0;
        public Transform Transform => transform;

        // ========== ITargetable Implementation ==========

        public int FactionId => factionId;
        public bool IsTargetable => IsAlive && gameObject.activeInHierarchy;
        public int TargetPriority => targetPriority;
        public Vector3 TargetPosition => transform.position + Vector3.up; // Center mass

        // ========== Lifecycle ==========

        protected override void Awake()
        {
            base.Awake();
            
            // Initialize health
            currentHealth = combatStats.MaxHealth;
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
            
            // Unregister as targetable
            TargetRegistry.Unregister(this);
        }

        /// <summary>
        /// Initialize the state machine with combat states.
        /// Override to add unit-specific states.
        /// </summary>
        protected override void InitializeStateMachine()
        {
            stateMachine = new StateMachine();

            // Add core combat states
            stateMachine.AddState(new CombatIdleState(this, stateMachine));
            stateMachine.AddState(new ChaseTargetState(this, stateMachine));
            stateMachine.AddState(new AttackingState(this, stateMachine));
            stateMachine.AddState(new AttackCooldownState(this, stateMachine));
            stateMachine.AddState(new MoveToTargetState(this, stateMachine, combatStats.AttackRange));

            // Allow subclasses to add additional states
            InitializeAdditionalStates();

            // Start in idle
            stateMachine.SetState<CombatIdleState>();
        }

        /// <summary>
        /// Override in subclasses to add unit-specific states.
        /// </summary>
        protected virtual void InitializeAdditionalStates()
        {
        }

        public override void UpdateNPC()
        {
            base.UpdateNPC();
            
            // Health regeneration
            if (IsAlive && combatStats.HealthRegenRate > 0)
            {
                Heal(combatStats.HealthRegenRate * Time.deltaTime);
            }
        }

        // ========== Damage & Health ==========

        public virtual void TakeDamage(float amount, GameObject source = null)
        {
            if (isDead || amount <= 0) return;

            currentHealth = Mathf.Max(0, currentHealth - amount);
            
            DebugManager.LogState($"{npcName}: Took {amount} damage. Health: {currentHealth}/{combatStats.MaxHealth}");

            // Publish damage event
            EventBus.Publish(new DamageEvent(gameObject, source, amount, currentHealth));

            // Check for death
            if (currentHealth <= 0)
            {
                Die(source);
            }
            else
            {
                // React to being damaged (could switch to chase attacker)
                OnDamaged(source);
            }
        }

        public virtual void Heal(float amount)
        {
            if (isDead || amount <= 0) return;

            currentHealth = Mathf.Min(combatStats.MaxHealth, currentHealth + amount);
        }

        /// <summary>
        /// Called when taking damage but not dying.
        /// Override to implement aggro, flee, etc.
        /// </summary>
        protected virtual void OnDamaged(GameObject source)
        {
            // Default: If we don't have a target and source is targetable, target it
            if (!HasTarget && source != null)
            {
                var targetable = source.GetComponent<ITargetable>();
                if (targetable != null && IsValidTarget(targetable))
                {
                    SetTarget(targetable);
                    stateMachine.SetState<ChaseTargetState>();
                }
            }
        }

        /// <summary>
        /// Handle death of this unit.
        /// </summary>
        protected virtual void Die(GameObject killer)
        {
            if (isDead) return;
            isDead = true;

            DebugManager.LogState($"{npcName}: Died!");

            // Clear target
            ClearTarget();

            // Publish death event
            EventBus.Publish(new EntityDeathEvent(gameObject, killer, transform.position));

            // Stop all behavior
            motor.Stop();

            // Default behavior: destroy after delay
            // Subclasses can override for death animations, loot drops, etc.
            OnDeath();
        }

        /// <summary>
        /// Called when this unit dies.
        /// Override for death animations, loot, etc.
        /// </summary>
        protected virtual void OnDeath()
        {
            // Default: Destroy after a short delay
            Destroy(gameObject, 0.1f);
        }

        // ========== Target Acquisition ==========

        /// <summary>
        /// Set the current target.
        /// </summary>
        public void SetTarget(ITargetable target)
        {
            if (target == currentTarget) return;

            currentTarget = target;

            if (target != null)
            {
                DebugManager.LogState($"{npcName}: Acquired target {target.Transform.name}");
                EventBus.Publish(new TargetAcquiredEvent(gameObject, target.Transform.gameObject));
            }
        }

        /// <summary>
        /// Clear the current target.
        /// </summary>
        public void ClearTarget(TargetLostReason reason = TargetLostReason.ManualClear)
        {
            if (currentTarget == null) return;

            DebugManager.LogState($"{npcName}: Lost target ({reason})");
            EventBus.Publish(new TargetLostEvent(gameObject, reason));

            currentTarget = null;
        }

        /// <summary>
        /// Find the nearest valid target within detection range.
        /// </summary>
        public virtual ITargetable FindNearestTarget()
        {
            ITargetable nearest = null;
            float nearestDistance = float.MaxValue;

            foreach (var targetable in TargetRegistry.AllTargetables)
            {
                if (!IsValidTarget(targetable)) continue;

                float distance = Vector3.Distance(transform.position, targetable.TargetPosition);
                
                if (distance <= combatStats.DetectionRange && distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = targetable;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Check if a target is valid for this unit to attack.
        /// Override for custom targeting rules.
        /// </summary>
        protected virtual bool IsValidTarget(ITargetable target)
        {
            // Can't target self
            if (target == (ITargetable)this) return false;

            // Must be targetable
            if (!target.IsTargetable) return false;

            // Must be hostile (different faction)
            if (target.FactionId == factionId) return false;

            // If it's damageable, it must be alive
            if (target is IDamageable damageable && !damageable.IsAlive) return false;

            return true;
        }

        /// <summary>
        /// Get distance to current target.
        /// </summary>
        public float GetDistanceToTarget()
        {
            if (currentTarget == null) return float.MaxValue;
            return Vector3.Distance(transform.position, currentTarget.TargetPosition);
        }

        // ========== Attack Execution ==========

        /// <summary>
        /// Check if this unit can currently attack.
        /// </summary>
        public virtual bool CanAttack()
        {
            if (!IsAlive) return false;
            if (!HasTarget) return false;

            return Time.time >= lastAttackTime + combatStats.AttackCooldown;
        }

        /// <summary>
        /// Execute an attack on the target.
        /// Must be implemented by subclasses.
        /// </summary>
        public abstract void ExecuteAttack(ITargetable target);

        /// <summary>
        /// Reset the attack cooldown timer.
        /// Called by AttackCooldownState when cooldown completes.
        /// </summary>
        public void ResetAttackCooldown()
        {
            lastAttackTime = Time.time;
        }

        /// <summary>
        /// Apply damage to a target.
        /// Helper method for ExecuteAttack implementations.
        /// </summary>
        protected void ApplyDamageToTarget(ITargetable target, float damage)
        {
            if (target is IDamageable damageable)
            {
                damageable.TakeDamage(damage, gameObject);
            }
        }

        // ========== Commands ==========

        /// <summary>
        /// Move to a destination position.
        /// Overrides base to use CombatIdleState instead of WorkerIdleState.
        /// </summary>
        public override void MoveTo(Vector3 destination)
        {
            ClearTarget();
            
            var moveState = stateMachine.GetState<MoveToTargetState>();
            if (moveState != null)
            {
                moveState.SetTarget(destination, () => stateMachine.SetState<CombatIdleState>());
                stateMachine.SetState<MoveToTargetState>();
            }
        }

        /// <summary>
        /// Command this unit to attack a specific target.
        /// </summary>
        public virtual void AttackTarget(ITargetable target)
        {
            if (target == null || !IsValidTarget(target)) return;

            SetTarget(target);
            stateMachine.SetState<ChaseTargetState>();
        }

        /// <summary>
        /// Command this unit to move and then attack enemies at location.
        /// </summary>
        public virtual void AttackMove(Vector3 destination)
        {
            ClearTarget();
            
            var moveState = stateMachine.GetState<MoveToTargetState>();
            if (moveState != null)
            {
                moveState.SetTarget(destination, () => stateMachine.SetState<CombatIdleState>());
                stateMachine.SetState<MoveToTargetState>();
            }
        }

        /// <summary>
        /// Command this unit to stop all actions.
        /// </summary>
        public virtual void Stop()
        {
            ClearTarget();
            motor.Stop();
            stateMachine.SetState<CombatIdleState>();
        }

        // ========== Debug ==========

#if UNITY_EDITOR
        protected virtual void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;

            // Detection range
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, combatStats.DetectionRange);

            // Attack range
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, combatStats.AttackRange);

            // Line to target
            if (currentTarget != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, currentTarget.TargetPosition);
            }
        }
#endif
    }
}
