using System.Collections.Generic;
using Assets.Scripts.Data.Resources;
using Assets.Scripts.Resources;
using Assets.Scripts.Shared.Enums;
using UnityEngine;

namespace Assets.Scripts.Generation.Resources
{
    public abstract class ResourceGenerationInstruction : ScriptableObject
    {
        [Header("Generation Config")]
        [SerializeField] protected ResourceGenerationConfig config;
        
        [Header("Manual Test Positions")]
        [SerializeField] protected List<Vector3> testSpawnPositions = new();
        
        public EResourceType ResourceType => config != null ? config.resourceType : EResourceType.Wood;
        public List<Resource> GeneratedResources { get; protected set; } = new();

        public ResourceGenerationConfig Config => config;

        public abstract Resource GenerateResourceAt(Vector3 position);
        public abstract void GenerateResources();
        public abstract void ClearGeneratedResources();
        
        /// <summary>
        /// Generate resources at all test positions (for testing)
        /// </summary>
        public virtual void GenerateAtTestPositions()
        {
            foreach (var pos in testSpawnPositions)
            {
                GenerateResourceAt(pos);
            }
        }
    }
}