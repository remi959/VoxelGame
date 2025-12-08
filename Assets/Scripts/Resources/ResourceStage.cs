using Assets.Scripts.Shared.Enums;
using UnityEngine;

namespace Assets.Scripts.Resources
{
    

    [System.Serializable]
    public class ResourceStage
    {
        [Header("Stage Info")]
        public string stageName;
        public GameObject stageVisual;

        [Header("Work Settings")]
        public float workTimePerPiece = 2f;
        public int piecesToDetach = 1;

        [Tooltip("If true, the last piece of this stage can be picked up instantly")]
        public bool instantPickupLastPiece = true;

        [Tooltip("Order in which pieces are harvested")]
        public EHarvestOrder harvestOrder = EHarvestOrder.Random;

        [Header("Transition")]
        public bool playTransitionAnimation = false;
        public float transitionDuration = 1f;
        public Vector3 transitionRotation = Vector3.zero;

        [Header("Completion")]
        public bool destroyOnComplete = false;
        public GameObject remainsAfterComplete;
    }
}