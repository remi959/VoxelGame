using Assets.Scripts.Resources;
using Assets.Scripts.Shared.Enums;
using Assets.Scripts.Shared.Pooling;
using Assets.Scripts.Shared.Utilities;
using UnityEngine;

namespace Assets.Scripts.Data.Resources
{
    /// <summary>
    /// Resource definition for tree-like structures with base, trunk, and top pieces.
    /// Supports variable trunk heights and optional decorative tops.
    /// Trees have two stages: Chopping (fells the tree) and Harvesting (collects pieces).
    /// </summary>
    [CreateAssetMenu(fileName = "TreeResourceDefinition", menuName = "Resources/Definitions/Tree Definition", order = 0)]
    public class TreeResourceDefinitionSO : ResourceDefinitionSO
    {
        [Header("Piece Prefabs")]
        [Tooltip("Prefabs for base pieces (placed at bottom only). Naming: '{Variant}BasePiece'")]
        public GameObject[] basePrefabs;

        [Tooltip("Prefabs for trunk/middle pieces. Naming: '{Variant}TrunkPiece' or '{Variant}MiddlePiece'")]
        public GameObject[] trunkPrefabs;

        [Tooltip("Prefabs for top pieces (placed at the very top). Naming: '{Variant}TopPiece'")]
        public GameObject[] topPrefabs;

        [Header("Structure Settings")]
        [Tooltip("Minimum number of trunk pieces (not counting base or top)")]
        public int minTrunkPieces = 3;

        [Tooltip("Maximum number of trunk pieces (not counting base or top)")]
        public int maxTrunkPieces = 10;

        [Tooltip("Whether to use a dedicated base piece (otherwise uses trunk for first piece)")]
        public bool useBasePiece = true;

        [Tooltip("Whether to include a top piece (e.g., tree crown)")]
        public bool includeTopPiece = true;

        [Tooltip("Whether the top piece is harvestable (has ResourcePiece component)")]
        public bool topPieceHarvestable = false;

        [Header("Chopping Stage")]
        [Tooltip("Time to chop down the tree before it falls")]
        public float chopTime = 2f;

        [Tooltip("Duration of the falling animation")]
        public float fallDuration = 1.5f;

        [Tooltip("Fall rotation angle (typically 90 degrees to lay flat)")]
        public float fallAngle = 90f;

        [Header("Stage Definitions")]
        [Tooltip("Define the stages for this tree. If empty, default Chop→Harvest stages will be generated.")]
        public ResourceStage[] stages;

        // Cached for the current generation
        private int cachedTrunkCount = -1;

        public override string VariantId
        {
            get
            {
                // Try trunk prefabs first
                if (trunkPrefabs != null && trunkPrefabs.Length > 0 && trunkPrefabs[0] != null)
                    return PrefabNamingUtility.GetVariantId(trunkPrefabs[0].name);

                // Then top prefabs
                if (topPrefabs != null && topPrefabs.Length > 0 && topPrefabs[0] != null)
                    return PrefabNamingUtility.GetVariantId(topPrefabs[0].name);

                // Then base prefabs
                if (basePrefabs != null && basePrefabs.Length > 0 && basePrefabs[0] != null)
                    return PrefabNamingUtility.GetVariantId(basePrefabs[0].name);

                // Fallback to resource name
                if (!string.IsNullOrEmpty(resourceName)) return resourceName.ToLowerInvariant().Replace(" ", "_");

                return "default";
            }
        }

        public override int GetPieceCount()
        {
            // Cache trunk count for consistent results during a single generation
            if (cachedTrunkCount < 0) cachedTrunkCount = Random.Range(minTrunkPieces, maxTrunkPieces + 1);

            int count = cachedTrunkCount;

            // Base piece replaces first trunk piece, doesn't add to count
            // Top piece adds to count if included
            if (includeTopPiece && topPrefabs != null && topPrefabs.Length > 0) count++;

            return count;
        }

        public override GameObject GetPrefabForIndex(int index, int totalPieces)
        {
            bool isFirst = index == 0;
            bool isLast = index == totalPieces - 1;

            // Top piece (last position, if enabled)
            if (isLast && includeTopPiece && topPrefabs != null && topPrefabs.Length > 0) return GetRandomFromArray(topPrefabs);


            // Base piece (first position, if enabled and available)
            if (isFirst && useBasePiece && basePrefabs != null && basePrefabs.Length > 0) return GetRandomFromArray(basePrefabs);

            // Trunk pieces (everything else)
            return GetRandomFromArray(trunkPrefabs);
        }

