// ============================================================================
// CombatSettingsSO.cs - Configuration for combat behavior
// ============================================================================
using UnityEngine;

namespace Assets.Scripts.Data
{
    /// <summary>
    /// Configuration for combat behavior settings.
    /// Centralizes combat-related magic numbers for designer-friendly editing.
    /// </summary>
    [CreateAssetMenu(fileName = "CombatSettings", menuName = "Config/Combat Settings")]
    public class CombatSettingsSO : ScriptableObject
    {
        [Header("Target Detection")]
        [Tooltip("Default range at which offensive units detect enemies")]
        public float DefaultDetectionRange = 15f;

        [Tooltip("How often to search for targets (seconds)")]
        public float TargetSearchInterval = 0.5f;

        [Tooltip("Maximum number of potential targets to consider")]
        public int MaxTargetCandidates = 10;

        [Header("Combat")]
        [Tooltip("Default attack range for melee units")]
        public float DefaultMeleeRange = 2f;

        [Tooltip("Default attack range for ranged units")]
        public float DefaultRangedRange = 10f;

        [Tooltip("Default attack damage")]
        public float DefaultAttackDamage = 10f;

        [Tooltip("Default attack cooldown in seconds")]
        public float DefaultAttackCooldown = 1f;

        [Header("Chase Behavior")]
        [Tooltip("Maximum distance to chase a target before giving up")]
        public float MaxChaseDistance = 25f;

        [Tooltip("Time to wait after losing target before searching")]
        public float LostTargetWaitTime = 2f;

        [Header("Combat Movement")]
        [Tooltip("Distance at which unit stops moving toward target to attack")]
        public float AttackStoppingDistance = 1.5f;

        [Tooltip("Multiplier for movement speed during combat")]
        public float CombatSpeedMultiplier = 1f;

        [Header("Health")]
        [Tooltip("Default health for offensive units")]
        public float DefaultMaxHealth = 100f;

        [Tooltip("Health regeneration rate per second (0 = no regen)")]
        public float HealthRegenRate = 0f;

        [Header("Debug")]
        [Tooltip("Show detection range gizmos in editor")]
        public bool ShowDetectionGizmos = true;

        [Tooltip("Log combat events to console")]
        public bool LogCombatEvents = true;

        #region Singleton Instance

        private static CombatSettingsSO _instance;

        /// <summary>
        /// Runtime instance loaded from Resources folder.
        /// Create a CombatSettings asset in Resources/Config/ folder.
        /// </summary>
        public static CombatSettingsSO Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = UnityEngine.Resources.Load<CombatSettingsSO>("Config/CombatSettings");
                    if (_instance == null)
                    {
                        Debug.LogWarning("CombatSettingsSO not found in Resources/Config/. Using defaults.");
                        _instance = CreateInstance<CombatSettingsSO>();
                    }
                }
                return _instance;
            }
        }

        #endregion
    }
}
