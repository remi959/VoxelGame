using Assets.Scripts.Core;
using Assets.Scripts.World;
using UnityEngine;

namespace Assets.Scripts.NPCs.Jobs.Drivers
{
    /// <summary>
    /// Walks to a mineable target, then works it for a duration.
    /// Inherits all movement and null-checking from JobDriver_Interact.
    /// </summary>
    public class JobDriver_Mine : JobDriver_Interact
    {
        private Workable workable;
        protected override void OnArrived()
        {
            workable = job.TargetObject.GetComponent<Workable>();
            DebugManager.LogJob($"{pawn.PawnName} started mining {job.TargetObject.name}");
        }

        protected override void OnWork()
        {
            workable.ApplyWork(pawn.WorkSpeed * Time.deltaTime);

            if (workable.IsFinished)
            {
                DebugManager.LogJob($"{pawn.PawnName} finished mining {job.TargetObject.name}");
                IsComplete = true;
            }
        }
    }
}
