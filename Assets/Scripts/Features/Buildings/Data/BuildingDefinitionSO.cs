// ============================================================================
// BuildingDefinitionSO.cs - Complete building data definition
// Single source of truth for building configuration
// ============================================================================
using System.Collections.Generic;
using Assets.Scripts.Shared.Enums;
using UnityEngine;

namespace Assets.Scripts.Buildings
{
    /// <summary>
    /// Complete definition of a building type.
    /// This is the SINGLE SOURCE OF TRUTH for building data.
    /// 
    /// Resource costs and parts are sourced from the BuildingHolder on the constructionPrefab.
    /// This avoids data duplication between the ScriptableObject and the prefab.
    /// </summary>
    [CreateAssetMenu(fileName = "BuildingDefinition", menuName = "Buildings/Building Definition")]
    public class BuildingDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique identifier for saves and networking")]
        public string buildingId;
        
        [Tooltip("Display name shown to players")]
        public string displayName;
        
        [Tooltip("Description for tooltips")]
        [TextArea(2, 4)]
        public string description;
        
        [Tooltip("Icon for UI")]
        public Sprite icon;
        
        [Tooltip("Category for build menu organization")]
        public string category = "General";
        
        [Header("Prefabs")]
        [Tooltip("Prefab with BuildingHolder for construction (contains BuildingPart children)")]
        public GameObject constructionPrefab;
        
        [Tooltip("Prefab spawned when construction completes (optional, uses construction prefab if null)")]
        public GameObject completedPrefab;
        
        [Header("Footprint")]
        [Tooltip("Grid size in tiles (auto-synced from BuildingHolder on construction prefab)")]
        [SerializeField] private Vector2Int footprint = new(2, 2);
        
        [Tooltip("Height for collision checks")]
        public float height = 3f;
        
        [Header("Construction")]
        [Tooltip("Maximum workers that can build simultaneously")]
        public int maxWorkers = 4;
        
        [Header("Placement Rules")]
        [Tooltip("Maximum terrain slope in degrees")]
        public float maxTerrainSlope = 30f;
        
        [Tooltip("Requires position on NavMesh")]
        public bool requiresNavMesh = true;
        
        [Tooltip("Minimum distance from other buildings")]
        public float minDistanceFromBuildings = 0f;
        
        [Tooltip("Required terrain types (empty = any)")]
        public string[] allowedTerrain;
        
        [Header("Prerequisites")]
        [Tooltip("Buildings that must exist before this can be built")]
        public BuildingDefinitionSO[] requiredBuildings;
        
        // ========== Cached References ==========

        private BuildingHolder cachedHolder;
        private Dictionary<EResourceType, int> _cachedTotalCosts;
        
        /// <summary>
        /// Cached reference to the BuildingHolder component on constructionPrefab.
        /// </summary>
        public BuildingHolder Holder
        {
            get
            {
                if (cachedHolder == null && constructionPrefab != null)
                {
                    cachedHolder = constructionPrefab.GetComponent<BuildingHolder>();
                }
                return cachedHolder;
            }
        }
        
        // ========== Computed Properties ==========
        
        /// <summary>
        /// Is this definition valid and ready to use?
        /// </summary>
        public bool IsValid => constructionPrefab != null;
        
        /// <summary>
        /// Get the building name (from displayName or fallback to asset name).
        /// </summary>
        public string BuildingName => !string.IsNullOrEmpty(displayName) ? displayName : name;
        
        /// <summary>
        /// Get footprint from BuildingHolder (single source of truth).
        /// Falls back to cached value if Holder is unavailable.
        /// </summary>
        public Vector2Int Footprint
        {
            get
            {
                // Always prefer the BuildingHolder's footprint as the source of truth
                if (Holder != null)
                {
                    return Holder.Footprint;
                }
                return footprint;
            }
        }
        
        // ========== Resource Methods ==========
        
        /// <summary>
        /// Get total resource costs as a dictionary.
        /// Costs are calculated from BuildingParts on the constructionPrefab.
        /// Result is cached; clear cache via OnValidate when the prefab changes.
        /// </summary>
        public Dictionary<EResourceType, int> GetTotalCosts()
        {
            if (_cachedTotalCosts != null) return _cachedTotalCosts;
            _cachedTotalCosts = Holder?.GetTotalCosts() ?? new Dictionary<EResourceType, int>();
            return _cachedTotalCosts;
        }
        
