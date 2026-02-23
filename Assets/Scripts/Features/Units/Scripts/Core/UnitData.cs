// ============================================================================
// UnitData.cs - Pure data for units (separated from MonoBehaviour logic)
// ============================================================================
using System;
using Assets.Scripts.Core;
using Assets.Scripts.Shared.Enums;
using UnityEngine;

namespace Assets.Scripts.NPCs
{
    /// <summary>
    /// Unit type enumeration for serialization and identification.
    /// </summary>
    public enum UnitType
    {
        Worker,
        Soldier,
        Archer,
        Cavalry,
        // Add more unit types as needed
    }

    /// <summary>
    /// Unit state flags for quick status checks.
    /// </summary>
    [Flags]
    public enum UnitStateFlags
    {
        None = 0,
        Idle = 1 << 0,
        Moving = 1 << 1,
        Gathering = 1 << 2,
        Building = 1 << 3,
        Combat = 1 << 4,
        Carrying = 1 << 5,
        Dead = 1 << 6,
        Selected = 1 << 7
    }

    /// <summary>
    /// Pure data representation of a unit.
    /// 
    /// Why this exists (Data/Logic Separation):
    /// - BuildingSite (data) / BuildingBlueprint (MonoBehaviour) pattern works great
    /// - Extending to units enables: save/load, networking, replay, debugging
    /// - Pure data can be serialized, compared, logged without Unity dependencies
    /// - AI can reason about UnitData without accessing GameObjects
    /// 
    /// Usage:
    /// - UnitData holds all serializable state
    /// - NPCBase (MonoBehaviour) holds the visual representation
    /// - UnitRegistry maps UnitId → UnitData and UnitId → NPCBase
    /// - Commands reference UnitId, not GameObject
    /// 
    /// Network sync:
    /// - Server authoritative UnitData
    /// - Clients receive UnitData updates
    /// - NPCBase syncs visuals from UnitData
    /// </summary>
    [Serializable]
    public class UnitData
    {
        // ========== Identity ==========

        /// <summary>
        /// Unique unit identifier (stable across network/save).
        /// </summary>
        public int UnitId { get; }

        /// <summary>
        /// Player who owns this unit.
        /// </summary>
        public int OwnerPlayerId { get; set; }

        /// <summary>
        /// Type of unit (Worker, Soldier, etc.)
        /// </summary>
        public UnitType UnitType { get; }

        /// <summary>
        /// Display name for UI.
        /// </summary>
        public string DisplayName { get; set; }

        // ========== Position & Movement ==========

        /// <summary>
        /// Current world position.
        /// </summary>
        public Vector3 Position { get; set; }

        /// <summary>
        /// Current rotation (Y-axis euler angle for simplicity).
        /// </summary>
        public float Rotation { get; set; }

        /// <summary>
        /// Target destination (if moving).
        /// </summary>
        public Vector3? MoveTarget { get; set; }

        /// <summary>
        /// Movement speed multiplier (1.0 = normal).
        /// </summary>
        public float SpeedMultiplier { get; set; } = 1f;

        // ========== Health & Combat ==========

        /// <summary>
        /// Current health points.
        /// </summary>
        public float CurrentHealth { get; set; }

        /// <summary>
        /// Maximum health points.
        /// </summary>
        public float MaxHealth { get; set; }

        /// <summary>
        /// Is unit alive?
        /// </summary>
        public bool IsAlive => CurrentHealth > 0;

        /// <summary>
        /// Health percentage (0-1).
        /// </summary>
        public float HealthPercent => MaxHealth > 0 ? CurrentHealth / MaxHealth : 0;

        // ========== State ==========

        /// <summary>
        /// Current state flags.
        /// </summary>
        public UnitStateFlags StateFlags { get; set; }

        /// <summary>
        /// Current state machine state name (for debugging/saving).
        /// </summary>
        public string CurrentStateName { get; set; }

        /// <summary>
        /// Tick when state last changed.
        /// </summary>
        public long StateChangedTick { get; set; }

        // ========== Inventory (for Workers) ==========

        /// <summary>
        /// Type of resource being carried.
        /// </summary>
        public EResourceType CarriedResourceType { get; set; }

        /// <summary>
        /// Amount of resource being carried.
        /// </summary>
        public int CarriedAmount { get; set; }

        /// <summary>
        /// Maximum carry capacity.
        /// </summary>
        public int CarryCapacity { get; set; }

        /// <summary>
        /// Is inventory full?
        /// </summary>
        public bool IsInventoryFull => CarriedAmount >= CarryCapacity;

        // ========== Targeting ==========

        /// <summary>
        /// Current target unit ID (for combat/follow).
        /// </summary>
        public int? TargetUnitId { get; set; }

        /// <summary>
        /// Current target resource ID (for gathering).
        /// </summary>
        public int? TargetResourceId { get; set; }

        /// <summary>
        /// Current target building site ID (for construction).
        /// </summary>
        public string TargetBuildingSiteId { get; set; }

        // ========== Timestamps ==========

        /// <summary>
        /// Time.time value when unit was created.
        /// </summary>
        public float CreatedAt { get; }

        /// <summary>
        /// Time.time value when unit data was last modified.
        /// </summary>
        public float LastModifiedAt { get; set; }

        // ========== Constructor ==========

        public UnitData(int unitId, UnitType unitType, int ownerPlayerId)
        {
            UnitId = unitId;
            UnitType = unitType;
            OwnerPlayerId = ownerPlayerId;
            CreatedAt = Time.time;
            LastModifiedAt = Time.time;
            StateFlags = UnitStateFlags.Idle;
            CurrentStateName = "Idle";
        }

        // ========== State Flag Helpers ==========

        public bool HasFlag(UnitStateFlags flag) => (StateFlags & flag) != 0;
        
        public void SetFlag(UnitStateFlags flag) => StateFlags |= flag;
        
        public void ClearFlag(UnitStateFlags flag) => StateFlags &= ~flag;

        public void SetFlagState(UnitStateFlags flag, bool enabled)
        {
            if (enabled) SetFlag(flag);
            else ClearFlag(flag);
        }

        // ========== Modification Tracking ==========

        /// <summary>
        /// Mark data as modified (call after changes).
        /// </summary>
        public void MarkModified()
        {
            LastModifiedAt = Time.time;
        }

        // ========== Cloning (for snapshots/rollback) ==========

        /// <summary>
        /// Create a deep copy of this unit data.
        /// </summary>
        public UnitData Clone()
        {
            return new UnitData(UnitId, UnitType, OwnerPlayerId)
            {
                DisplayName = DisplayName,
                Position = Position,
                Rotation = Rotation,
                MoveTarget = MoveTarget,
                SpeedMultiplier = SpeedMultiplier,
                CurrentHealth = CurrentHealth,
                MaxHealth = MaxHealth,
                StateFlags = StateFlags,
                CurrentStateName = CurrentStateName,
                StateChangedTick = StateChangedTick,
                CarriedResourceType = CarriedResourceType,
                CarriedAmount = CarriedAmount,
                CarryCapacity = CarryCapacity,
                TargetUnitId = TargetUnitId,
                TargetResourceId = TargetResourceId,
                TargetBuildingSiteId = TargetBuildingSiteId,
            };
        }
    }
}
