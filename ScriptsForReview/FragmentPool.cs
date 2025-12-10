using System.Collections.Generic;
using Assets.Scripts.Core;
using Assets.Scripts.Shared.Enums;
using Assets.Scripts.Shared.Pooling;
using Assets.Scripts.Shared.Utilities;
using UnityEngine;

namespace Assets.Scripts.Resources
{
    public class FragmentPool : MonoBehaviour
    {
        public static FragmentPool Instance { get; private set; }

        [Header("Pool Settings")]
        [SerializeField] private int initialPoolSizePerVariant = 5;
        [SerializeField] private int maxPoolSizePerVariant = 20;

        [Header("Fragment Prefabs")]
        [Tooltip("Add prefabs here. Variant ID and piece type are auto-parsed from prefab names.")]
        [SerializeField] private List<FragmentPrefabEntry> fragmentPrefabs = new();

        [System.Serializable]
        public class FragmentPrefabEntry
        {
            [Tooltip("The resource type this fragment represents")]
            public EResourceType resourceType;

            [Tooltip("The prefab to instantiate. Naming: '{Variant}{PieceType}Piece' (e.g., 'OakTrunkPiece')")]
            public GameObject prefab;

            [Tooltip("If true, fragments of this type won't be pooled (always create/destroy)")]
            public bool skipPooling = false;

            // Cached parsed values
            [HideInInspector] public string variantId;
            [HideInInspector] public EPieceType pieceType;

            public VisualKey GetKey()
            {
                // Parse from prefab name if not already cached
                if (string.IsNullOrEmpty(variantId) && prefab != null)
                {
                    PrefabNamingUtility.Parse(prefab.name, out variantId, out pieceType);
                }
                return VisualKey.ForResource(resourceType, variantId, pieceType);
            }
        }

        // Pools organized by visual key
        private readonly Dictionary<VisualKey, Queue<ResourceFragment>> pools = new();
        private readonly Dictionary<VisualKey, FragmentPrefabEntry> prefabLookup = new();
        private readonly HashSet<ResourceFragment> activeFragments = new();

        private Transform poolContainer;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            InitializePools();
        }

        private void InitializePools()
        {
            // Create hidden container for pooled objects
            poolContainer = new GameObject("PooledFragments").transform;
            poolContainer.SetParent(transform);
            poolContainer.gameObject.SetActive(false);

            // Build prefab lookup and pre-warm pools
            foreach (var entry in fragmentPrefabs)
            {
                if (entry.prefab == null)
                {
                    DebugManager.LogWarning($"FragmentPool: Null prefab entry for {entry.resourceType}");
                    continue;
                }

                // Parse variant and piece type from prefab name
                PrefabNamingUtility.Parse(entry.prefab.name, out entry.variantId, out entry.pieceType);

                var key = entry.GetKey();

                if (prefabLookup.ContainsKey(key))
                {
                    DebugManager.LogWarning($"FragmentPool: Duplicate key {key}, skipping");
                    continue;
                }

                prefabLookup[key] = entry;

                DebugManager.LogPooling($"FragmentPool: Registered {entry.prefab.name} → {key}");

                // Pre-warm pools for non-skipped variants
                if (!entry.skipPooling)
                {
                    pools[key] = new Queue<ResourceFragment>();
                    PrewarmPool(key, entry, initialPoolSizePerVariant);
                }
            }

            DebugManager.LogPooling($"FragmentPool: Initialized {prefabLookup.Count} variants, pre-warmed {pools.Count} pools");
        }

