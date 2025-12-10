using Assets.Scripts.Core;
using Assets.Scripts.Data.Resources;
using Assets.Scripts.Resources;
using Assets.Scripts.Shared.Utilities;
using UnityEngine;

namespace Assets.Scripts.Generation.Resources
{
    [CreateAssetMenu(fileName = "OreInstructions", menuName = "Resources/Instructions/Ore Instructions")]
    public class OreInstructions : ResourceGenerationInstruction
    {
        [Header("Ore-Specific Settings")]
        [SerializeField] private float groundCheckHeight = 10f;
        [SerializeField] private LayerMask groundLayer;

        [Header("Vein Layout Settings")]
        [SerializeField] private float pieceSpacing = 1f;

        [Header("Pattern Settings")]
        [Tooltip("If true, slightly randomize piece positions for more natural look")]
        [SerializeField] private bool randomizePositions = true;
        [SerializeField] private float positionRandomness = 0.1f;

        [Tooltip("If true, slightly randomize piece rotations")]
        [SerializeField] private bool randomizeRotations = true;
        [SerializeField] private float rotationRandomness = 15f;

        // Base pattern for vein pieces (4 on bottom layer forming a diamond-ish shape)
        private static readonly Vector3[] BaseLayerPattern = new Vector3[]
        {
            new(0f, 0f, 0f),       // Center-back
            new(1f, 0f, 0f),       // Center-front
            new(0.5f, 0f, 0.5f),   // Right
            new(0.5f, 0f, -0.5f),  // Left
        };

        // Upper layer pattern (centered above base)
        private static readonly Vector3[] UpperLayerPattern = new Vector3[]
        {
            new(0.5f, 1f, 0f),     // Center top
            new(0f, 1f, 0.25f),    // Back-right top
            new(1f, 1f, -0.25f),   // Front-left top
            new(0.5f, 2f, 0f),     // Peak
        };

        public override void GenerateResources() => GenerateAtTestPositions();

        public override void ClearGeneratedResources()
        {
            // The Destroy object line is left outside the #endif so the object is just destroyed in a build
            foreach (var resource in GeneratedResources)
            {
                if (resource != null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying) DestroyImmediate(resource.gameObject);
                    else
#endif
                        Destroy(resource.gameObject);
                }
            }
            GeneratedResources.Clear();
        }

        public override Resource GenerateResourceAt(Vector3 position)
        {
            if (Config == null) { DebugManager.LogWarning("OreInstructions: No config assigned!"); return null; }

            var spawnEntry = Config.GetRandomEntry();
            if (spawnEntry?.definition == null) { DebugManager.LogWarning("OreInstructions: No valid resource definitions in config!"); return null; }

            // Prefer SimpleResourceDefinitionSO but work with base class too
            var simpleDefinition = spawnEntry.definition as SimpleResourceDefinitionSO;

            Vector3 spawnPos = SnapToGround(position);
            Resource vein = AssembleOreVein(spawnPos, spawnEntry.definition, simpleDefinition);

            if (vein != null) { GeneratedResources.Add(vein); DebugManager.LogSpawning($"OreInstructions: Generated {spawnEntry.definition.resourceName} vein at {spawnPos}"); }

            return vein;
        }

