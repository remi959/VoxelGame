using UnityEngine;

namespace Assets.Scripts.NPCs.Jobs
{
    /// <summary>
    /// A concrete job instance: what to do, where, and on what.
    /// Pure data — contains no logic. The JobDriver reads this to know what to execute.
    /// </summary>
    public class Job
    {
        public JobDef Def { get; }
        public Vector3 TargetPosition { get; }
        public GameObject TargetObject { get; }

        public Job(JobDef def, Vector3 targetPosition = default, GameObject targetObject = null)
        {
            Def = def;
            TargetPosition = targetPosition;
            TargetObject = targetObject;
        }

        public override string ToString()
        {
            if (TargetObject != null)
                return $"{Def.Name} -> {TargetObject.name}";

            return $"{Def.Name} -> {TargetPosition}";
        }
    }
}
