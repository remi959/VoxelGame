// ============================================================================
// ContextMenuEvents.cs - Events for the right-click context menu system
// ============================================================================
using Assets.Scripts.Shared.Interfaces;
using UnityEngine;

namespace Assets.Scripts.Core.Events
{
    /// <summary>
    /// Event fired when a context menu is requested via right-click.
    /// </summary>
    public struct ContextMenuRequestedEvent
    {
        /// <summary>
        /// The target that was right-clicked.
        /// </summary>
        public IHoverTarget Target;

        /// <summary>
        /// The GameObject of the target.
        /// </summary>
        public GameObject TargetGameObject;

        /// <summary>
        /// World position where the click occurred.
        /// </summary>
        public Vector3 HitPosition;

        /// <summary>
        /// Screen position where the click occurred (for UI positioning).
        /// </summary>
        public Vector2 ScreenPosition;
    }

    /// <summary>
    /// Event fired when the context menu is closed.
    /// </summary>
    public struct ContextMenuClosedEvent
    {
    }

    // Legacy events kept for compatibility - can be removed if unused
    /// <summary>
    /// Event fired when the hover target changes.
    /// </summary>
    public struct HoverTargetChangedEvent
    {
        public IHoverTarget Target;
        public GameObject TargetGameObject;
        public Vector3 HitPosition;
    }

    /// <summary>
    /// Event fired when hover detection is enabled or disabled.
    /// </summary>
    public struct HoverDetectionStateChangedEvent
    {
        public bool IsEnabled;
    }
}
