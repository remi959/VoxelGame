// ============================================================================
// IResourceDataProvider.cs - Interface for resource data access
// ============================================================================
using System;
using System.Collections.Generic;
using Assets.Scripts.Shared.Enums;

namespace Assets.UI.Scripts.Core
{
    /// <summary>
    /// Provides resource data to the UI layer without coupling to EconomyService.
    /// 
    /// Design notes:
    /// - Abstracts the source of resource data (could be EconomyService, mock data, etc.)
    /// - UI only depends on this interface, not concrete implementations
    /// - Enables testing UI without game systems
    /// - Follows Dependency Inversion Principle
    /// 
    /// SOURCE OF TRUTH:
    /// - Resource amounts come from StoragePoints (aggregated by ResourceStorageAggregator)
    /// - GetResourceAmount returns available (total minus reservations)
    /// - GetResourceTotal returns total across all storage points
    /// </summary>
    public interface IResourceDataProvider
    {
        /// <summary>
        /// Gets all resource types that should be displayed in the UI.
        /// </summary>
        IEnumerable<EResourceType> GetAllResourceTypes();

        /// <summary>
        /// Gets the current available amount of a specific resource for a player.
        /// This is total minus any reservations (planned spending).
        /// </summary>
        int GetResourceAmount(int playerId, EResourceType resourceType);

        /// <summary>
        /// Gets the display name for a resource type.
        /// </summary>
        string GetResourceDisplayName(EResourceType resourceType);

        /// <summary>
        /// Gets the icon path for a resource type (can return null if no icon).
        /// </summary>
        string GetResourceIconPath(EResourceType resourceType);

        /// <summary>
        /// Event fired when any resource value changes.
        /// Parameters: playerId, resourceType, oldValue, newValue
        /// </summary>
        event Action<int, EResourceType, int, int> OnResourceValueChanged;
    }
}
