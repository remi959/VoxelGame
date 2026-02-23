// ============================================================================
// ResourceUIController.cs - Manages the resource UI panel
// ============================================================================
using System;
using System.Collections.Generic;
using Assets.Scripts.Shared.Enums;
using Assets.UI.Scripts.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.UI.Scripts.Views
{
    /// <summary>
    /// Controller for the resource panel UI.
    /// 
    /// Responsibilities:
    /// - Initializes the resource panel from UXML
    /// - Dynamically creates ResourceEntryView instances for each resource type
    /// - Subscribes to data provider for value changes
    /// - Updates entry views when values change
    /// - Cleans up on destroy
    /// 
    /// Design notes:
    /// - Does NOT know about EconomyService directly (uses IResourceDataProvider)
    /// - Does NOT contain resource logic, only UI orchestration
    /// - Entry views are created dynamically, not hard-coded
    /// - Uses dictionary for O(1) lookup of entries by resource type
    /// </summary>
    public class ResourceUIController : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("The UIDocument component for the game UI")]
        [SerializeField] private UIDocument uiDocument;

        [Header("Templates")]
        [Tooltip("UXML template for resource entries")]
        [SerializeField] private VisualTreeAsset resourceEntryTemplate;

        [Header("Configuration")]
        [Tooltip("Player ID to display resources for (default: 0 for local player)")]
        [SerializeField] private int playerId = 0;

        // Runtime references
        private VisualElement entriesContainer;
        private IResourceDataProvider dataProvider;
        private readonly Dictionary<EResourceType, ResourceEntryView> entryViews = new();
        private bool isInitialized;

        #region Unity Lifecycle

        private void OnEnable()
        {
            // Defer initialization to allow other systems to set up
            // In production, you might use a proper initialization order system
        }

        private void OnDisable()
        {
            Cleanup();
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Initializes the resource UI with a data provider.
        /// Call this after EconomyService is ready.
        /// </summary>
        /// <param name="provider">Data provider for resource information</param>
        public void Initialize(IResourceDataProvider provider)
        {
            if (isInitialized)
            {
                Debug.LogWarning("ResourceUIController: Already initialized");
                return;
            }

            if (provider == null)
            {
                Debug.LogError("ResourceUIController: Cannot initialize with null provider");
                return;
            }

            dataProvider = provider;

            // Set up UI
            if (!SetupUIReferences())
            {
                Debug.LogError("ResourceUIController: Failed to set up UI references");
                return;
            }

            // Create entry views for all resource types
            CreateResourceEntries();

            // Subscribe to value changes
            dataProvider.OnResourceValueChanged += HandleResourceValueChanged;

            // Load initial values
            RefreshAllValues();

            isInitialized = true;
            Debug.Log($"ResourceUIController: Initialized with {entryViews.Count} resource entries");
        }

        /// <summary>
        /// Forces a refresh of all displayed values.
        /// </summary>
        public void RefreshAllValues()
        {
            if (dataProvider == null) return;

            foreach (var kvp in entryViews)
            {
                int value = dataProvider.GetResourceAmount(playerId, kvp.Key);
                kvp.Value.SetValue(value);
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Sets up references to UI elements from the UXML document.
        /// </summary>
        private bool SetupUIReferences()
        {
            if (uiDocument == null)
            {
                Debug.LogError("ResourceUIController: UIDocument is not assigned");
                return false;
            }

            var root = uiDocument.rootVisualElement;
            if (root == null)
            {
                Debug.LogError("ResourceUIController: UIDocument has no root element");
                return false;
            }

            // Find the entries container within the resource panel
            entriesContainer = root.Q<VisualElement>("entries-container");
            if (entriesContainer == null)
            {
                // Try finding via the panel first
                var panel = root.Q<VisualElement>("resource-panel");
                if (panel != null)
                {
                    entriesContainer = panel.Q<VisualElement>("entries-container");
                }
            }

            if (entriesContainer == null)
            {
                Debug.LogError("ResourceUIController: Could not find 'entries-container' in UI");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Creates entry views for all resource types from the data provider.
        /// </summary>
        private void CreateResourceEntries()
        {
            if (resourceEntryTemplate == null)
            {
                Debug.LogError("ResourceUIController: Resource entry template is not assigned");
                return;
            }

            // Clear existing entries
            entriesContainer.Clear();
            entryViews.Clear();

            // Get all resource types as a list to know which is last
            var resourceTypes = new System.Collections.Generic.List<EResourceType>(dataProvider.GetAllResourceTypes());
            
            // Create an entry for each resource type
            for (int i = 0; i < resourceTypes.Count; i++)
            {
                bool isLast = (i == resourceTypes.Count - 1);
                CreateResourceEntry(resourceTypes[i], isLast);
            }
        }

        /// <summary>
        /// Creates a single resource entry view.
        /// </summary>
        private void CreateResourceEntry(EResourceType resourceType, bool isLast = false)
        {
            // Instantiate template
            var templateInstance = resourceEntryTemplate.Instantiate();
            if (templateInstance == null)
            {
                Debug.LogError($"ResourceUIController: Failed to instantiate template for {resourceType}");
                return;
            }

            // Get display info from provider
            string displayName = dataProvider.GetResourceDisplayName(resourceType);
            string iconPath = dataProvider.GetResourceIconPath(resourceType);

            // Try to get direct sprite reference (for config-based providers)
            UnityEngine.Sprite iconSprite = null;
            if (dataProvider is Core.ResourceDataProvider concreteProvider)
            {
                iconSprite = concreteProvider.GetResourceIcon(resourceType);
            }

            // Create view
            var entryView = new ResourceEntryView(
                templateInstance,
                resourceType,
                displayName,
                iconPath,
                iconSprite
            );

            // Apply last-entry modifier class (Unity USS doesn't support :last-child)
            if (isLast)
            {
                entryView.Root?.AddToClassList("resource-entry--last");
            }

            // Add to container
            entriesContainer.Add(templateInstance);

            // Track for updates
            entryViews[resourceType] = entryView;
        }

        /// <summary>
        /// Handles resource value change events from the data provider.
        /// </summary>
        private void HandleResourceValueChanged(int changedPlayerId, EResourceType resourceType, int oldValue, int newValue)
        {
            // Filter to our player
            if (changedPlayerId != playerId) return;

            // Update the corresponding entry view
            if (entryViews.TryGetValue(resourceType, out var entryView))
            {
                entryView.SetValue(newValue);
            }
        }

        /// <summary>
        /// Cleans up resources and subscriptions.
        /// </summary>
        private void Cleanup()
        {
            if (dataProvider != null)
            {
                dataProvider.OnResourceValueChanged -= HandleResourceValueChanged;

                // Dispose if provider implements IDisposable
                if (dataProvider is IDisposable disposable)
                {
                    disposable.Dispose();
                }

                dataProvider = null;
            }

            // Dispose all entry views
            foreach (var entryView in entryViews.Values)
            {
                entryView.Dispose();
            }
            entryViews.Clear();

            isInitialized = false;
        }

        #endregion
    }
}
