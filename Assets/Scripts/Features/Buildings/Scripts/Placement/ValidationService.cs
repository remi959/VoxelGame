// ============================================================================
// ValidationService.cs - Pure validation logic, no side effects
// ============================================================================
using System.Collections.Generic;
using Assets.Scripts.Core.Services;
using Assets.Scripts.Data;
using Assets.Scripts.Shared;  // For EntityRegistry
using Assets.Scripts.Shared.Utilities;  // For SpatialHash
using UnityEngine;
using UnityEngine.AI;

namespace Assets.Scripts.Buildings.Placement
{
    /// <summary>
    /// Pure validation service for building placement.
    /// 
    /// Design principles:
    /// - All methods are pure functions (no side effects)
    /// - Can be used by AI, human players, and replay validation
    /// - Returns detailed results for UI feedback
    /// - Does NOT check resources (that's EconomyService's job)
    /// 
    /// Performance:
    /// - Uses SpatialHash for building distance checks (no physics queries)
    /// - Physics.CheckBox only used for obstacle layer overlap
    /// 
    /// PERSISTENCE:
    /// This component should be placed as a child of the [Services] GameObject.
    /// PersistentServices handles DontDestroyOnLoad for the entire hierarchy.
    /// </summary>
    public class ValidationService : MonoBehaviour
    {
        public static ValidationService Instance { get; private set; }

        [Header("Layers")]
        [SerializeField] private LayerMask groundLayer;
        [SerializeField] private LayerMask obstacleLayer;

        [Header("Default Settings")]
        [SerializeField] private float defaultMaxSlope = 30f;
        [SerializeField] private float navMeshSampleDistance = 2f;
        [SerializeField] private float groundCheckHeight = 10f;

        [Header("Spatial Hash Settings")]
        [SerializeField] private float spatialHashCellSize = 20f;

        // Spatial hash for building blueprints (updated by BuildingBlueprint)
        private static SpatialHash<BuildingBlueprint> buildingSpatialHash;
        
        /// <summary>
        /// Get the shared building spatial hash.
        /// Buildings register/unregister themselves.
        /// </summary>
        public static SpatialHash<BuildingBlueprint> BuildingSpatialHash
        {
            get
            {
                if (buildingSpatialHash == null)
                {
                    // Create with reasonable cell size for buildings (20m default)
                    float cellSize = Instance != null ? Instance.spatialHashCellSize : 20f;
                    buildingSpatialHash = new SpatialHash<BuildingBlueprint>(cellSize);
                }
                return buildingSpatialHash;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            ServiceLocator.Register(this);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                ServiceLocator.Unregister<ValidationService>();
            }
        }

        /// <summary>
        /// Perform full validation of a building placement.
        /// </summary>
        /// <param name="position">World position to validate</param>
        /// <param name="rotation">Building rotation</param>
        /// <param name="definition">Building definition</param>
        /// <returns>Validation result with all errors</returns>
        public ValidationResult Validate(Vector3 position, Quaternion rotation, BuildingDefinitionSO definition)
        {
            if (definition == null || !definition.IsValid)
            {
                return ValidationResult.Failure("Invalid building definition");
            }

            var errors = new List<string>();

            // 1. Ground detection
            if (!ValidateGroundExists(position, out string groundError))
            {
                errors.Add(groundError);
            }

            // 2. Terrain slope
            float maxSlope = definition.maxTerrainSlope > 0 ? definition.maxTerrainSlope : defaultMaxSlope;
            if (!ValidateSlope(position, maxSlope, out string slopeError))
            {
                errors.Add(slopeError);
            }

            // 3. Collision/overlap
            if (!ValidateNoOverlap(position, rotation, definition, out string overlapError))
            {
                errors.Add(overlapError);
            }

            // 4. NavMesh accessibility (if required)
            if (definition.requiresNavMesh)
            {
                if (!ValidateNavMesh(position, out string navError))
                {
                    errors.Add(navError);
                }
            }

            // 5. Terrain type (if restricted)
            if (definition.allowedTerrain != null && definition.allowedTerrain.Length > 0)
            {
                if (!ValidateTerrainType(position, definition.allowedTerrain, out string terrainError))
                {
                    errors.Add(terrainError);
                }
            }

            // 6. Distance from other buildings (if required)
            if (definition.minDistanceFromBuildings > 0)
            {
                if (!ValidateDistanceFromBuildings(position, definition.minDistanceFromBuildings, out string distError))
                {
                    errors.Add(distError);
                }
            }

            if (errors.Count > 0)
            {
                return ValidationResult.Failure(errors);
            }

            return ValidationResult.Success();
        }

