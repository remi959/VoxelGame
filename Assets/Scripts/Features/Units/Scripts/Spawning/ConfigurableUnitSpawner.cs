// ============================================================================
// ConfigurableUnitSpawner.cs - Data-driven unit spawner
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.NPCs.Spawning
{
    /// <summary>
    /// Configurable unit spawner that uses a list of UnitDefinitionSO assets.
    /// 
    /// Design Notes:
    /// - Most flexible spawner type
    /// - Configure spawnable units in the Inspector
    /// - Supports any combination of unit types
    /// - Use on buildings that spawn specific unit types
    /// 
    /// Usage:
    /// 1. Add this component to a building prefab
    /// 2. Assign UnitDefinitionSO assets to the spawnableUnits list
    /// 3. Configure spawn position offset
    /// </summary>
    public class ConfigurableUnitSpawner : UnitSpawnerBase
    {
        [Header("Spawnable Units")]
        [Tooltip("List of unit types this spawner can produce")]
        [SerializeField] private List<UnitDefinitionSO> spawnableUnits = new();

        // ========== Cached Data ==========

        private readonly Dictionary<string, UnitDefinitionSO> unitDefinitionLookup = new();
        private readonly List<SpawnableUnitInfo> cachedSpawnableInfo = new();
        private bool lookupInitialized;

        // ========== Lifecycle ==========

        protected override void Awake()
        {
            base.Awake();
            InitializeLookup();
        }

        private void InitializeLookup()
        {
            if (lookupInitialized) return;

            unitDefinitionLookup.Clear();
            foreach (var definition in spawnableUnits)
            {
                if (definition != null && definition.IsValid)
                {
                    unitDefinitionLookup[definition.unitId] = definition;
                }
            }

            lookupInitialized = true;
        }

        // ========== IUnitSpawner Implementation ==========

        public override IReadOnlyList<SpawnableUnitInfo> GetSpawnableUnits()
        {
            InitializeLookup();
            cachedSpawnableInfo.Clear();

            foreach (var definition in spawnableUnits)
            {
                if (definition == null || !definition.IsValid) continue;

                var costs = BuildCostInfoList(definition);
                bool affordable = CanAffordUnit(definition);
                bool onCooldown = IsOnCooldown(definition.unitId);
                float cooldownRemaining = GetCooldownRemaining(definition.unitId);
                bool atMaxCount = IsAtMaxCount(definition);

                string disabledReason = "";
                if (!affordable)
                    disabledReason = "Not enough resources";
                else if (onCooldown)
                    disabledReason = $"Cooldown: {cooldownRemaining:F1}s";
                else if (atMaxCount)
                    disabledReason = "Maximum units reached";

                cachedSpawnableInfo.Add(new SpawnableUnitInfo(
                    unitId: definition.unitId,
                    displayName: definition.GetDisplayName(),
                    description: definition.description,
                    icon: definition.icon,
                    costs: costs,
                    isAffordable: affordable,
                    isOnCooldown: onCooldown,
                    cooldownRemaining: cooldownRemaining,
                    isAtMaxCount: atMaxCount,
                    disabledReason: disabledReason
                ));
            }

            return cachedSpawnableInfo;
        }

        public override bool CanSpawnUnit(string unitId, out string reason)
        {
            InitializeLookup();

            // Check if spawner is active
            if (!IsSpawnerActive)
            {
                reason = "Spawner is not active";
                return false;
            }

            // Check if unit type exists
            if (!unitDefinitionLookup.TryGetValue(unitId, out var definition))
            {
                reason = $"Unknown unit type: {unitId}";
                return false;
            }

            // Check cooldown
            if (IsOnCooldown(unitId))
            {
                reason = $"On cooldown ({GetCooldownRemaining(unitId):F1}s remaining)";
                return false;
            }

            // Check max count
            if (IsAtMaxCount(definition))
            {
                reason = "Maximum unit count reached";
                return false;
            }

            // Check affordability
            if (!CanAffordUnit(definition))
            {
                reason = "Not enough resources";
                return false;
            }

            reason = "";
            return true;
        }

        // ========== Abstract Method Implementation ==========

        protected override UnitDefinitionSO GetUnitDefinition(string unitId)
        {
            InitializeLookup();
            return unitDefinitionLookup.TryGetValue(unitId, out var definition) ? definition : null;
        }

        protected override IReadOnlyList<UnitDefinitionSO> GetAllUnitDefinitions()
        {
            return spawnableUnits;
        }

        // ========== Helper Methods ==========

        /// <summary>
        /// Check if the maximum count for this unit type has been reached.
        /// </summary>
        private bool IsAtMaxCount(UnitDefinitionSO definition)
        {
            if (definition.maxCount < 0) return false; // Unlimited

            // Count existing units of this type
            int count = 0;
            foreach (var npc in NPCBase.All)
            {
                if (npc.OwnerPlayerId == ownerPlayerId && MatchesUnitType(npc, definition))
                {
                    count++;
                }
            }

            return count >= definition.maxCount;
        }

        /// <summary>
        /// Check if an NPC matches a unit definition type.
        /// </summary>
        private bool MatchesUnitType(NPCBase npc, UnitDefinitionSO definition)
        {
            // Match by name convention for now
            // Could be extended to use UnitData.UnitType matching
            return npc.NPCName.ToLowerInvariant().Contains(definition.unitType.ToString().ToLowerInvariant());
        }

        // ========== Editor Support ==========

#if UNITY_EDITOR
        /// <summary>
        /// Add a unit definition to this spawner (for editor scripts).
        /// </summary>
        public void AddUnitDefinition(UnitDefinitionSO definition)
        {
            if (definition != null && !spawnableUnits.Contains(definition))
            {
                spawnableUnits.Add(definition);
                lookupInitialized = false;
            }
        }

        /// <summary>
        /// Remove a unit definition from this spawner (for editor scripts).
        /// </summary>
        public void RemoveUnitDefinition(UnitDefinitionSO definition)
        {
            if (spawnableUnits.Remove(definition))
            {
                lookupInitialized = false;
            }
        }

        private void OnValidate()
        {
            // Re-initialize lookup when definitions change in Inspector
            lookupInitialized = false;
        }
#endif
    }
}
