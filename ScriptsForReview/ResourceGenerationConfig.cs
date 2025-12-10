using System.Collections.Generic;
using Assets.Scripts.Data.Resources;
using Assets.Scripts.Shared.Enums;
using UnityEngine;

namespace Assets.Scripts.Data.Resources
{
    [CreateAssetMenu(fileName = "GenerationConfig", menuName = "Resources/Generation Config")]
    public class ResourceGenerationConfig : ScriptableObject
    {
        [System.Serializable]
        public class ResourceSpawnEntry
        {
            public ResourceDefinitionSO definition;
            [Range(0f, 1f)] public float spawnWeight = 1f;
        }

        public EResourceType resourceType;
        public List<ResourceSpawnEntry> possibleResources = new();

        [Header("Placement Settings")]
        public float minDistanceBetween = 3f;
        public float maxSlopeAngle = 30f;
        public LayerMask validGroundLayers;

        /// <summary>
        /// Get a weighted random resource definition
        /// </summary>
        public ResourceSpawnEntry GetRandomEntry()
        {
            if (possibleResources == null || possibleResources.Count == 0)
                return null;

            float totalWeight = 0f;
            foreach (var entry in possibleResources)
            {
                if (entry.definition != null)
                    totalWeight += entry.spawnWeight;
            }

            float random = Random.Range(0f, totalWeight);
            float cumulative = 0f;

            foreach (var entry in possibleResources)
            {
                if (entry.definition == null) continue;
                
                cumulative += entry.spawnWeight;
                if (random <= cumulative)
                    return entry;
            }

            return possibleResources[0];
        }
    }
}