// ============================================================================
// IBuildingDataProvider.cs - Interface for building data access
// ============================================================================
using System;
using System.Collections.Generic;
using Assets.Scripts.Buildings;
using Assets.Scripts.Shared.Enums;

namespace Assets.UI.Scripts.Core
{
    /// <summary>
    /// Represents a building's resource cost for UI display.
    /// </summary>
    public struct BuildingCostInfo
    {
        public EResourceType ResourceType;
        public int Amount;
        public bool CanAfford;
    }

    /// <summary>
    /// Represents building information for UI display.
    /// This is a data transfer object - UI-safe representation of building data.
    /// </summary>
    public struct BuildingInfo
    {
        public string Id;
        public string DisplayName;
        public string Description;
        public string Category;
        public UnityEngine.Sprite Icon;
        public List<BuildingCostInfo> Costs;
        public bool IsUnlocked;
        public bool CanAfford;
    }

    /// <summary>
    /// Provides building data to the UI layer without coupling to building system internals.
    /// 
    /// Design Notes:
    /// - Abstracts the source of building data (BuildingCatalogSO, network, etc.)
    /// - UI only depends on this interface, not concrete implementations
    /// - Enables testing UI without game systems
    /// - Follows Dependency Inversion Principle
    /// - Mirrors the pattern established by IResourceDataProvider
    /// </summary>
    public interface IBuildingDataProvider : IDisposable
    {
        /// <summary>
        /// Gets all available buildings that should be displayed in the UI.
        /// Buildings are returned in display order.
        /// </summary>
        IEnumerable<BuildingInfo> GetAvailableBuildings();

        /// <summary>
        /// Gets all available buildings in a specific category.
        /// </summary>
        /// <param name="category">The category to filter by</param>
        IEnumerable<BuildingInfo> GetBuildingsByCategory(string category);

        /// <summary>
        /// Gets all unique building categories.
        /// </summary>
        IEnumerable<string> GetCategories();

        /// <summary>
        /// Gets detailed information about a specific building.
        /// </summary>
        /// <param name="buildingId">The unique building ID</param>
        /// <returns>Building info, or null if not found</returns>
        BuildingInfo? GetBuildingInfo(string buildingId);

        /// <summary>
        /// Gets the BuildingDefinitionSO for a building ID.
        /// Used when the UI needs to pass the definition to other systems (e.g., placement).
        /// </summary>
        /// <param name="buildingId">The unique building ID</param>
        /// <returns>The building definition, or null if not found</returns>
        BuildingDefinitionSO GetBuildingDefinition(string buildingId);

        /// <summary>
        /// Checks if the player can currently afford a specific building.
        /// </summary>
        /// <param name="buildingId">The unique building ID</param>
        bool CanAffordBuilding(string buildingId);

        /// <summary>
        /// Checks if a building is unlocked (prerequisites met).
        /// </summary>
        /// <param name="buildingId">The unique building ID</param>
        bool IsBuildingUnlocked(string buildingId);

        /// <summary>
        /// Event fired when a building becomes unlocked.
        /// Parameter: buildingId
        /// </summary>
        event Action<string> OnBuildingUnlocked;

        /// <summary>
        /// Event fired when affordability changes for any building.
        /// Parameters: buildingId, canAfford
        /// </summary>
        event Action<string, bool> OnAffordabilityChanged;

        /// <summary>
        /// Event fired when the building catalog changes (buildings added/removed).
        /// </summary>
        event Action OnCatalogChanged;
    }
}