        private void PrewarmPool(VisualKey key, FragmentPrefabEntry entry, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var fragment = CreateNewFragment(key, entry);
                if (fragment != null)
                {
                    ReturnToPoolInternal(fragment, key);
                }
            }
        }

        private ResourceFragment CreateNewFragment(VisualKey key, FragmentPrefabEntry entry)
        {
            if (entry.prefab == null) return null;

            GameObject obj = Instantiate(entry.prefab, poolContainer);
            obj.name = $"Fragment_{key}";

            var fragment = obj.GetComponent<ResourceFragment>();
            if (fragment == null)
            {
                fragment = obj.AddComponent<ResourceFragment>();
            }

            // Store the visual key on the fragment
            fragment.SetVisualKey(key);

            return fragment;
        }

        /// <summary>
        /// Get a fragment from the pool with a specific visual variant.
        /// Will try fallbacks if exact match not found.
        /// </summary>
        public ResourceFragment Get(VisualKey key, Vector3 position, int value)
        {
            FragmentPrefabEntry entry = null;

            // Try exact match first
            if (prefabLookup.TryGetValue(key, out entry))
            {
                return GetOrCreateFragment(key, entry, position, value);
            }

            // Fallback 1: Try without piece type (generic variant)
            var fallbackKey = key.WithoutPieceType();
            if (prefabLookup.TryGetValue(fallbackKey, out entry))
            {
                DebugManager.LogPooling($"FragmentPool: Using fallback (no piece type) {fallbackKey} for {key}");
                return GetOrCreateFragment(fallbackKey, entry, position, value);
            }

            // Fallback 2: Try default variant with same resource type
            var defaultKey = new VisualKey(key.Category, "default", EPieceType.Any);
            if (prefabLookup.TryGetValue(defaultKey, out entry))
            {
                DebugManager.LogPooling($"FragmentPool: Using default fallback {defaultKey} for {key}");
                return GetOrCreateFragment(defaultKey, entry, position, value);
            }

            DebugManager.LogWarning($"FragmentPool: No prefab found for {key} (tried fallbacks)");
            return null;
        }

        private ResourceFragment GetOrCreateFragment(VisualKey key, FragmentPrefabEntry entry, Vector3 position, int value)
        {
            // If this variant skips pooling, always create new
            if (entry.skipPooling)
            {
                return CreateAndActivateFragment(key, entry, position, value);
            }

            // Try to get from pool
            if (!pools.TryGetValue(key, out var pool))
            {
                pool = new Queue<ResourceFragment>();
                pools[key] = pool;
            }

            ResourceFragment fragment = null;

            // Find valid fragment in pool
            while (pool.Count > 0 && fragment == null)
            {
                var candidate = pool.Dequeue();
                if (candidate != null)
                {
                    fragment = candidate;
                }
            }

            // Create new if pool was empty
            if (fragment == null)
            {
                fragment = CreateNewFragment(key, entry);
            }

            if (fragment == null)
            {
                DebugManager.LogError($"FragmentPool: Failed to get or create fragment for {key}");
                return null;
            }

            // Activate and setup
            ActivateFragment(fragment, key, position, value);

            return fragment;
        }

        private ResourceFragment CreateAndActivateFragment(VisualKey key, FragmentPrefabEntry entry, Vector3 position, int value)
        {
            var fragment = CreateNewFragment(key, entry);

            if (fragment != null)
            {
                ActivateFragment(fragment, key, position, value);
            }

            return fragment;
        }

        private void ActivateFragment(ResourceFragment fragment, VisualKey key, Vector3 position, int value)
        {
            fragment.transform.SetParent(null);
            fragment.gameObject.SetActive(true);
            fragment.Setup(GetResourceTypeFromKey(key), value, position);
            fragment.SetVisualKey(key);

            activeFragments.Add(fragment);
        }

        /// <summary>
        /// Extract resource type from visual key category.
        /// </summary>
        private EResourceType GetResourceTypeFromKey(VisualKey key)
        {
            if (System.Enum.TryParse<EResourceType>(key.Category, out var result))
            {
                return result;
            }
            return EResourceType.Wood; // Default fallback
        }

        /// <summary>
        /// Convenience overload using resource type, variant, and piece type.
        /// </summary>
        public ResourceFragment Get(EResourceType type, string variantId, EPieceType pieceType, Vector3 position, int value)
        {
            return Get(VisualKey.ForResource(type, variantId, pieceType), position, value);
        }

        /// <summary>
        /// Convenience overload using resource type and variant (any piece type).
        /// </summary>
        public ResourceFragment Get(EResourceType type, string variantId, Vector3 position, int value)
        {
            return Get(VisualKey.ForResource(type, variantId, EPieceType.Any), position, value);
        }

        /// <summary>
        /// Return a fragment to the pool.
        /// </summary>
        public void Return(ResourceFragment fragment)
        {
            if (fragment == null) return;

            activeFragments.Remove(fragment);

            var key = fragment.VisualKey;

            // Check if this variant skips pooling
            if (prefabLookup.TryGetValue(key, out var entry) && entry.skipPooling)
            {
                Destroy(fragment.gameObject);
                return;
            }

            ReturnToPoolInternal(fragment, key);
        }

        private void ReturnToPoolInternal(ResourceFragment fragment, VisualKey key)
        {
            if (fragment == null) return;

            if (!pools.TryGetValue(key, out var pool))
            {
                pool = new Queue<ResourceFragment>();
                pools[key] = pool;
            }

            // Check pool size limit
            if (pool.Count >= maxPoolSizePerVariant)
            {
                Destroy(fragment.gameObject);
                return;
            }

            fragment.ResetForPool();
            fragment.transform.SetParent(poolContainer);
            fragment.gameObject.SetActive(false);

            pool.Enqueue(fragment);
        }

        /// <summary>
        /// Check if a variant has available pooled fragments.
        /// </summary>
        public bool HasAvailable(VisualKey key)
        {
            return pools.TryGetValue(key, out var pool) && pool.Count > 0;
        }

        /// <summary>
        /// Check if a variant is registered in the pool (exact match).
        /// </summary>
        public bool HasVariant(VisualKey key)
        {
            return prefabLookup.ContainsKey(key);
        }

        /// <summary>
        /// Check if a variant or its fallbacks are registered.
        /// </summary>
        public bool HasVariantOrFallback(VisualKey key)
        {
            if (prefabLookup.ContainsKey(key)) return true;
            if (prefabLookup.ContainsKey(key.WithoutPieceType())) return true;
            if (prefabLookup.ContainsKey(new VisualKey(key.Category, "default", EPieceType.Any))) return true;
            return false;
        }

        /// <summary>
        /// Get the count of active (in-use) fragments.
        /// </summary>
        public int ActiveCount => activeFragments.Count;

        /// <summary>
        /// Get pool statistics for debugging.
        /// </summary>
        public Dictionary<VisualKey, int> GetPoolStats()
        {
            var stats = new Dictionary<VisualKey, int>();

            foreach (var kvp in pools)
            {
                stats[kvp.Key] = kvp.Value.Count;
            }

            return stats;
        }

        /// <summary>
        /// Return all active fragments to the pool.
        /// </summary>
        public void ReturnAllActive()
        {
            var fragmentsToReturn = new List<ResourceFragment>(activeFragments);

            foreach (var fragment in fragmentsToReturn)
            {
                if (fragment != null)
                {
                    Return(fragment);
                }
            }

            activeFragments.Clear();
        }

        /// <summary>
        /// Clear all pools and destroy all fragments.
        /// </summary>
        public void ClearAllPools()
        {
            ReturnAllActive();

            foreach (var pool in pools.Values)
            {
                while (pool.Count > 0)
                {
                    var fragment = pool.Dequeue();
                    if (fragment != null)
                    {
                        Destroy(fragment.gameObject);
                    }
                }
            }

            pools.Clear();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Log Pool Stats")]
        private void LogPoolStats()
        {
            Debug.Log($"=== FragmentPool Stats ===");
            Debug.Log($"Active fragments: {activeFragments.Count}");
            Debug.Log($"Registered variants: {prefabLookup.Count}");

            foreach (var kvp in prefabLookup)
            {
                var pooled = pools.TryGetValue(kvp.Key, out var pool) ? pool.Count : 0;
                Debug.Log($"  {kvp.Key}: {pooled} pooled, skip={kvp.Value.skipPooling}");
            }
        }

        [ContextMenu("Refresh Prefab Parsing")]
        private void RefreshPrefabParsing()
        {
            foreach (var entry in fragmentPrefabs)
            {
                if (entry.prefab != null)
                {
                    PrefabNamingUtility.Parse(entry.prefab.name, out entry.variantId, out entry.pieceType);
                    Debug.Log($"Parsed {entry.prefab.name}: variant={entry.variantId}, type={entry.pieceType}");
                }
            }
        }
#endif
    }
}