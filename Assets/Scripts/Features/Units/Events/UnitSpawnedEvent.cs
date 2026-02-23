// ============================================================================
// UnitSpawnedEvent.cs - Event fired when a unit is spawned
// ============================================================================
using Assets.Scripts.NPCs.Spawning;
using UnityEngine;

namespace Assets.Scripts.Events
{
    /// <summary>
    /// Event fired when a unit is successfully spawned.
    /// </summary>
    public struct UnitSpawnedEvent
    {
        /// <summary>
        /// The spawned unit instance.
        /// </summary>
        public Assets.Scripts.NPCs.NPCBase SpawnedUnit;

        /// <summary>
        /// The definition of the spawned unit.
        /// </summary>
        public UnitDefinitionSO UnitDefinition;

        /// <summary>
        /// The building that spawned this unit.
        /// </summary>
        public GameObject SpawnerBuilding;

        /// <summary>
        /// Player who owns the spawned unit.
        /// </summary>
        public int OwnerId;

        /// <summary>
        /// World position where the unit was spawned.
        /// </summary>
        public Vector3 SpawnPosition;
    }
}
