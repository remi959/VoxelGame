// ============================================================================
// ContextMenuUIController.cs - Controller for the right-click context menu UI
// ============================================================================
using System.Collections.Generic;
using Assets.Scripts.Core.Events;
using Assets.Scripts.Shared.Interfaces;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Assets.UI.Scripts.Views
{
    /// <summary>
    /// Controller for the right-click context menu UI panel.
    /// 
    /// Responsibilities:
    /// - Listens for ContextMenuRequestedEvent
    /// - Populates UI with actions from IHoverTarget
    /// - Positions panel at the click location (static)
    /// - Binds button clicks to target actions
    /// - Closes menu on click outside or Escape
    /// 
    /// Design Notes:
    /// - NO gameplay logic - only UI presentation
    /// - Uses events to communicate with context menu service
    /// - Dynamically creates action buttons
    /// </summary>
    public class ContextMenuUIController : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("The UIDocument component for the game UI")]
        [SerializeField] private UIDocument uiDocument;

        [Header("Positioning")]
        [Tooltip("Offset from click position")]
        [SerializeField] private Vector2 clickOffset = new(10f, 10f);

        [Tooltip("Keep panel within screen bounds")]
        [SerializeField] private float screenPadding = 10f;

        // UI Elements
        private VisualElement root;
        private VisualElement hoverPanel;
        private Label titleLabel;
        private Label typeLabel;
        private VisualElement actionsContainer;

        // State
        private IHoverTarget currentTarget;
        private Vector2 menuScreenPosition;
        private readonly List<Button> actionButtons = new();
        private bool isInitialized;
        private bool isMenuOpen;

        #region Unity Lifecycle

        private void OnEnable()
        {
            // Subscribe to context menu events
            EventBus.Subscribe<ContextMenuRequestedEvent>(OnContextMenuRequested);
            EventBus.Subscribe<ContextMenuClosedEvent>(OnContextMenuClosed);
        }

        private void OnDisable()
        {
            // Unsubscribe from events
            EventBus.Unsubscribe<ContextMenuRequestedEvent>(OnContextMenuRequested);
            EventBus.Unsubscribe<ContextMenuClosedEvent>(OnContextMenuClosed);
            
            Cleanup();
        }

        private void Start()
        {
            Initialize();
        }

        private void Update()
        {
            if (!isInitialized || !isMenuOpen) return;

            // Check for clicks outside the panel or Escape key to close
            CheckForCloseInput();
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize the controller.
        /// </summary>
        public void Initialize()
        {
            if (isInitialized) return;

            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }

            if (uiDocument == null)
            {
                Debug.LogError("ContextMenuUIController: No UIDocument found");
                return;
            }

            root = uiDocument.rootVisualElement;
            if (root == null)
            {
                Debug.LogError("ContextMenuUIController: Root visual element is null");
                return;
            }

            // Get UI elements
            hoverPanel = root.Q<VisualElement>("hover-panel");
            titleLabel = root.Q<Label>("hover-title");
            typeLabel = root.Q<Label>("hover-type");
            actionsContainer = root.Q<VisualElement>("hover-actions");

            if (hoverPanel == null)
            {
                Debug.LogError("ContextMenuUIController: hover-panel not found in UXML");
                return;
            }

            // Ensure panel starts hidden
            HidePanel();

            isInitialized = true;
            Debug.Log("ContextMenuUIController: Initialized");
        }

        private void Cleanup()
        {
            ClearActionButtons();
            currentTarget = null;
            isMenuOpen = false;
        }

        #endregion

        #region Event Handlers

        private void OnContextMenuRequested(ContextMenuRequestedEvent e)
        {
            if (!isInitialized)
            {
                Initialize();
                if (!isInitialized) return;
            }

            currentTarget = e.Target;
            menuScreenPosition = e.ScreenPosition;

            if (currentTarget != null)
            {
                PopulatePanel(currentTarget);
                ShowPanel();
                isMenuOpen = true;
            }
        }

        private void OnContextMenuClosed(ContextMenuClosedEvent e)
        {
            HidePanel();
            currentTarget = null;
            isMenuOpen = false;
        }

        private void CheckForCloseInput()
        {
            // Close on Escape key
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                RequestClose();
                return;
            }

            // Close on left-click outside panel
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                Vector2 mousePos = Mouse.current.position.ReadValue();
                
                // Convert to UI Toolkit coordinates
                float uiY = Screen.height - mousePos.y;
                Vector2 uiMousePos = new(mousePos.x, uiY);

                // Check if click is outside panel
                if (hoverPanel != null && !IsPointInsidePanel(uiMousePos))
                {
                    RequestClose();
                }
            }
        }

        private bool IsPointInsidePanel(Vector2 point)
        {
            if (hoverPanel == null) return false;

            var panelRect = hoverPanel.worldBound;
            return panelRect.Contains(point);
        }

        private void RequestClose()
        {
            // Publish close event - service will handle state
            EventBus.Publish(new ContextMenuClosedEvent());
        }

        #endregion

        #region Panel Management

        private void PopulatePanel(IHoverTarget target)
        {
            // Update header
            if (titleLabel != null)
            {
                titleLabel.text = target.DisplayName;
            }

            if (typeLabel != null)
            {
                typeLabel.text = GetTypeDisplayName(target.TargetType);
            }

            // Update panel type class for styling
            UpdatePanelTypeClass(target.TargetType);

            // Clear existing action buttons
            ClearActionButtons();

            // Create new action buttons
            var actions = target.GetAvailableActions();
            foreach (var action in actions)
            {
                CreateActionButton(action);
            }
        }

        private void CreateActionButton(HoverAction action)
        {
            if (actionsContainer == null) return;

            // Create button
            var button = new Button(() => ExecuteAction(action.Id))
            {
                name = $"action-{action.Id}",
                text = action.Label
            };

            // Add base class
            button.AddToClassList("hover-action");

            // Add modifier classes
            if (action.IsDestructive)
            {
                button.AddToClassList("hover-action--destructive");
            }

            if (!action.IsEnabled)
            {
                button.AddToClassList("hover-action--disabled");
                button.SetEnabled(false);
            }

            // Add tooltip if available
            if (!string.IsNullOrEmpty(action.Tooltip))
            {
                button.tooltip = action.Tooltip;
            }

            // Add to container and track
            actionsContainer.Add(button);
            actionButtons.Add(button);
        }

        private void ClearActionButtons()
        {
            foreach (var button in actionButtons)
            {
                button.RemoveFromHierarchy();
            }
            actionButtons.Clear();
        }

        private void ExecuteAction(string actionId)
        {
            if (currentTarget == null)
            {
                Debug.LogWarning("ContextMenuUIController: No current target for action");
                return;
            }

            Debug.Log($"ContextMenuUIController: Executing action '{actionId}' on {currentTarget.DisplayName}");

            bool success = currentTarget.ExecuteAction(actionId);

            if (success)
            {
                // Action executed - target may be destroyed, close menu
                RequestClose();
            }
        }

        private void UpdatePanelTypeClass(HoverTargetType type)
        {
            if (hoverPanel == null) return;

            // Remove existing type classes
            hoverPanel.RemoveFromClassList("hover-panel--building");
            hoverPanel.RemoveFromClassList("hover-panel--construction");
            hoverPanel.RemoveFromClassList("hover-panel--storage");

            // Add appropriate type class
            switch (type)
            {
                case HoverTargetType.CompletedBuilding:
                    hoverPanel.AddToClassList("hover-panel--building");
                    break;
                case HoverTargetType.BuildingUnderConstruction:
                    hoverPanel.AddToClassList("hover-panel--construction");
                    break;
                case HoverTargetType.StoragePoint:
                    hoverPanel.AddToClassList("hover-panel--storage");
                    break;
            }
        }

        private string GetTypeDisplayName(HoverTargetType type)
        {
            return type switch
            {
                HoverTargetType.CompletedBuilding => "Building",
                HoverTargetType.BuildingUnderConstruction => "Under Construction",
                HoverTargetType.StoragePoint => "Storage Point",
                _ => "Unknown"
            };
        }

        #endregion

        #region Positioning

        private void PositionPanelAtClick()
        {
            if (hoverPanel == null || root == null) return;

            // Convert screen position to UI Toolkit coordinates (Y is inverted)
            float panelX = menuScreenPosition.x + clickOffset.x;
            float panelY = Screen.height - menuScreenPosition.y + clickOffset.y;

            // Get panel size (use scheduled callback if size not resolved yet)
            hoverPanel.schedule.Execute(() => AdjustPanelPosition(panelX, panelY));
        }

        private void AdjustPanelPosition(float panelX, float panelY)
        {
            if (hoverPanel == null) return;

            float panelWidth = hoverPanel.resolvedStyle.width;
            float panelHeight = hoverPanel.resolvedStyle.height;

            // Use estimated size if resolved size is 0
            if (panelWidth <= 0) panelWidth = 200;
            if (panelHeight <= 0) panelHeight = 150;

            // Keep within screen bounds
            float maxX = Screen.width - panelWidth - screenPadding;
            float maxY = Screen.height - panelHeight - screenPadding;

            panelX = Mathf.Clamp(panelX, screenPadding, maxX);
            panelY = Mathf.Clamp(panelY, screenPadding, maxY);

            // Apply position
            hoverPanel.style.left = panelX;
            hoverPanel.style.top = panelY;
        }

        #endregion

        #region Show/Hide

        private void ShowPanel()
        {
            if (hoverPanel == null) return;

            hoverPanel.RemoveFromClassList("hidden");
            PositionPanelAtClick();
        }

        private void HidePanel()
        {
            if (hoverPanel == null) return;

            hoverPanel.AddToClassList("hidden");
        }

        #endregion

        #region Public API

        /// <summary>
        /// Force hide the panel.
        /// </summary>
        public void ForceHide()
        {
            RequestClose();
        }

        /// <summary>
        /// Check if the panel is currently visible.
        /// </summary>
        public bool IsPanelVisible => isMenuOpen && !hoverPanel.ClassListContains("hidden");

        #endregion
    }
}
