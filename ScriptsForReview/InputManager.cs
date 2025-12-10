using Assets.Scripts.Events;
using Assets.Scripts.Shared.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Scripts.Core
{
    public class InputManager : MonoBehaviour
    {
        // Input Actions
        private InputAction moveAction;
        private InputAction leftClickAction;
        private InputAction rightClickAction;

        // Layer Masks
        private LayerMask selectableLayerMask;
        private LayerMask commandLayerMask;
        private int groundLayer;
        private int npcLayer;
        private int interactableLayer;

        // Controller References
        [SerializeField] private CameraController cameraController;
        [SerializeField] private SelectionBox selectionBox;

        // Click state to prevent repeated firing
        private bool leftClickHeld = false;
        private bool rightClickHeld = false;

        // Drag selection
        private Vector2 dragStartPosition;
        private bool isDragging = false;
        private const float DragThreshold = 10f;

        private void Start()
        {
            moveAction = InputSystem.actions.FindAction(Strings.MoveActionName);
            leftClickAction = InputSystem.actions.FindAction(Strings.LeftClickActionName);
            rightClickAction = InputSystem.actions.FindAction(Strings.RightClickActionName);

            // For left-click selection: NPCs and Ground
            selectableLayerMask = LayerMask.GetMask(Strings.GroundLayerName, Strings.NPCLayerName);
            
            // For right-click commands: Ground and Interactables
            commandLayerMask = LayerMask.GetMask(Strings.GroundLayerName, Strings.InteractableLayerName);

            groundLayer = LayerMask.NameToLayer(Strings.GroundLayerName);
            npcLayer = LayerMask.NameToLayer(Strings.NPCLayerName);
            interactableLayer = LayerMask.NameToLayer(Strings.InteractableLayerName);
        }

        private void Update()
        {
            HandleMovementInput();
            HandleLeftClick();
            HandleRightClick();
        }

        private void HandleMovementInput()
        {
            Vector2 moveValue = moveAction.ReadValue<Vector2>();
            cameraController.Move(moveValue);
        }

        private void HandleLeftClick()
        {
            bool isPressed = leftClickAction.ReadValue<float>() > 0;
            Vector2 mousePosition = Mouse.current.position.ReadValue();

            if (isPressed && !leftClickHeld)
            {
                leftClickHeld = true;
                dragStartPosition = mousePosition;
                isDragging = false;
            }
            else if (isPressed && leftClickHeld)
            {
                float dragDistance = Vector2.Distance(dragStartPosition, mousePosition);

                if (dragDistance >= DragThreshold)
                {
                    isDragging = true;
                    selectionBox.UpdateSelectionBox(dragStartPosition, mousePosition);
                }
            }
            else if (!isPressed && leftClickHeld)
            {
                leftClickHeld = false;

                if (isDragging)
                {
                    selectionBox.FinishSelection(dragStartPosition, mousePosition);
                    isDragging = false;
                }
                else
                {
                    ProcessLeftClick();
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
                    bool addToSelection = Keyboard.current.shiftKey.isPressed;

                    EventBus.Publish(new NPCSelectedEvent
                    {
                        NPC = hitInfo.collider.gameObject,
                        AddToSelection = addToSelection
                    });
                }
                else if (hitLayer == groundLayer)
                {
                    EventBus.Publish(new SelectionClearedEvent());
                }
            }
        }

        private void HandleRightClick()
        {
            bool isPressed = rightClickAction.ReadValue<float>() > 0;

            if (isPressed && !rightClickHeld)
            {
                rightClickHeld = true;
                ProcessRightClick();
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
                    // Right-clicked on an interactable (resource, building, etc.)
                    EventBus.Publish(new InteractCommandEvent
                    {
                        Target = hitInfo.collider.gameObject
                    });
                }
                else if (hitLayer == groundLayer)
                {
                    // Right-clicked on ground - move command
                    EventBus.Publish(new MoveCommandEvent
                    {
                        Destination = hitInfo.point
                    });
                }
            }
        }
    }
}