// ============================================================================
// UnitDefinitionSO.cs - ScriptableObject defining a unit type
// ============================================================================
using System;
using System.Collections.Generic;
using Assets.Scripts.Shared.Enums;
using UnityEngine;

namespace Assets.Scripts.NPCs.Spawning
{
    /// <summary>
    /// Resource cost entry for spawning a unit.
    /// </summary>
    [Serializable]
    public struct ResourceCost
    {
        [Tooltip("Type of resource required")]
        public EResourceType ResourceType;

        [Tooltip("Amount of this resource required")]
        public int Amount;

        public ResourceCost(EResourceType type, int amount)
        {
            ResourceType = type;
            Amount = amount;
        }
    }

    /// <summary>
    /// ScriptableObject that defines a spawnable unit type.
    /// 
    /// Design Notes:
    /// - Single source of truth for unit data
    /// - Used by spawners to validate and execute spawns
    /// - Used by UI to display unit information
    /// - Supports future expansion (icons, abilities, etc.)
    /// </summary>
    [CreateAssetMenu(fileName = "UnitDefinition", menuName = "NPCs/Unit Definition")]
    public class UnitDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique identifier for saves and networking")]
        public string unitId;

        [Tooltip("Display name shown to players")]
        public string displayName;

        [Tooltip("Description for tooltips")]
        [TextArea(2, 4)]
        public string description;

        [Tooltip("Icon for UI display")]
        public Sprite icon;

        [Tooltip("Unit type classification")]
        public UnitType unitType = UnitType.Worker;

        [Header("Prefab")]
        [Tooltip("Prefab to instantiate when spawning this unit")]
        public GameObject prefab;

        [Header("Costs")]
        [Tooltip("Resources required to spawn this unit")]
        public List<ResourceCost> spawnCosts = new();

        [Header("Spawning")]
        [Tooltip("Time in seconds to spawn this unit (0 = instant)")]
        public float spawnTime = 0f;

        [Tooltip("Population cost of this unit")]
        public int populationCost = 1;

        [Header("Limits")]
        [Tooltip("Maximum number that can exist simultaneously (-1 = unlimited)")]
        public int maxCount = -1;

        [Tooltip("Cooldown between spawns of this unit type in seconds")]
        public float spawnCooldown = 0f;

        // ========== Computed Properties ==========

        /// <summary>
        /// Is this definition valid and ready to use?
        /// </summary>
        public bool IsValid => prefab != null && !string.IsNullOrEmpty(unitId);

        /// <summary>
        /// Get the display name, falling back to asset name if not set.
        /// </summary>
        public string GetDisplayName() => string.IsNullOrEmpty(displayName) ? name : displayName;

        /// <summary>
        /// Check if this unit has any spawn costs.
        /// </summary>
        public bool HasCosts => spawnCosts != null && spawnCosts.Count > 0;

        /// <summary>
        /// Get the cost for a specific resource type.
        /// </summary>
        public int GetCostForResource(EResourceType type)
        {
            foreach (var cost in spawnCosts)
            {
                if (cost.ResourceType == type)
                    return cost.Amount;
            }
            return 0;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Auto-generate unitId from asset name if empty
            if (string.IsNullOrEmpty(unitId))
            {
                unitId = name.ToLowerInvariant().Replace(" ", "_");
            }

            // Auto-set display name if empty
            if (string.IsNullOrEmpty(displayName))
            {
                displayName = name;
            }
        }
#endif
    }
}