        /// <summary>
        /// Check if a specific resource type is required.
        /// </summary>
        public bool RequiresResource(EResourceType type)
        {
            var costs = GetTotalCosts();
            return costs.ContainsKey(type) && costs[type] > 0;
        }
        
        /// <summary>
        /// Get the cost for a specific resource type.
        /// </summary>
        public int GetCostFor(EResourceType type)
        {
            var costs = GetTotalCosts();
            return costs.TryGetValue(type, out int amount) ? amount : 0;
        }
        
        // ========== Part Methods ==========
        
        /// <summary>
        /// Get all BuildingPart children sorted by Y position (bottom-up).
        /// </summary>
        public List<BuildingPart> GetPartsSorted()
        {
            return Holder?.GetPartsSorted() ?? new List<BuildingPart>();
        }
        
        /// <summary>
        /// Get total number of parts.
        /// </summary>
        public int GetPartCount()
        {
            return Holder?.GetPartsSorted().Count ?? 0;
        }
        
        // ========== Construction Time ==========
        
        /// <summary>
        /// Get total build time based on parts.
        /// </summary>
        public float GetTotalBuildTime()
        {
            return Holder?.GetTotalConstructionTime() ?? 0f;
        }
        
        // ========== Prerequisite Checking ==========
        
        /// <summary>
        /// Check if all prerequisite buildings exist for a player.
        /// </summary>
        /// <param name="playerBuildingChecker">Function that returns true if player has the building</param>
        public bool ArePrerequisitesMet(System.Func<BuildingDefinitionSO, bool> playerBuildingChecker)
        {
            if (requiredBuildings == null || requiredBuildings.Length == 0)
                return true;
            
            foreach (var required in requiredBuildings)
            {
                if (required != null && !playerBuildingChecker(required))
                    return false;
            }
            
            return true;
        }
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            // Auto-generate buildingId from name if empty
            if (string.IsNullOrEmpty(buildingId))
            {
                buildingId = name.ToLowerInvariant().Replace(" ", "_");
            }
            
            // Auto-set displayName from name if empty
            if (string.IsNullOrEmpty(displayName))
            {
                displayName = name;
            }
            
            // Clear cached references when prefab changes
            cachedHolder = null;
            _cachedTotalCosts = null;
            
            // Validate construction prefab has BuildingHolder
            if (constructionPrefab != null && constructionPrefab.GetComponent<BuildingHolder>() == null)
            {
                Debug.LogWarning($"BuildingDefinitionSO '{name}': constructionPrefab is missing BuildingHolder component!");
            }
            
            // Always sync footprint from BuildingHolder (single source of truth)
            if (Holder != null)
            {
                footprint = Holder.Footprint;
            }
        }
        
        /// <summary>
        /// Force refresh footprint from the BuildingHolder.
        /// </summary>
        [ContextMenu("Sync Footprint from Prefab")]
        private void SyncFootprintFromPrefab()
        {
            if (Holder != null)
            {
                footprint = Holder.Footprint;
                UnityEditor.EditorUtility.SetDirty(this);
                Debug.Log($"BuildingDefinitionSO '{name}': Synced footprint to {footprint}");
            }
            else
            {
                Debug.LogWarning($"BuildingDefinitionSO '{name}': No BuildingHolder found on construction prefab");
            }
        }
        
        [ContextMenu("Log Building Info")]
        private void LogBuildingInfo()
        {
            Debug.Log($"=== {displayName} ({buildingId}) ===");
            Debug.Log($"Footprint: {Footprint}");
            Debug.Log($"Max Workers: {maxWorkers}");
            Debug.Log($"Parts: {GetPartCount()}");
            Debug.Log($"Build Time: {GetTotalBuildTime():F1}s");
            
            var costs = GetTotalCosts();
            if (costs.Count > 0)
            {
                Debug.Log("Costs:");
                foreach (var kvp in costs)
                {
                    Debug.Log($"  {kvp.Key}: {kvp.Value}");
                }
            }
        }
#endif
    }
}