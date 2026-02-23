using System.Collections.Generic;
using Assets.Scripts.Shared.Enums;
using UnityEngine;

namespace Assets.Scripts.Buildings
{
    /// <summary>
    /// Attach to each building part prefab.
    /// Defines resource costs and construction properties.
    /// </summary>
    public class BuildingPart : MonoBehaviour
    {
        [Header("Construction")]
        [SerializeField] private float constructionTime = 5f;
        [SerializeField] private List<ResourceCost> resourceCosts = new();

        [Header("Carry Settings")]
        [SerializeField] private float carryScale = 0.3f;
        [SerializeField] private float placeAnimationDuration = 0.5f;

        public float ConstructionTime => constructionTime;
        public IReadOnlyList<ResourceCost> ResourceCosts => resourceCosts;
        public float CarryScale => carryScale;
        public float PlaceAnimationDuration => placeAnimationDuration;

        public int GetCostForType(EResourceType type)
        {
            foreach (var cost in resourceCosts)
                if (cost.resourceType == type)
                    return cost.amount;
            return 0;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (constructionTime < 0.1f) constructionTime = 0.1f;
        }
#endif
    }

    [System.Serializable]
    public struct ResourceCost
    {
        public EResourceType resourceType;
        public int amount;

        public ResourceCost(EResourceType type, int amt)
        {
            resourceType = type;
            amount = amt;
        }
    }
}