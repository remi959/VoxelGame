// ============================================================================
// BuildingCommands.cs - Command objects for building actions
// ============================================================================
using UnityEngine;

namespace Assets.Scripts.Buildings.Commands
{
    /// <summary>
    /// Command to enter build mode for a specific building type.
    /// </summary>
    public struct EnterBuildModeCommand
    {
        public BuildingDefinitionSO BuildingDefinition;
        public int PlayerId;
    }

    /// <summary>
    /// Command to exit build mode without placing.
    /// </summary>
    public struct ExitBuildModeCommand
    {
        public int PlayerId;
    }

    /// <summary>
    /// Command to confirm building placement at a location.
    /// </summary>
    public struct PlaceBuildingCommand
    {
        public BuildingDefinitionSO BuildingDefinition;
        public Vector3 Position;
        public Quaternion Rotation;
        public int PlayerId;
    }

    /// <summary>
    /// Command to cancel a planned building (before construction starts).
    /// </summary>
    public struct CancelBuildingCommand
    {
        public string BuildingSiteId;
        public int PlayerId;
    }
}