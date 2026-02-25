using Assets.Scripts.World;
using UnityEngine;

namespace Assets.Scripts.NPCs.Jobs.Givers
{
    /// <summary>
    /// Scans for Mineable objects in the scene and returns the nearest
    /// one as a Mine job. Higher priority than farming (checked first).
    /// </summary>
    public class WorkGiver_Mine : WorkGiver
    {
        public override int Priority => 2;
        public override string Label => "Mining";

        public override Job TryGetJob(Pawn pawn)
        {
            Mineable[] targets = Object.FindObjectsByType<Mineable>(FindObjectsSortMode.None);

            if (targets.Length == 0)
                return null;

            Mineable closest = null;
            float closestDist = float.MaxValue;
            Vector3 pawnPos = pawn.transform.position;

            foreach (Mineable target in targets)
            {
                float dist = Vector3.Distance(pawnPos, target.transform.position);

                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = target;
                }
            }

            return new Job(JobDef.Mine, targetObject: closest.gameObject);
        }
    }
}
