// ============================================================================
// BuildingMenuController.cs - Manages the building selection menu UI
// ============================================================================
using System;
using System.Collections.Generic;
using Assets.Scripts.Buildings;
using Assets.Scripts.Buildings.Commands;
using Assets.Scripts.Core.Events;
using Assets.Scripts.Events;
using Assets.UI.Scripts.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Assets.UI.Scripts.Views
{
    /// <summary>
    /// Controller for the building menu UI panel.
    /// 
    /// Responsibilities:
    /// - Initializes the building menu from UXML
    /// - Dynamically creates BuildingEntryView instances for each building
    /// - Handles building selection and menu open/close
    /// - Subscribes to affordability changes and updates entries
    /// - Publishes events for selected buildings
    /// 
    /// Design Notes:
    /// - Does NOT know about PlacementService directly
    /// - Uses events to communicate building selection
    /// - UI only depends on IBuildingDataProvider
    /// - Menu visibility is controlled via USS classes
    /// </summary>
    public class BuildingMenuController : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("The UIDocument component for the game UI")]
        [SerializeField] private UIDocument uiDocument;

        [Header("Templates")]
        [Tooltip("UXML template for building entries")]
        [SerializeField] private VisualTreeAsset buildingEntryTemplate;

        [Header("Configuration")]
        [Tooltip("Player ID to display buildings for (default: 0 for local player)")]
        [SerializeField] private int playerId = 0;

        // Runtime references
        private VisualElement buildingMenu;
        private VisualElement entriesContainer;
        private IBuildingDataProvider dataProvider;
        private readonly Dictionary<string, BuildingEntryView> entryViews = new();
        private bool isInitialized;
        private bool isMenuOpen;

        #region Unity Lifecycle

        private void OnEnable()
        {
            // Subscribe to menu commands
            EventBus.Subscribe<OpenBuildingMenuCommand>(OnOpenMenuCommand);
            EventBus.Subscribe<CloseBuildingMenuCommand>(OnCloseMenuCommand);
        }

        private void OnDisable()
        {
            // Unsubscribe from events
            EventBus.Unsubscribe<OpenBuildingMenuCommand>(OnOpenMenuCommand);
            EventBus.Unsubscribe<CloseBuildingMenuCommand>(OnCloseMenuCommand);
            
            Cleanup();
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        private void Update()
        {
            // Handle ESC key to close menu
            if (isMenuOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CloseMenu();
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Sets the building entry template. Call before Initialize if not set in inspector.
        /// </summary>
        public void SetBuildingEntryTemplate(VisualTreeAsset template)
        {
            buildingEntryTemplate = template;
        }

        /// <summary>
        /// Initializes the building menu with a data provider.
        /// Call this after game systems are ready.
        /// </summary>
        /// <param name="provider">Data provider for building information</param>
        public void Initialize(IBuildingDataProvider provider)
        {
            if (isInitialized)
            {
                Debug.LogWarning("BuildingMenuController: Already initialized");
                return;
            }

            if (provider == null)
            {
                Debug.LogError("BuildingMenuController: Cannot initialize with null provider");
                return;
            }

            dataProvider = provider;

            // Set up UI references
            if (!SetupUIReferences())
            {
                Debug.LogError("BuildingMenuController: Failed to set up UI references");
                return;
            }

            // Create building entries
            CreateBuildingEntries();

            // Subscribe to affordability changes
            dataProvider.OnAffordabilityChanged += HandleAffordabilityChanged;

            // Ensure menu starts hidden
            HideMenu();

            isInitialized = true;
            Debug.Log($"BuildingMenuController: Initialized with {entryViews.Count} building entries");
        }

        /// <summary>
        /// Opens the building menu.
        /// </summary>
        public void OpenMenu()
        {
            if (!isInitialized)
            {
                Debug.LogWarning("BuildingMenuController: Cannot open menu - not initialized");
                return;
            }

            if (isMenuOpen) return;

            // Refresh building data before showing
            RefreshAllEntries();

            ShowMenu();
            isMenuOpen = true;

            EventBus.Publish(new BuildingMenuOpenedEvent { PlayerId = playerId });
        }

        /// <summary>
        /// Closes the building menu.
        /// </summary>
        public void CloseMenu()
        {
            if (!isMenuOpen) return;

            HideMenu();
            isMenuOpen = false;

            EventBus.Publish(new BuildingMenuClosedEvent { PlayerId = playerId });
        }

        /// <summary>
        /// Toggles the building menu open/closed.
        /// </summary>
        public void ToggleMenu()
        {
            if (isMenuOpen)
                CloseMenu();
            else
                OpenMenu();
        }

        /// <summary>
        /// Gets whether the menu is currently open.
        /// </summary>
        public bool IsMenuOpen => isMenuOpen;

        #endregion

        #region Event Handlers

        private void OnOpenMenuCommand(OpenBuildingMenuCommand cmd)
        {
            if (cmd.PlayerId == playerId)
            {
                OpenMenu();
            }
        }

        private void OnCloseMenuCommand(CloseBuildingMenuCommand cmd)
        {
            if (cmd.PlayerId == playerId)
            {
                CloseMenu();
            }
        }

        private void HandleAffordabilityChanged(string buildingId, bool canAfford)
        {
            if (entryViews.TryGetValue(buildingId, out var view))
            {
                var buildingInfo = dataProvider.GetBuildingInfo(buildingId);
                if (buildingInfo.HasValue)
                {
                    view.UpdateAffordability(canAfford, buildingInfo.Value.Costs);
                }
            }
        }

        private void HandleBuildingEntryClicked(string buildingId)
        {
            // Get the building definition
            var definition = dataProvider.GetBuildingDefinition(buildingId);
            if (definition == null)
            {
                Debug.LogWarning($"BuildingMenuController: Building definition not found for {buildingId}");
                return;
            }

            // Check if affordable
            if (!dataProvider.CanAffordBuilding(buildingId))
            {
                Debug.Log($"BuildingMenuController: Cannot afford {definition.displayName}");
                // Could add feedback here (shake animation, sound, etc.)
                return;
            }

            // Close the menu
            CloseMenu();

            // Publish selection event
            EventBus.Publish(new BuildingSelectedFromMenuEvent
            {
                BuildingId = buildingId,
                Definition = definition,
                PlayerId = playerId
            });

            // Also enter build mode via the existing command
            EventBus.Publish(new EnterBuildModeCommand
            {
                BuildingDefinition = definition,
                PlayerId = playerId
            });

            Debug.Log($"BuildingMenuController: Selected {definition.displayName} for placement");
        }

        #endregion

        #region UI Setup

        private bool SetupUIReferences()
        {
            if (uiDocument == null)
            {
                Debug.LogError("BuildingMenuController: UIDocument not assigned");
                return false;
            }

            var root = uiDocument.rootVisualElement;
            if (root == null)
            {
                Debug.LogError("BuildingMenuController: Root visual element is null");
                return false;
            }

            // Find building menu elements
            buildingMenu = root.Q<VisualElement>("building-menu");
            if (buildingMenu == null)
            {
                Debug.LogError("BuildingMenuController: building-menu element not found in UXML");
                return false;
            }

            entriesContainer = root.Q<VisualElement>("building-entries-container");
            if (entriesContainer == null)
            {
                Debug.LogError("BuildingMenuController: building-entries-container element not found in UXML");
                return false;
            }

            return true;
        }

        private void CreateBuildingEntries()
        {
            if (entriesContainer == null || buildingEntryTemplate == null)
            {
                Debug.LogError("BuildingMenuController: Cannot create entries - missing references");
                return;
            }

            // Clear existing entries
            entriesContainer.Clear();
            foreach (var view in entryViews.Values)
            {
                view.Dispose();
            }
            entryViews.Clear();

            // Create entry for each building
            foreach (var buildingInfo in dataProvider.GetAvailableBuildings())
            {
                CreateBuildingEntry(buildingInfo);
            }
        }

        private void CreateBuildingEntry(BuildingInfo buildingInfo)
        {
            // Validate template is assigned
            if (buildingEntryTemplate == null)
            {
                Debug.LogError($"BuildingMenuController: buildingEntryTemplate is not assigned! Cannot create entry for {buildingInfo.Id}");
                return;
            }

            // Instantiate template
            var templateInstance = buildingEntryTemplate.Instantiate();
            if (templateInstance == null)
            {
                Debug.LogError($"BuildingMenuController: Failed to instantiate template for {buildingInfo.Id}");
                return;
            }

            // Debug: Log template contents
            if (templateInstance.childCount == 0)
            {
                Debug.LogWarning($"BuildingMenuController: Template instance for {buildingInfo.Id} has 0 children. Template name: {buildingEntryTemplate.name}");
            }

            // Create view
            var entryView = new BuildingEntryView(templateInstance, buildingInfo);
            entryView.OnClicked += HandleBuildingEntryClicked;

            // Add to container and tracking
            entriesContainer.Add(templateInstance);
            entryViews[buildingInfo.Id] = entryView;
        }

        private void RefreshAllEntries()
        {
            foreach (var buildingInfo in dataProvider.GetAvailableBuildings())
            {
                if (entryViews.TryGetValue(buildingInfo.Id, out var view))
                {
                    view.UpdateAffordability(buildingInfo.CanAfford, buildingInfo.Costs);
                }
            }
        }

        #endregion

        #region Visibility

        private void ShowMenu()
        {
            buildingMenu?.RemoveFromClassList("hidden");
        }

        private void HideMenu()
        {
            buildingMenu?.AddToClassList("hidden");
        }

        #endregion

        #region Cleanup

        private void Cleanup()
        {
            // Unsubscribe from provider events
            if (dataProvider != null)
            {
                dataProvider.OnAffordabilityChanged -= HandleAffordabilityChanged;
            }

            // Dispose all entry views
            foreach (var view in entryViews.Values)
            {
                view.Dispose();
            }
            entryViews.Clear();

            isInitialized = false;
        }

        #endregion
    }
}
