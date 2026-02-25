using Assets.Scripts.World;
using UnityEngine;

namespace Assets.Scripts.NPCs.Jobs.Givers
{
    /// <summary>
    /// Scans for Farmable objects in the scene and returns the nearest
    /// one as a Farm job. Lower priority than mining (checked after).
    /// </summary>
    public class WorkGiver_Farm : WorkGiver
    {
        public override int Priority => 3;
        public override string Label => "Farming";

        public override Job TryGetJob(Pawn pawn)
        {
            Farmable[] targets = Object.FindObjectsByType<Farmable>(FindObjectsSortMode.None);

            if (targets.Length == 0)
                return null;

            Farmable closest = null;
            float closestDist = float.MaxValue;
            Vector3 pawnPos = pawn.transform.position;

            foreach (Farmable target in targets)
            {
                float dist = Vector3.Distance(pawnPos, target.transform.position);

                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = target;
                }
            }

            return new Job(JobDef.Farm, targetObject: closest.gameObject);
        }
    }
}
