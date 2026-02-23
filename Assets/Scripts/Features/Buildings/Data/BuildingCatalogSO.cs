// ============================================================================
// BuildingCatalogSO.cs - Registry of all available building definitions
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Buildings
{
    /// <summary>
    /// Scriptable Object that serves as the registry/catalog of all available buildings.
    /// This is the single source of truth for what buildings exist in the game.
    /// 
    /// Design Notes:
    /// - Buildings can be manually assigned in the Inspector
    /// - Editor utility can auto-populate from project assets
    /// - UI systems query this catalog to display available buildings
    /// - Does NOT know about UI, placement, or construction systems
    /// 
    /// Usage:
    /// 1. Create asset via Assets/Create/Buildings/Building Catalog
    /// 2. Assign building definitions to the list
    /// 3. Reference from GameUIManager or other systems that need building data
    /// </summary>
    [CreateAssetMenu(fileName = "BuildingCatalog", menuName = "Buildings/Building Catalog")]
    public class BuildingCatalogSO : ScriptableObject
    {
        [Header("Building Definitions")]
        [Tooltip("All available building definitions. Order determines display order in UI.")]
        [SerializeField] private List<BuildingDefinitionSO> buildings = new();

        /// <summary>
        /// Read-only access to all building definitions.
        /// </summary>
        public IReadOnlyList<BuildingDefinitionSO> Buildings => buildings;

        /// <summary>
        /// Number of buildings in the catalog.
        /// </summary>
        public int Count => buildings.Count;

        /// <summary>
        /// Get a building definition by index.
        /// </summary>
        public BuildingDefinitionSO this[int index] => buildings[index];

        /// <summary>
        /// Get a building definition by its unique ID.
        /// </summary>
        /// <param name="buildingId">The unique building ID</param>
        /// <returns>The building definition, or null if not found</returns>
        public BuildingDefinitionSO GetById(string buildingId)
        {
            if (string.IsNullOrEmpty(buildingId)) return null;
            
            foreach (var building in buildings)
            {
                if (building != null && building.buildingId == buildingId)
                    return building;
            }
            return null;
        }

        /// <summary>
        /// Get all buildings in a specific category.
        /// </summary>
        /// <param name="category">The category to filter by</param>
        /// <returns>List of buildings in that category</returns>
        public List<BuildingDefinitionSO> GetByCategory(string category)
        {
            var result = new List<BuildingDefinitionSO>();
            foreach (var building in buildings)
            {
                if (building != null && building.category == category)
                    result.Add(building);
            }
            return result;
        }

        /// <summary>
        /// Get all unique categories in the catalog.
        /// </summary>
        /// <returns>List of unique category names</returns>
        public List<string> GetCategories()
        {
            var categories = new HashSet<string>();
            foreach (var building in buildings)
            {
                if (building != null && !string.IsNullOrEmpty(building.category))
                    categories.Add(building.category);
            }
            return new List<string>(categories);
        }

        /// <summary>
        /// Check if a building ID exists in the catalog.
        /// </summary>
        public bool Contains(string buildingId)
        {
            return GetById(buildingId) != null;
        }

        /// <summary>
        /// Get all valid buildings (those with valid construction prefabs).
        /// </summary>
        public List<BuildingDefinitionSO> GetValidBuildings()
        {
            var result = new List<BuildingDefinitionSO>();
            foreach (var building in buildings)
            {
                if (building != null && building.IsValid)
                    result.Add(building);
            }
            return result;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor utility: Find all BuildingDefinitionSO assets in the project.
        /// </summary>
        [ContextMenu("Auto-populate from Project")]
        private void AutoPopulateFromProject()
        {
            buildings.Clear();
            
            // Find all BuildingDefinitionSO assets
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:BuildingDefinitionSO");
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var definition = UnityEditor.AssetDatabase.LoadAssetAtPath<BuildingDefinitionSO>(path);
                if (definition != null)
                {
                    buildings.Add(definition);
                }
            }
            
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"BuildingCatalog: Found and added {buildings.Count} building definitions");
        }

        /// <summary>
        /// Editor utility: Validate all building references.
        /// </summary>
        [ContextMenu("Validate Buildings")]
        private void ValidateBuildings()
        {
            int validCount = 0;
            int invalidCount = 0;

            for (int i = 0; i < buildings.Count; i++)
            {
                var building = buildings[i];
                if (building == null)
                {
                    Debug.LogWarning($"BuildingCatalog: Null entry at index {i}");
                    invalidCount++;
                    continue;
                }

                if (!building.IsValid)
                {
                    Debug.LogWarning($"BuildingCatalog: Invalid building '{building.displayName}' - missing construction prefab");
                    invalidCount++;
                    continue;
                }

                validCount++;
            }

            Debug.Log($"BuildingCatalog: {validCount} valid, {invalidCount} invalid buildings");
        }

        private void OnValidate()
        {
            // Remove null entries
            buildings.RemoveAll(b => b == null);
        }
#endif
    }
}
