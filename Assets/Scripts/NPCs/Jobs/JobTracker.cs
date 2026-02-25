using System.Collections.Generic;
using Assets.Scripts.Core;
using Assets.Scripts.NPCs.Jobs.Givers;
using UnityEngine;

namespace Assets.Scripts.NPCs.Jobs
{
    /// <summary>
    /// Lives on each Pawn. Manages the current job, a job queue, and autonomous
    /// work-finding via WorkGivers. This is the single place that controls what
    /// a pawn is "doing" at any moment.
    ///
    /// ## Job Lifecycle (per frame)
    ///
    /// The Update loop follows this priority chain:
    ///
    ///   1. Active job? → Tick it. If complete → EndCurrentJob().
    ///   2. Queue has jobs? → Dequeue the next one → StartJob().
    ///   3. Queue empty? → Ask WorkGivers for autonomous work (throttled).
    ///   4. No work found? → Pawn idles.
    ///
    /// ## Three Ways Jobs Enter the System
    ///
    /// 1. **StartJob(job)** — Immediate. Ends any current job and starts this one
    ///    right now. Used for player commands and urgent interrupts.
    ///
    /// 2. **EnqueueJob(job)** — Queued. Added to the back of the queue. Processed
    ///    in FIFO order after the current job completes. Used for chaining work:
    ///    "go here, then build that, then return."
    ///
    /// 3. **WorkGivers** — Autonomous. When both the active job and queue are empty,
    ///    the pawn scans registered WorkGivers by priority to find work on its own.
    ///    This is how RimWorld-style "colonists find their own tasks" works.
    ///
    /// ## WorkGiver Scanning
    ///
    /// WorkGivers are checked in priority order (lowest number first). The scan is
    /// throttled to avoid running expensive world queries every frame. The interval
    /// is configurable via the Inspector (default: 0.5s).
    ///
    /// When a WorkGiver returns a Job, it enters the system through StartJob() —
    /// identical to a player-issued command from that point onward.
    /// </summary>
    [RequireComponent(typeof(Pawn))]
    public class JobTracker : MonoBehaviour
    {
        [Header("Work Scanning")]
        [Tooltip("Seconds between autonomous work scans when idle. Lower = more responsive but more expensive.")]
        [SerializeField] private float workScanInterval = 0.5f;

        private Pawn pawn;
        private float nextWorkScanTime;

        private readonly Queue<Job> jobQueue = new();
        private readonly List<WorkGiver> workGivers = new();

        public Job CurrentJob { get; private set; }
        public JobDriver CurrentDriver { get; private set; }
        public bool HasJob => CurrentJob != null;
        public int QueuedJobCount => jobQueue.Count;

        private void Awake()
        {
            pawn = GetComponent<Pawn>();

            // Register the idle fallback. Concrete WorkGivers (construction, farming, etc.)
            // are registered externally via RegisterWorkGiver() — either by the pawn's setup
            // code, a spawner, or a configuration system.
            RegisterWorkGiver(new WorkGiver_Idle());
        }

        private void Update()
        {
            // --- Priority 1: Tick the active job ---
            if (CurrentDriver != null)
            {
                CurrentDriver.Tick();

                if (CurrentDriver.IsComplete)
                    EndCurrentJob();

                return;
            }

            // --- Priority 2: Dequeue the next queued job ---
            if (jobQueue.Count > 0)
            {
                Job next = jobQueue.Dequeue();
                DebugManager.LogJob($"{pawn.PawnName} dequeued job: {next} ({jobQueue.Count} remaining in queue)");
                StartJob(next);
                return;
            }

            // --- Priority 3: Scan WorkGivers for autonomous work (throttled) ---
            if (Time.time >= nextWorkScanTime)
            {
                nextWorkScanTime = Time.time + workScanInterval;
                TryFindWork();
            }
        }

        /// <summary>
        /// Register a WorkGiver for autonomous work-finding. The list is kept
        /// sorted by priority so scanning always checks the most important work first.
        /// </summary>
        public void RegisterWorkGiver(WorkGiver giver)
        {
            workGivers.Add(giver);
            workGivers.Sort((a, b) => a.Priority.CompareTo(b.Priority));

            DebugManager.LogJob($"{pawn.PawnName} registered WorkGiver: {giver.Label} (priority {giver.Priority})");
        }

        /// <summary>
        /// Remove a previously registered WorkGiver.
        /// </summary>
        public void UnregisterWorkGiver(WorkGiver giver)
        {
            workGivers.Remove(giver);
        }

        /// <summary>
        /// Assigns a new job immediately. Ends any current job first.
        /// The driver is created automatically from the job's definition.
        /// </summary>
        public void StartJob(Job job)
        {
            EndCurrentJob();

            CurrentJob = job;
            CurrentDriver = job.Def.CreateDriver();
            CurrentDriver.Setup(pawn, job);
            CurrentDriver.Start();

            DebugManager.LogJob($"{pawn.PawnName} started job: {job}");
        }

        /// <summary>
        /// Adds a job to the back of the queue. It will be started automatically
        /// when the current job (and any jobs ahead of it in the queue) complete.
        /// </summary>
        public void EnqueueJob(Job job)
        {
            jobQueue.Enqueue(job);
            DebugManager.LogJob($"{pawn.PawnName} enqueued job: {job} (queue size: {jobQueue.Count})");
        }

        /// <summary>
        /// Ends the current job immediately (whether completed or interrupted).
        /// Does NOT clear the queue — queued jobs will start on the next frame.
        /// </summary>
        public void EndCurrentJob()
        {
            if (CurrentDriver == null) return;

            DebugManager.LogJob($"{pawn.PawnName} ended job: {CurrentJob}");

            CurrentDriver.End();
            CurrentDriver = null;
            CurrentJob = null;
        }

        /// <summary>
        /// Clears the job queue without affecting the currently active job.
        /// Use this when the pawn receives a new direct command and should
        /// abandon its planned work chain.
        /// </summary>
        public void ClearQueue()
        {
            if (jobQueue.Count > 0)
            {
                DebugManager.LogJob($"{pawn.PawnName} cleared job queue ({jobQueue.Count} jobs discarded)");
                jobQueue.Clear();
            }
        }

        /// <summary>
        /// Ends the current job AND clears the queue. The pawn will revert to
        /// autonomous work-finding on the next scan cycle.
        /// </summary>
        public void ClearAllWork()
        {
            ClearQueue();
            EndCurrentJob();
        }

        /// <summary>
        /// Iterates through registered WorkGivers in priority order.
        /// The first one that returns a non-null Job wins.
        /// </summary>
        private void TryFindWork()
        {
            foreach (WorkGiver giver in workGivers)
            {
                Job job = giver.TryGetJob(pawn);

                if (job != null)
                {
                    DebugManager.LogJob($"{pawn.PawnName} found work via {giver.Label}: {job}");
                    StartJob(job);
                    return;
                }
            }
        }
    }
}
