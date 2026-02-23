// ============================================================================
// ResourceUIConfigSO.cs - Configuration for resource UI display
// ============================================================================
using System;
using Assets.Scripts.Shared.Enums;
using UnityEngine;

namespace Assets.UI.Scripts.Data
{
    /// <summary>
    /// ScriptableObject that configures how resources are displayed in the UI.
    /// 
    /// Benefits:
    /// - Artists/designers can configure without code changes
    /// - Icon paths and display names are data, not code
    /// - Supports localization by swapping config assets
    /// - New resources can be configured without code changes
    /// </summary>
    [CreateAssetMenu(fileName = "ResourceUIConfig", menuName = "VoxelGame/UI/Resource UI Config")]
    public class ResourceUIConfigSO : ScriptableObject
    {
        [Header("Resource Display Settings")]
        [Tooltip("Configuration for each resource type")]
        [SerializeField] private ResourceDisplayConfig[] resourceConfigs;

        /// <summary>
        /// Gets the display configuration for a resource type.
        /// Returns null if not configured.
        /// </summary>
        public ResourceDisplayConfig GetConfig(EResourceType resourceType)
        {
            if (resourceConfigs == null) return null;

            foreach (var config in resourceConfigs)
            {
                if (config.ResourceType == resourceType)
                {
                    return config;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets display name for a resource type.
        /// Falls back to enum name if not configured.
        /// </summary>
        public string GetDisplayName(EResourceType resourceType)
        {
            var config = GetConfig(resourceType);
            if (config != null && !string.IsNullOrEmpty(config.DisplayName))
            {
                return config.DisplayName;
            }
            return resourceType.ToString();
        }

        /// <summary>
        /// Gets icon for a resource type.
        /// Returns null if not configured.
        /// </summary>
        public Sprite GetIcon(EResourceType resourceType)
        {
            return GetConfig(resourceType)?.Icon;
        }

        /// <summary>
        /// Gets custom color for a resource type.
        /// Returns null/white if not configured.
        /// </summary>
        public Color GetColor(EResourceType resourceType)
        {
            var config = GetConfig(resourceType);
            return config?.CustomColor ?? Color.white;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor utility to populate default configs for all resource types.
        /// </summary>
        [ContextMenu("Populate Default Configs")]
        private void PopulateDefaultConfigs()
        {
            var resourceTypes = (EResourceType[])Enum.GetValues(typeof(EResourceType));
            resourceConfigs = new ResourceDisplayConfig[resourceTypes.Length];

            for (int i = 0; i < resourceTypes.Length; i++)
            {
                resourceConfigs[i] = new ResourceDisplayConfig
                {
                    ResourceType = resourceTypes[i],
                    DisplayName = resourceTypes[i].ToString(),
                    CustomColor = GetDefaultColor(resourceTypes[i])
                };
            }

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"ResourceUIConfigSO: Populated {resourceConfigs.Length} default configs");
        }

        private Color GetDefaultColor(EResourceType type)
        {
            return type switch
            {
                EResourceType.Wood => new Color(0.8f, 0.52f, 0.25f),  // Brown
                EResourceType.Stone => new Color(0.66f, 0.66f, 0.66f), // Gray
                EResourceType.Food => new Color(0.56f, 0.93f, 0.56f),  // Light green
                EResourceType.Gold => new Color(1f, 0.84f, 0f),        // Gold
                _ => Color.white
            };
        }
#endif
    }

    /// <summary>
    /// Configuration for a single resource type's UI display.
    /// </summary>
    [Serializable]
    public class ResourceDisplayConfig
    {
        [Tooltip("The resource type this config applies to")]
        public EResourceType ResourceType;

        [Tooltip("Display name shown in UI (can be localized)")]
        public string DisplayName;

        [Tooltip("Icon shown in UI (optional)")]
        public Sprite Icon;

        [Tooltip("Custom color for the counter text")]
        public Color CustomColor = Color.white;

        [Tooltip("Whether to show this resource in the main bar")]
        public bool ShowInMainBar = true;

        [Tooltip("Display order (lower = earlier)")]
        public int DisplayOrder = 0;
    }
}
