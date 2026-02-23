// ============================================================================
// IHoverTarget.cs - Interface for objects that show contextual UI on hover
// ============================================================================
using System.Collections.Generic;

namespace Assets.Scripts.Shared.Interfaces
{
    /// <summary>
    /// Types of hover targets for UI styling and behavior.
    /// </summary>
    public enum HoverTargetType
    {
        /// <summary>A fully constructed building.</summary>
        CompletedBuilding,

        /// <summary>A building currently under construction (blueprint).</summary>
        BuildingUnderConstruction,

        /// <summary>A resource storage point.</summary>
        StoragePoint
    }

    /// <summary>
    /// Represents an action available on a hover target.
    /// </summary>
    public struct HoverAction
    {
        /// <summary>
        /// Unique identifier for this action (e.g., "destroy", "cancel", "toggle_enabled").
        /// </summary>
        public string Id;

        /// <summary>
        /// Display label for the action button.
        /// </summary>
        public string Label;

        /// <summary>
        /// Optional tooltip with additional information.
        /// </summary>
        public string Tooltip;

        /// <summary>
        /// Whether this action is destructive (shown in red).
        /// </summary>
        public bool IsDestructive;

        /// <summary>
        /// Whether this action is currently available.
        /// </summary>
        public bool IsEnabled;

        /// <summary>
        /// Create a new hover action.
        /// </summary>
        public HoverAction(string id, string label, bool isDestructive = false, bool isEnabled = true, string tooltip = null)
        {
            Id = id;
            Label = label;
            IsDestructive = isDestructive;
            IsEnabled = isEnabled;
            Tooltip = tooltip;
        }
    }

    /// <summary>
    /// Interface for objects that can display contextual UI when hovered.
    /// 
    /// Design Notes:
    /// - World objects implement this to expose their available actions
    /// - UI queries this interface, doesn't know about specific types
    /// - Actions are executed via ID, decoupling UI from implementation
    /// </summary>
    public interface IHoverTarget
    {
        /// <summary>
        /// Display name shown in the hover UI header.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Type of target for UI styling.
        /// </summary>
        HoverTargetType TargetType { get; }

        /// <summary>
        /// Get all actions currently available for this target.
        /// List may change based on object state.
        /// </summary>
        IReadOnlyList<HoverAction> GetAvailableActions();

        /// <summary>
        /// Execute an action by its ID.
        /// </summary>
        /// <param name="actionId">The action ID from HoverAction.Id</param>
        /// <returns>True if action was executed, false if invalid or unavailable</returns>
        bool ExecuteAction(string actionId);
    }
}
