namespace Assets.Scripts.NPCs.Jobs
{
    /// <summary>
    /// Abstract base class for autonomous work-finding.
    ///
    /// Each WorkGiver represents one category of work a pawn can do (construction,
    /// farming, hauling, etc.). When a pawn has no queued jobs, the JobTracker iterates
    /// through all registered WorkGivers in priority order and asks each one:
    /// "Is there work available for this pawn?"
    ///
    /// This is inspired by RimWorld's WorkGiver system. Adding a new type of autonomous
    /// work means creating a new WorkGiver subclass — no modifications to existing code.
    ///
    /// ## How Priority Works
    /// - Lower values = higher priority (checked first).
    /// - If a WorkGiver with priority 1 returns a job, priority 2+ are never checked.
    /// - Use this to model importance: emergency repairs (1) before farming (5) before
    ///   cleaning (10).
    ///
    /// ## How It Connects to Jobs
    /// A WorkGiver does NOT execute work — it only finds it. It returns a Job instance
    /// that the JobTracker then executes via the normal JobDriver pipeline:
    ///
    ///   WorkGiver.TryGetJob() → Job → JobTracker.StartJob() → JobDriver.Start/Tick/End
    ///
    /// ## Lifetime
    /// WorkGivers are long-lived instances registered on the JobTracker. They are
    /// reused across many scan cycles — do not store per-scan state in fields.
    /// </summary>
    public abstract class WorkGiver
    {
        /// <summary>
        /// Priority order for this work type. Lower = checked first.
        /// Example: Construction=1, Farming=3, Hauling=5, Cleaning=10.
        /// </summary>
        public abstract int Priority { get; }

        /// <summary>
        /// Human-readable label for this work type (used in debug logs).
        /// Example: "Construction", "Farming", "Hauling".
        /// </summary>
        public abstract string Label { get; }

        /// <summary>
        /// Scan the world for available work that this pawn can do.
        /// Returns a Job if work was found, or null if nothing is available.
        ///
        /// This is called periodically (throttled, not every frame) when the pawn
        /// has no active job and no queued jobs. Keep this method lightweight —
        /// avoid expensive searches or allocations.
        /// </summary>
        public abstract Job TryGetJob(Pawn pawn);
    }
}
