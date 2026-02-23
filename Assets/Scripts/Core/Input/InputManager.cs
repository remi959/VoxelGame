using Assets.Scripts.Buildings;
using Assets.Scripts.Buildings.Commands;
using Assets.Scripts.Core.Cam;
using Assets.Scripts.Core.Events;
using Assets.Scripts.Core.Services;
using Assets.Scripts.Events;
using Assets.Scripts.Shared.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Scripts.Core.Input
{
    /// <summary>
    /// Handles player input for camera movement, selection, commands, and build mode.
    /// Uses event-driven architecture for building placement.
    /// 
    /// Build Mode Flow:
    /// 1. User presses B hotkey → Opens building menu OR closes menu/exits build mode
    /// 2. User selects building from menu → BuildingMenuController publishes EnterBuildModeCommand
    /// 3. PlacementService receives command, starts placement session
    /// 4. BuildingPlacementInputHandler handles placement input (confirm/cancel/rotate)
    /// 5. BuildingPreviewRenderer shows the preview
    /// 6. On confirm, PlacementService creates BuildingSite via ConstructionService
    /// 
    /// Design Notes:
    /// - InputManager only detects input, does NOT select buildings
    /// - Building selection is handled by BuildingMenuController
    /// - SRP: Input detection is separate from building selection logic
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        // ========== Input Actions ==========
        private InputAction moveAction;
        private InputAction leftClickAction;
        private InputAction rightClickAction;
        private InputAction buildAction;
        private InputAction dropPartAction;

        // ========== Layer Masks ==========
        private LayerMask selectableLayerMask;
        private LayerMask commandLayerMask;
        private int groundLayer;
        private int npcLayer;
        private int interactableLayer;

        // ========== Controller References ==========
        [Header("Controller References")]
        [SerializeField] private CameraController cameraController;
        [SerializeField] private SelectionBox selectionBox;

        [Header("Player Configuration")]
        [Tooltip("Player ID for commands (0 for single player)")]
        [SerializeField] private int playerId = 0;

        // ========== Click State ==========
        private bool leftClickHeld = false;
        private bool rightClickHeld = false;

        // ========== Drag Selection ==========
        private Vector2 dragStartPosition;
        private bool isDragging = false;
        private const float DragThreshold = 10f;

        // ========== Build Mode State ==========
        // Track build mode locally to prevent unit commands while placing
        private bool isInBuildMode = false;
        // Track menu state to handle B key correctly
        private bool isBuildingMenuOpen = false;

        #region Lifecycle

        private void Start()
        {
            // Get input actions from the new Input System
            moveAction = InputSystem.actions.FindAction(Strings.MoveActionName);
            leftClickAction = InputSystem.actions.FindAction(Strings.LeftClickActionName);
            rightClickAction = InputSystem.actions.FindAction(Strings.RightClickActionName);
            buildAction = InputSystem.actions.FindAction(Strings.BuildActionName);
            dropPartAction = InputSystem.actions.FindAction(Strings.DropPartActionName);

            // Cache layer masks for raycasting
            // For left-click selection: NPCs and Ground
            selectableLayerMask = LayerMask.GetMask(Strings.GroundLayerName, Strings.NPCLayerName);

            // For right-click commands: Ground and Interactables
            commandLayerMask = LayerMask.GetMask(Strings.GroundLayerName, Strings.InteractableLayerName);

            // Cache layer indices for comparisons
            groundLayer = LayerMask.NameToLayer(Strings.GroundLayerName);
            npcLayer = LayerMask.NameToLayer(Strings.NPCLayerName);
            interactableLayer = LayerMask.NameToLayer(Strings.InteractableLayerName);
        }

        private void OnEnable()
        {
            // Subscribe to build mode events to track state
            EventBus.Subscribe<BuildModeStartedEvent>(OnBuildModeStarted);
            EventBus.Subscribe<BuildModeEndedEvent>(OnBuildModeEnded);
            EventBus.Subscribe<BuildingMenuOpenedEvent>(OnBuildingMenuOpened);
            EventBus.Subscribe<BuildingMenuClosedEvent>(OnBuildingMenuClosed);
        }

        private void OnDisable()
        {
            // Unsubscribe from events
            EventBus.Unsubscribe<BuildModeStartedEvent>(OnBuildModeStarted);
            EventBus.Unsubscribe<BuildModeEndedEvent>(OnBuildModeEnded);
            EventBus.Unsubscribe<BuildingMenuOpenedEvent>(OnBuildingMenuOpened);
            EventBus.Unsubscribe<BuildingMenuClosedEvent>(OnBuildingMenuClosed);
        }

        private void Update()
        {
            HandleMovementInput();
            HandleLeftClick();
            HandleRightClick();
            HandleBuildHotkeys();
            HandleDropPart();
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Called when build mode is started (via PlacementService).
        /// </summary>
        private void OnBuildModeStarted(BuildModeStartedEvent e)
        {
            if (e.PlayerId == playerId)
            {
                isInBuildMode = true;
                DebugManager.LogInput($"InputManager: Build mode started for {e.Definition.displayName}");
            }
        }

        /// <summary>
        /// Called when build mode ends (via PlacementService).
        /// </summary>
        private void OnBuildModeEnded(BuildModeEndedEvent e)
        {
            if (e.PlayerId == playerId)
            {
                isInBuildMode = false;
                DebugManager.LogInput("InputManager: Build mode ended");
            }
        }

        /// <summary>
        /// Called when building menu is opened.
        /// </summary>
        private void OnBuildingMenuOpened(BuildingMenuOpenedEvent e)
        {
            if (e.PlayerId == playerId)
            {
                isBuildingMenuOpen = true;
                DebugManager.LogInput("InputManager: Building menu opened");
            }
        }

        /// <summary>
        /// Called when building menu is closed.
        /// </summary>
        private void OnBuildingMenuClosed(BuildingMenuClosedEvent e)
        {
            if (e.PlayerId == playerId)
            {
                isBuildingMenuOpen = false;
                DebugManager.LogInput("InputManager: Building menu closed");
            }
        }

        #endregion

        #region Camera Movement

        private void HandleMovementInput()
        {
            Vector2 moveValue = moveAction.ReadValue<Vector2>();
            cameraController.Move(moveValue);
        }

        #endregion

        #region Left Click (Selection)

        private void HandleLeftClick()
        {
            bool isPressed = leftClickAction.ReadValue<float>() > 0;
            Vector2 mousePosition = Mouse.current.position.ReadValue();

            if (isPressed && !leftClickHeld)
            {
                // Left click pressed - start potential drag
                leftClickHeld = true;
                dragStartPosition = mousePosition;
                isDragging = false;
            }
            else if (isPressed && leftClickHeld)
            {
                // Left click held - check for drag
                float dragDistance = Vector2.Distance(dragStartPosition, mousePosition);

                if (dragDistance >= DragThreshold)
                {
                    isDragging = true;
                    selectionBox.UpdateSelectionBox(dragStartPosition, mousePosition);
                }
            }
            else if (!isPressed && leftClickHeld)
            {
                // Left click released
                leftClickHeld = false;

                if (isDragging)
                {
                    // Finish box selection
                    selectionBox.FinishSelection(dragStartPosition, mousePosition);
                    isDragging = false;
                }
                else
                {
                    // Single click selection (only if not in build mode)
                    // Build mode has its own click handling via BuildingPlacementInputHandler
                    if (!isInBuildMode)
                    {
                        ProcessLeftClick();
                    }
                }
            }
        }

        private void ProcessLeftClick()
        {
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (Physics.Raycast(ray, out RaycastHit hitInfo, Mathf.Infinity, selectableLayerMask))
            {
                int hitLayer = hitInfo.collider.gameObject.layer;

                if (hitLayer == npcLayer)
                {
                    // Clicked on NPC - select it
                    bool addToSelection = Keyboard.current.shiftKey.isPressed;

                    EventBus.Publish(new NPCSelectedEvent
                    {
                        NPC = hitInfo.collider.gameObject,
                        AddToSelection = addToSelection
                    });
                }
                else if (hitLayer == groundLayer)
                {
                    // Clicked on ground - clear selection
                    EventBus.Publish(new SelectionClearedEvent());
                }
            }
        }

        #endregion

        #region Right Click (Commands)

        private void HandleRightClick()
        {
            bool isPressed = rightClickAction.ReadValue<float>() > 0;

            if (isPressed && !rightClickHeld)
            {
                rightClickHeld = true;

                // Safety: sync isInBuildMode with PlacementService to prevent stuck state
                if (isInBuildMode && (PlacementService.Instance == null || !PlacementService.Instance.IsActive))
                {
                    isInBuildMode = false;
                }

                // Don't process unit commands while in build mode
                if (!isInBuildMode)
                {
                    ProcessRightClick();
                }
            }
            else if (!isPressed)
            {
                rightClickHeld = false;
            }
        }

        private void ProcessRightClick()
        {
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (Physics.Raycast(ray, out RaycastHit hitInfo, Mathf.Infinity, commandLayerMask))
            {
                int hitLayer = hitInfo.collider.gameObject.layer;

                if (hitLayer == interactableLayer)
                {
                    // Clicked on interactable (resource, building, etc.)
                    EventBus.Publish(new InteractCommandEvent { Target = hitInfo.collider.gameObject });
                }
                else if (hitLayer == groundLayer)
                {
                    // Clicked on ground - move command
                    EventBus.Publish(new MoveCommandEvent { Destination = hitInfo.point });
                }
            }
        }

        #endregion

        #region Drop Part

        /// <summary>
        /// Handle drop part hotkey - allows workers to drop carried building parts.
        /// </summary>
        private void HandleDropPart()
        {
            if (dropPartAction != null && dropPartAction.WasPressedThisFrame())
            {
                DebugManager.LogInput("Hotkey: Drop carried part");
                EventBus.Publish(new DropPartCommandEvent());
            }
        }

        #endregion

        #region Build Hotkeys

        /// <summary>
        /// Handle building hotkey (B key).
        /// - If building menu is closed and not in build mode: Open building menu
        /// - If building menu is open: Close building menu
        /// - If in build mode: Exit build mode
        /// 
        /// Building selection is handled by BuildingMenuController, not here.
        /// </summary>
        private void HandleBuildHotkeys()
        {
            if (buildAction.WasPressedThisFrame())
            {
                if (isInBuildMode)
                {
                    // Currently placing a building - cancel placement
                    DebugManager.LogInput("Hotkey B: Exit build mode (cancelled)");
                    
                    EventBus.Publish(new ExitBuildModeCommand
                    {
                        PlayerId = playerId
                    });
                }
                else if (isBuildingMenuOpen)
                {
                    // Menu is open - close it
                    DebugManager.LogInput("Hotkey B: Close building menu");
                    
                    EventBus.Publish(new CloseBuildingMenuCommand
                    {
                        PlayerId = playerId
                    });
                }
                else
                {
                    // Not in build mode and menu is closed - open building menu
                    DebugManager.LogInput("Hotkey B: Open building menu");
                    
                    EventBus.Publish(new OpenBuildingMenuCommand
                    {
                        PlayerId = playerId
                    });
                }
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Enter build mode for a specific building.
        /// Can be called by UI buttons or other systems.
        /// </summary>
        public void StartBuildMode(BuildingDefinitionSO definition)
        {
            if (definition == null)
            {
                DebugManager.LogWarning("InputManager: Cannot start build mode with null definition");
                return;
            }

            EventBus.Publish(new EnterBuildModeCommand
            {
                BuildingDefinition = definition,
                PlayerId = playerId
            });
        }

        /// <summary>
        /// Cancel build mode if active.
        /// </summary>
        public void CancelBuildMode()
        {
            if (isInBuildMode)
            {
                EventBus.Publish(new ExitBuildModeCommand
                {
                    PlayerId = playerId
                });
            }
        }

        /// <summary>
        /// Opens the building selection menu.
        /// </summary>
        public void OpenBuildingMenu()
        {
            if (!isBuildingMenuOpen && !isInBuildMode)
            {
                EventBus.Publish(new OpenBuildingMenuCommand
                {
                    PlayerId = playerId
                });
            }
        }

        /// <summary>
        /// Closes the building selection menu.
        /// </summary>
        public void CloseBuildingMenu()
        {
            if (isBuildingMenuOpen)
            {
                EventBus.Publish(new CloseBuildingMenuCommand
                {
                    PlayerId = playerId
                });
            }
        }

        /// <summary>
        /// Check if currently in build mode.
        /// </summary>
        public bool IsInBuildMode => isInBuildMode;

        /// <summary>
        /// Check if building menu is currently open.
        /// </summary>
        public bool IsBuildingMenuOpen => isBuildingMenuOpen;

        /// <summary>
        /// Get the current player ID.
        /// </summary>
        public int PlayerId => playerId;

        #endregion
    }
}