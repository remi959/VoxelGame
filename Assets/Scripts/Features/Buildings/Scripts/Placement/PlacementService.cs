// ============================================================================
// PlacementService.cs - Orchestrates placement logic
// ============================================================================
using Assets.Scripts.Buildings.Commands;
using Assets.Scripts.Buildings.Construction;
using Assets.Scripts.Core.Events;
using Assets.Scripts.Core.Services;
using Assets.Scripts.Economy;
using Assets.Scripts.Events;
using UnityEngine;

namespace Assets.Scripts.Buildings.Placement
{
    /// <summary>
    /// Central service for building placement.
    /// 
    /// Responsibilities:
    /// - Coordinate placement flow
    /// - Validate placement requests
    /// - Reserve resources
    /// - Create building sites
    /// 
    /// Does NOT:
    /// - Handle input directly
    /// - Render previews
    /// - Manage construction
    /// 
    /// PERSISTENCE:
    /// This component should be placed as a child of the [Services] GameObject.
    /// PersistentServices handles DontDestroyOnLoad for the entire hierarchy.
    /// </summary>
    public class PlacementService : MonoBehaviour
    {
        public static PlacementService Instance { get; private set; }

        [Header("Dependencies")]
        [SerializeField] private ValidationService validationService;

        // Current placement session
        private bool isActive;
        private BuildingDefinitionSO currentDefinition;
        private int currentPlayerId;
        private Vector3 lastValidatedPosition;
        private Quaternion lastValidatedRotation;
        private ValidationResult lastValidationResult;

        // Throttle validation
        private float lastValidationTime;
        private const float ValidationInterval = 0.05f;  // 20 Hz

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            ServiceLocator.Register(this);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                ServiceLocator.Unregister<PlacementService>();
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<EnterBuildModeCommand>(OnEnterBuildMode);
            EventBus.Subscribe<ExitBuildModeCommand>(OnExitBuildMode);
            EventBus.Subscribe<PlaceBuildingCommand>(OnPlaceBuilding);
            EventBus.Subscribe<BuildingCursorMovedEvent>(OnCursorMoved);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<EnterBuildModeCommand>(OnEnterBuildMode);
            EventBus.Unsubscribe<ExitBuildModeCommand>(OnExitBuildMode);
            EventBus.Unsubscribe<PlaceBuildingCommand>(OnPlaceBuilding);
            EventBus.Unsubscribe<BuildingCursorMovedEvent>(OnCursorMoved);
        }

        /// <summary>
        /// Start placement mode for a building.
        /// Can be called by UI, hotkeys, or AI.
        /// </summary>
        public void StartPlacement(BuildingDefinitionSO definition, int playerId)
        {
            if (isActive)
            {
                EndPlacement();
            }

            isActive = true;
            currentDefinition = definition;
            currentPlayerId = playerId;

            EventBus.Publish(new BuildModeStartedEvent
            {
                Definition = definition,
                PlayerId = playerId
            });
        }

        private void EndPlacement()
        {
            if (!isActive) return;

            isActive = false;
            currentDefinition = null;

            EventBus.Publish(new BuildModeEndedEvent
            {
                PlayerId = currentPlayerId
            });
        }

        private void OnEnterBuildMode(EnterBuildModeCommand cmd)
        {
            StartPlacement(cmd.BuildingDefinition, cmd.PlayerId);
        }

        private void OnExitBuildMode(ExitBuildModeCommand cmd)
        {
            if (cmd.PlayerId == currentPlayerId)
            {
                EndPlacement();
            }
        }

        private void OnCursorMoved(BuildingCursorMovedEvent e)
        {
            if (!isActive) return;

            // Throttle validation to reduce CPU usage
            if (Time.time - lastValidationTime < ValidationInterval) return;
            lastValidationTime = Time.time;

            lastValidatedPosition = e.WorldPosition;
            lastValidatedRotation = e.Rotation;

            // Validate placement
            lastValidationResult = validationService.Validate(
                e.WorldPosition,
                e.Rotation,
                currentDefinition
            );

            // Publish validation result for preview
            EventBus.Publish(new PlacementValidationResultEvent
            {
                IsValid = lastValidationResult.IsValid,
                Position = e.WorldPosition,
                Rotation = e.Rotation,
                Errors = lastValidationResult.Errors
            });
        }

        private void OnPlaceBuilding(PlaceBuildingCommand cmd)
        {
            Debug.Log($"[PlacementService] OnPlaceBuilding received! isActive: {isActive}, cmd.Definition: {cmd.BuildingDefinition?.displayName}");

            if (!isActive || cmd.BuildingDefinition != currentDefinition)
            {
                PublishPlacementResult(false, "Not in build mode for this building");
                return;
            }

            // Re-validate at final position
            var validation = validationService.Validate(cmd.Position, cmd.Rotation, cmd.BuildingDefinition);
            if (!validation.IsValid)
            {
                Debug.LogWarning($"[PlacementService] Validation FAILED: {string.Join(", ", validation.Errors)}");
                PublishPlacementResult(false, string.Join(", ", validation.Errors));
                return;
            }

            // Check and reserve resources
            var costs = cmd.BuildingDefinition.GetTotalCosts();
            Debug.Log($"[PlacementService] Checking resources. EconomyService null: {EconomyService.Instance == null}");

            var reservation = EconomyService.Instance.CreateReservation(cmd.PlayerId, ConvertCosts(costs));
            Debug.Log($"[PlacementService] Reservation result: {(reservation != null ? "SUCCESS" : "FAILED - insufficient resources")}");

            if (reservation == null)
            {
                PublishPlacementResult(false, "Insufficient resources");
                return;
            }

            // Create building site
            Debug.Log($"[PlacementService] Creating building site. ConstructionService null: {ConstructionService.Instance == null}");
            var site = ConstructionService.Instance.CreateBuildingSite(
                cmd.BuildingDefinition,
                cmd.Position,
                cmd.Rotation,
                cmd.PlayerId,
                reservation
            );

            Debug.Log($"[PlacementService] Site creation result: {(site != null ? $"SUCCESS - {site.SiteId}" : "FAILED")}");

            if (site == null)
            {
                EconomyService.Instance.CancelReservation(reservation.Id);
                PublishPlacementResult(false, "Failed to create building site");
                return;
            }

            PublishPlacementResult(true, null);
            EndPlacement();
        }

        private void PublishPlacementResult(bool success, string error)
        {
            EventBus.Publish(new BuildingPlacementResultEvent
            {
                Success = success,
                Error = error
            });
        }

        private System.Collections.Generic.IEnumerable<ResourceCost> ConvertCosts(
            System.Collections.Generic.Dictionary<Shared.Enums.EResourceType, int> costs)
        {
            foreach (var kvp in costs)
            {
                yield return new ResourceCost(kvp.Key, kvp.Value);
            }
        }
    }
}