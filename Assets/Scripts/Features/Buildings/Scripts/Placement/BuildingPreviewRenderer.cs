// ============================================================================
// BuildingPreviewRenderer.cs - ONLY handles preview visuals
// ============================================================================
using Assets.Scripts.Core.Events;
using Assets.Scripts.Events;
using Assets.Scripts.Shared.Utilities;
using UnityEngine;

namespace Assets.Scripts.Buildings.Preview
{
    /// <summary>
    /// Renders the building preview during placement.
    /// 
    /// Responsibilities:
    /// - Create/destroy preview GameObject
    /// - Update position and rotation
    /// - Apply valid/invalid visual feedback
    /// 
    /// Does NOT:
    /// - Handle input
    /// - Validate placement
    /// - Know about resources
    /// - Create actual buildings
    /// </summary>
    public class BuildingPreviewRenderer : MonoBehaviour
    {
        [Header("Visual Settings")]
        [SerializeField] private Material validMaterial;
        [SerializeField] private Material invalidMaterial;
        [SerializeField] private Color validColor = new(0f, 1f, 0f, 0.5f);
        [SerializeField] private Color invalidColor = new(1f, 0f, 0f, 0.5f);

        [Header("Animation")]
        [SerializeField] private float positionSmoothTime = 0.05f;
        [SerializeField] private float rotationSmoothSpeed = 720f;

        // Current preview state
        private GameObject previewObject;
        private BuildingDefinitionSO currentDefinition;
        private bool isActive;
        private bool isCurrentlyValid;

        // Smoothing
        private Vector3 targetPosition;
        private Quaternion targetRotation;
        private Vector3 positionVelocity;

        // Cached materials for this preview
        private Material cachedValidMaterial;
        private Material cachedInvalidMaterial;

        private void OnEnable()
        {
            EventBus.Subscribe<BuildModeStartedEvent>(OnBuildModeStarted);
            EventBus.Subscribe<BuildModeEndedEvent>(OnBuildModeEnded);
            EventBus.Subscribe<BuildingCursorMovedEvent>(OnCursorMoved);
            EventBus.Subscribe<RotateBuildingPreviewEvent>(OnRotate);
            EventBus.Subscribe<PlacementValidationResultEvent>(OnValidationResult);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<BuildModeStartedEvent>(OnBuildModeStarted);
            EventBus.Unsubscribe<BuildModeEndedEvent>(OnBuildModeEnded);
            EventBus.Unsubscribe<BuildingCursorMovedEvent>(OnCursorMoved);
            EventBus.Unsubscribe<RotateBuildingPreviewEvent>(OnRotate);
            EventBus.Unsubscribe<PlacementValidationResultEvent>(OnValidationResult);

            DestroyPreview();
        }

        private void Update()
        {
            if (!isActive || previewObject == null) return;

            // Smooth position interpolation
            previewObject.transform.position = Vector3.SmoothDamp(
                previewObject.transform.position,
                targetPosition,
                ref positionVelocity,
                positionSmoothTime
            );

            // Smooth rotation interpolation
            previewObject.transform.rotation = Quaternion.RotateTowards(
                previewObject.transform.rotation,
                targetRotation,
                rotationSmoothSpeed * Time.deltaTime
            );
        }

        // ========== Event Handlers ==========

        private void OnBuildModeStarted(BuildModeStartedEvent e)
        {
            CreatePreview(e.Definition);
        }

        private void OnBuildModeEnded(BuildModeEndedEvent e)
        {
            DestroyPreview();
        }

        private void OnCursorMoved(BuildingCursorMovedEvent e)
        {
            if (!isActive) return;

            targetPosition = e.WorldPosition;
            targetRotation = e.Rotation;
        }

        private void OnRotate(RotateBuildingPreviewEvent e)
        {
            if (!isActive) return;

            targetRotation *= Quaternion.Euler(0f, e.DeltaDegrees, 0f);
        }

        private void OnValidationResult(PlacementValidationResultEvent e)
        {
            if (!isActive) return;

            SetValid(e.IsValid);
        }

        // ========== Preview Management ==========

        private void CreatePreview(BuildingDefinitionSO definition)
        {
            DestroyPreview();

            if (definition == null || definition.constructionPrefab == null)
            {
                Debug.LogWarning("BuildingPreviewRenderer: Invalid definition");
                return;
            }

            currentDefinition = definition;

            // Instantiate the construction prefab as preview
            previewObject = Instantiate(definition.constructionPrefab);
            previewObject.name = $"Preview_{definition.displayName}";

            // Disable all gameplay components on preview
            DisableGameplayComponents(previewObject);

            // Create preview materials
            CreatePreviewMaterials();

            // Apply initial material (invalid until validated)
            ApplyMaterial(cachedInvalidMaterial);
            isCurrentlyValid = false;

            // Initialize position
            targetPosition = Vector3.zero;
            targetRotation = Quaternion.identity;
            previewObject.transform.position = targetPosition;
            previewObject.transform.rotation = targetRotation;

            isActive = true;
        }

        private void DestroyPreview()
        {
            isActive = false;

            if (previewObject != null)
            {
                Destroy(previewObject);
                previewObject = null;
            }

            // Clean up materials
            if (cachedValidMaterial != null)
            {
                Destroy(cachedValidMaterial);
                cachedValidMaterial = null;
            }
            if (cachedInvalidMaterial != null)
            {
                Destroy(cachedInvalidMaterial);
                cachedInvalidMaterial = null;
            }

            currentDefinition = null;
        }

        private void SetValid(bool valid)
        {
            if (!isActive || previewObject == null) return;
            if (isCurrentlyValid == valid) return;  // No change

            isCurrentlyValid = valid;
            ApplyMaterial(valid ? cachedValidMaterial : cachedInvalidMaterial);
        }

        // ========== Material Management ==========

        private void CreatePreviewMaterials()
        {
            // Use GhostMaterialFactory for consistent material creation
            cachedValidMaterial = GhostMaterialFactory.CreateValidPreviewMaterial(validColor, validMaterial);
            cachedInvalidMaterial = GhostMaterialFactory.CreateInvalidPreviewMaterial(invalidColor, validMaterial);
        }

        private void ApplyMaterial(Material mat)
        {
            if (previewObject == null || mat == null) return;

            // Use factory utility for consistent application
            GhostMaterialFactory.ApplyToAllRenderers(previewObject, mat);
        }

        // ========== Cleanup ==========

        private void DisableGameplayComponents(GameObject obj)
        {
            // Disable all colliders
            foreach (var col in obj.GetComponentsInChildren<Collider>())
            {
                col.enabled = false;
            }

            // Destroy gameplay components
            foreach (var part in obj.GetComponentsInChildren<BuildingPart>())
            {
                Destroy(part);
            }

            var holder = obj.GetComponent<BuildingHolder>();
            if (holder != null) Destroy(holder);

            // Disable any NavMeshObstacles
            foreach (var obstacle in obj.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>())
            {
                obstacle.enabled = false;
            }

            // Disable any Rigidbodies
            foreach (var rb in obj.GetComponentsInChildren<Rigidbody>())
            {
                rb.isKinematic = true;
            }
        }
    }
}