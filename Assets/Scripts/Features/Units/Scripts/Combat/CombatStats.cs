// ============================================================================
// CombatStats.cs - Runtime combat statistics for offensive units
// ============================================================================
using UnityEngine;

namespace Assets.Scripts.NPCs.Units
{
    /// <summary>
    /// Runtime combat statistics for an offensive unit.
    /// Serializable for Inspector editing, copied from settings at runtime.
    /// 
    /// Design Notes:
    /// - Allows per-unit customization while maintaining defaults
    /// - Can be modified at runtime (buffs, debuffs, upgrades)
    /// - Separates stat storage from behavior
    /// </summary>
    [System.Serializable]
    public class CombatStats
    {
        [Header("Health")]
        [Tooltip("Maximum health points")]
        public float MaxHealth = 100f;

        [Tooltip("Health regeneration per second")]
        public float HealthRegenRate = 0f;

        [Header("Detection")]
        [Tooltip("Range at which targets can be detected")]
        public float DetectionRange = 15f;

        [Tooltip("Interval between target searches (seconds)")]
        public float TargetSearchInterval = 0.5f;

        [Header("Attack")]
        [Tooltip("Damage dealt per attack")]
        public float AttackDamage = 10f;

        [Tooltip("Range at which attacks can be executed")]
        public float AttackRange = 2f;

        [Tooltip("Time between attacks")]
        public float AttackCooldown = 1f;

        [Header("Chase")]
        [Tooltip("Maximum distance to chase before giving up")]
        public float MaxChaseDistance = 25f;

        [Tooltip("Time to wait after losing target")]
        public float LostTargetWaitTime = 2f;

        /// <summary>
        /// Create combat stats with default values from CombatSettingsSO.
        /// </summary>
        public static CombatStats CreateDefault()
        {
            var settings = Data.CombatSettingsSO.Instance;
            return new CombatStats
            {
                MaxHealth = settings.DefaultMaxHealth,
                HealthRegenRate = settings.HealthRegenRate,
                DetectionRange = settings.DefaultDetectionRange,
                TargetSearchInterval = settings.TargetSearchInterval,
                AttackDamage = settings.DefaultAttackDamage,
                AttackRange = settings.DefaultMeleeRange,
                AttackCooldown = settings.DefaultAttackCooldown,
                MaxChaseDistance = settings.MaxChaseDistance,
                LostTargetWaitTime = settings.LostTargetWaitTime
            };
        }

        /// <summary>
        /// Create a copy of these stats.
        /// </summary>
        public CombatStats Clone()
        {
            return new CombatStats
            {
                MaxHealth = MaxHealth,
                HealthRegenRate = HealthRegenRate,
                DetectionRange = DetectionRange,
                TargetSearchInterval = TargetSearchInterval,
                AttackDamage = AttackDamage,
                AttackRange = AttackRange,
                AttackCooldown = AttackCooldown,
                MaxChaseDistance = MaxChaseDistance,
                LostTargetWaitTime = LostTargetWaitTime
            };
        }
    }
}
