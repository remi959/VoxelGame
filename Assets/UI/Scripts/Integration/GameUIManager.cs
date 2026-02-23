// ============================================================================
// GameUIManager.cs - Main entry point for game UI initialization
// ============================================================================
using Assets.Scripts.Buildings;
using Assets.Scripts.Core;
using Assets.Scripts.Economy;
using Assets.UI.Scripts.Core;
using Assets.UI.Scripts.Data;
using Assets.UI.Scripts.Views;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.UI.Scripts.Integration
{
    /// <summary>
    /// Central manager for initializing and coordinating all game UI systems.
    /// 
    /// Responsibilities:
    /// - Waits for game systems to be ready
    /// - Creates data providers for UI controllers
    /// - Initializes UI controllers in correct order
    /// - Handles scene transitions and cleanup
    /// 
    /// Design notes:
    /// - This is the ONLY script that knows about both game systems and UI
    /// - UI controllers receive abstract interfaces, not concrete classes
    /// - Follows the Composition Root pattern
    /// </summary>
    public class GameUIManager : MonoBehaviour
    {
        [Header("UI Documents")]
        [Tooltip("Main game UI document")]
        [SerializeField] private UIDocument mainUIDocument;

        [Header("Templates")]
        [Tooltip("Template for resource entries")]
        [SerializeField] private VisualTreeAsset resourceEntryTemplate;

        [Tooltip("Template for building entries")]
        [SerializeField] private VisualTreeAsset buildingEntryTemplate;

        [Header("Configuration")]
        [Tooltip("Resource UI configuration (optional - uses defaults if not assigned)")]
        [SerializeField] private ResourceUIConfigSO resourceUIConfig;

        [Tooltip("Building catalog containing all available buildings")]
        [SerializeField] private BuildingCatalogSO buildingCatalog;

        [Header("Controllers")]
        [Tooltip("Resource UI controller (will be auto-found if not assigned)")]
        [SerializeField] private ResourceUIController resourceUIController;

        [Tooltip("Building menu controller (will be auto-found if not assigned)")]
        [SerializeField] private BuildingMenuController buildingMenuController;

        [Tooltip("Context menu UI controller (will be auto-found if not assigned)")]
        [SerializeField] private ContextMenuUIController contextMenuController;

        [Tooltip("Spawn UI controller (will be auto-found if not assigned)")]
        [SerializeField] private SpawnUIController spawnUIController;

        [Header("Player Settings")]
        [Tooltip("Local player ID")]
        [SerializeField] private int localPlayerId = 0;

        [Tooltip("Delay before initializing UI (allows other systems to start)")]
        [SerializeField] private float initializationDelay = 0.1f;

        private ResourceDataProvider resourceDataProvider; 
        private BuildingDataProvider buildingDataProvider;
        private bool isInitialized;

        #region Unity Lifecycle

        private void Start()
        {
            // Delay initialization to ensure game systems are ready
            Invoke(nameof(InitializeUI), initializationDelay);
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes all UI systems.
        /// </summary>
        private void InitializeUI()
        {
            if (isInitialized) return;

            // Validate dependencies
            if (!ValidateDependencies())
            {
                Debug.LogError("GameUIManager: Missing dependencies, UI initialization failed");
                return;
            }

            // Initialize resource UI
            InitializeResourceUI();

            // Initialize building UI
            InitializeBuildingUI();

            // Initialize hover UI
            InitializeHoverUI();

            // Initialize spawn UI
            InitializeSpawnUI();

            // Future: Initialize other UI systems here
            // InitializeUnitUI();
            // InitializeMinimapUI();

            isInitialized = true;
            Debug.Log("GameUIManager: UI initialization complete");
        }

        /// <summary>
        /// Validates that required dependencies are available.
        /// </summary>
        private bool ValidateDependencies()
        {
            // Check for EconomyService
            if (EconomyService.Instance == null)
            {
                Debug.LogError("GameUIManager: EconomyService not found");
                return false;
            }

            // Check for UIDocument
            if (mainUIDocument == null)
            {
                Debug.LogError("GameUIManager: Main UI Document not assigned");
                return false;
            }

            // Auto-find resource controller if not assigned
            if (resourceUIController == null)
            {
                resourceUIController = GetComponentInChildren<ResourceUIController>();
                if (resourceUIController == null)
                {
                    Debug.LogError("GameUIManager: ResourceUIController not found");
                    return false;
                }
            }

            // Auto-find building menu controller if not assigned
            if (buildingMenuController == null)
            {
                buildingMenuController = GetComponentInChildren<BuildingMenuController>();
                // Building menu controller is optional - log warning but don't fail
                if (buildingMenuController == null)
                {
                    Debug.LogWarning("GameUIManager: BuildingMenuController not found - building menu will not be available");
                }
            }

            // Auto-find context menu controller if not assigned
            if (contextMenuController == null)
            {
                contextMenuController = GetComponentInChildren<ContextMenuUIController>();
                // Context menu controller is optional - log warning but don't fail
                if (contextMenuController == null)
                {
                    Debug.LogWarning("GameUIManager: ContextMenuUIController not found - context menu will not be available");
                }
            }

            // Auto-find spawn UI controller if not assigned
            if (spawnUIController == null)
            {
                spawnUIController = GetComponentInChildren<SpawnUIController>();
                // Spawn UI controller is optional - log warning but don't fail
                if (spawnUIController == null)
                {
                    Debug.LogWarning("GameUIManager: SpawnUIController not found - spawn UI will not be available");
                }
            }

            // Warn if building catalog is missing
            if (buildingCatalog == null)
            {
                Debug.LogWarning("GameUIManager: BuildingCatalog not assigned - building menu will be empty");
            }

            return true;
        }

        /// <summary>
        /// Initializes the resource UI system.
        /// </summary>
        private void InitializeResourceUI()
        {
            // Create data provider (bridges EconomyService to UI)
            // Pass optional config for customization
            resourceDataProvider = new ResourceDataProvider(
                localPlayerId, 
                EconomyService.Instance,
                resourceUIConfig
            );

            // Initialize controller with the provider
            resourceUIController.Initialize(resourceDataProvider);

            Debug.Log("GameUIManager: Resource UI initialized");
        }

        /// <summary>
        /// Initializes the building menu UI system.
        /// </summary>
        private void InitializeBuildingUI()
        {
            if (buildingMenuController == null)
            {
                Debug.Log("GameUIManager: Skipping building UI initialization - no controller");
                return;
            }

            // Create building data provider (bridges BuildingCatalog and EconomyService to UI)
            buildingDataProvider = new BuildingDataProvider(
                localPlayerId,
                buildingCatalog,
                EconomyService.Instance
            );

            // Pass template if assigned here but not on controller
            if (buildingEntryTemplate != null)
            {
                buildingMenuController.SetBuildingEntryTemplate(buildingEntryTemplate);
            }

            // Initialize controller with the provider
            buildingMenuController.Initialize(buildingDataProvider);

            Debug.Log("GameUIManager: Building UI initialized");
        }

        /// <summary>
        /// Initializes the context menu UI system.
        /// </summary>
        private void InitializeHoverUI()
        {
            if (contextMenuController == null)
            {
                Debug.Log("GameUIManager: Skipping context menu initialization - no controller");
                return;
            }

            // Context menu controller initializes itself in Start()
            // but we can explicitly initialize it here for consistency
            contextMenuController.Initialize();

            Debug.Log("GameUIManager: Context menu UI initialized");
        }

        /// <summary>
        /// Initializes the spawn UI system.
        /// </summary>
        private void InitializeSpawnUI()
        {
            if (spawnUIController == null)
            {
                Debug.Log("GameUIManager: Skipping spawn UI initialization - no controller");
                return;
            }

            // Spawn UI controller initializes itself in Start()
            // but we can explicitly initialize it here for consistency
            spawnUIController.Initialize();

            Debug.Log("GameUIManager: Spawn UI initialized");
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// Cleans up all UI resources.
        /// </summary>
        private void Cleanup()
        {
            // Dispose data providers
            resourceDataProvider?.Dispose();
            resourceDataProvider = null;

            buildingDataProvider?.Dispose();
            buildingDataProvider = null;

            isInitialized = false;
        }

        #endregion

        #region Public API (for testing/debugging)

        /// <summary>
        /// Forces re-initialization of all UI.
        /// Useful for editor testing.
        /// </summary>
        [ContextMenu("Reinitialize UI")]
        public void ReinitializeUI()
        {
            Cleanup();
            isInitialized = false;
            InitializeUI();
        }

        #endregion
    }
}
