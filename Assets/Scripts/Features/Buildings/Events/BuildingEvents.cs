// ============================================================================
// BuildingEvents.cs - Events for building system
// ============================================================================
using Assets.Scripts.Buildings;
using Assets.Scripts.Buildings.Construction;
using UnityEngine;

namespace Assets.Scripts.Events
{
    // ========== Build Mode Events ==========

    public struct BuildModeStartedEvent
    {
        public BuildingDefinitionSO Definition;
        public int PlayerId;
    }

    public struct BuildModeEndedEvent
    {
        public int PlayerId;
    }

    public struct BuildingCursorMovedEvent
    {
        public Vector3 WorldPosition;
        public Quaternion Rotation;
    }

    public struct RotateBuildingPreviewEvent
    {
        public float DeltaDegrees;
    }

    // ========== Placement Events ==========

    public struct PlacementValidationResultEvent
    {
        public bool IsValid;
        public Vector3 Position;
        public Quaternion Rotation;
        public System.Collections.Generic.List<string> Errors;
    }

    public struct BuildingPlacementResultEvent
    {
        public bool Success;
        public string Error;
    }

    // ========== Construction Events ==========

    public struct BuildingSiteCreatedEvent
    {
        public string SiteId;
        public BuildingDefinitionSO Definition;
        public Vector3 Position;
        public int PlayerId;
    }

    public struct BuildingSiteStateChangedEvent
    {
        public string SiteId;
        public BuildingSiteState OldState;
        public BuildingSiteState NewState;
    }

    public struct BuildingConstructionProgressEvent
    {
        public string SiteId;
        public float Progress;  // 0.0 to 1.0
        public int CurrentPhase;
        public int TotalPhases;
    }

    public struct BuildingCompletedEvent
    {
        public string SiteId;
        public BuildingDefinitionSO Definition;
        public GameObject CompletedBuilding;
        public int PlayerId;
        public float ConstructionDuration;
    }

    public struct BuildingCancelledEvent
    {
        public string SiteId;
        public BuildingDefinitionSO Definition;
        public int PlayerId;
        public bool ResourcesRefunded;
        public float RefundPercentage;
    }

    // ========== Worker Assignment Events ==========

    public struct WorkerAssignedToBuildingEvent
    {
        public string SiteId;
        public int WorkerId;
        public int TotalWorkers;
    }

    public struct WorkerUnassignedFromBuildingEvent
    {
        public string SiteId;
        public int WorkerId;
        public int TotalWorkers;
    }

    /// <summary>
    /// Event fired when a construction site is cancelled.
    /// Workers assigned to this site should stop their tasks.
    /// </summary>
    public struct ConstructionSiteCancelledForWorkerEvent
    {
        public string SiteId;
        public int WorkerId;
    }

    // ========== Building Menu Events ==========

    /// <summary>
    /// Command to open the building menu.
    /// Published by InputManager when B is pressed.
    /// </summary>
    public struct OpenBuildingMenuCommand
    {
        public int PlayerId;
    }

    /// <summary>
    /// Command to close the building menu.
    /// Published by InputManager or UI when ESC is pressed or building is selected.
    /// </summary>
    public struct CloseBuildingMenuCommand
    {
        public int PlayerId;
    }

    /// <summary>
    /// Event fired when the building menu is opened.
    /// Published by BuildingMenuController.
    /// </summary>
    public struct BuildingMenuOpenedEvent
    {
        public int PlayerId;
    }

    /// <summary>
    /// Event fired when the building menu is closed.
    /// Published by BuildingMenuController.
    /// </summary>
    public struct BuildingMenuClosedEvent
    {
        public int PlayerId;
    }

    /// <summary>
    /// Event fired when a building is selected from the menu.
    /// Published by BuildingMenuController.
    /// The selected building definition is passed to the placement system.
    /// </summary>
    public struct BuildingSelectedFromMenuEvent
    {
        public string BuildingId;
        public BuildingDefinitionSO Definition;
        public int PlayerId;
    }

    // ========== Building Destruction Events ==========

    /// <summary>
    /// Event fired when a completed building is destroyed.
    /// </summary>
    public struct BuildingDestroyedEvent
    {
        public Buildings.CompletedBuilding Building;
        public string BuildingName;
        public Vector3 Position;
        public int OwnerId;
        public BuildingDefinitionSO Definition;
    }

    /// <summary>
    /// Event fired when construction is cancelled with resource refund.
    /// </summary>
    public struct ConstructionCancelledWithRefundEvent
    {
        public string SiteId;
        public BuildingDefinitionSO Definition;
        public int PlayerId;
        public System.Collections.Generic.Dictionary<Shared.Enums.EResourceType, int> RefundedResources;
    }
}