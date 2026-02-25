using UnityEngine;

namespace Assets.Scripts.World
{
    /// <summary>
    /// Marker component for objects that can be farmed.
    /// Add this to any GameObject in the scene (crops, plants, etc.)
    /// and WorkGiver_Farm will find it automatically.
    /// </summary>
    public class Farmable : Workable
    {
        protected override void OnWorkCompleted()
        {
            Destroy(gameObject);
        }
    }
}
