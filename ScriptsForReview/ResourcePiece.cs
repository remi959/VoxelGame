using Assets.Scripts.Core;
using Assets.Scripts.Shared.Enums;
using Assets.Scripts.Shared.Pooling;
using Assets.Scripts.Shared.Utilities;
using UnityEngine;

namespace Assets.Scripts.Resources
{
    /// <summary>
    /// Represents a piece of a resource that can be detached.
    /// When detached, spawns a pooled fragment (if available) or converts itself to a fragment.
    /// 
    /// Variant ID and piece type are automatically parsed from the GameObject name.
    /// Expected naming: "{Variant}{PieceType}Piece" (e.g., "OakTrunkPiece", "AcaciaTopPiece")
    /// </summary>
    public class ResourcePiece : MonoBehaviour
    {
        [Header("Fragment Settings")]
        [SerializeField] private int resourceValue = 5;

        [Header("Size")]
        public float PieceSize = 1f;

        [Header("Auto-Parsed (Read Only)")]
        [SerializeField, Tooltip("Parsed from GameObject name")]
        private string variantId = "default";

        [SerializeField, Tooltip("Parsed from GameObject name")]
        private EPieceType pieceType = EPieceType.Any;

        private bool isDetached = false;
        private bool isParsed = false;

        #region Properties

        public bool IsDetached => isDetached;
        public int ResourceValue => resourceValue;

        /// <summary>
        /// Variant ID parsed from prefab name (e.g., "oak" from "OakTrunkPiece")
        /// </summary>
        public string VariantId
        {
            get
            {
                EnsureParsed();
                return variantId;
            }
        }

        /// <summary>
        /// Piece type parsed from prefab name (e.g., Trunk from "OakTrunkPiece")
        /// </summary>
        public EPieceType PieceType
        {
            get
            {
                EnsureParsed();
                return pieceType;
            }
        }

        #endregion

        private void Awake()
        {
            EnsureParsed();
        }

        /// <summary>
        /// Parse variant ID and piece type from GameObject name if not already done.
        /// </summary>
        private void EnsureParsed()
        {
            if (isParsed) return;

            PrefabNamingUtility.Parse(gameObject.name, out variantId, out pieceType);
            isParsed = true;
        }

        /// <summary>
        /// Configure this piece programmatically (used by generation system).
        /// If variant is null/empty, it will be parsed from the GameObject name.
        /// </summary>
        public void SetConfiguration(int value, float pieceSize, string variant = null, EPieceType? type = null)
        {
            resourceValue = value;
            PieceSize = pieceSize;

            // If variant/type not provided, parse from name
            if (string.IsNullOrEmpty(variant) || !type.HasValue)
            {
                PrefabNamingUtility.Parse(gameObject.name, out string parsedVariant, out EPieceType parsedType);

                variantId = string.IsNullOrEmpty(variant) ? parsedVariant : variant;
                pieceType = type ?? parsedType;
            }
            else
            {
                variantId = variant;
                pieceType = type.Value;
            }

            isParsed = true;
        }

        /// <summary>
        /// Detach this piece and spawn a fragment.
        /// Tries to use object pool first, falls back to converting this object.
        /// </summary>
        public ResourceFragment Detach(EResourceType resourceType)
        {
            if (isDetached) return null;
            isDetached = true;

            EnsureParsed();

            var visualKey = VisualKey.ForResource(resourceType, variantId, pieceType);

            // Try to get from pool first
            if (FragmentPool.Instance != null)
            {
                // First try exact match (with piece type)
                if (FragmentPool.Instance.HasVariant(visualKey))
                {
                    var fragment = FragmentPool.Instance.Get(visualKey, transform.position, resourceValue);

                    if (fragment != null)
                    {
                        fragment.Launch();
                        gameObject.SetActive(false);
                        DebugManager.LogGathering($"ResourcePiece: Spawned pooled fragment for {visualKey}");
                        return fragment;
                    }
                }

                // Fallback: try without piece type (generic variant)
                var fallbackKey = visualKey.WithoutPieceType();
                if (FragmentPool.Instance.HasVariant(fallbackKey))
                {
                    var fragment = FragmentPool.Instance.Get(fallbackKey, transform.position, resourceValue);

                    if (fragment != null)
                    {
                        fragment.Launch();
                        gameObject.SetActive(false);
                        DebugManager.LogGathering($"ResourcePiece: Spawned pooled fragment using fallback {fallbackKey}");
                        return fragment;
                    }
                }
            }

            // Fallback: convert this object into a fragment
            DebugManager.LogGathering($"ResourcePiece: Creating local fragment for {visualKey} (no pool available)");
            return CreateLocalFragment(resourceType);
        }

        /// <summary>
        /// Convert this piece directly into a fragment (used when pooling isn't available).
        /// </summary>
        private ResourceFragment CreateLocalFragment(EResourceType resourceType)
        {
            // Detach from parent
            transform.SetParent(null);

            // Add physics
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.linearDamping = 0.5f;
            rb.angularDamping = 0.5f;

            // Add collider if missing
            if (GetComponent<Collider>() == null)
            {
                var col = gameObject.AddComponent<BoxCollider>();
                var mr = GetComponent<MeshRenderer>();

                if (mr != null)
                {
                    col.center = mr.bounds.center - transform.position;
                    col.size = mr.bounds.size;
                }
            }

            // Add fragment component
            var fragment = gameObject.AddComponent<ResourceFragment>();
            fragment.SetVisualKey(VisualKey.ForResource(resourceType, variantId, pieceType));
            fragment.Setup(resourceType, resourceValue, transform.position);
            fragment.Launch();

            return fragment;
        }

        /// <summary>
        /// Check if this piece can be placed at a given index in a structure.
        /// </summary>
        public bool CanPlaceAt(int index, int totalPieces)
        {
            EnsureParsed();
            return PrefabNamingUtility.CanPlaceAt(pieceType, index, totalPieces);
        }

        /// <summary>
        /// Reset this piece for reuse (e.g., when resource is respawned).
        /// </summary>
        public void Reset()
        {
            isDetached = false;
            gameObject.SetActive(true);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Parse name in editor for preview
            if (!isParsed && !string.IsNullOrEmpty(gameObject.name))
            {
                PrefabNamingUtility.Parse(gameObject.name, out variantId, out pieceType);
            }
        }

        [ContextMenu("Re-parse Name")]
        private void ReParseName()
        {
            isParsed = false;
            EnsureParsed();
            DebugManager.LogSpawning($"Parsed: Variant={variantId}, Type={pieceType}");
        }
#endif
    }
}