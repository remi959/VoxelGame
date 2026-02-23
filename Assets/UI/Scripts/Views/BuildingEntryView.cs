// ============================================================================
// BuildingEntryView.cs - View component for a single building entry
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
    /// Manages a single building entry in the building menu UI.
    /// 
    /// Responsibilities:
    /// - Holds references to UI elements for one building
    /// - Updates affordability display when resources change
    /// - Handles click events for building selection
    /// - Applies visual styling based on state
    /// 
    /// This is a lightweight view class, NOT a MonoBehaviour.
    /// Created and managed by BuildingMenuController.
    /// </summary>
    public class BuildingEntryView
    {
        private readonly VisualElement root;
        private readonly VisualElement iconContainer;
        private readonly VisualElement icon;
        private readonly Label nameLabel;
        private readonly Label descriptionLabel;
        private readonly VisualElement costsContainer;

        private readonly string buildingId;
        private readonly List<CostElement> costElements = new();
        private bool isAffordable = true;

        /// <summary>
        /// The building ID this entry represents.
        /// </summary>
        public string BuildingId => buildingId;

        /// <summary>
        /// The root visual element for this entry.
        /// </summary>
        public VisualElement Root => root;

        /// <summary>
        /// Event fired when this entry is clicked.
        /// </summary>
        public event Action<string> OnClicked;

        /// <summary>
        /// Represents a single cost display element.
        /// </summary>
        private class CostElement
        {
            public VisualElement Container;
            public Label AmountLabel;
            public EResourceType ResourceType;
        }

        /// <summary>
        /// Creates a new building entry view from a template instance.
        /// </summary>
        /// <param name="templateInstance">Instantiated UXML template</param>
        /// <param name="buildingInfo">The building information to display</param>
        public BuildingEntryView(TemplateContainer templateInstance, BuildingInfo buildingInfo)
        {
            buildingId = buildingInfo.Id;

            // Query elements from template
            // Note: TemplateContainer wraps the actual content, so we query from it directly
            // or from its first child if the direct query fails
            root = templateInstance.Q<VisualElement>("entry-container");
            
            // If root not found directly, the template might be structured differently
            // Try getting the first child element with the class
            if (root == null)
            {
                root = templateInstance.Q<VisualElement>(className: "building-entry");
            }
            
            // As a fallback, use the template container itself or its first child
            if (root == null && templateInstance.childCount > 0)
            {
                root = templateInstance[0];
            }
            
            if (root == null)
            {
                // Last resort - use the template container as the root
                root = templateInstance;
            }

            iconContainer = root.Q<VisualElement>("icon-container");
            icon = root.Q<VisualElement>("icon");
            nameLabel = root.Q<Label>("building-name");
            descriptionLabel = root.Q<Label>("building-description");
            costsContainer = root.Q<VisualElement>("costs-container");

            // Validate required elements
            if (nameLabel == null || costsContainer == null)
            {
                Debug.LogError($"BuildingEntryView: Missing required elements in template for {buildingId}. " +
                               $"nameLabel={nameLabel != null}, costsContainer={costsContainer != null}, " +
                               $"root has {root.childCount} children");
                return;
            }

            // Set up basic info
            nameLabel.text = buildingInfo.DisplayName;
            
            if (descriptionLabel != null)
            {
                descriptionLabel.text = string.IsNullOrEmpty(buildingInfo.Description) 
                    ? "" 
                    : buildingInfo.Description;
            }

            // Set up icon
            SetupIcon(buildingInfo.Icon);

            // Create cost elements
            CreateCostElements(buildingInfo.Costs);

            // Set initial affordability state
            UpdateAffordability(buildingInfo.CanAfford, buildingInfo.Costs);

            // Register click handler
            root.RegisterCallback<ClickEvent>(HandleClick);
        }

        /// <summary>
        /// Sets up the icon from a sprite.
        /// </summary>
        private void SetupIcon(Sprite sprite)
        {
            if (sprite == null || icon == null)
            {
                root.AddToClassList("building-entry--no-icon");
                return;
            }

            icon.style.backgroundImage = new StyleBackground(sprite);
        }

        /// <summary>
        /// Creates cost display elements for each resource cost.
        /// </summary>
        private void CreateCostElements(List<BuildingCostInfo> costs)
        {
            if (costsContainer == null || costs == null) return;

            costsContainer.Clear();
            costElements.Clear();

            foreach (var cost in costs)
            {
                var costContainer = new VisualElement();
                costContainer.AddToClassList("building-cost");
                costContainer.AddToClassList($"building-cost--{cost.ResourceType.ToString().ToLower()}");

                // Resource icon placeholder (can be enhanced with actual icons)
                var costIcon = new VisualElement();
                costIcon.AddToClassList("building-cost__icon");
                costContainer.Add(costIcon);

                // Amount label
                var amountLabel = new Label(cost.Amount.ToString());
                amountLabel.AddToClassList("building-cost__amount");
                costContainer.Add(amountLabel);

                // Add tooltip with resource name
                costContainer.tooltip = $"{cost.ResourceType}: {cost.Amount}";

                costsContainer.Add(costContainer);

                costElements.Add(new CostElement
                {
                    Container = costContainer,
                    AmountLabel = amountLabel,
                    ResourceType = cost.ResourceType
                });
            }
        }

        /// <summary>
        /// Updates the affordability display for this building.
        /// </summary>
        /// <param name="canAfford">Whether the player can afford this building</param>
        /// <param name="costs">Updated cost information with affordability per resource</param>
        public void UpdateAffordability(bool canAfford, List<BuildingCostInfo> costs = null)
        {
            // Guard against uninitialized state
            if (root == null) return;
            
            isAffordable = canAfford;

            // Update overall affordability class
            if (canAfford)
            {
                root.RemoveFromClassList("building-entry--unaffordable");
            }
            else
            {
                root.AddToClassList("building-entry--unaffordable");
            }

            // Update individual cost affordability if provided
            if (costs != null)
            {
                for (int i = 0; i < costs.Count && i < costElements.Count; i++)
                {
                    var cost = costs[i];
                    var element = costElements[i];

                    element.Container.RemoveFromClassList("building-cost--affordable");
                    element.Container.RemoveFromClassList("building-cost--unaffordable");
                    element.Container.AddToClassList(cost.CanAfford ? "building-cost--affordable" : "building-cost--unaffordable");
                }
            }
        }

        /// <summary>
        /// Sets the selected state of this entry.
        /// </summary>
        public void SetSelected(bool selected)
        {
            if (selected)
            {
                root.AddToClassList("building-entry--selected");
            }
            else
            {
                root.RemoveFromClassList("building-entry--selected");
            }
        }

        /// <summary>
        /// Handles click events on the entry.
        /// </summary>
        private void HandleClick(ClickEvent evt)
        {
            // Only respond to left clicks
            if (evt.button != 0) return;

            OnClicked?.Invoke(buildingId);
            evt.StopPropagation();
        }

        /// <summary>
        /// Cleans up event handlers.
        /// </summary>
        public void Dispose()
        {
            root?.UnregisterCallback<ClickEvent>(HandleClick);
            OnClicked = null;
        }
    }
}
