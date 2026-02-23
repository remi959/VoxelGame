// ============================================================================
// BuildingDataProvider.cs - Concrete implementation of IBuildingDataProvider
// ============================================================================
using System;
using System.Collections.Generic;
using Assets.Scripts.Buildings;
using Assets.Scripts.Core.Events;
using Assets.Scripts.Economy;
using Assets.Scripts.Economy.Events;
using Assets.Scripts.Events;
using Assets.Scripts.Shared.Enums;
using UnityEngine;

namespace Assets.UI.Scripts.Core
{
    /// <summary>
    /// Provides building data to the UI layer.
    /// Bridges BuildingCatalogSO and EconomyService to the UI.
    /// 
    /// Responsibilities:
    /// - Transforms BuildingDefinitionSO data into UI-friendly BuildingInfo
    /// - Checks affordability via EconomyService
    /// - Subscribes to resource changes to track affordability updates
    /// - Fires events when affordability changes
    /// 
    /// Design Notes:
    /// - Created and owned by GameUIManager
    /// - Lifetime matches the UI system lifetime
    /// - Does NOT modify any game state, only reads
    /// </summary>
    public class BuildingDataProvider : IBuildingDataProvider
    {
        private readonly int playerId;
        private readonly BuildingCatalogSO catalog;
        private readonly EconomyService economyService;
        
        // Cache affordability state to detect changes
        private readonly Dictionary<string, bool> affordabilityCache = new();
        
        // Events
        public event Action<string> OnBuildingUnlocked;
        public event Action<string, bool> OnAffordabilityChanged; 
        public event Action OnCatalogChanged;

        private bool isDisposed;

        /// <summary>
        /// Creates a new BuildingDataProvider.
        /// </summary>
        /// <param name="playerId">The player ID to check affordability for</param>
        /// <param name="catalog">The building catalog containing all definitions</param>
        /// <param name="economyService">The economy service for resource checks</param>
        public BuildingDataProvider(int playerId, BuildingCatalogSO catalog, EconomyService economyService)
        {
            this.playerId = playerId;
            this.catalog = catalog;
            this.economyService = economyService;

            if (catalog == null)
            {
                Debug.LogWarning("BuildingDataProvider: No catalog provided, building UI will be empty");
            }

            // Subscribe to EventBus for resource changes
            EventBus.Subscribe<ResourceChangedEvent>(HandleResourceChangedEvent);

            // Initialize affordability cache
            InitializeAffordabilityCache();
        }

        /// <summary>
        /// Initialize the affordability cache for all buildings.
        /// </summary>
        private void InitializeAffordabilityCache()
        {
            if (catalog == null) return;

            foreach (var building in catalog.Buildings)
            {
                if (building != null && building.IsValid)
                {
                    affordabilityCache[building.buildingId] = CanAffordBuilding(building.buildingId);
                }
            }
        }

        /// <summary>
        /// Handle resource changes from EventBus.
        /// </summary>
        private void HandleResourceChangedEvent(ResourceChangedEvent e)
        {
            if (e.PlayerId != playerId) return;
            CheckAffordabilityChanges();
        }

        /// <summary>
        /// Check if any building's affordability has changed.
        /// </summary>
        private void CheckAffordabilityChanges()
        {
            if (catalog == null) return;

            foreach (var building in catalog.Buildings)
            {
                if (building == null || !building.IsValid) continue;

                bool currentlyAffordable = CanAffordBuilding(building.buildingId);
                
                if (affordabilityCache.TryGetValue(building.buildingId, out bool wasAffordable))
                {
                    if (currentlyAffordable != wasAffordable)
                    {
                        affordabilityCache[building.buildingId] = currentlyAffordable;
                        OnAffordabilityChanged?.Invoke(building.buildingId, currentlyAffordable);
                    }
                }
                else
                {
                    affordabilityCache[building.buildingId] = currentlyAffordable;
                }
            }
        }

        public IEnumerable<BuildingInfo> GetAvailableBuildings()
        {
            if (catalog == null) yield break;

            foreach (var building in catalog.GetValidBuildings())
            {
                yield return CreateBuildingInfo(building);
            }
        }

        public IEnumerable<BuildingInfo> GetBuildingsByCategory(string category)
        {
            if (catalog == null) yield break;

            foreach (var building in catalog.GetByCategory(category))
            {
                if (building != null && building.IsValid)
                {
                    yield return CreateBuildingInfo(building);
                }
            }
        }

        public IEnumerable<string> GetCategories()
        {
            if (catalog == null) return Array.Empty<string>();
            return catalog.GetCategories();
        }

        public BuildingInfo? GetBuildingInfo(string buildingId)
        {
            var definition = catalog?.GetById(buildingId);
            if (definition == null || !definition.IsValid)
                return null;

            return CreateBuildingInfo(definition);
        }

        public BuildingDefinitionSO GetBuildingDefinition(string buildingId)
        {
            return catalog?.GetById(buildingId);
        }

        public bool CanAffordBuilding(string buildingId)
        {
            if (economyService == null) return true; // No economy = always affordable

            var definition = catalog?.GetById(buildingId);
            if (definition == null) return false;

            var costs = definition.GetTotalCosts();
            foreach (var kvp in costs)
            {
                if (economyService.GetAvailable(playerId, kvp.Key) < kvp.Value)
                    return false;
            }
            return true;
        }

        public bool IsBuildingUnlocked(string buildingId)
        {
            var definition = catalog?.GetById(buildingId);
            if (definition == null) return false;

            // Check prerequisites
            // For now, we'll assume all buildings are unlocked if they exist
            // This can be expanded to check PlayerBuildingTracker or similar
            return definition.ArePrerequisitesMet(prerequisite => 
            {
                // TODO: Check if player has built the prerequisite building
                // For now, return true (all unlocked)
                return true;
            });
        }

        /// <summary>
        /// Creates a BuildingInfo DTO from a BuildingDefinitionSO.
        /// </summary>
        private BuildingInfo CreateBuildingInfo(BuildingDefinitionSO definition)
        {
            var costs = new List<BuildingCostInfo>();
            var totalCosts = definition.GetTotalCosts();

            foreach (var kvp in totalCosts)
            {
                int available = economyService?.GetAvailable(playerId, kvp.Key) ?? int.MaxValue;
                costs.Add(new BuildingCostInfo
                {
                    ResourceType = kvp.Key,
                    Amount = kvp.Value,
                    CanAfford = available >= kvp.Value
                });
            }

            bool canAfford = true;
            foreach (var cost in costs)
            {
                if (!cost.CanAfford)
                {
                    canAfford = false;
                    break;
                }
            }

            return new BuildingInfo
            {
                Id = definition.buildingId,
                DisplayName = definition.BuildingName,
                Description = definition.description,
                Category = definition.category,
                Icon = definition.icon,
                Costs = costs,
                IsUnlocked = IsBuildingUnlocked(definition.buildingId),
                CanAfford = canAfford
            };
        }

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;

            // Unsubscribe from events
            EventBus.Unsubscribe<ResourceChangedEvent>(HandleResourceChangedEvent);

            affordabilityCache.Clear();
        }
    }
}
