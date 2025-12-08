using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.Events;
using Assets.Scripts.Shared.Enums;
using UnityEngine;

namespace Assets.Scripts.Resources
{
    public class Resource : MonoBehaviour
    {
        [Header("Resource Settings")]
        [SerializeField] private EResourceType resourceType;
        [SerializeField] private ResourceStage[] stages;

        private int currentStageIndex = 0;
        private int piecesDetachedThisStage = 0;
        private List<ResourcePiece> availablePieces = new();
        private Coroutine workCoroutine;

        public EResourceType Type => resourceType;
        public bool IsDepleted => currentStageIndex >= stages.Length;
        public Vector3 WorkPosition => transform.position;
        public bool HasMoreStages => currentStageIndex < stages.Length - 1;

        private ResourceStage CurrentStage =>
            currentStageIndex < stages.Length ? stages[currentStageIndex] : null;

        private void Start()
        {
            InitializeStage(0);
        }

        private void InitializeStage(int stageIndex)
        {
            currentStageIndex = stageIndex;
            piecesDetachedThisStage = 0;

            // Hide all stage visuals and remains
            foreach (var stage in stages)
            {
                if (stage.stageVisual != null)
                    stage.stageVisual.SetActive(false);
                if (stage.remainsAfterComplete != null)
                    stage.remainsAfterComplete.SetActive(false);
            }

            // Show current stage
            if (CurrentStage?.stageVisual != null)
            {
                CurrentStage.stageVisual.SetActive(true);
                RefreshAvailablePieces();
            }

            Debug.Log($"Resource: Initialized stage {stageIndex} ({CurrentStage?.stageName})");
        }

        private void RefreshAvailablePieces()
        {
            availablePieces.Clear();

            if (CurrentStage?.stageVisual == null) return;

            // Find all pieces in current stage
            var pieces = CurrentStage.stageVisual.GetComponentsInChildren<ResourcePiece>();
            foreach (var piece in pieces)
            {
                if (!piece.IsDetached)
                {
                    availablePieces.Add(piece);
                }
            }

            // Order pieces based on harvest order setting
            switch (CurrentStage.harvestOrder)
            {
                case EHarvestOrder.Random:
                    ShuffleList(availablePieces);
                    break;
                    
                case EHarvestOrder.Sequential:
                    // Already in hierarchy order from GetComponentsInChildren
                    // Sort by sibling index to ensure correct order
                    availablePieces.Sort((a, b) => 
                        a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
                    break;
                    
                case EHarvestOrder.ReverseSequential:
                    // Reverse hierarchy order (last child first)
                    availablePieces.Sort((a, b) => 
                        b.transform.GetSiblingIndex().CompareTo(a.transform.GetSiblingIndex()));
                    break;
            }

            Debug.Log($"Resource: Found {availablePieces.Count} pieces in stage (Order: {CurrentStage.harvestOrder})");
        }

        public void StartWorking(System.Action<ResourceFragment> onFragmentReady)
        {
            if (IsDepleted || CurrentStage == null)
            {
                onFragmentReady?.Invoke(null);
                return;
            }

            if (workCoroutine != null)
                StopCoroutine(workCoroutine);

            workCoroutine = StartCoroutine(WorkRoutine(onFragmentReady));
        }

        public void StopWorking()
        {
            if (workCoroutine != null)
            {
                StopCoroutine(workCoroutine);
                workCoroutine = null;
            }
        }

        private IEnumerator WorkRoutine(System.Action<ResourceFragment> onFragmentReady)
        {
            Debug.Log($"Resource: Working on stage {CurrentStage.stageName}...");

            // Check if this is the last piece and instant pickup is enabled
            bool isLastPiece = availablePieces.Count == 1;
            bool skipWorkTime = isLastPiece && CurrentStage.instantPickupLastPiece;

            if (!skipWorkTime)
            {
                yield return new WaitForSeconds(CurrentStage.workTimePerPiece);
            }
            else
            {
                Debug.Log("Resource: Instant pickup for last piece!");
                yield return null;
            }

            ResourceFragment fragment = null;

            if (CurrentStage.piecesToDetach > 0 && availablePieces.Count > 0)
            {
                fragment = DetachNextPiece();
                piecesDetachedThisStage++;

                Debug.Log($"Resource: Detached piece {piecesDetachedThisStage}/{CurrentStage.piecesToDetach}");

                if (piecesDetachedThisStage >= CurrentStage.piecesToDetach || availablePieces.Count == 0)
                {
                    yield return StartCoroutine(TransitionToNextStage());
                }
            }
            else
            {
                Debug.Log($"Resource: Stage {CurrentStage.stageName} complete, transitioning...");
                yield return StartCoroutine(TransitionToNextStage());
            }

            workCoroutine = null;
            onFragmentReady?.Invoke(fragment);
        }

        private ResourceFragment DetachNextPiece()
        {
            if (availablePieces.Count == 0) return null;

            // Always take the first piece (order was determined in RefreshAvailablePieces)
            ResourcePiece piece = availablePieces[0];
            availablePieces.RemoveAt(0);

            return piece.Detach(resourceType);
        }

        private IEnumerator TransitionToNextStage()
        {
            var completedStage = CurrentStage;

            if (completedStage.playTransitionAnimation && completedStage.stageVisual != null)
            {
                Debug.Log($"Resource: Playing transition animation");
                yield return StartCoroutine(PlayTransitionAnimation(completedStage));
            }

            if (completedStage.stageVisual != null)
            {
                completedStage.stageVisual.SetActive(false);
            }

            if (completedStage.remainsAfterComplete != null)
            {
                completedStage.remainsAfterComplete.SetActive(true);
            }

            currentStageIndex++;

            if (currentStageIndex < stages.Length)
            {
                InitializeStage(currentStageIndex);
            }
            else
            {
                Debug.Log($"Resource: Fully depleted");

                if (completedStage.destroyOnComplete)
                {
                    EventBus.Publish(new ResourceDepletedEvent { Resource = gameObject });
                    Destroy(gameObject, 0.5f);
                }
            }
        }

        private IEnumerator PlayTransitionAnimation(ResourceStage stage)
        {
            Transform visual = stage.stageVisual.transform;
            Quaternion startRot = visual.localRotation;
            Quaternion endRot = Quaternion.Euler(stage.transitionRotation);

            float elapsed = 0f;
            while (elapsed < stage.transitionDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / stage.transitionDuration;
                t = 1f - Mathf.Pow(1f - t, 2f);
                visual.localRotation = Quaternion.Slerp(startRot, endRot, t);
                yield return null;
            }

            visual.localRotation = endRot;
        }

        private void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}