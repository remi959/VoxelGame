// ============================================================================
// SpawnableUnitInfo.cs - UI-friendly unit information struct
// ============================================================================
using System.Collections.Generic;
using Assets.Scripts.Shared.Enums;
using UnityEngine;

namespace Assets.Scripts.NPCs.Spawning
{
    /// <summary>
    /// UI-friendly representation of a spawnable unit.
    /// 
    /// Design Notes:
    /// - Decouples UI from ScriptableObjects
    /// - Contains only data needed for display
    /// - Can be constructed from UnitDefinitionSO
    /// - Includes runtime state (affordability, cooldown)
    /// </summary>
    public readonly struct SpawnableUnitInfo
    {
        /// <summary>
        /// Unique identifier for the unit type.
        /// </summary>
        public string UnitId { get; }

        /// <summary>
        /// Display name for UI.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Description for tooltips.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Icon sprite (may be null).
        /// </summary>
        public Sprite Icon { get; }

        /// <summary>
        /// Resource costs to spawn this unit.
        /// </summary>
        public IReadOnlyList<ResourceCostInfo> Costs { get; }

        /// <summary>
        /// Whether the player can currently afford this unit.
        /// </summary>
        public bool IsAffordable { get; }

        /// <summary>
        /// Whether spawning is currently on cooldown.
        /// </summary>
        public bool IsOnCooldown { get; }

        /// <summary>
        /// Remaining cooldown time in seconds (0 if not on cooldown).
        /// </summary>
        public float CooldownRemaining { get; }

        /// <summary>
        /// Whether the unit limit has been reached.
        /// </summary>
        public bool IsAtMaxCount { get; }

        /// <summary>
        /// Whether this unit can currently be spawned.
        /// </summary>
        public bool CanSpawn => IsAffordable && !IsOnCooldown && !IsAtMaxCount;

        /// <summary>
        /// Reason why spawning is disabled (empty if can spawn).
        /// </summary>
        public string DisabledReason { get; }

        public SpawnableUnitInfo(
            string unitId,
            string displayName,
            string description,
            Sprite icon,
            IReadOnlyList<ResourceCostInfo> costs,
            bool isAffordable,
            bool isOnCooldown,
            float cooldownRemaining,
            bool isAtMaxCount,
            string disabledReason = "")
        {
            UnitId = unitId;
            DisplayName = displayName;
            Description = description;
            Icon = icon;
            Costs = costs;
            IsAffordable = isAffordable;
            IsOnCooldown = isOnCooldown;
            CooldownRemaining = cooldownRemaining;
            IsAtMaxCount = isAtMaxCount;
            DisabledReason = disabledReason;
        }
    }

    /// <summary>
    /// UI-friendly representation of a resource cost.
    /// </summary>
    public readonly struct ResourceCostInfo
    {
        /// <summary>
        /// Type of resource.
        /// </summary>
        public EResourceType ResourceType { get; }

        /// <summary>
        /// Amount required.
        /// </summary>
        public int Amount { get; }

        /// <summary>
        /// Whether the player has enough of this resource.
        /// </summary>
        public bool IsAffordable { get; }

        /// <summary>
        /// Current player amount of this resource.
        /// </summary>
        public int CurrentAmount { get; }

        public ResourceCostInfo(EResourceType type, int amount, bool isAffordable, int currentAmount)
        {
            ResourceType = type;
            Amount = amount;
            IsAffordable = isAffordable;
            CurrentAmount = currentAmount;
        }
    }
}