        public override bool IsPieceHarvestable(int index, int totalPieces)
        {
            bool isLast = index == totalPieces - 1;

            // Top piece harvestability is configurable
            if (isLast && includeTopPiece) return topPieceHarvestable;

            // All other pieces are harvestable
            return true;
        }

        /// <summary>
        /// Build the stages array for this tree.
        /// Returns user-defined stages if set, otherwise generates default Chop→Harvest stages.
        /// </summary>
        public override ResourceStage[] BuildStages()
        {
            // If user defined custom stages, use those
            if (stages != null && stages.Length > 0) return stages;
            
            // Otherwise generate default two-stage progression: Chopping → Harvesting
            return GenerateDefaultStages(Vector3.right * fallAngle);
        }

        /// <summary>
        /// Build stages with a specific fall rotation (used by instruction to randomize fall direction).
        /// </summary>
        public ResourceStage[] BuildStages(Vector3 fallRotation)
        {
            // If user defined custom stages, use those but update the transition rotation
            if (stages != null && stages.Length > 0)
            {
                // Clone stages so we don't modify the asset
                var clonedStages = new ResourceStage[stages.Length];
                for (int i = 0; i < stages.Length; i++)
                {
                    clonedStages[i] = stages[i].Clone();

                    // Update transition rotation for stages that have animations
                    if (clonedStages[i].playTransitionAnimation) clonedStages[i].transitionRotation = fallRotation;

                }
                return clonedStages;
            }

            // Otherwise generate default stages with the specified fall rotation
            return GenerateDefaultStages(fallRotation);
        }

        private ResourceStage[] GenerateDefaultStages(Vector3 fallRotation)
        {
            return new ResourceStage[]
            {
                new() {
                    stageName = "Chopping",
                    workTimePerPiece = chopTime,
                    yieldsPieces = false,
                    instantPickupLastPiece = false,
                    playTransitionAnimation = true,
                    transitionDuration = fallDuration,
                    transitionRotation = fallRotation,
                    destroyOnComplete = false
                },
                new() {
                    stageName = "Harvesting",
                    workTimePerPiece = harvestTimePerPiece,
                    yieldsPieces = true,
                    instantPickupLastPiece = true,
                    playTransitionAnimation = false,
                    destroyOnComplete = destroyOnComplete
                }
            };
        }

        /// <summary>
        /// Reset cached values for a new generation pass.
        /// Call this before generating a new tree to get fresh random values.
        /// </summary>
        public void ResetCache() => cachedTrunkCount = -1;

        private GameObject GetRandomFromArray(GameObject[] array)
        {
            if (array == null || array.Length == 0) return null;
            return array[Random.Range(0, array.Length)];
        }

        #region Visual Key Helpers

        /// <summary>
        /// Gets the visual key for trunk fragment pooling.
        /// </summary>
        public VisualKey GetTrunkVisualKey() => VisualKey.ForResource(resourceType, VariantId, EPieceType.Trunk);

        /// <summary>
        /// Gets the visual key for top fragment pooling.
        /// </summary>
        public VisualKey GetTopVisualKey() => VisualKey.ForResource(resourceType, VariantId, EPieceType.Top);

        /// <summary>
        /// Gets the visual key for base fragment pooling.
        /// </summary>
        public VisualKey GetBaseVisualKey() => VisualKey.ForResource(resourceType, VariantId, EPieceType.Base);

        #endregion

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            if (minTrunkPieces < 1) minTrunkPieces = 1;
            if (maxTrunkPieces < minTrunkPieces) maxTrunkPieces = minTrunkPieces;
        }

        [ContextMenu("Log Variant Info")]
        private void LogVariantInfo()
        {
            Debug.Log($"Resource: {resourceName}");
            Debug.Log($"Variant ID: {VariantId}");
            Debug.Log($"Trunk Key: {GetTrunkVisualKey()}");
            Debug.Log($"Top Key: {GetTopVisualKey()}");
            Debug.Log($"Base Key: {GetBaseVisualKey()}");
        }
#endif
    }
}