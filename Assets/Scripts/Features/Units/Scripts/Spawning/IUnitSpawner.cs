// ============================================================================
// IUnitSpawner.cs - Interface for components that can spawn units
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.NPCs.Spawning
{
    /// <summary>
    /// Result of a spawn request.
    /// </summary>
    public readonly struct SpawnResult
    {
        /// <summary>
        /// Whether the spawn was successful.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// The spawned unit (null if failed).
        /// </summary>
        public NPCBase SpawnedUnit { get; }

        /// <summary>
        /// Error message if spawn failed.
        /// </summary>
        public string ErrorMessage { get; }

        public static SpawnResult Succeeded(NPCBase unit) => new(true, unit, string.Empty);
        public static SpawnResult Failed(string reason) => new(false, null, reason);

        private SpawnResult(bool success, NPCBase unit, string error)
        {
            Success = success;
            SpawnedUnit = unit;
            ErrorMessage = error;
        }
    }

    /// <summary>
    /// Interface for components that can spawn units.
    /// 
    /// Design Notes:
    /// - Buildings can have multiple spawners (e.g., barracks spawns soldiers AND archers)
    /// - Spawners are discoverable by UI
    /// - Spawners handle their own validation and cooldowns
    /// - UI only displays; spawners execute
    /// 
    /// Usage:
    /// - Attach spawner components to building prefabs
    /// - UI queries building for IUnitSpawner components
    /// - UI displays GetSpawnableUnits() results
    /// - UI calls TrySpawnUnit() on selection
    /// </summary>
    public interface IUnitSpawner
    {
        /// <summary>
        /// Whether this spawner is currently active and can spawn units.
        /// False if building is under construction, destroyed, etc.
        /// </summary>
        bool IsSpawnerActive { get; }

        /// <summary>
        /// Display name of this spawner for UI (e.g., "Infantry Training").
        /// </summary>
        string SpawnerDisplayName { get; }

        /// <summary>
        /// Player ID that owns this spawner.
        /// </summary>
        int OwnerPlayerId { get; }

        /// <summary>
        /// Get all unit types this spawner can produce, with current availability state.
        /// </summary>
        IReadOnlyList<SpawnableUnitInfo> GetSpawnableUnits();

        /// <summary>
        /// Check if a specific unit type can currently be spawned.
        /// </summary>
        /// <param name="unitId">The unit ID to check</param>
        /// <param name="reason">Reason why spawning is not possible (if returning false)</param>
        bool CanSpawnUnit(string unitId, out string reason);

        /// <summary>
        /// Attempt to spawn a unit of the specified type.
        /// </summary>
        /// <param name="unitId">The unit ID to spawn</param>
        /// <returns>Result containing success state and spawned unit or error</returns>
        SpawnResult TrySpawnUnit(string unitId);

        /// <summary>
        /// Event fired when the spawnable units list changes (cooldowns, affordability, etc.)
        /// </summary>
        event Action OnSpawnableUnitsChanged;
    }
}
