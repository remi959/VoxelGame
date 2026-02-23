using System.Collections.Generic;
using Assets.Scripts.Data.Resources;
using Assets.Scripts.Resources;
using Assets.Scripts.Shared.Enums;
using Assets.Scripts.Shared.Utilities;
using UnityEngine;

namespace Assets.Scripts.Generation.Resources
{
    /// <summary>
    /// Base class for resource generation instructions.
    /// Contains common functionality shared by all instruction types.
    /// </summary>
    public abstract class ResourceGenerationInstruction : ScriptableObject
    {
        [Header("Generation Config")]
        [SerializeField] protected ResourceGenerationConfig config;

        [Header("Ground Detection")]
        [SerializeField] protected float groundCheckHeight = 10f;
        [SerializeField] protected LayerMask groundLayer;

        [Header("Manual Test Positions")]
        [SerializeField] protected List<Vector3> testSpawnPositions = new();

        public EResourceType ResourceType => config != null ? config.resourceType : EResourceType.Wood;
        public List<Resource> GeneratedResources { get; protected set; } = new();
        public ResourceGenerationConfig Config => config;

        #region Abstract Methods

        public abstract Resource GenerateResourceAt(Vector3 position);
        public abstract void GenerateResources();

        #endregion

        #region Shared Implementations

        /// <summary>
        /// Clear all generated resources. Can be overridden for custom cleanup.
        /// </summary>
        public virtual void ClearGeneratedResources()
        {
            foreach (var resource in GeneratedResources)
            {
                if (resource == null) continue;

#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(resource.gameObject);
                else
#endif
                    Destroy(resource.gameObject);
            }
            GeneratedResources.Clear();
        }

        /// <summary>
        /// Generate resources at all test positions (for testing).
        /// </summary>
        public virtual void GenerateAtTestPositions()
        {
            foreach (var pos in testSpawnPositions) GenerateResourceAt(pos);
        }

        /// <summary>
        /// Snap a position to the ground using raycast.
        /// </summary>
        protected Vector3 SnapToGround(Vector3 position)
        {
            Vector3 rayStart = position + Vector3.up * groundCheckHeight;

            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, groundCheckHeight * 2f, groundLayer))
                return hit.point;

            return position;
        }

        /// <summary>
        /// Set layer recursively for a GameObject and all children.
        /// </summary>
        protected void SetLayerRecursive(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform) SetLayerRecursive(child.gameObject, layer);
        }

        /// <summary>
        /// Set the Interactable layer recursively.
        /// </summary>
        protected void SetInteractableLayer(GameObject obj) => SetLayerRecursive(obj, LayerMask.NameToLayer(Strings.InteractableLayerName));
        protected void SetResourceTag(GameObject obj) => obj.tag = Strings.ResourceTag;

        #endregion
    }
}