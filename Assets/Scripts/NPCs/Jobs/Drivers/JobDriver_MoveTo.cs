namespace Assets.Scripts.NPCs.Jobs.Drivers
{
    /// <summary>
    /// Navigates the pawn to a world position. Completes when the pawn arrives.
    /// </summary>
    public class JobDriver_MoveTo : JobDriver
    {
        public override void Start()
        {
            pawn.Motor.SetDestination(job.TargetPosition);
        }

        public override void Tick()
        {
            if (!pawn.Motor.IsMoving)
                IsComplete = true;
        }

        public override void End()
        {
            pawn.Motor.Stop();
        }
    }
}
