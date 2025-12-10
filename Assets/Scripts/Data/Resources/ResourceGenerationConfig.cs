using System.Collections.Generic;
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
        /// Get a weighted random resource definition.
        /// </summary>
        public ResourceSpawnEntry GetRandomEntry()
        {
            if (possibleResources == null || possibleResources.Count == 0) return null;

            float totalWeight = 0f;
            foreach (var entry in possibleResources)
                if (entry.definition != null) totalWeight += entry.spawnWeight;

            float random = Random.Range(0f, totalWeight);
            float cumulative = 0f;

            foreach (var entry in possibleResources)
            {
                if (entry.definition == null) continue;

                cumulative += entry.spawnWeight;
                if (random <= cumulative) return entry;
            }

            return possibleResources[0];
        }

        /// <summary>
        /// Get a random entry and cast to a specific definition type.
        /// Returns null if the definition is not of the expected type.
        /// </summary>
        public T GetRandomEntryAs<T>() where T : ResourceDefinitionSO
        {
            var entry = GetRandomEntry();
            return entry?.definition as T;
        }

        /// <summary>
        /// Get a random entry, ensuring it's of the specified type.
        /// Keeps trying until it finds one or runs out of options.
        /// </summary>
        public ResourceSpawnEntry GetRandomEntryOfType<T>() where T : ResourceDefinitionSO
        {
            if (possibleResources == null || possibleResources.Count == 0) return null;

            // Filter to only matching types
            List<ResourceSpawnEntry> matching = new();
            float totalWeight = 0f;

            foreach (var entry in possibleResources)
            {
                if (entry.definition is T)
                {
                    matching.Add(entry);
                    totalWeight += entry.spawnWeight;
                }
            }

            if (matching.Count == 0) return null;

            float random = Random.Range(0f, totalWeight);
            float cumulative = 0f;

            foreach (var entry in matching)
            {
                cumulative += entry.spawnWeight;
                if (random <= cumulative) return entry;
            }

            return matching[0];
        }
    }
}