using Assets.Scripts.Resources;
using Assets.Scripts.Shared.Enums;
using Assets.Scripts.Shared.Pooling;
using Assets.Scripts.Shared.Utilities;
using UnityEngine;

namespace Assets.Scripts.Data.Resources
{
    /// <summary>
    /// Resource definition for simple resources with uniform pieces (ore veins, stone piles, food, etc.).
    /// All pieces use the same pool of prefabs with no structural hierarchy.
    /// Simple resources have a single harvesting stage.
    /// </summary>
    [CreateAssetMenu(fileName = "SimpleResourceDefinition", menuName = "Resources/Definitions/Simple Definition", order = 1)]
    public class SimpleResourceDefinitionSO : ResourceDefinitionSO
    {
        [Header("Piece Prefabs")]
        [Tooltip("Prefabs for resource pieces. All pieces use this pool randomly. Naming: '{Variant}Piece'")]
        public GameObject[] piecePrefabs;

        [Header("Structure Settings")]
        [Tooltip("Minimum number of pieces")]
        public int minPieces = 3;

        [Tooltip("Maximum number of pieces")]
        public int maxPieces = 8;

        [Header("Harvest Behavior")]
        [Tooltip("If true, the last piece can be picked up instantly without waiting")]
        public bool instantPickupLastPiece = true;

        [Header("Stage Definitions")]
        [Tooltip("Define the stages for this resource. If empty, a default Harvest stage will be generated.")]
        public ResourceStage[] stages;

        // Cached for the current generation
        private int cachedPieceCount = -1;

        public override string VariantId
        {
            get
            {
                if (piecePrefabs != null && piecePrefabs.Length > 0 && piecePrefabs[0] != null)
                    return PrefabNamingUtility.GetVariantId(piecePrefabs[0].name);

                // Fallback to resource name
                if (!string.IsNullOrEmpty(resourceName)) return resourceName.ToLowerInvariant().Replace(" ", "_");

                return "default";
            }
        }

        public override int GetPieceCount()
        {
            // Cache piece count for consistent results during a single generation
            if (cachedPieceCount < 0) cachedPieceCount = Random.Range(minPieces, maxPieces + 1);

            return cachedPieceCount;
        }

        public override GameObject GetPrefabForIndex(int index, int totalPieces) => GetRandomPrefab();
        /// <summary>
        /// Get a random prefab from the pool.
        /// </summary>
        public GameObject GetRandomPrefab()
        {
            if (piecePrefabs == null || piecePrefabs.Length == 0) return null;
            return piecePrefabs[Random.Range(0, piecePrefabs.Length)];
        }

        /// <summary>
        /// Reset cached values for a new generation pass.
        /// Call this before generating a new resource to get fresh random values.
        /// </summary>
        public void ResetCache() => cachedPieceCount = -1;

        /// <summary>
        /// Build the stages array for this resource.
        /// Returns user-defined stages if set, otherwise generates a default Harvest stage.
        /// </summary>
        public override ResourceStage[] BuildStages()
        {
            // If user defined custom stages, use those
            if (stages != null && stages.Length > 0) return stages;

            // Otherwise generate default single-stage progression
            return new ResourceStage[]
            {
                new() {
                    stageName = "Harvest",
                    workTimePerPiece = harvestTimePerPiece,
                    yieldsPieces = true,
                    instantPickupLastPiece = instantPickupLastPiece,
                    playTransitionAnimation = false,
                    destroyOnComplete = destroyOnComplete
                }
            };
        }

        #region Visual Key Helpers

        /// <summary>
        /// Gets the visual key for fragment pooling.
        /// </summary>
        public VisualKey GetVisualKey() => VisualKey.ForResource(resourceType, VariantId, EPieceType.Any);

        #endregion

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            if (minPieces < 1) minPieces = 1;
            if (maxPieces < minPieces) maxPieces = minPieces;
        }

        [ContextMenu("Log Variant Info")]
        private void LogVariantInfo()
        {
            Debug.Log($"Resource: {resourceName}");
            Debug.Log($"Variant ID: {VariantId}");
            Debug.Log($"Visual Key: {GetVisualKey()}");
        }
#endif
    }
}