        /// <summary>
        /// Quick check without detailed error messages.
        /// Use for frequent polling (e.g., preview color).
        /// </summary>
        public bool IsValid(Vector3 position, Quaternion rotation, BuildingDefinitionSO definition)
        {
            return Validate(position, rotation, definition).IsValid;
        }

        // ========== Individual Validation Methods ==========

        private bool ValidateGroundExists(Vector3 position, out string error)
        {
            error = null;
            Vector3 rayOrigin = position + Vector3.up * groundCheckHeight;

            if (!Physics.Raycast(rayOrigin, Vector3.down, out _, groundCheckHeight * 2f, groundLayer))
            {
                error = "No ground detected";
                return false;
            }

            return true;
        }

        private bool ValidateSlope(Vector3 position, float maxAngle, out string error)
        {
            error = null;
            Vector3 rayOrigin = position + Vector3.up * groundCheckHeight;

            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, groundCheckHeight * 2f, groundLayer))
            {
                float angle = Vector3.Angle(hit.normal, Vector3.up);
                if (angle > maxAngle)
                {
                    error = $"Terrain too steep ({angle:F1}° > {maxAngle}°)";
                    return false;
                }
            }

            return true;
        }

        private bool ValidateNoOverlap(Vector3 position, Quaternion rotation, BuildingDefinitionSO definition, out string error)
        {
            error = null;

            // Use settings if available, otherwise use definition values
            float height = definition.height > 0 ? definition.height : 2f;

            Vector3 halfExtents = new(
                definition.Footprint.x / 2f,
                height / 2f,
                definition.Footprint.y / 2f
            );

            Vector3 center = position + Vector3.up * (height / 2f);

            if (Physics.CheckBox(center, halfExtents, rotation, obstacleLayer))
            {
                error = "Overlaps existing objects";
                return false;
            }

            return true;
        }

        private bool ValidateNavMesh(Vector3 position, out string error)
        {
            error = null;

            if (!NavMesh.SamplePosition(position, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
            {
                error = "Not on navigable terrain";
                return false;
            }

            float distance = Vector3.Distance(position, hit.position);
            if (distance > navMeshSampleDistance * 0.5f)
            {
                error = "Too far from navigable area";
                return false;
            }

            return true;
        }

        private bool ValidateTerrainType(Vector3 position, string[] allowedTypes, out string error)
        {
            error = null;

            // TODO: Implement terrain type detection based on your terrain system
            // This could use:
            // - Terrain texture splatmap
            // - Physics materials
            // - Custom terrain zone system

            // For now, always pass
            return true;
        }

        private bool ValidateDistanceFromBuildings(Vector3 position, float minDistance, out string error)
        {
            error = null;

            // Check distance from existing building sites using SpatialHash
            // Much faster than EntityRegistry.FindNearest for dense areas
            if (BuildingSpatialHash.AnyWithinRadius(position, minDistance))
            {
                // Get actual nearest for error message
                var nearest = BuildingSpatialHash.FindNearest(position, out float distance, minDistance);
                if (nearest != null)
                {
                    error = $"Too close to another building ({distance:F1}m < {minDistance}m required)";
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Result of a placement validation.
    /// </summary>
    public struct ValidationResult
    {
        public bool IsValid;
        public List<string> Errors;

        public static ValidationResult Success()
        {
            return new ValidationResult
            {
                IsValid = true,
                Errors = new List<string>()
            };
        }

        public static ValidationResult Failure(string error)
        {
            return new ValidationResult
            {
                IsValid = false,
                Errors = new List<string> { error }
            };
        }

        public static ValidationResult Failure(List<string> errors)
        {
            return new ValidationResult
            {
                IsValid = false,
                Errors = errors
            };
        }
    }
}