using System.Collections.Generic;
using Assets.Scripts.Resources;
using Assets.Scripts.Shared.Enums;
using UnityEngine;

namespace Assets.Scripts.Generation.Resources
{
    public class ResourcePointGenerator : MonoBehaviour
    {
        [SerializeField] private List<ResourceGenerationInstruction> generationInstructions = new();

        private Dictionary<EResourceType, ResourceGenerationInstruction> instructionLookup;

        public List<ResourceGenerationInstruction> Instructions => generationInstructions;

        private void Awake()
        {
            ConvertInstructionsToLookup();
        }

        private void ConvertInstructionsToLookup()
        {
            instructionLookup = new Dictionary<EResourceType, ResourceGenerationInstruction>();
            foreach (var instruction in generationInstructions)
            {
                if (instruction != null && !instructionLookup.ContainsKey(instruction.ResourceType))
                {
                    instructionLookup.Add(instruction.ResourceType, instruction);
                }
            }
        }

        /// <summary>
        /// Generate a resource of the given type at the position
        /// </summary>
        public Resource GenerateAt(EResourceType type, Vector3 position)
        {
            if (instructionLookup == null)
                ConvertInstructionsToLookup();

            if (instructionLookup.TryGetValue(type, out var instruction))
            {
                return instruction.GenerateResourceAt(position);
            }

            Debug.LogWarning($"ResourcePointGenerator: No instruction found for {type}");
            return null;
        }

        /// <summary>
        /// Generate all resources using their configured test positions
        /// </summary>
        public void GenerateAllTestResources()
        {
            foreach (var instruction in generationInstructions)
            {
                instruction?.GenerateAtTestPositions();
            }
        }

        /// <summary>
        /// Clear all generated resources
        /// </summary>
        public void ClearAllResources()
        {
            foreach (var instruction in generationInstructions)
            {
                instruction?.ClearGeneratedResources();
            }
        }
    }
}