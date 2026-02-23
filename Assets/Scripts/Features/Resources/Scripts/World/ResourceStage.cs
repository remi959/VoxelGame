using UnityEngine;

namespace Assets.Scripts.Resources
{
    [System.Serializable]
    public class ResourceStage
    {
        [Header("Stage Info")]
        public string stageName;

        [Header("Work Settings")]
        public float workTimePerPiece = 2f;
        
        [Tooltip("If true, this stage will detach pieces when worked")]
        public bool yieldsPieces = false;

        [Tooltip("If true, the last piece of this stage can be picked up instantly")]
        public bool instantPickupLastPiece = true;

        [Header("Transition")]
        public bool playTransitionAnimation = false;
        public float transitionDuration = 1f;
        public Vector3 transitionRotation = Vector3.zero;

        [Header("Completion")]
        public bool destroyOnComplete = false;

        /// <summary>
        /// Create a shallow copy of this stage.
        /// </summary>
        public ResourceStage Clone()
        {
            return new ResourceStage
            {
                stageName = stageName,
                workTimePerPiece = workTimePerPiece,
                yieldsPieces = yieldsPieces,
                instantPickupLastPiece = instantPickupLastPiece,
                playTransitionAnimation = playTransitionAnimation,
                transitionDuration = transitionDuration,
                transitionRotation = transitionRotation,
                destroyOnComplete = destroyOnComplete
            };
        }
    }
}