// ============================================================================
// BuildingPlacementInputHandler.cs - ONLY handles input
// ============================================================================
using Assets.Scripts.Buildings.Commands;
using Assets.Scripts.Core;
using Assets.Scripts.Core.Events;
using Assets.Scripts.Events;
using Assets.Scripts.Shared.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Scripts.Buildings.Input
{
    /// <summary>
    /// Handles raw input for building placement.
    /// 
    /// Responsibilities:
    /// - Detect clicks, key presses, scroll
    /// - Convert to commands
    /// - Publish commands to EventBus
    /// 
    /// Does NOT:
    /// - Know about building types
    /// - Validate placement
    /// - Create any GameObjects
    /// - Track placement state
    /// </summary>
    public class BuildingPlacementInputHandler : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private LayerMask groundLayer;
        [SerializeField] private float maxRaycastDistance = 1000f;

        // Looked up at runtime via InputSystem.actions
        private InputAction confirmAction;
        private InputAction cancelAction;
        private InputAction rotateLeftAction;
        private InputAction rotateRightAction;

        // Current placement session (if any)
        private bool isInBuildMode;
        private BuildingDefinitionSO currentDefinition;
        private float currentRotation;
        private int currentPlayerId;

        private Camera mainCamera;

        private void Awake()
        {
            mainCamera = Camera.main;
            LookupInputActions();
        }

        private void LookupInputActions()
        {
            confirmAction = InputSystem.actions.FindAction(Strings.ConfirmActionName);
            cancelAction = InputSystem.actions.FindAction(Strings.CancelActionName);
            rotateLeftAction = InputSystem.actions.FindAction(Strings.RotateLeftActionName);
            rotateRightAction = InputSystem.actions.FindAction(Strings.RotateRightActionName);

            // Log warnings for any actions not found
            if (confirmAction == null)
                Debug.LogError($"[{nameof(BuildingPlacementInputHandler)}] Could not find action '{Strings.ConfirmActionName}'");
            if (cancelAction == null)
                Debug.LogError($"[{nameof(BuildingPlacementInputHandler)}] Could not find action '{Strings.CancelActionName}'");
            if (rotateLeftAction == null)
                Debug.LogError($"[{nameof(BuildingPlacementInputHandler)}] Could not find action '{Strings.RotateLeftActionName}'");
            if (rotateRightAction == null)
                Debug.LogError($"[{nameof(BuildingPlacementInputHandler)}] Could not find action '{Strings.RotateRightActionName}'");
        }

        private void OnEnable()
        {
            EventBus.Subscribe<BuildModeStartedEvent>(OnBuildModeStarted);
            EventBus.Subscribe<BuildModeEndedEvent>(OnBuildModeEnded);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<BuildModeStartedEvent>(OnBuildModeStarted);
            EventBus.Unsubscribe<BuildModeEndedEvent>(OnBuildModeEnded);
        }

        private void OnBuildModeStarted(BuildModeStartedEvent e)
        {
            isInBuildMode = true;
            currentDefinition = e.Definition;
            currentRotation = 0f;
            currentPlayerId = e.PlayerId;
        }

        private void OnBuildModeEnded(BuildModeEndedEvent e)
        {
            isInBuildMode = false;
            currentDefinition = null;
        }

        private void Update()
        {
            if (!isInBuildMode) return;

            // Handle cursor position
            UpdateCursorPosition();

            // Handle rotation
            if (rotateLeftAction != null && rotateLeftAction.WasPressedThisFrame())
            {
                EventBus.Publish(new RotateBuildingPreviewEvent { DeltaDegrees = -45f });
                currentRotation -= 45f;
            }
            if (rotateRightAction != null && rotateRightAction.WasPressedThisFrame())
            {
                EventBus.Publish(new RotateBuildingPreviewEvent { DeltaDegrees = 45f });
                currentRotation += 45f;
            }

            // Handle scroll wheel rotation
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.1f)
            {
                float delta = Mathf.Sign(scroll) * 45f;
                EventBus.Publish(new RotateBuildingPreviewEvent { DeltaDegrees = delta });
                currentRotation += delta;
            }

            if (confirmAction != null && confirmAction.WasPressedThisFrame())
            {
                Debug.Log("[InputHandler] CONFIRM DETECTED - calling TryConfirmPlacement()");
                TryConfirmPlacement();
            }
            if (cancelAction != null && cancelAction.WasPressedThisFrame())
            {
                EventBus.Publish(new ExitBuildModeCommand { PlayerId = currentPlayerId });
            }
        }

        private void UpdateCursorPosition()
        {
            Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance, groundLayer))
            {
                EventBus.Publish(new BuildingCursorMovedEvent
                {
                    WorldPosition = hit.point,
                    Rotation = Quaternion.Euler(0f, currentRotation, 0f)
                });
            }
        }

        private void TryConfirmPlacement()
        {
            Debug.Log($"[InputHandler] TryConfirmPlacement - groundLayer: {groundLayer.value}");

            Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance, groundLayer))
            {
                Debug.Log($"[InputHandler] Raycast HIT at {hit.point}, publishing PlaceBuildingCommand");
                EventBus.Publish(new PlaceBuildingCommand
                {
                    BuildingDefinition = currentDefinition,
                    Position = hit.point,
                    Rotation = Quaternion.Euler(0f, currentRotation, 0f),
                    PlayerId = currentPlayerId
                });
            }
            else
            {
                Debug.LogWarning($"[InputHandler] Raycast MISSED! LayerMask: {groundLayer.value}, Ray: {ray.origin} -> {ray.direction}");
            }
        }
    }
}