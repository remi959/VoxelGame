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

            // Ensure we have a tree definition
            var treeDefinition = spawnEntry.definition as TreeResourceDefinitionSO;
            if (treeDefinition == null)
            {
                DebugManager.LogWarning($"WoodInstructions: Definition '{spawnEntry.definition.resourceName}' is not a TreeResourceDefinitionSO!");
                return null;
            }

            Vector3 spawnPos = SnapToGround(position);
            Resource tree = AssembleTreeResource(spawnPos, treeDefinition);

            if (tree != null)
            {
                GeneratedResources.Add(tree);
                DebugManager.LogSpawning($"WoodInstructions: Generated {treeDefinition.resourceName} (variant: {treeDefinition.VariantId}) at {spawnPos}");
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

        private Resource AssembleTreeResource(Vector3 position, TreeResourceDefinitionSO definition)
        {
            // Reset cache for fresh random values
            definition.ResetCache();

            // Create parent object
            GameObject treeObj = new($"Tree_{definition.resourceName}");

            // Add random Y rotation
            treeObj.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));

            // Create a container for all tree visuals
            GameObject treeVisual = new("TreeVisual");
            treeVisual.transform.SetParent(treeObj.transform);
            treeVisual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            treeVisual.transform.localScale = Vector3.one;

            // Build tree visuals using the definition's methods
            BuildTreeVisuals(treeVisual, definition);

            // Add Resource component
            var resource = treeObj.AddComponent<Resource>();

            // Random fall direction
            float fallAngle = Random.Range(0f, 360f);
            Vector3 fallRotation = Quaternion.Euler(0f, fallAngle, 0f) * new Vector3(definition.fallAngle, 0f, 0f);

            // Build stages from definition with the random fall rotation
            ResourceStage[] stages = definition.BuildStages(fallRotation);

            // Initialize using the general method
            resource.Initialize(
                type: definition.resourceType,
                visual: treeVisual,
                resourceStages: stages,
                order: definition.harvestOrder
            );

            // Set the tree object to interactable layer
            SetLayerRecursive(treeObj, LayerMask.NameToLayer(Strings.InteractableLayerName));

            return resource;
        }

        private void BuildTreeVisuals(GameObject stageVisual, TreeResourceDefinitionSO definition)
        {
            float currentHeight = 0f;
            int totalPieces = definition.GetPieceCount();

            for (int i = 0; i < totalPieces; i++)
            {
                GameObject prefab = definition.GetPrefabForIndex(i, totalPieces);

                if (prefab == null)
                {
                    DebugManager.LogWarning($"WoodInstructions: No prefab available for piece {i}");
                    continue;
                }

                GameObject pieceObj = Instantiate(prefab, stageVisual.transform);
                pieceObj.transform.localPosition = new Vector3(0f, currentHeight, 0f);
                pieceObj.transform.localRotation = Quaternion.identity;
                pieceObj.name = $"Piece_{i}";

                // Only add ResourcePiece if this piece is harvestable
                if (definition.IsPieceHarvestable(i, totalPieces))
                {
                    var piece = pieceObj.GetComponent<ResourcePiece>();
                    if (piece == null)
                    {
                        piece = pieceObj.AddComponent<ResourcePiece>();
                    }

                    // Configure the piece - variant and type will be auto-parsed from name
                    piece.SetConfiguration(
                        value: definition.valuePerPiece,
                        pieceSize: definition.pieceSize > 0 ? definition.pieceSize : piece.PieceSize
                    );

                    currentHeight += piece.PieceSize;
                }
                else
                {
                    // Non-harvestable piece (like a decorative crown)
                    // Still need to account for its size
                    currentHeight += definition.pieceSize;
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