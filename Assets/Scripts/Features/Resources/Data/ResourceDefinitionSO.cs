using Assets.Scripts.Resources;
using Assets.Scripts.Shared.Enums;
using UnityEngine;

namespace Assets.Scripts.Data.Resources
{
    /// <summary>
    /// Base class for resource definitions. Contains common properties shared by all resource types.
    /// Inherit from this to create specialized definitions for different resource structures.
    /// </summary>
    public abstract class ResourceDefinitionSO : ScriptableObject
    {
        [Header("Resource Identity")]
        [Tooltip("Display name for this resource")]
        public string resourceName;

        [Tooltip("The type of resource this produces")]
        public EResourceType resourceType;

        [Header("Harvest Settings")]
        [Tooltip("Value per piece when harvested")]
        public int valuePerPiece = 5;

        [Tooltip("Size/height of each piece")]
        public float pieceSize = 1f;

        [Tooltip("Time to harvest each piece")]
        public float harvestTimePerPiece = 1f;

        [Tooltip("Order in which pieces are harvested")]
        public EHarvestOrder harvestOrder = EHarvestOrder.Random;

        [Tooltip("Destroy the resource GameObject when fully harvested")]
        public bool destroyOnComplete = true;

        /// <summary>
        /// Gets the variant ID for this resource definition.
        /// Used for fragment pooling and visual identification.
        /// </summary>
        public abstract string VariantId { get; }

        /// <summary>
        /// Get the total number of pieces this resource will generate.
        /// </summary>
        public abstract int GetPieceCount();

        /// <summary>
        /// Get a prefab for a piece at the given index.
        /// </summary>
        /// <param name="index">The index of the piece (0-based)</param>
        /// <param name="totalPieces">Total number of pieces being generated</param>
        /// <returns>A prefab GameObject, or null if not available</returns>
        public abstract GameObject GetPrefabForIndex(int index, int totalPieces);

        /// <summary>
        /// Check if the piece at the given index should be harvestable.
        /// </summary>
        public virtual bool IsPieceHarvestable(int index, int totalPieces)
        {
            return true; // Default: all pieces are harvestable
        }

        /// <summary>
        /// Build the stages array for this resource type.
        /// Override in subclasses to define resource-specific stage progression.
        /// </summary>
        public abstract ResourceStage[] BuildStages();

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            if (valuePerPiece < 1) valuePerPiece = 1;
            if (pieceSize <= 0) pieceSize = 1f;
        }
#endif
    }
}