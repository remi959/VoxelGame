// ============================================================================
// UnitRegistry.cs - Central registry mapping UnitId to UnitData and NPCBase
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.NPCs
{
    /// <summary>
    /// Central registry for all units in the game.
    /// 
    /// Why this exists:
    /// - Commands reference units by ID, not GameObject
    /// - Network code needs stable unit identifiers
    /// - Decouples unit data from visual representation
    /// - Enables unit lookup without Transform.Find or similar
    /// 
    /// Usage:
    /// - Units register on spawn, unregister on death/destroy
    /// - Commands use UnitId to find units
    /// - AI queries use filters on UnitData
    /// - Network sync uses UnitData snapshots
    /// </summary>
    public static class UnitRegistry
    {
        // ========== Storage ==========

        // UnitId → UnitData (authoritative game state)
        private static readonly Dictionary<int, UnitData> dataById = new();

        // UnitId → NPCBase (visual representation)
        private static readonly Dictionary<int, NPCBase> npcById = new();

        // Cached lists for queries
        private static readonly List<UnitData> queryResultsData = new();
        private static readonly List<NPCBase> queryResultsNpc = new();

        // ID generation
        private static int nextUnitId = 1;

        // ========== Events ==========

        /// <summary>
        /// Fired when a unit is registered.
        /// </summary>
        public static event Action<UnitData> OnUnitRegistered;

        /// <summary>
        /// Fired when a unit is unregistered.
        /// </summary>
        public static event Action<UnitData> OnUnitUnregistered;

        // ========== Statistics ==========

        public static int UnitCount => dataById.Count;

        // ========== ID Generation ==========

        /// <summary>
        /// Generate a new unique unit ID.
        /// </summary>
        public static int GenerateUnitId()
        {
            return nextUnitId++;
        }

        /// <summary>
        /// Set the next unit ID (for loading saved games).
        /// </summary>
        public static void SetNextUnitId(int id)
        {
            nextUnitId = id;
        }

        // ========== Registration ==========

        /// <summary>
        /// Register a unit with its data and visual representation.
        /// </summary>
        public static void Register(UnitData data, NPCBase npc)
        {
            if (data == null)
            {
                Debug.LogError("UnitRegistry: Cannot register null UnitData");
                return;
            }

            dataById[data.UnitId] = data;
            
            if (npc != null)
            {
                npcById[data.UnitId] = npc;
            }

            OnUnitRegistered?.Invoke(data);
        }

        /// <summary>
        /// Register just the data (for network sync where NPC doesn't exist yet).
        /// </summary>
        public static void RegisterData(UnitData data)
        {
            Register(data, null);
        }

        /// <summary>
        /// Associate an NPCBase with existing UnitData.
        /// </summary>
        public static void AssociateNPC(int unitId, NPCBase npc)
        {
            if (npc != null)
            {
                npcById[unitId] = npc;
            }
        }

        /// <summary>
        /// Unregister a unit.
        /// </summary>
        public static void Unregister(int unitId)
        {
            if (dataById.TryGetValue(unitId, out var data))
            {
                dataById.Remove(unitId);
                npcById.Remove(unitId);
                OnUnitUnregistered?.Invoke(data);
            }
        }

        // ========== Lookups ==========

        /// <summary>
        /// Get UnitData by ID.
        /// </summary>
        public static UnitData GetData(int unitId)
        {
            return dataById.TryGetValue(unitId, out var data) ? data : null;
        }

        /// <summary>
        /// Get NPCBase by ID.
        /// </summary>
        public static NPCBase GetNPC(int unitId)
        {
            return npcById.TryGetValue(unitId, out var npc) ? npc : null;
        }

        /// <summary>
        /// Get typed NPC by ID.
        /// </summary>
        public static T GetNPC<T>(int unitId) where T : NPCBase
        {
            return npcById.TryGetValue(unitId, out var npc) ? npc as T : null;
        }

        /// <summary>
        /// Try to get both data and NPC.
        /// </summary>
        public static bool TryGet(int unitId, out UnitData data, out NPCBase npc)
        {
            data = GetData(unitId);
            npc = GetNPC(unitId);
            return data != null;
        }

        /// <summary>
        /// Check if a unit exists.
        /// </summary>
        public static bool Exists(int unitId)
        {
            return dataById.ContainsKey(unitId);
        }

        // ========== Queries ==========

        /// <summary>
        /// Get all units for a player.
        /// Returns shared list - do not store reference!
        /// </summary>
        public static List<UnitData> GetUnitsForPlayer(int playerId)
        {
            queryResultsData.Clear();
            foreach (var data in dataById.Values)
            {
                if (data.OwnerPlayerId == playerId && data.IsAlive)
                {
                    queryResultsData.Add(data);
                }
            }
            return queryResultsData;
        }

        /// <summary>
        /// Get all units of a specific type.
        /// </summary>
        public static List<UnitData> GetUnitsByType(UnitType type)
        {
            queryResultsData.Clear();
            foreach (var data in dataById.Values)
            {
                if (data.UnitType == type && data.IsAlive)
                {
                    queryResultsData.Add(data);
                }
            }
            return queryResultsData;
        }

        /// <summary>
        /// Get all units matching a predicate.
        /// </summary>
        public static List<UnitData> GetUnitsWhere(Func<UnitData, bool> predicate)
        {
            queryResultsData.Clear();
            foreach (var data in dataById.Values)
            {
                if (predicate(data))
                {
                    queryResultsData.Add(data);
                }
            }
            return queryResultsData;
        }

        /// <summary>
        /// Get all units within range of a position.
        /// </summary>
        public static List<UnitData> GetUnitsInRange(Vector3 position, float range, int? playerId = null)
        {
            queryResultsData.Clear();
            float rangeSqr = range * range;

            foreach (var data in dataById.Values)
            {
                if (!data.IsAlive) continue;
                if (playerId.HasValue && data.OwnerPlayerId != playerId.Value) continue;

                float distSqr = (data.Position - position).sqrMagnitude;
                if (distSqr <= rangeSqr)
                {
                    queryResultsData.Add(data);
                }
            }
            return queryResultsData;
        }

        /// <summary>
        /// Find nearest unit to position.
        /// </summary>
        public static UnitData FindNearest(Vector3 position, Func<UnitData, bool> filter = null)
        {
            UnitData nearest = null;
            float nearestDistSqr = float.MaxValue;

            foreach (var data in dataById.Values)
            {
                if (!data.IsAlive) continue;
                if (filter != null && !filter(data)) continue;

                float distSqr = (data.Position - position).sqrMagnitude;
                if (distSqr < nearestDistSqr)
                {
                    nearestDistSqr = distSqr;
                    nearest = data;
                }
            }

            return nearest;
        }

        // ========== All Units ==========

        /// <summary>
        /// Get all unit data (for saving/iteration).
        /// </summary>
        public static IEnumerable<UnitData> AllData => dataById.Values;

        /// <summary>
        /// Get all NPCs.
        /// </summary>
        public static IEnumerable<NPCBase> AllNPCs => npcById.Values;

        // ========== Cleanup ==========

        /// <summary>
        /// Clear all units (for scene transitions).
        /// </summary>
        public static void Clear()
        {
            dataById.Clear();
            npcById.Clear();
            queryResultsData.Clear();
            queryResultsNpc.Clear();
        }

        /// <summary>
        /// Remove units with destroyed NPCs.
        /// </summary>
        public static void CleanupDestroyed()
        {
            var toRemove = new List<int>();

            foreach (var kvp in npcById)
            {
                if (kvp.Value == null)
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var id in toRemove)
            {
                Unregister(id);
            }
        }
    }
}
