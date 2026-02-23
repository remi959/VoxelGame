// ============================================================================
// CompletedBuilding.cs - Component for fully constructed buildings
// ============================================================================
using System.Collections.Generic;
using Assets.Scripts.Buildings.Construction;
using Assets.Scripts.Core;
using Assets.Scripts.Core.Events;
using Assets.Scripts.Events;
using Assets.Scripts.NPCs.Spawning;
using Assets.Scripts.Shared;
using Assets.Scripts.Shared.Enums;
using Assets.Scripts.Shared.Interfaces;
using UnityEngine;

namespace Assets.Scripts.Buildings
{
    /// <summary>
    /// Component attached to completed buildings.
    /// 
    /// Responsibilities:
    /// - Identifies a building as completed (vs under construction)
    /// - Exposes building metadata
    /// - Implements IHoverTarget for contextual UI
    /// - Handles building destruction
    /// 
    /// Note: This component should be added to completed building prefabs
    /// or instantiated by ConstructionService on completion.
    /// </summary>
    public class CompletedBuilding : MonoBehaviour, IHoverTarget
    {
        // ========== Constants ==========
        private const string ACTION_DESTROY = "destroy";

        // ========== Serialized Fields ==========
        [Header("Building Info")]
        [SerializeField] private string buildingName;
        [SerializeField] private int ownerId;
        [SerializeField] private BuildingDefinitionSO definition;

        // ========== Runtime State ==========
        private readonly List<HoverAction> cachedActions = new();
        private bool isBeingDestroyed;

        // ========== Spawner Support ==========
        private IUnitSpawner[] cachedSpawners;
        private bool spawnersInitialized;

        // ========== Properties ==========

        /// <summary>
        /// The building definition this was constructed from.
        /// </summary>
        public BuildingDefinitionSO Definition => definition;

        /// <summary>
        /// Player who owns this building.
        /// </summary>
        public int OwnerId => ownerId;

        /// <summary>
        /// Whether this building has any unit spawners attached.
        /// </summary>
        public bool HasSpawners => GetSpawners().Length > 0;

        #region IHoverTarget Implementation

        /// <inheritdoc />
        public string DisplayName => string.IsNullOrEmpty(buildingName) ? gameObject.name : buildingName;

        /// <inheritdoc />
        public HoverTargetType TargetType => HoverTargetType.CompletedBuilding;

        /// <inheritdoc />
        public IReadOnlyList<HoverAction> GetAvailableActions()
        {
            cachedActions.Clear();

            if (!isBeingDestroyed)
            {
                cachedActions.Add(new HoverAction(
                    id: ACTION_DESTROY,
                    label: "Destroy Building",
                    isDestructive: true,
                    isEnabled: true,
                    tooltip: "Permanently remove this building"
                ));
            }

            return cachedActions;
        }

        /// <inheritdoc />
        public bool ExecuteAction(string actionId)
        {
            if (isBeingDestroyed) return false;

            switch (actionId)
            {
                case ACTION_DESTROY:
                    DestroyBuilding();
                    return true;

                default:
                    Debug.LogWarning($"CompletedBuilding: Unknown action '{actionId}'");
                    return false;
            }
        }

        #endregion

        #region Lifecycle

        private void Awake()
        {
            if (string.IsNullOrEmpty(buildingName))
            {
                buildingName = gameObject.name;
            }
        }

        private void OnEnable()
        {
            EntityRegistry<CompletedBuilding>.Register(this);
        }

        private void OnDisable()
        {
            EntityRegistry<CompletedBuilding>.Unregister(this);
        }

        #endregion

        #region Public API

        /// <summary>
        /// Initialize the completed building with data from construction.
        /// Called by ConstructionService when building completes.
        /// </summary>
        public void Initialize(BuildingDefinitionSO def, int ownerPlayerId, string name = null)
        {
            definition = def;
            ownerId = ownerPlayerId;
            buildingName = name ?? def?.displayName ?? gameObject.name;

            // Initialize spawners with owner ID
            InitializeSpawners();
        }

        /// <summary>
        /// Destroy this building.
        /// </summary>
        public void DestroyBuilding()
        {
            if (isBeingDestroyed) return;
            isBeingDestroyed = true;

            // Deactivate spawners before destruction
            DeactivateSpawners();

            Debug.Log($"CompletedBuilding: Destroying {DisplayName}");

            // Publish destruction event before destroying
            EventBus.Publish(new BuildingDestroyedEvent
            {
                Building = this,
                BuildingName = DisplayName,
                Position = transform.position,
                OwnerId = ownerId,
                Definition = definition
            });

            // Destroy the game object
            Destroy(gameObject);
        }

        #endregion

        #region Spawner Support

        /// <summary>
        /// Get all unit spawners attached to this building.
        /// </summary>
        public IUnitSpawner[] GetSpawners()
        {
            if (!spawnersInitialized)
            {
                cachedSpawners = GetComponents<IUnitSpawner>();
                spawnersInitialized = true;
            }
            return cachedSpawners ?? System.Array.Empty<IUnitSpawner>();
        }

        /// <summary>
        /// Get the first active spawner, if any.
        /// </summary>
        public IUnitSpawner GetPrimarySpawner()
        {
            var spawners = GetSpawners();
            foreach (var spawner in spawners)
            {
                if (spawner.IsSpawnerActive)
                    return spawner;
            }
            return null;
        }

        /// <summary>
        /// Initialize all spawners with the building's owner ID.
        /// </summary>
        private void InitializeSpawners()
        {
            var spawners = GetSpawners();
            foreach (var spawner in spawners)
            {
                if (spawner is UnitSpawnerBase spawnerBase)
                {
                    spawnerBase.SetOwner(ownerId);
                }
            }
        }

        /// <summary>
        /// Deactivate all spawners when building is destroyed.
        /// </summary>
        private void DeactivateSpawners()
        {
            var spawners = GetSpawners();
            foreach (var spawner in spawners)
            {
                if (spawner is UnitSpawnerBase spawnerBase)
                {
                    spawnerBase.SetActive(false);
                }
            }
        }

        #endregion

        #region Static Helpers

        /// <summary>
        /// Find the nearest completed building to a position.
        /// </summary>
        public static CompletedBuilding FindNearest(Vector3 position, int? ownerFilter = null)
        {
            return EntityRegistry<CompletedBuilding>.FindNearest(position, b =>
                !ownerFilter.HasValue || b.OwnerId == ownerFilter.Value);
        }

        #endregion

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(buildingName))
            {
                buildingName = gameObject.name;
            }
        }
#endif
    }
}
