using UnityEngine;

namespace Assets.Scripts.Data
{
    /// <summary>
    /// Configuration for building system settings.
    /// Centralizes magic numbers for designer-friendly editing.
    /// </summary>
    [CreateAssetMenu(fileName = "BuildingSettings", menuName = "Config/Building Settings")]
    public class BuildingSettingsSO : ScriptableObject
    {
        [Header("Placement - Raycast")]
        [Tooltip("Maximum raycast distance for placement")]
        public float MaxRaycastDistance = 1000f;

        [Tooltip("Layer mask for ground detection during placement")]
        public LayerMask GroundLayer;

        [Tooltip("Layer mask for obstacle detection during validation")]
        public LayerMask ObstacleLayer;

        [Header("Placement - Rotation")]
        [Tooltip("Degrees to rotate building per scroll/key press")]
        public float RotationStep = 45f;

        [Tooltip("Rotation speed for smooth rotation (unused if using step rotation)")]
        public float RotationSpeed = 90f;

        [Header("Placement - Validation")]
        [Tooltip("Maximum terrain slope angle in degrees for valid placement")]
        public float MaxSlopeAngle = 30f;

        [Tooltip("Minimum distance from edge of NavMesh for valid placement")]
        public float MinNavMeshDistance = 0.5f;

        [Tooltip("NavMesh sample distance for validity check")]
        public float NavMeshSampleDistance = 2f;

        [Tooltip("Height offset for collision box check")]
        public float CollisionCheckHeightOffset = 1f;

        [Tooltip("Height of collision check box")]
        public float CollisionCheckHeight = 2f;

        [Header("Construction")]
        [Tooltip("Default maximum workers per building")]
        public int DefaultMaxWorkers = 4;

        [Header("NavMesh Obstacle")]
        [Tooltip("Height of NavMesh obstacle when building completes")]
        public float NavMeshObstacleHeight = 3f;

        [Tooltip("Time before NavMesh obstacle starts carving after building stops moving")]
        public float CarvingTimeToStationary = 0.5f;

        [Tooltip("Movement threshold for NavMesh carving")]
        public float CarvingMoveThreshold = 0.1f;

        [Header("Preview")]
        [Tooltip("Valid placement color (green with alpha)")]
        public Color ValidPlacementColor = new(0f, 1f, 0f, 0.5f);

        [Tooltip("Invalid placement color (red with alpha)")]
        public Color InvalidPlacementColor = new(1f, 0f, 0f, 0.5f);

        [Tooltip("Ghost/blueprint color")]
        public Color GhostColor = new(0.5f, 0.5f, 1f, 0.3f);

        #region Singleton Instance

        private static BuildingSettingsSO _instance;

        /// <summary>
        /// Runtime instance loaded from Resources folder.
        /// Create a BuildingSettings asset in Resources/Config/ folder.
        /// </summary>
        public static BuildingSettingsSO Instance  
        {
            get
            {
                if (_instance == null)
                {
                    _instance = UnityEngine.Resources.Load<BuildingSettingsSO>("Config/BuildingSettings");
                    if (_instance == null)
                    {
                        Debug.LogWarning("BuildingSettingsSO not found in Resources/Config/. Using defaults.");
                        _instance = CreateInstance<BuildingSettingsSO>();
                    }
                }
                return _instance;
            }
        }

        #endregion
    }
}
