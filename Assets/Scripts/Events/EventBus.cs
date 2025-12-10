using System;
using System.Collections.Generic;

namespace Assets.Scripts.Events
{
    public static class EventBus
    {
        private static readonly Dictionary<Type, Delegate> events = new();

        public static void Subscribe<T>(Action<T> handler) where T : struct
        {
            var type = typeof(T);
            if (events.TryGetValue(type, out var existing))
                events[type] = Delegate.Combine(existing, handler);
            else
                events[type] = handler;
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            var type = typeof(T);
            if (events.TryGetValue(type, out var existing))
            {
                var result = Delegate.Remove(existing, handler);
                if (result == null)
                    events.Remove(type);
                else
                    events[type] = result;
            }
        }

        public static void Publish<T>(T eventData) where T : struct
        {
            if (events.TryGetValue(typeof(T), out var handler))
                ((Action<T>)handler)?.Invoke(eventData);
        }

        public static void Clear() => events.Clear();
    }
}