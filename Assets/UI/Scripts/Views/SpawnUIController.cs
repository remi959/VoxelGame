// ============================================================================
// SpawnUIController.cs - Controller for the unit spawn UI panel
// ============================================================================
using System.Collections.Generic;
using Assets.Scripts.Core.Events;
using Assets.Scripts.NPCs.Spawning;
using Assets.Scripts.Shared.Enums;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Assets.UI.Scripts.Views
{
    /// <summary>
    /// Controller for the unit spawn UI panel.
    /// 
    /// Responsibilities:
    /// - Listens for SpawnUIRequestedEvent
    /// - Queries spawner for available units
    /// - Dynamically populates unit entries
    /// - Handles unit selection and spawn requests
    /// - Positions panel at click location
    /// - Closes on Escape or click outside
    /// 
    /// Design Notes:
    /// - NO gameplay logic - only UI presentation
    /// - Uses events to communicate with spawner
    /// - Dynamically creates unit entries from UXML templates
    /// </summary>
    public class SpawnUIController : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("The UIDocument component for the game UI")]
        [SerializeField] private UIDocument uiDocument;

        [Header("Templates")]
        [Tooltip("UXML template for spawn unit entries")]
        [SerializeField] private VisualTreeAsset unitEntryTemplate;

        [Header("Positioning")]
        [Tooltip("Offset from click position")]
        [SerializeField] private Vector2 clickOffset = new(10f, 10f);

        [Tooltip("Keep panel within screen bounds")]
        [SerializeField] private float screenPadding = 10f;

        // UI Elements
        private VisualElement root;
        private VisualElement spawnPanel;
        private Label titleLabel;
        private Label buildingNameLabel;
        private VisualElement entriesContainer;

        // State
        private IUnitSpawner currentSpawner;
        private Vector2 panelScreenPosition;
        private readonly List<VisualElement> entryElements = new();
        private bool isInitialized;
        private bool isPanelOpen;

        #region Unity Lifecycle

        private void OnEnable()
        {
            // Subscribe to spawn UI events
            EventBus.Subscribe<SpawnUIRequestedEvent>(OnSpawnUIRequested);
            EventBus.Subscribe<SpawnUIClosedEvent>(OnSpawnUIClosed);
        }

        private void OnDisable()
        {
            // Unsubscribe from events
            EventBus.Unsubscribe<SpawnUIRequestedEvent>(OnSpawnUIRequested);
            EventBus.Unsubscribe<SpawnUIClosedEvent>(OnSpawnUIClosed);

            Cleanup();
        }

        private void Start()
        {
            Initialize();
        }

        private void Update()
        {
            if (!isInitialized || !isPanelOpen) return;

            // Check for close input
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
                Debug.LogError("SpawnUIController: No UIDocument found");
                return;
            }

            root = uiDocument.rootVisualElement;
            if (root == null)
            {
                Debug.LogError("SpawnUIController: Root visual element is null");
                return;
            }

            // Get UI elements
            spawnPanel = root.Q<VisualElement>("spawn-panel");
            titleLabel = root.Q<Label>("spawn-title");
            buildingNameLabel = root.Q<Label>("spawn-building-name");
            entriesContainer = root.Q<VisualElement>("spawn-entries");

            if (spawnPanel == null)
            {
                Debug.LogError("SpawnUIController: spawn-panel not found in UXML");
                return;
            }

            // Ensure panel starts hidden
            HidePanel();

            isInitialized = true;
            Debug.Log("SpawnUIController: Initialized");
        }

        private void Cleanup()
        {
            ClearEntries();
            if (currentSpawner != null)
            {
                currentSpawner.OnSpawnableUnitsChanged -= RefreshEntries;
            }
            currentSpawner = null;
            isPanelOpen = false;
        }

        #endregion

        #region Event Handlers

        private void OnSpawnUIRequested(SpawnUIRequestedEvent e)
        {
            if (!isInitialized)
            {
                Initialize();
                if (!isInitialized) return;
            }

            // Unsubscribe from previous spawner
            if (currentSpawner != null)
            {
                currentSpawner.OnSpawnableUnitsChanged -= RefreshEntries;
            }

            currentSpawner = e.Spawner;
            panelScreenPosition = e.ScreenPosition;

            if (currentSpawner != null)
            {
                // Subscribe to spawner changes
                currentSpawner.OnSpawnableUnitsChanged += RefreshEntries;

                // Update header
                if (titleLabel != null)
                {
                    titleLabel.text = currentSpawner.SpawnerDisplayName;
                }
                if (buildingNameLabel != null && e.Building != null)
                {
                    buildingNameLabel.text = e.Building.DisplayName;
                }

                // Populate entries
                PopulateEntries();

                // Show and position panel
                ShowPanel();
                isPanelOpen = true;
            }
        }

        private void OnSpawnUIClosed(SpawnUIClosedEvent e)
        {
            HidePanel();
            Cleanup();
        }

        private void CheckForCloseInput()
        {
            // Close on Escape key
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                RequestClose();
                return;
            }

            // Close on right-click
            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
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
                if (spawnPanel != null && !IsPointInsidePanel(uiMousePos))
                {
                    RequestClose();
                }
            }
        }

        private bool IsPointInsidePanel(Vector2 point)
        {
            if (spawnPanel == null) return false;

            Rect panelRect = spawnPanel.worldBound;
            return panelRect.Contains(point);
        }

        private void RequestClose()
        {
            EventBus.Publish(new SpawnUIClosedEvent());
        }

        #endregion

        #region Panel Display

        private void ShowPanel()
        {
            if (spawnPanel == null) return;

            spawnPanel.RemoveFromClassList("hidden");

            // Position the panel at the click location
            PositionPanel();
        }

        private void HidePanel()
        {
            if (spawnPanel == null) return;

            spawnPanel.AddToClassList("hidden");
            isPanelOpen = false;
        }

        private void PositionPanel()
        {
            if (spawnPanel == null) return;

            // Wait for layout to be computed
            spawnPanel.RegisterCallback<GeometryChangedEvent>(OnPanelGeometryChanged);

            // Set initial position (may be adjusted after layout)
            float x = panelScreenPosition.x + clickOffset.x;
            float y = Screen.height - panelScreenPosition.y + clickOffset.y;

            spawnPanel.style.left = x;
            spawnPanel.style.top = y;
        }

        private void OnPanelGeometryChanged(GeometryChangedEvent evt)
        {
            spawnPanel.UnregisterCallback<GeometryChangedEvent>(OnPanelGeometryChanged);

            // Adjust position to keep panel on screen
            float x = panelScreenPosition.x + clickOffset.x;
            float y = Screen.height - panelScreenPosition.y + clickOffset.y;

            float panelWidth = spawnPanel.resolvedStyle.width;
            float panelHeight = spawnPanel.resolvedStyle.height;

            // Keep within screen bounds
            if (x + panelWidth > Screen.width - screenPadding)
            {
                x = Screen.width - panelWidth - screenPadding;
            }
            if (y + panelHeight > Screen.height - screenPadding)
            {
                y = Screen.height - panelHeight - screenPadding;
            }
            if (x < screenPadding) x = screenPadding;
            if (y < screenPadding) y = screenPadding;

            spawnPanel.style.left = x;
            spawnPanel.style.top = y;
        }

        #endregion

        #region Entry Population

        private void PopulateEntries()
        {
            ClearEntries();

            if (currentSpawner == null || entriesContainer == null) return;

            var spawnableUnits = currentSpawner.GetSpawnableUnits();

            foreach (var unitInfo in spawnableUnits)
            {
                CreateUnitEntry(unitInfo);
            }
        }

        private void RefreshEntries()
        {
            // Re-populate when spawner state changes
            PopulateEntries();
        }

        private void CreateUnitEntry(SpawnableUnitInfo unitInfo)
        {
            VisualElement entry;

            if (unitEntryTemplate != null)
            {
                entry = unitEntryTemplate.Instantiate();
            }
            else
            {
                // Create entry programmatically if no template
                entry = CreateEntryProgrammatically(unitInfo);
            }

            // Set up the entry
            SetupUnitEntry(entry, unitInfo);

            entriesContainer.Add(entry);
            entryElements.Add(entry);
        }

        private void SetupUnitEntry(VisualElement entry, SpawnableUnitInfo unitInfo)
        {
            // Get elements
            var nameLabel = entry.Q<Label>("entry-name");
            var descLabel = entry.Q<Label>("entry-description");
            var iconElement = entry.Q<VisualElement>("entry-icon");
            var costsContainer = entry.Q<VisualElement>("entry-costs");
            var statusText = entry.Q<Label>("entry-status-text");

            // Set name and description
            if (nameLabel != null)
            {
                nameLabel.text = unitInfo.DisplayName;
            }
            if (descLabel != null)
            {
                descLabel.text = unitInfo.Description ?? "";
            }

            // Set icon
            if (iconElement != null && unitInfo.Icon != null)
            {
                iconElement.style.backgroundImage = new StyleBackground(unitInfo.Icon);
            }
            else
            {
                entry.AddToClassList("spawn-unit-entry--no-icon");
            }

            // Add costs
            if (costsContainer != null && unitInfo.Costs != null)
            {
                foreach (var cost in unitInfo.Costs)
                {
                    CreateCostElement(costsContainer, cost);
                }
            }

            // Set status
            if (statusText != null)
            {
                if (!string.IsNullOrEmpty(unitInfo.DisabledReason))
                {
                    statusText.text = unitInfo.DisabledReason;
                }
                else
                {
                    statusText.text = "";
                }
            }

            // Set disabled state
            if (!unitInfo.CanSpawn)
            {
                entry.AddToClassList("spawn-unit-entry--disabled");
            }

            // Add click handler
            string unitId = unitInfo.UnitId;
            entry.RegisterCallback<ClickEvent>(evt =>
            {
                if (unitInfo.CanSpawn)
                {
                    OnUnitEntryClicked(unitId);
                }
            });
        }

        private void CreateCostElement(VisualElement container, ResourceCostInfo cost)
        {
            var costElement = new VisualElement();
            costElement.AddToClassList("spawn-cost");

            // Add resource type class for coloring
            string resourceClass = $"spawn-cost--{cost.ResourceType.ToString().ToLowerInvariant()}";
            costElement.AddToClassList(resourceClass);

            // Add affordability class
            costElement.AddToClassList(cost.IsAffordable ? "spawn-cost--affordable" : "spawn-cost--unaffordable");

            // Icon (placeholder - would need resource icons)
            var iconElement = new VisualElement();
            iconElement.AddToClassList("spawn-cost__icon");
            costElement.Add(iconElement);

            // Amount label
            var amountLabel = new Label($"{cost.Amount}");
            amountLabel.AddToClassList("spawn-cost__amount");
            costElement.Add(amountLabel);

            container.Add(costElement);
        }

        private VisualElement CreateEntryProgrammatically(SpawnableUnitInfo unitInfo)
        {
            // Fallback: create entry without template
            var entry = new VisualElement();
            entry.AddToClassList("spawn-unit-entry");

            // Icon container
            var iconContainer = new VisualElement { name = "entry-icon-container" };
            iconContainer.AddToClassList("spawn-unit-entry__icon-container");
            var icon = new VisualElement { name = "entry-icon" };
            icon.AddToClassList("spawn-unit-entry__icon");
            iconContainer.Add(icon);
            entry.Add(iconContainer);

            // Info container
            var infoContainer = new VisualElement { name = "entry-info" };
            infoContainer.AddToClassList("spawn-unit-entry__info");

            var nameLabel = new Label { name = "entry-name" };
            nameLabel.AddToClassList("spawn-unit-entry__name");
            infoContainer.Add(nameLabel);

            var descLabel = new Label { name = "entry-description" };
            descLabel.AddToClassList("spawn-unit-entry__description");
            infoContainer.Add(descLabel);

            entry.Add(infoContainer);

            // Costs container
            var costsContainer = new VisualElement { name = "entry-costs" };
            costsContainer.AddToClassList("spawn-unit-entry__costs");
            entry.Add(costsContainer);

            // Status container
            var statusContainer = new VisualElement { name = "entry-status" };
            statusContainer.AddToClassList("spawn-unit-entry__status");
            var statusText = new Label { name = "entry-status-text" };
            statusText.AddToClassList("spawn-unit-entry__status-text");
            statusContainer.Add(statusText);
            entry.Add(statusContainer);

            return entry;
        }

        private void ClearEntries()
        {
            foreach (var entry in entryElements)
            {
                entry.RemoveFromHierarchy();
            }
            entryElements.Clear();
        }

        #endregion

        #region Spawn Handling

        private void OnUnitEntryClicked(string unitId)
        {
            if (currentSpawner == null) return;

            // Attempt to spawn
            var result = currentSpawner.TrySpawnUnit(unitId);

            if (result.Success)
            {
                Debug.Log($"SpawnUIController: Successfully spawned {unitId}");
                // Optionally close panel after spawn, or leave open for multiple spawns
                // RequestClose();
            }
            else
            {
                Debug.LogWarning($"SpawnUIController: Failed to spawn {unitId}: {result.ErrorMessage}");
            }

            // Refresh entries to update affordability/cooldowns
            RefreshEntries();
        }

        #endregion
    }
}
