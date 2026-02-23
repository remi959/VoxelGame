// ============================================================================
// ResourceEntryView.cs - View component for a single resource entry
// ============================================================================
using Assets.Scripts.Shared.Enums;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.UI.Scripts.Views
{
    /// <summary>
    /// Manages a single resource entry in the UI.
    /// 
    /// Responsibilities:
    /// - Holds references to UI elements for one resource
    /// - Updates counter display when value changes
    /// - Applies visual styling based on resource type
    /// - Handles optional icon display
    /// 
    /// This is a lightweight view class, NOT a MonoBehaviour.
    /// Created and managed by ResourceUIController.
    /// </summary>
    public class ResourceEntryView
    {
        private readonly VisualElement root;
        private readonly VisualElement iconContainer;
        private readonly VisualElement icon;
        private readonly Label nameLabel;
        private readonly Label counterLabel;
        
        private readonly EResourceType resourceType;
        private int currentValue;

        /// <summary>
        /// The resource type this entry displays.
        /// </summary>
        public EResourceType ResourceType => resourceType;

        /// <summary>
        /// The root visual element for this entry.
        /// </summary>
        public VisualElement Root => root;

        /// <summary>
        /// Creates a new resource entry view from a template instance.
        /// </summary>
        /// <param name="templateInstance">Instantiated UXML template</param>
        /// <param name="resourceType">The resource type to display</param>
        /// <param name="displayName">Human-readable name</param>
        /// <param name="iconPath">Path to icon texture (can be null)</param>
        /// <param name="iconSprite">Direct sprite reference (alternative to path)</param>
        public ResourceEntryView(
            TemplateContainer templateInstance,
            EResourceType resourceType,
            string displayName,
            string iconPath,
            Sprite iconSprite = null)
        {
            this.resourceType = resourceType;

            // Query elements from template
            root = templateInstance.Q<VisualElement>("entry-container");
            iconContainer = templateInstance.Q<VisualElement>("icon-container");
            icon = templateInstance.Q<VisualElement>("icon");
            nameLabel = templateInstance.Q<Label>("resource-name");
            counterLabel = templateInstance.Q<Label>("resource-counter");

            // Validate required elements exist
            if (root == null || nameLabel == null || counterLabel == null)
            {
                Debug.LogError($"ResourceEntryView: Missing required elements in template for {resourceType}");
                return;
            }

            // Set display name
            nameLabel.text = displayName;

            // Apply resource type modifier class for styling
            string typeClass = $"resource-entry--{resourceType.ToString().ToLower()}";
            root.AddToClassList(typeClass);

            // Configure icon (prefer direct sprite over path)
            if (iconSprite != null)
            {
                SetupIconFromSprite(iconSprite);
            }
            else
            {
                SetupIcon(iconPath);
            }
        }

        /// <summary>
        /// Sets up the icon from a direct Sprite reference.
        /// </summary>
        private void SetupIconFromSprite(Sprite sprite)
        {
            if (sprite == null || icon == null)
            {
                root.AddToClassList("resource-entry--no-icon");
                return;
            }

            icon.style.backgroundImage = new StyleBackground(sprite);
        }

        /// <summary>
        /// Sets up the icon display. Hides container if no icon.
        /// </summary>
        private void SetupIcon(string iconPath)
        {
            if (string.IsNullOrEmpty(iconPath))
            {
                // No icon - add modifier class to hide icon container
                root.AddToClassList("resource-entry--no-icon");
                return;
            }

            // Check for sprite marker (from config-based provider)
            if (iconPath.StartsWith("sprite:"))
            {
                // Icon will be set separately via SetupIconFromSprite
                root.AddToClassList("resource-entry--no-icon");
                return;
            }

            // Load icon texture from Resources
            var texture = Resources.Load<Texture2D>(iconPath);
            if (texture != null && icon != null)
            {
                icon.style.backgroundImage = new StyleBackground(texture);
            }
            else
            {
                // Icon path provided but failed to load - hide container
                root.AddToClassList("resource-entry--no-icon");
                
                if (texture == null)
                {
                    Debug.LogWarning($"ResourceEntryView: Failed to load icon at '{iconPath}' for {resourceType}");
                }
            }
        }

        /// <summary>
        /// Updates the displayed value.
        /// </summary>
        /// <param name="newValue">New resource amount</param>
        public void SetValue(int newValue)
        {
            int previousValue = currentValue;
            currentValue = newValue;

            if (counterLabel != null)
            {
                counterLabel.text = FormatValue(newValue);
            }

            // Apply visual feedback for value changes
            ApplyValueChangeFeedback(previousValue, newValue);
        }

        /// <summary>
        /// Formats the value for display.
        /// Can be extended for abbreviations (1.2K, 1.5M, etc.)
        /// </summary>
        private string FormatValue(int value)
        {
            // Simple format for now - can add abbreviations later
            if (value >= 1000000)
            {
                return $"{value / 1000000f:F1}M";
            }
            if (value >= 10000)
            {
                return $"{value / 1000f:F1}K";
            }
            return value.ToString();
        }

        /// <summary>
        /// Applies visual feedback when value changes.
        /// </summary>
        private void ApplyValueChangeFeedback(int oldValue, int newValue)
        {
            // Remove previous feedback classes
            root.RemoveFromClassList("resource-entry--value-increased");
            root.RemoveFromClassList("resource-entry--value-decreased");

            // Apply new feedback class
            if (newValue > oldValue)
            {
                root.AddToClassList("resource-entry--value-increased");
            }
            else if (newValue < oldValue)
            {
                root.AddToClassList("resource-entry--value-decreased");
            }

            // Note: To implement fade-out, you would schedule removal of these classes
            // using VisualElement.schedule.Execute() after a delay
        }

        /// <summary>
        /// Cleans up the view. Call when removing from UI.
        /// </summary>
        public void Dispose()
        {
            // Clear references (helps GC)
            if (root != null)
            {
                root.RemoveFromHierarchy();
            }
        }
    }
}
