using Assets.Scripts.Resources;
using Assets.Scripts.Shared.Enums;
using Assets.Scripts.Shared.Pooling;
using Assets.Scripts.Shared.Utilities;
using UnityEngine;

namespace Assets.Scripts.Data.Resources
{
    [CreateAssetMenu(fileName = "ResourceDefinition", menuName = "Resources/Resource Definition", order = 0)]
    public class ResourceDefinitionSO : ScriptableObject
    {
        [Header("Resource Identity")]
        [Tooltip("Display name for this resource")]
        public string resourceName;

        [Tooltip("The type of resource this produces")]
        public EResourceType resourceType;

        [Header("Stage Definitions")]
        public ResourceStage[] stageDefinitions;

        [Header("Piece Prefabs")]
        [Tooltip("Prefabs for middle pieces (placed from bottom to second-to-last). Naming: '{Variant}MiddlePiece'")]
        public GameObject[] middlePrefabs;

        [Tooltip("Prefabs for top pieces (placed at the very top). Naming: '{Variant}TopPiece'")]
        public GameObject[] topPrefabs;

        [Tooltip("Prefabs for base pieces (optional, placed at bottom only). Naming: '{Variant}BasePiece'")]
        public GameObject[] basePrefabs;

        [Header("Generation Settings")]
        [Tooltip("Minimum number of trunk pieces (not counting top)")]
        public int minTrunkPieces = 3;

        [Tooltip("Maximum number of trunk pieces (not counting top)")]
        public int maxTrunkPieces = 10;

        [Tooltip("Whether to include a top piece (e.g., tree crown)")]
        public bool includeTopPiece = true;

        [Tooltip("Whether the top piece is harvestable (has ResourcePiece component)")]
        public bool topPieceHarvestable = false;

        [Tooltip("Value per piece when harvested")]
        public int valuePerPiece = 5;

        [Tooltip("Size/height of each piece")]
        public float pieceSize = 1f;

        /// <summary>
        /// Gets the variant ID from the first available prefab name.
        /// </summary>
        public string VariantId
        {
            get
            {
                // Try trunk prefabs first
                if (middlePrefabs != null && middlePrefabs.Length > 0 && middlePrefabs[0] != null)
                {
                    return PrefabNamingUtility.GetVariantId(middlePrefabs[0].name);
                }

                // Then top prefabs
                if (topPrefabs != null && topPrefabs.Length > 0 && topPrefabs[0] != null)
                {
                    return PrefabNamingUtility.GetVariantId(topPrefabs[0].name);
                }

                // Then base prefabs
                if (basePrefabs != null && basePrefabs.Length > 0 && basePrefabs[0] != null)
                {
                    return PrefabNamingUtility.GetVariantId(basePrefabs[0].name);
                }

                // Fallback to resource name
                if (!string.IsNullOrEmpty(resourceName))
                    return resourceName.ToLowerInvariant().Replace(" ", "_");

                return "default";
            }
        }

        /// <summary>
        /// Gets the visual key for trunk fragment pooling.
        /// </summary>
        public VisualKey GetTrunkVisualKey()
        {
            return VisualKey.ForResource(resourceType, VariantId, EPieceType.Trunk);
        }

        /// <summary>
        /// Gets the visual key for top fragment pooling.
        /// </summary>
        public VisualKey GetTopVisualKey()
        {
            return VisualKey.ForResource(resourceType, VariantId, EPieceType.Top);
        }

        /// <summary>
        /// Get a random trunk prefab.
        /// </summary>
        public GameObject GetRandomTrunkPrefab()
        {
            if (middlePrefabs == null || middlePrefabs.Length == 0) return null;
            return middlePrefabs[Random.Range(0, middlePrefabs.Length)];
        }

        /// <summary>
        /// Get a random top prefab.
        /// </summary>
        public GameObject GetRandomTopPrefab()
        {
            if (topPrefabs == null || topPrefabs.Length == 0) return null;
            return topPrefabs[Random.Range(0, topPrefabs.Length)];
        }

        /// <summary>
        /// Get a random base prefab.
        /// </summary>
        public GameObject GetRandomBasePrefab()
        {
            if (basePrefabs == null || basePrefabs.Length == 0) return null;
            return basePrefabs[Random.Range(0, basePrefabs.Length)];
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (minTrunkPieces < 1) minTrunkPieces = 1;
            if (maxTrunkPieces < minTrunkPieces) maxTrunkPieces = minTrunkPieces;
            if (valuePerPiece < 1) valuePerPiece = 1;
            if (pieceSize <= 0) pieceSize = 1f;
        }

        [ContextMenu("Log Variant Info")]
        private void LogVariantInfo()
        {
            Debug.Log($"Resource: {resourceName}");
            Debug.Log($"Variant ID: {VariantId}");
            Debug.Log($"Trunk Key: {GetTrunkVisualKey()}");
            Debug.Log($"Top Key: {GetTopVisualKey()}");
        }
#endif
    }
}