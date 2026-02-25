namespace Assets.Scripts.NPCs.Jobs
{
    /// <summary>
    /// Executes a Job on a Pawn over time.
    /// Each subclass implements the behavior for one type of job.
    /// Lifecycle: Setup → Start → Tick (every frame) → End.
    /// </summary>
    public abstract class JobDriver
    {
        protected Pawn pawn;
        protected Job job;

        public bool IsComplete { get; protected set; }

        /// <summary>
        /// Called by JobTracker to inject the pawn and job references.
        /// </summary>
        public void Setup(Pawn pawn, Job job)
        {
            this.pawn = pawn;
            this.job = job;
        }

        /// <summary>Called once when the job begins.</summary>
        public abstract void Start();

        /// <summary>Called every frame while the job is active.</summary>
        public abstract void Tick();

        /// <summary>Called when the job ends, whether completed or interrupted.</summary>
        public abstract void End();
    }
}
