// ============================================================================
// ResourceDataProvider.cs - Bridges EconomyService to UI layer
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Core.Events;
using Assets.Scripts.Economy;
using Assets.Scripts.Economy.Events;
using Assets.Scripts.Events;
using Assets.Scripts.Shared.Enums;
using Assets.UI.Scripts.Data;
using UnityEngine;

namespace Assets.UI.Scripts.Core
{
    /// <summary>
    /// Concrete implementation of IResourceDataProvider that wraps EconomyService.
    /// 
    /// Responsibilities:
    /// - Translates EconomyService data to UI-friendly format
    /// - Subscribes to EventBus for resource changes
    /// - Provides display names and icon paths for resources
    /// - Acts as the single point of connection between game systems and UI
    /// 
    /// This class exists to:
    /// - Decouple UI from EconomyService implementation details
    /// - Allow UI to be tested independently with mock providers
    /// - Centralize resource display logic (names, icons)
    /// </summary>
    public class ResourceDataProvider : IResourceDataProvider, IDisposable
    {
        private readonly int playerId;
        private readonly EconomyService economyService;
        private readonly ResourceUIConfigSO config;
        private bool isDisposed;

        // Fallback icon paths (used when no config is provided)
        private static readonly Dictionary<EResourceType, string> FallbackIconPaths = new()
        {
            { EResourceType.Wood, "Icons/resource_wood" },
            { EResourceType.Stone, "Icons/resource_stone" },
            { EResourceType.Food, "Icons/resource_food" },
            { EResourceType.Gold, "Icons/resource_gold" }
        };

        // Fallback display names (used when no config is provided)
        private static readonly Dictionary<EResourceType, string> FallbackDisplayNames = new()
        {
            { EResourceType.Wood, "Wood" },
            { EResourceType.Stone, "Stone" },
            { EResourceType.Food, "Food" },
            { EResourceType.Gold, "Gold" }
        };

        public event Action<int, EResourceType, int, int> OnResourceValueChanged;

        /// <summary>
        /// Creates a resource data provider with optional configuration.
        /// </summary>
        /// <param name="playerId">The player whose resources to track</param>
        /// <param name="economyService">The economy service instance</param>
        /// <param name="config">Optional UI configuration (uses fallbacks if null)</param>
        public ResourceDataProvider(int playerId, EconomyService economyService, ResourceUIConfigSO config = null)
        {
            this.playerId = playerId;
            this.economyService = economyService ?? throw new ArgumentNullException(nameof(economyService));
            this.config = config;

            // Subscribe to EventBus for resource changes
            EventBus.Subscribe<ResourceChangedEvent>(HandleResourceChanged);
        }

        /// <summary>
        /// Returns all resource types defined in the enum.
        /// Optionally filtered/sorted by config.
        /// </summary>
        public IEnumerable<EResourceType> GetAllResourceTypes()
        {
            var allTypes = (EResourceType[])Enum.GetValues(typeof(EResourceType));

            // If config exists, filter to only showable resources and sort by display order
            if (config != null)
            {
                return allTypes
                    .Select(t => new { Type = t, Config = config.GetConfig(t) })
                    .Where(x => x.Config == null || x.Config.ShowInMainBar)
                    .OrderBy(x => x.Config?.DisplayOrder ?? 999)
                    .Select(x => x.Type);
            }

            return allTypes;
        }

        /// <summary>
        /// Gets current available resource amount from EconomyService.
        /// This is total minus reservations.
        /// </summary>
        public int GetResourceAmount(int playerId, EResourceType resourceType)
        {
            if (economyService == null) return 0;
            return economyService.GetAvailable(playerId, resourceType);
        }

        /// <summary>
        /// Gets total resource amount (including reserved).
        /// Use this when you want to show the total stored, not just available.
        /// </summary>
        public int GetResourceTotal(int playerId, EResourceType resourceType)
        {
            if (economyService == null) return 0;
            return economyService.GetTotal(playerId, resourceType);
        }

        /// <summary>
        /// Gets display name for resource type.
        /// Uses config if available, falls back to dictionary, then enum name.
        /// </summary>
        public string GetResourceDisplayName(EResourceType resourceType)
        {
            // Try config first
            if (config != null)
            {
                return config.GetDisplayName(resourceType);
            }

            // Fallback to dictionary
            return FallbackDisplayNames.TryGetValue(resourceType, out var name) 
                ? name 
                : resourceType.ToString();
        }

        /// <summary>
        /// Gets icon path for resource type.
        /// Returns null if no icon is configured.
        /// </summary>
        public string GetResourceIconPath(EResourceType resourceType)
        {
            // Try config first (note: config uses Sprite, this returns path)
            // For Sprite-based approach, see GetResourceIcon() if needed
            if (config != null)
            {
                var sprite = config.GetIcon(resourceType);
                if (sprite != null)
                {
                    // Config has a sprite, but interface expects path
                    // Return a marker that indicates sprite should be used
                    return $"sprite:{resourceType}";
                }
                return null; // Config exists but no icon
            }

            // Fallback to path dictionary
            return FallbackIconPaths.TryGetValue(resourceType, out var path) ? path : null;
        }

        /// <summary>
        /// Gets the Sprite icon directly (when using config).
        /// </summary>
        public Sprite GetResourceIcon(EResourceType resourceType)
        {
            return config?.GetIcon(resourceType);
        }

        /// <summary>
        /// Gets custom color for resource type from config.
        /// Returns white if not configured.
        /// </summary>
        public Color GetResourceColor(EResourceType resourceType)
        {
            return config?.GetColor(resourceType) ?? Color.white;
        }

        /// <summary>
        /// Handles resource change events from EventBus.
        /// Filters to relevant player and forwards to UI.
        /// </summary>
        private void HandleResourceChanged(ResourceChangedEvent evt)
        {
            // Only forward events for our player
            if (evt.PlayerId != playerId) return;

            OnResourceValueChanged?.Invoke(
                evt.PlayerId,
                evt.ResourceType,
                evt.OldAmount,
                evt.NewAmount
            );
        }

        /// <summary>
        /// Cleanup EventBus subscription.
        /// </summary>
        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;

            EventBus.Unsubscribe<ResourceChangedEvent>(HandleResourceChanged);
        }
    }
}
