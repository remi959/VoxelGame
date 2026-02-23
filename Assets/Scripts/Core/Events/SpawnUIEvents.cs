// ============================================================================
// SpawnUIEvents.cs - Events for the unit spawn UI system
// ============================================================================
using Assets.Scripts.Buildings;
using Assets.Scripts.NPCs.Spawning;
using UnityEngine;

namespace Assets.Scripts.Core.Events
{
    /// <summary>
    /// Event fired when a spawn UI should be opened for a building.
    /// </summary>
    public struct SpawnUIRequestedEvent
    {
        /// <summary>
        /// The spawner that was selected.
        /// </summary>
        public IUnitSpawner Spawner;

        /// <summary>
        /// The building GameObject containing the spawner.
        /// </summary>
        public GameObject BuildingObject;

        /// <summary>
        /// The completed building component (if available).
        /// </summary>
        public CompletedBuilding Building;

        /// <summary>
        /// Screen position where the UI should appear.
        /// </summary>
        public Vector2 ScreenPosition;

        /// <summary>
        /// World position of the building.
        /// </summary>
        public Vector3 WorldPosition;
    }

    /// <summary>
    /// Event fired when the spawn UI should be closed.
    /// </summary>
    public struct SpawnUIClosedEvent
    {
    }

    /// <summary>
    /// Event fired when a unit spawn is requested from the UI.
    /// </summary>
    public struct UnitSpawnRequestedEvent
    {
        /// <summary>
        /// The spawner to spawn from.
        /// </summary>
        public IUnitSpawner Spawner;

        /// <summary>
        /// The unit ID to spawn.
        /// </summary>
        public string UnitId;

        /// <summary>
        /// Player requesting the spawn.
        /// </summary>
        public int PlayerId;
    }
}
