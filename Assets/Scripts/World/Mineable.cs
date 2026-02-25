using UnityEngine;

namespace Assets.Scripts.World
{
    /// <summary>
    /// Marker component for objects that can be mined.
    /// Add this to any GameObject in the scene (rocks, ore deposits, etc.)
    /// and WorkGiver_Mine will find it automatically.
    /// </summary>
    public class Mineable : Workable
    {
        protected override void OnWorkCompleted()
        {
            Destroy(gameObject);
        }
    }
}