        private Vector3 SnapToGround(Vector3 position)
        {
            Vector3 rayStart = position + Vector3.up * groundCheckHeight;

            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, groundCheckHeight * 2f, groundLayer))
                return hit.point;

            return position;
        }

        private Resource AssembleOreVein(Vector3 position, ResourceDefinitionSO definition, SimpleResourceDefinitionSO simpleDefinition)
        {
            // Reset cache if using SimpleResourceDefinitionSO
            if (simpleDefinition != null) simpleDefinition.ResetCache();

            // Create parent object
            GameObject veinObj = new($"Vein_{definition.resourceName}");

            // Add random Y rotation to the whole vein
            veinObj.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));

            // Create a container for all ore visuals
            GameObject veinVisual = new("VeinVisual");
            veinVisual.transform.SetParent(veinObj.transform);
            veinVisual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            veinVisual.transform.localScale = Vector3.one;

            // Build vein visuals with pattern placement
            BuildVeinVisuals(veinVisual, definition, simpleDefinition);

            // Add Resource component
            var resource = veinObj.AddComponent<Resource>();

            // Build stages from definition
            ResourceStage[] stages = definition.BuildStages();

            // Initialize using the general method
            resource.Initialize(
                type: definition.resourceType,
                visual: veinVisual,
                resourceStages: stages,
                order: definition.harvestOrder
            );

            // Set the vein object to interactable layer
            SetLayerRecursive(veinObj, LayerMask.NameToLayer(Strings.InteractableLayerName));

            return resource;
        }

        private void BuildVeinVisuals(GameObject veinVisual, ResourceDefinitionSO definition, SimpleResourceDefinitionSO simpleDefinition)
        {
            int pieceCount = definition.GetPieceCount();
            int pieceValue = definition.valuePerPiece;
            float pieceSize = definition.pieceSize;

            // Generate positions for the vein
            Vector3[] positions = GenerateVeinPositions(pieceCount);

            for (int i = 0; i < pieceCount; i++)
            {
                // Get prefab using the definition's method
                GameObject prefab = definition.GetPrefabForIndex(i, pieceCount);

                if (prefab == null) { DebugManager.LogWarning($"OreInstructions: No prefab available for piece {i}"); continue; }

                GameObject pieceObj = Instantiate(prefab, veinVisual.transform);

                // Calculate position with spacing
                Vector3 localPos = positions[i] * pieceSpacing;

                // Apply randomization if enabled
                if (randomizePositions)
                {
                    localPos += new Vector3(
                        Random.Range(-positionRandomness, positionRandomness),
                        Random.Range(0, positionRandomness * 0.5f), // Less Y randomness
                        Random.Range(-positionRandomness, positionRandomness)
                    );
                }

                pieceObj.transform.localPosition = localPos;

                // Apply rotation
                if (randomizeRotations)
                {
                    pieceObj.transform.localRotation = Quaternion.Euler(
                        Random.Range(-rotationRandomness, rotationRandomness),
                        Random.Range(0f, 360f),
                        Random.Range(-rotationRandomness, rotationRandomness)
                    );
                }
                else pieceObj.transform.localRotation = Quaternion.identity;

                pieceObj.name = $"OrePiece_{i}";

                // Ensure it has a ResourcePiece component
                if (!pieceObj.TryGetComponent<ResourcePiece>(out var piece)) piece = pieceObj.AddComponent<ResourcePiece>();

                // Configure the piece - variant and type will be auto-parsed from name
                piece.SetConfiguration(
                    value: pieceValue,
                    pieceSize: pieceSize > 0 ? pieceSize : piece.PieceSize
                );
            }
        }

        /// <summary>
        /// Generate positions for ore pieces following the vein pattern.
        /// Pattern: 4 pieces on bottom layer in diamond shape, additional pieces stack on top.
        /// </summary>
        private Vector3[] GenerateVeinPositions(int count)
        {
            Vector3[] positions = new Vector3[count];

            for (int i = 0; i < count; i++)
            {
                if (i < BaseLayerPattern.Length) positions[i] = BaseLayerPattern[i];
                else
                {
                    // Use upper layer pattern
                    int upperIndex = i - BaseLayerPattern.Length;

                    if (upperIndex < UpperLayerPattern.Length) positions[i] = UpperLayerPattern[upperIndex];
                    else
                    {
                        // Additional pieces: stack even higher with some randomness
                        int extraIndex = upperIndex - UpperLayerPattern.Length;
                        int layer = 3 + (extraIndex / 2);
                        float xOffset = (extraIndex % 2 == 0) ? 0.3f : 0.7f;
                        positions[i] = new Vector3(xOffset, layer, Random.Range(-0.2f, 0.2f));
                    }
                }
            }

            return positions;
        }

        private void SetLayerRecursive(GameObject obj, int layer)
        {
            obj.layer = layer;

            foreach (Transform child in obj.transform) SetLayerRecursive(child.gameObject, layer);
        }

#if UNITY_EDITOR
        [ContextMenu("Preview Vein Pattern (5 pieces)")]
        private void PreviewVeinPattern()
        {
            var positions = GenerateVeinPositions(5);
            for (int i = 0; i < positions.Length; i++) Debug.Log($"Piece {i + 1}: {positions[i]}");
        }

        [ContextMenu("Preview Vein Pattern (8 pieces)")]
        private void PreviewVeinPatternLarge()
        {
            var positions = GenerateVeinPositions(8);
            for (int i = 0; i < positions.Length; i++) Debug.Log($"Piece {i + 1}: {positions[i]}");
        }
#endif
    }
}