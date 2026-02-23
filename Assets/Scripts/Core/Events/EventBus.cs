using System;
using System.Collections.Generic;

namespace Assets.Scripts.Core.Events
{
    /// <summary>
    /// Zero-allocation event bus using the generic static class pattern.
    /// 
    /// How it works:
    /// - Each event type T gets its own static class (EventHolder&lt;T&gt;)
    /// - No dictionary lookups, no typeof() allocations, no delegate casting
    /// - C# compiler generates separate static storage for each type parameter
    /// 
    /// Performance: O(1) publish with zero GC allocations.
    /// </summary>
    public static class EventBus
    {
        /// <summary>
        /// Static generic class - C# creates a unique class for each type T.
        /// EventHolder&lt;MoveEvent&gt; and EventHolder&lt;DamageEvent&gt; are separate classes.
        /// </summary>
        private static class EventHolder<T> where T : struct
        {
            /// <summary>
            /// Directly typed delegate - no boxing, no casting needed.
            /// </summary>
            public static Action<T> Handlers;
        }

        /// <summary>
        /// Track registered types for Clear() functionality.
        /// This is the only allocation, and it's rare (once per event type).
        /// </summary>
        private static readonly List<Action> clearActions = new();

        /// <summary>
        /// Subscribe to an event type. Handler will be called when Publish is invoked.
        /// </summary>
        public static void Subscribe<T>(Action<T> handler) where T : struct
        {
            // First subscriber for this type? Register clear action
            if (EventHolder<T>.Handlers == null)
            {
                clearActions.Add(() => EventHolder<T>.Handlers = null);
            }

            // Direct delegate combine - no boxing
            EventHolder<T>.Handlers += handler;
        }

        /// <summary>
        /// Unsubscribe from an event type.
        /// </summary>
        public static void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            EventHolder<T>.Handlers -= handler;
        }

        /// <summary>
        /// Publish an event to all subscribers. Zero allocations.
        /// </summary>
        public static void Publish<T>(T eventData) where T : struct
        {
            // Direct invocation - no dictionary lookup, no typeof(), no cast
            EventHolder<T>.Handlers?.Invoke(eventData);
        }

        /// <summary>
        /// Clear all event subscriptions. Call on scene unload to prevent memory leaks.
        /// </summary>
        public static void Clear()
        {
            foreach (var clearAction in clearActions)
            {
                clearAction?.Invoke();
            }
            clearActions.Clear();
        }

        /// <summary>
        /// Check if an event type has any subscribers (useful for debugging).
        /// </summary>
        public static bool HasSubscribers<T>() where T : struct
        {
            return EventHolder<T>.Handlers != null;
        }

        /// <summary>
        /// Get subscriber count for an event type (useful for debugging).
        /// Note: This allocates due to GetInvocationList(), use only for debugging.
        /// </summary>
        public static int GetSubscriberCount<T>() where T : struct
        {
            return EventHolder<T>.Handlers?.GetInvocationList().Length ?? 0;
        }
    }
}