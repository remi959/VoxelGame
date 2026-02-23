// ============================================================================
// TargetRegistry.cs - Global registry for all targetable entities
// ============================================================================
using System.Collections.Generic;
using Assets.Scripts.Shared.Interfaces;

namespace Assets.Scripts.NPCs.Units
{
    /// <summary>
    /// Global registry for all targetable entities in the scene.
    /// 
    /// Why this exists:
    /// - Centralized target tracking for efficient queries
    /// - Avoids FindObjectsOfType calls during combat
    /// - Shared between all offensive units and targetable entities
    /// - O(1) registration/unregistration, O(n) queries
    /// 
    /// Usage:
    /// - ITargetable implementations register OnEnable, unregister OnDisable
    /// - Offensive units query AllTargetables to find targets
    /// </summary>
    public static class TargetRegistry
    {
        /// <summary>
        /// All active targetable entities in the scene.
        /// </summary>
        private static readonly HashSet<ITargetable> allTargetables = new();

        /// <summary>
        /// Read-only access to all targetable entities.
        /// </summary>
        public static IEnumerable<ITargetable> AllTargetables => allTargetables;

        /// <summary>
        /// Current count of registered targetables.
        /// </summary>
        public static int Count => allTargetables.Count;

        /// <summary>
        /// Register a targetable entity.
        /// Call from OnEnable.
        /// </summary>
        public static void Register(ITargetable targetable)
        {
            if (targetable != null)
            {
                allTargetables.Add(targetable);
            }
        }

        /// <summary>
        /// Unregister a targetable entity.
        /// Call from OnDisable.
        /// </summary>
        public static void Unregister(ITargetable targetable)
        {
            if (targetable != null)
            {
                allTargetables.Remove(targetable);
            }
        }

        /// <summary>
        /// Clear all registered targetables.
        /// Call on scene unload.
        /// </summary>
        public static void Clear()
        {
            allTargetables.Clear();
        }

        /// <summary>
        /// Check if a targetable is registered.
        /// </summary>
        public static bool Contains(ITargetable targetable)
        {
            return targetable != null && allTargetables.Contains(targetable);
        }
    }
}
