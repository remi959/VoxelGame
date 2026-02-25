using Assets.Scripts.Core;

namespace Assets.Scripts.NPCs.Jobs.Drivers
{
    /// <summary>
    /// Base driver for any job that targets a world object: navigate to it,
    /// then optionally work on it over time.
    ///
    /// Handles two concerns that all object-interaction jobs share:
    ///   1. Walking to the target (with null-checks for destroyed targets).
    ///   2. Calling subclass hooks once arrived.
    ///
    /// ## Subclass contract
    ///
    /// Override these virtual methods to define what happens at the target:
    ///
    /// - **OnArrived()** — Called once when the pawn reaches the target.
    ///   Use this to initialize work state (start timers, play animations).
    ///   Base implementation sets IsComplete = true (instant interaction).
    ///
    /// - **OnWork()** — Called every frame after arrival, as long as the job
    ///   is still active. Use this for timed work (increment timers, check
    ///   progress). Set IsComplete = true when done.
    ///   Base implementation does nothing (only needed for timed work).
    ///
    /// For instant interactions (e.g., pick up an item), only override OnArrived
    /// and set IsComplete = true. For timed work (e.g., mining, farming), override
    /// both: use OnArrived to log/initialize, and OnWork to count time.
    /// </summary>
    public class JobDriver_Interact : JobDriver
    {
        private enum Phase { Moving, Working }
        private Phase phase;

        public override void Start()
        {
            if (job.TargetObject == null)
            {
                IsComplete = true;
                return;
            }

            phase = Phase.Moving;
            pawn.Motor.SetDestination(job.TargetObject.transform.position);
        }

        public override void Tick()
        {
            if (job.TargetObject == null)
            {
                IsComplete = true;
                return;
            }

            switch (phase)
            {
                case Phase.Moving:
                    if (!pawn.Motor.IsMoving)
                    {
                        phase = Phase.Working;
                        OnArrived();
                    }
                    break;

                case Phase.Working:
                    OnWork();
                    break;
            }
        }

        public override void End()
        {
            pawn.Motor.Stop();
        }

        /// <summary>
        /// Called once when the pawn reaches the target. Override to initialize
        /// work state. Base implementation completes the job immediately.
        /// </summary>
        protected virtual void OnArrived()
        {
            DebugManager.LogJob($"{pawn.PawnName} arrived at {job.TargetObject.name}");
            IsComplete = true;
        }

        /// <summary>
        /// Called every frame after arrival while the job is active.
        /// Override for timed work — set IsComplete = true when done.
        /// Base implementation does nothing.
        /// </summary>
        protected virtual void OnWork()
        {
        }
    }
}
