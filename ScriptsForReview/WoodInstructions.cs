using Assets.Scripts.Core;
using Assets.Scripts.Data.Resources;
using Assets.Scripts.Resources;
using Assets.Scripts.Shared.Enums;
using Assets.Scripts.Shared.Utilities;
using UnityEngine;

namespace Assets.Scripts.Generation.Resources
{
    [CreateAssetMenu(fileName = "WoodInstructions", menuName = "Resources/Instructions/Wood Instructions")]
    public class WoodInstructions : ResourceGenerationInstruction
    {
        [Header("Wood-Specific Settings")]
        [SerializeField] private float groundCheckHeight = 10f;
        [SerializeField] private LayerMask groundLayer;

        [Header("Tree Settings")]
        [SerializeField] private float chopTime = 2f;
        [SerializeField] private float harvestTimePerPiece = 1f;
        [SerializeField] private float fallDuration = 1.5f;

        public override void GenerateResources()
        {
            GenerateAtTestPositions();
        }

        public override void ClearGeneratedResources()
        {
            foreach (var resource in GeneratedResources)
            {
                if (resource != null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                        DestroyImmediate(resource.gameObject);
                    else
#endif
                        Object.Destroy(resource.gameObject);
                }
            }
            GeneratedResources.Clear();
        }

        public override Resource GenerateResourceAt(Vector3 position)
        {
            if (Config == null)
            {
                DebugManager.LogWarning("WoodInstructions: No config assigned!");
                return null;
            }

            var spawnEntry = Config.GetRandomEntry();
            if (spawnEntry?.definition == null)
            {
                DebugManager.LogWarning("WoodInstructions: No valid resource definitions in config!");
                return null;
            }

            Vector3 spawnPos = SnapToGround(position);
            Resource tree = AssembleTreeResource(spawnPos, spawnEntry);

            if (tree != null)
            {
                GeneratedResources.Add(tree);
                DebugManager.LogSpawning($"WoodInstructions: Generated {spawnEntry.definition.resourceName} (variant: {spawnEntry.definition.VariantId}) at {spawnPos}");
            }

            return tree;
        }

        private Vector3 SnapToGround(Vector3 position)
        {
            Vector3 rayStart = position + Vector3.up * groundCheckHeight;

            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, groundCheckHeight * 2f, groundLayer))
            {
                return hit.point;
            }

            return position;
        }

        private Resource AssembleTreeResource(Vector3 position, ResourceGenerationConfig.ResourceSpawnEntry entry)
        {
            var definition = entry.definition;

            // Create parent object
            GameObject treeObj = new($"Tree_{definition.resourceName}");

            // Add random Y rotation
            treeObj.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));

            // Create a container for all tree visuals
            GameObject treeVisual = new("TreeVisual");
            treeVisual.transform.SetParent(treeObj.transform);
            treeVisual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            treeVisual.transform.localScale = Vector3.one;

            // Build tree visuals with piece type placement rules
            BuildTreeVisuals(treeVisual, definition);

            // Add Resource component and initialize as tree
            var resource = treeObj.AddComponent<Resource>();

            // Random fall direction
            float fallAngle = Random.Range(0f, 360f);
            Vector3 fallRotation = Quaternion.Euler(0f, fallAngle, 0f) * new Vector3(90f, 0f, 0f);

            resource.InitializeTree(
                type: definition.resourceType,
                visual: treeVisual,
                chopTime: chopTime,
                harvestTimePerPiece: harvestTimePerPiece,
                fallRotation: fallRotation,
                fallDuration: fallDuration,
                order: EHarvestOrder.ClosestEndFirst
            );

            // Set the tree object to interactable layer
            SetLayerRecursive(treeObj, LayerMask.NameToLayer(Strings.InteractableLayerName));

            return resource;
        }

        private void BuildTreeVisuals(GameObject stageVisual, ResourceDefinitionSO definition)
        {
            float currentHeight = 0f;

            // Get configuration from definition
            int pieceValue = definition.valuePerPiece;
            float pieceSize = definition.pieceSize;

            // Determine number of trunk pieces
            int trunkCount = Random.Range(definition.minTrunkPieces, definition.maxTrunkPieces + 1);

            // Check if we have base prefabs to use for the first piece
            bool useBasePiece = definition.basePrefabs != null && definition.basePrefabs.Length > 0;

            // Build trunk pieces
            for (int i = 0; i < trunkCount; i++)
            {
                GameObject prefab;

                // First piece: use base prefab if available, otherwise trunk
                if (i == 0 && useBasePiece) prefab = definition.GetRandomBasePrefab();
                else prefab = definition.GetRandomTrunkPrefab();


                if (prefab == null)
                {
                    DebugManager.LogWarning($"WoodInstructions: No prefab available for trunk piece {i}");
                    continue;
                }

                GameObject trunkObj = Instantiate(prefab, stageVisual.transform);
                trunkObj.transform.localPosition = new Vector3(0f, currentHeight, 0f);
                trunkObj.transform.localRotation = Quaternion.identity;
                trunkObj.name = $"Trunk_{i}";

                // Ensure it has a ResourcePiece component
                var piece = trunkObj.GetComponent<ResourcePiece>();
                if (piece == null)
                {
                    piece = trunkObj.AddComponent<ResourcePiece>();
                }

                // Configure the piece - variant and type will be auto-parsed from name
                piece.SetConfiguration(
                    value: pieceValue,
                    pieceSize: pieceSize > 0 ? pieceSize : piece.PieceSize
                );

                currentHeight += piece.PieceSize;
            }

            // Add top piece (crown) if configured
            if (definition.includeTopPiece)
            {
                GameObject topPrefab = definition.GetRandomTopPrefab();

                if (topPrefab != null)
                {
                    GameObject topObj = Instantiate(topPrefab, stageVisual.transform);
                    topObj.transform.localPosition = new Vector3(0f, currentHeight, 0f);
                    topObj.transform.localRotation = Quaternion.identity;
                    topObj.name = "Crown";

                    // Add ResourcePiece only if top is harvestable
                    if (definition.topPieceHarvestable)
                    {
                        var piece = topObj.GetComponent<ResourcePiece>();
                        if (piece == null)
                        {
                            piece = topObj.AddComponent<ResourcePiece>();
                        }

                        piece.SetConfiguration(
                            value: pieceValue,
                            pieceSize: pieceSize > 0 ? pieceSize : piece.PieceSize
                        );
                    }
                }
            }
        }

        private void SetLayerRecursive(GameObject obj, int layer)
        {
            obj.layer = layer;

            foreach (Transform child in obj.transform)
            {
                SetLayerRecursive(child.gameObject, layer);
            }
        }
    }
}