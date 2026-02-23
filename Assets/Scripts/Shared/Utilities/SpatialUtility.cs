using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Shared.Utilities
{
    /// <summary>
    /// Utility class for common spatial operations like finding nearest objects.
    /// </summary>
    public static class SpatialUtility
    {
        #region Find Nearest

        /// <summary>
        /// Find the nearest component from a collection.
        /// </summary>
        /// <typeparam name="T">Component type</typeparam>
        /// <param name="position">Reference position</param>
        /// <param name="candidates">Collection to search</param>
        /// <param name="filter">Optional filter predicate</param>
        /// <returns>Nearest matching component, or null if none found</returns>
        public static T FindNearest<T>(Vector3 position, IEnumerable<T> candidates, Func<T, bool> filter = null)
            where T : Component
        {
            T nearest = null;
            float nearestDistanceSqr = float.MaxValue;

            foreach (var candidate in candidates)
            {
                if (candidate == null) continue;
                if (filter != null && !filter(candidate)) continue;

                // Use sqrMagnitude to avoid sqrt calculation
                float distanceSqr = (candidate.transform.position - position).sqrMagnitude;
                if (distanceSqr < nearestDistanceSqr)
                {
                    nearestDistanceSqr = distanceSqr;
                    nearest = candidate;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Find the nearest component from a collection, also returning the distance.
        /// </summary>
        public static T FindNearest<T>(Vector3 position, IEnumerable<T> candidates, out float distance, Func<T, bool> filter = null)
            where T : Component
        {
            T nearest = null;
            float nearestDistanceSqr = float.MaxValue;

            foreach (var candidate in candidates)
            {
                if (candidate == null) continue;
                if (filter != null && !filter(candidate)) continue;

                float distanceSqr = (candidate.transform.position - position).sqrMagnitude;
                if (distanceSqr < nearestDistanceSqr)
                {
                    nearestDistanceSqr = distanceSqr;
                    nearest = candidate;
                }
            }

            distance = nearest != null ? Mathf.Sqrt(nearestDistanceSqr) : float.MaxValue;
            return nearest;
        }

        #endregion

        #region Find All Within Range

        /// <summary>
        /// Find all components within a given range.
        /// </summary>
        public static List<T> FindAllInRange<T>(Vector3 position, float range, IEnumerable<T> candidates, Func<T, bool> filter = null)
            where T : Component
        {
            var result = new List<T>();
            float rangeSqr = range * range;

            foreach (var candidate in candidates)
            {
                if (candidate == null) continue;
                if (filter != null && !filter(candidate)) continue;

                float distanceSqr = (candidate.transform.position - position).sqrMagnitude;
                if (distanceSqr <= rangeSqr) result.Add(candidate);
            }

            return result;
        }

        /// <summary>
        /// Find all components within range, sorted by distance (nearest first).
        /// </summary>
        public static List<T> FindAllInRangeSorted<T>(Vector3 position, float range, IEnumerable<T> candidates, Func<T, bool> filter = null)
            where T : Component
        {
            var result = new List<(T item, float distSqr)>();
            float rangeSqr = range * range;

            foreach (var candidate in candidates)
            {
                if (candidate == null) continue;
                if (filter != null && !filter(candidate)) continue;

                float distanceSqr = (candidate.transform.position - position).sqrMagnitude;
                if (distanceSqr <= rangeSqr) result.Add((candidate, distanceSqr));
            }

            result.Sort((a, b) => a.distSqr.CompareTo(b.distSqr));

            var sortedResult = new List<T>(result.Count);
            foreach (var item in result) sortedResult.Add(item.item);

            return sortedResult;
        }

        #endregion

        #region Distance Helpers

        /// <summary>
        /// Check if two positions are within a given distance (more efficient than Vector3.Distance for comparisons).
        /// </summary>
        public static bool IsWithinDistance(Vector3 a, Vector3 b, float distance) => (a - b).sqrMagnitude <= distance * distance;

        /// <summary>
        /// Get horizontal distance (ignoring Y axis).
        /// </summary>
        public static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            GetHorizontalDelta(a, b, out float dx, out float dz);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// Check if within horizontal distance (ignoring Y axis).
        /// </summary>
        public static bool IsWithinHorizontalDistance(Vector3 a, Vector3 b, float distance)
        {
            GetHorizontalDelta(a, b, out float dx, out float dz);
            return (dx * dx + dz * dz) <= distance * distance;
        }

        private static void GetHorizontalDelta(Vector3 a, Vector3 b, out float dx, out float dz)
        {
            dx = a.x - b.x;
            dz = a.z - b.z;
        }

        #endregion
    }
}

// ============================================================================
// Example usage - refactored StoragePoint.FindNearest:
// ============================================================================
/*
// Before (in StoragePoint.cs):
public static StoragePoint FindNearest(Vector3 position, EResourceType resourceType)
{
    StoragePoint nearest = null;
    float nearestDistance = float.MaxValue;

    foreach (var storage in allStoragePoints)
    {
        if (storage == null) continue;
        if (!storage.AcceptsType(resourceType)) continue;

        float distance = Vector3.Distance(position, storage.transform.position);
        if (distance < nearestDistance)
        {
            nearestDistance = distance;
            nearest = storage;
        }
    }

    return nearest;
}

// After:
public static StoragePoint FindNearest(Vector3 position, EResourceType resourceType)
{
    return SpatialUtility.FindNearest(position, allStoragePoints, s => s.AcceptsType(resourceType));
}
*/