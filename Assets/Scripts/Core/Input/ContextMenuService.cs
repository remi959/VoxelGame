// ============================================================================
// ContextMenuService.cs - Service for right-click context menu on world objects
// ============================================================================
using Assets.Scripts.Buildings;
using Assets.Scripts.Core.Events;
using Assets.Scripts.Core.Services;
using Assets.Scripts.Events;
using Assets.Scripts.NPCs.Spawning;
using Assets.Scripts.Shared.Interfaces;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Scripts.Core.Input
{
    /// <summary>
    /// Service that detects right-clicks on world objects and opens context menus.
    /// 
    /// Responsibilities:
    /// - Detects right-click on world objects
    /// - Raycasts from camera through click position
    /// - Fires ContextMenuRequestedEvent when a valid target is clicked
    /// - Disables during build mode and other blocking states
    /// 
    /// Design Notes:
    /// - Only detects, does not render UI
    /// - Works with any object implementing IHoverTarget
    /// - Can be paused/disabled via events
    /// 
    /// PERSISTENCE:
    /// This component should be placed as a child of the [Services] GameObject.
    /// PersistentServices handles DontDestroyOnLoad for the entire hierarchy.
    /// </summary>
    public class ContextMenuService : MonoBehaviour
    {
        public static ContextMenuService Instance { get; private set; }

        [Header("Detection Settings")]
        [Tooltip("Layer mask for context menu detection")]
        [SerializeField] private LayerMask contextMenuLayerMask;

        [Tooltip("Maximum raycast distance")]
        [SerializeField] private float maxRaycastDistance = 100f;

        [Header("Debug")]
        [SerializeField] private bool showDebugRay;

        // State
        private bool isEnabled = true;
        private bool isBuildModeActive;
        private bool isBuildingMenuOpen;
        private bool isContextMenuOpen;
        private bool isSpawnUIOpen;

        // Cached for performance
        private Camera mainCamera;

        // Public accessors
        public bool IsDetectionEnabled => isEnabled && !isBuildModeActive && !isBuildingMenuOpen;
        public bool IsContextMenuOpen => isContextMenuOpen;
        public bool IsSpawnUIOpen => isSpawnUIOpen;

        #region Lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            ServiceLocator.Register(this);

            // Setup default layer mask if not configured
            if (contextMenuLayerMask == 0)
            {
                contextMenuLayerMask = LayerMask.GetMask("Interactable");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                ServiceLocator.Unregister<ContextMenuService>();
            }
        }

        private void OnEnable()
        {
            // Subscribe to build mode events
            EventBus.Subscribe<BuildModeStartedEvent>(OnBuildModeStarted);
            EventBus.Subscribe<BuildModeEndedEvent>(OnBuildModeEnded);
            EventBus.Subscribe<BuildingMenuOpenedEvent>(OnBuildingMenuOpened);
            EventBus.Subscribe<BuildingMenuClosedEvent>(OnBuildingMenuClosed);
            EventBus.Subscribe<ContextMenuClosedEvent>(OnContextMenuClosed);
            EventBus.Subscribe<SpawnUIClosedEvent>(OnSpawnUIClosed);
        }

        private void OnDisable()
        {
            // Unsubscribe from events
            EventBus.Unsubscribe<BuildModeStartedEvent>(OnBuildModeStarted);
            EventBus.Unsubscribe<BuildModeEndedEvent>(OnBuildModeEnded);
            EventBus.Unsubscribe<BuildingMenuOpenedEvent>(OnBuildingMenuOpened);
            EventBus.Unsubscribe<BuildingMenuClosedEvent>(OnBuildingMenuClosed);
            EventBus.Unsubscribe<ContextMenuClosedEvent>(OnContextMenuClosed);
            EventBus.Unsubscribe<SpawnUIClosedEvent>(OnSpawnUIClosed);
        }

        private void Start()
        {
            mainCamera = Camera.main;
        }

        private void Update()
        {
            if (!IsDetectionEnabled) return;

            // Check for right-click
            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            {
                HandleRightClick();
            }
        }

        #endregion

        #region Event Handlers

        private void OnBuildModeStarted(BuildModeStartedEvent e)
        {
            isBuildModeActive = true;
            CloseContextMenu();
            CloseSpawnUI();
        }

        private void OnBuildModeEnded(BuildModeEndedEvent e)
        {
            isBuildModeActive = false;
        }

        private void OnBuildingMenuOpened(BuildingMenuOpenedEvent e)
        {
            isBuildingMenuOpen = true;
            CloseContextMenu();
            CloseSpawnUI();
        }

        private void OnBuildingMenuClosed(BuildingMenuClosedEvent e)
        {
            isBuildingMenuOpen = false;
        }

        private void OnContextMenuClosed(ContextMenuClosedEvent e)
        {
            isContextMenuOpen = false;
        }

        private void OnSpawnUIClosed(SpawnUIClosedEvent e)
        {
            isSpawnUIOpen = false;
        }

        #endregion

        #region Detection Logic

        private void HandleRightClick()
        {
            // Don't open context menu if NPCs are selected - right-click is for commanding them
            if (SelectionManager.Instance != null && SelectionManager.Instance.SelectedNPCs.Count > 0)
            {
                return;
            }

            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null) return;
            }

            // Get mouse position
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = mainCamera.ScreenPointToRay(mousePos);

            if (showDebugRay)
            {
                Debug.DrawRay(ray.origin, ray.direction * maxRaycastDistance, Color.yellow, 1f);
            }

            // Raycast against context menu layer
            if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance, contextMenuLayerMask))
            {
                DebugManager.LogInput($"ContextMenuService: Raycast hit {hit.collider.gameObject.name} on layer {hit.collider.gameObject.layer}");

                // Check for completed building with spawner first
                var completedBuilding = hit.collider.GetComponent<CompletedBuilding>();
                if (completedBuilding == null)
                {
                    completedBuilding = hit.collider.GetComponentInParent<CompletedBuilding>();
                }

                if (completedBuilding != null)
                {
                    DebugManager.LogInput($"ContextMenuService: Found CompletedBuilding '{completedBuilding.DisplayName}', HasSpawners={completedBuilding.HasSpawners}");
                    
                    if (completedBuilding.HasSpawners)
                    {
                        // Open spawn UI instead of context menu
                        OpenSpawnUI(completedBuilding, hit.point, mousePos);
                        return;
                    }
                }

                // Check for BuildingHolder (pre-placed buildings that haven't gone through construction)
                var buildingHolder = hit.collider.GetComponent<BuildingHolder>();
                if (buildingHolder == null)
                {
                    buildingHolder = hit.collider.GetComponentInParent<BuildingHolder>();
                }

                if (buildingHolder != null)
                {
                    DebugManager.LogInput($"ContextMenuService: Found BuildingHolder '{buildingHolder.BuildingName}'");
                    
                    // Check for spawners on the BuildingHolder
                    var spawners = buildingHolder.GetComponentsInChildren<IUnitSpawner>();
                    DebugManager.LogInput($"ContextMenuService: BuildingHolder has {spawners.Length} spawner(s)");
                    
                    if (spawners.Length > 0)
                    {
                        // Open spawn UI for BuildingHolder
                        OpenSpawnUI(buildingHolder, spawners, hit.point, mousePos);
                        return;
                    }
                }

                // Fall back to standard context menu
                var contextTarget = hit.collider.GetComponent<IHoverTarget>();
                
                // If not on collider, try parent
                if (contextTarget == null)
                {
                    contextTarget = hit.collider.GetComponentInParent<IHoverTarget>();
                }

                if (contextTarget != null)
                {
                    OpenContextMenu(contextTarget, hit.collider.gameObject, hit.point, mousePos);
                }
                else
                {
                    DebugManager.LogInput($"ContextMenuService: No IHoverTarget found on {hit.collider.gameObject.name}");
                }
            }
            else
            {
                DebugManager.LogInput($"ContextMenuService: Raycast missed. LayerMask={contextMenuLayerMask.value}");
            }
        }

        private void OpenSpawnUI(CompletedBuilding building, Vector3 hitPosition, Vector2 screenPosition)
        {
            // Close any existing menus
            CloseContextMenu();

            var spawner = building.GetPrimarySpawner();
            if (spawner == null || !spawner.IsSpawnerActive)
            {
                DebugManager.LogInput($"ContextMenuService: Building {building.DisplayName} has no active spawner");
                return;
            }

            isSpawnUIOpen = true;

            EventBus.Publish(new SpawnUIRequestedEvent
            {
                Spawner = spawner,
                BuildingObject = building.gameObject,
                Building = building,
                ScreenPosition = screenPosition,
                WorldPosition = hitPosition
            });

            DebugManager.LogInput($"ContextMenuService: Opened spawn UI for {building.DisplayName}");
        }

        /// <summary>
        /// Opens spawn UI for a BuildingHolder (pre-placed building without CompletedBuilding component).
        /// </summary>
        private void OpenSpawnUI(BuildingHolder buildingHolder, IUnitSpawner[] spawners, Vector3 hitPosition, Vector2 screenPosition)
        {
            // Close any existing menus
            CloseContextMenu();

            // Find first active spawner
            IUnitSpawner activeSpawner = null;
            foreach (var spawner in spawners)
            {
                if (spawner.IsSpawnerActive)
                {
                    activeSpawner = spawner;
                    break;
                }
            }

            if (activeSpawner == null)
            {
                DebugManager.LogInput($"ContextMenuService: BuildingHolder {buildingHolder.BuildingName} has no active spawner");
                return;
            }

            isSpawnUIOpen = true;

            EventBus.Publish(new SpawnUIRequestedEvent
            {
                Spawner = activeSpawner,
                BuildingObject = buildingHolder.gameObject,
                Building = null, // No CompletedBuilding for pre-placed buildings
                ScreenPosition = screenPosition,
                WorldPosition = hitPosition
            });

            DebugManager.LogInput($"ContextMenuService: Opened spawn UI for BuildingHolder {buildingHolder.BuildingName}");
        }

        private void OpenContextMenu(IHoverTarget target, GameObject targetObject, Vector3 hitPosition, Vector2 screenPosition)
        {
            isContextMenuOpen = true;

            EventBus.Publish(new ContextMenuRequestedEvent
            {
                Target = target,
                TargetGameObject = targetObject,
                HitPosition = hitPosition,
                ScreenPosition = screenPosition
            });

            DebugManager.LogInput($"ContextMenuService: Opened menu for {target.DisplayName}");
        }

        private void CloseContextMenu()
        {
            if (!isContextMenuOpen) return;

            isContextMenuOpen = false;

            EventBus.Publish(new ContextMenuClosedEvent());

            DebugManager.LogInput("ContextMenuService: Closed context menu");
        }

        private void CloseSpawnUI()
        {
            if (!isSpawnUIOpen) return;

            isSpawnUIOpen = false;

            EventBus.Publish(new SpawnUIClosedEvent());

            DebugManager.LogInput("ContextMenuService: Closed spawn UI");
        }

        #endregion

        #region Public API

        /// <summary>
        /// Enable or disable context menu detection.
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            if (isEnabled == enabled) return;

            isEnabled = enabled;

            if (!enabled)
            {
                CloseContextMenu();
                CloseSpawnUI();
            }
        }

        /// <summary>
        /// Request closing the context menu.
        /// </summary>
        public void RequestCloseMenu()
        {
            CloseContextMenu();
        }

        /// <summary>
        /// Request closing the spawn UI.
        /// </summary>
        public void RequestCloseSpawnUI()
        {
            CloseSpawnUI();
        }

        #endregion
    }
}
