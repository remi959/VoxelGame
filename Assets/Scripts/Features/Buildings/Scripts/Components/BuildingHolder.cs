using System.Collections.Generic;
using Assets.Scripts.Shared.Enums;
using UnityEngine;

namespace Assets.Scripts.Buildings
{
    /// <summary>
    /// Attach to the root of an assembled building prefab.
    /// Child objects should have BuildingPart components.
    /// </summary>
    public class BuildingHolder : MonoBehaviour
    {
        [Header("Building Info")]
        [SerializeField] private string buildingName;
        [SerializeField] private Vector2Int footprint = new(2, 2);

        public string BuildingName => string.IsNullOrEmpty(buildingName) ? gameObject.name : buildingName;
        public Vector2Int Footprint => footprint;

        private List<BuildingPart> _cachedParts;

        private void OnEnable()
        {
            _cachedParts = null;
        }

        /// <summary>
        /// Get all BuildingPart children sorted by Y position (bottom-up).
        /// Result is cached after first call; cache is invalidated on OnEnable.
        /// </summary>
        public List<BuildingPart> GetPartsSorted()
        {
            if (_cachedParts != null) return _cachedParts;

            _cachedParts = new List<BuildingPart>(GetComponentsInChildren<BuildingPart>());
            _cachedParts.Sort((a, b) =>
            {
                int y = a.transform.localPosition.y.CompareTo(b.transform.localPosition.y);
                if (y != 0) return y;
                int x = a.transform.localPosition.x.CompareTo(b.transform.localPosition.x);
                if (x != 0) return x;
                return a.transform.localPosition.z.CompareTo(b.transform.localPosition.z);
            });
            return _cachedParts;
        }

        /// <summary>
        /// Get total resource costs for all parts.
        /// </summary>
        public Dictionary<EResourceType, int> GetTotalCosts()
        {
            var costs = new Dictionary<EResourceType, int>();
            foreach (var part in GetComponentsInChildren<BuildingPart>())
            {
                foreach (var cost in part.ResourceCosts)
                {
                    if (costs.ContainsKey(cost.resourceType))
                        costs[cost.resourceType] += cost.amount;
                    else
                        costs[cost.resourceType] = cost.amount;
                }
            }
            return costs;
        }

        /// <summary>
        /// Get total construction time.
        /// </summary>
        public float GetTotalConstructionTime()
        {
            float total = 0f;
            foreach (var part in GetComponentsInChildren<BuildingPart>())
                total += part.ConstructionTime;
            return total;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(buildingName))
                buildingName = gameObject.name;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 0.5f, 1f, 0.3f);
            Vector3 size = new(footprint.x, 0.1f, footprint.y);
            Gizmos.DrawCube(transform.position + Vector3.up * 0.05f, size);
        }
#endif
    }
}