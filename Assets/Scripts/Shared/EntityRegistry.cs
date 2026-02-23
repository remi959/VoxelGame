using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Shared
{
    /// <summary>
    /// Generic registry for tracking game entities.
    /// Replaces scattered static List patterns across the codebase.
    /// Uses HashSet for O(1) add/remove and prevents duplicates.
    /// </summary>
    public static class EntityRegistry<T> where T : Component
    {
        private static readonly HashSet<T> entities = new();
        private static readonly List<T> sharedBuffer = new();

        /// <summary>
        /// All registered entities of this type.
        /// </summary>
        public static IReadOnlyCollection<T> All => entities;

        /// <summary>
        /// Number of registered entities.
        /// </summary>
        public static int Count => entities.Count;

        /// <summary>
        /// Register an entity. Safe to call multiple times.
        /// </summary>
        public static void Register(T entity)
        {
            if (entity != null)
                entities.Add(entity);
        }

        /// <summary>
        /// Unregister an entity. Safe to call if not registered.
        /// </summary>
        public static void Unregister(T entity)
        {
            if (entity != null)
                entities.Remove(entity);
        }

        /// <summary>
        /// Check if an entity is registered.
        /// </summary>
        public static bool IsRegistered(T entity)
        {
            return entity != null && entities.Contains(entity);
        }

        /// <summary>
        /// Find the nearest entity to a position.
        /// </summary>
        public static T FindNearest(Vector3 position, Func<T, bool> filter = null)
        {
            T nearest = null;
            float nearestDistanceSqr = float.MaxValue;

            foreach (var entity in entities)
            {
                if (entity == null) continue;
                if (filter != null && !filter(entity)) continue;

                float distanceSqr = (entity.transform.position - position).sqrMagnitude;
                if (distanceSqr < nearestDistanceSqr)
                {
                    nearestDistanceSqr = distanceSqr;
                    nearest = entity;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Find the nearest entity to a position, also returning the distance.
        /// </summary>
        public static T FindNearest(Vector3 position, out float distance, Func<T, bool> filter = null)
        {
            T nearest = null;
            float nearestDistanceSqr = float.MaxValue;

            foreach (var entity in entities)
            {
                if (entity == null) continue;
                if (filter != null && !filter(entity)) continue;

                float distanceSqr = (entity.transform.position - position).sqrMagnitude;
                if (distanceSqr < nearestDistanceSqr)
                {
                    nearestDistanceSqr = distanceSqr;
                    nearest = entity;
                }
            }

            distance = nearest != null ? Mathf.Sqrt(nearestDistanceSqr) : float.MaxValue;
            return nearest;
        }

        /// <summary>
        /// Find all entities within a given range.
        /// </summary>
        /// <remarks>
        /// WARNING: Returns a shared static buffer. Iterate the results immediately after calling.
        /// Do NOT store the returned reference — it will be overwritten on the next call.
        /// </remarks>
        public static List<T> FindAllInRange(Vector3 position, float range, Func<T, bool> filter = null)
        {
            sharedBuffer.Clear();
            float rangeSqr = range * range;

            foreach (var entity in entities)
            {
                if (entity == null) continue;
                if (filter != null && !filter(entity)) continue;

                float distanceSqr = (entity.transform.position - position).sqrMagnitude;
                if (distanceSqr <= rangeSqr)
                    sharedBuffer.Add(entity);
            }

            return sharedBuffer;
        }

        /// <summary>
        /// Find any entity matching the filter.
        /// </summary>
        public static T FindAny(Func<T, bool> filter = null)
        {
            foreach (var entity in entities)
            {
                if (entity == null) continue;
                if (filter == null || filter(entity))
                    return entity;
            }
            return null;
        }

        /// <summary>
        /// Clear all registered entities. Use with caution.
        /// </summary>
        public static void Clear()
        {
            entities.Clear();
        }

        /// <summary>
        /// Remove any null references (cleanup after scene unload).
        /// </summary>
        public static void RemoveNulls()
        {
            entities.RemoveWhere(e => e == null);
        }
    }
}
