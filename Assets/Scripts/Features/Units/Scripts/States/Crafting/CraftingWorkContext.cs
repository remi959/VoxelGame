// ============================================================================
// CraftingWorkContext.cs - Per-worker crafting context
// ============================================================================
using Assets.Scripts.Buildings;
using System;

namespace Assets.Scripts.NPCs.States.Crafting
{
    /// <summary>
    /// Per-worker context for crafting operations.
    /// 
    /// Persists across state transitions (e.g., when moving to a bench output)
    /// to prevent duplicate craft requests and track crafting state.
    /// 
    /// Designed to be extensible for future crafting mechanics like:
    /// - Weapon crafting
    /// - Tool creation
    /// - Item processing
    /// </summary>
    public class CraftingWorkContext
    {
        /// <summary>
        /// The crafting bench being used.
        /// </summary>
        public CraftingBench TargetBench { get; set; }

        /// <summary>
        /// Whether a craft request has been submitted to the bench.
        /// Prevents duplicate requests when re-entering states.
        /// </summary>
        public bool CraftRequested { get; set; }

        /// <summary>
        /// Whether the worker has arrived at the output position.
        /// </summary>
        public bool ArrivedAtOutput { get; set; }

        /// <summary>
        /// The crafted part once it's ready (set by callback).
        /// </summary>
        public CraftedPart CompletedPart { get; set; }

        /// <summary>
        /// Check if we're waiting for a craft to complete.
        /// </summary>
        public bool IsWaitingForCraft => CraftRequested && CompletedPart == null;

        /// <summary>
        /// Check if craft is complete and ready for pickup.
        /// </summary>
        public bool IsCraftComplete => CompletedPart != null;

        /// <summary>
        /// Clear all crafting context.
        /// Call when crafting is complete, cancelled, or worker is reassigned.
        /// </summary>
        public void Clear()
        {
            TargetBench = null;
            CraftRequested = false;
            ArrivedAtOutput = false;
            CompletedPart = null;
        }

        /// <summary>
        /// Reset for a new craft cycle (same bench, new part).
        /// </summary>
        public void ResetForNextCraft()
        {
            CraftRequested = false;
            ArrivedAtOutput = false;
            CompletedPart = null;
        }
    }
}
