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
        private int totalPiecesInStage = 0; // Track total pieces for this stage
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

            if (currentStageIndex >= stages.Length)
            {
                Debug.Log("Resource: No more stages");
                return;
            }

            // Hide all stage visuals, show current
            for (int i = 0; i < stages.Length; i++)
            {
                if (stages[i].stageVisual != null)
                {
                    stages[i].stageVisual.SetActive(i == currentStageIndex);
                }
            }

            // Get piece count from the actual objects
            totalPiecesInStage = CurrentStage.GetPieceCount();
            
            RefreshAvailablePieces();

            Debug.Log($"Resource: Initialized stage {currentStageIndex} ({CurrentStage.stageName}) with {totalPiecesInStage} pieces");
        }

        private void RefreshAvailablePieces()
        {
            availablePieces.Clear();

            if (CurrentStage?.stageVisual == null) return;

            // Get all ResourcePiece components that haven't been detached yet
            var allPieces = CurrentStage.stageVisual.GetComponentsInChildren<ResourcePiece>(false);
            foreach (var piece in allPieces)
            {
                if (!piece.IsDetached)
                {
                    availablePieces.Add(piece);
                }
            }

            // Sort based on harvest order
            switch (CurrentStage.harvestOrder)
            {
                case EHarvestOrder.Random:
                    // Shuffle the list
                    for (int i = availablePieces.Count - 1; i > 0; i--)
                    {
                        int j = Random.Range(0, i + 1);
                        (availablePieces[i], availablePieces[j]) = (availablePieces[j], availablePieces[i]);
                    }
                    break;

                case EHarvestOrder.Sequential:
                    // Already in hierarchy order from GetComponentsInChildren
                    break;

                case EHarvestOrder.ReverseSequential:
                    availablePieces.Reverse();
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
            bool skipWorkTime = isLastPiece && CurrentStage.instantPickupLastPiece && piecesDetachedThisStage > 0;

            if (!skipWorkTime)
            {
                yield return new WaitForSeconds(CurrentStage.workTimePerPiece);
            }
            else
            {
                Debug.Log("Resource: Instant pickup for last piece!");
                yield return null;
            }

            workCoroutine = null;

            // Detach a piece if available
            if (totalPiecesInStage > 0 && availablePieces.Count > 0)
            {
                ResourceFragment fragment = DetachNextPiece();
                piecesDetachedThisStage++;
                Debug.Log($"Resource: Detached piece {piecesDetachedThisStage}/{totalPiecesInStage}");

                // Check if stage is complete (all pieces detached)
                if (availablePieces.Count == 0)
                {
                    yield return StartCoroutine(TransitionToNextStage());
                }

                onFragmentReady?.Invoke(fragment);
            }
            else
            {
                // No pieces to detach, just transition
                yield return StartCoroutine(TransitionToNextStage());
                onFragmentReady?.Invoke(null);
            }
        }

        private ResourceFragment DetachNextPiece()
        {
            if (availablePieces.Count == 0) return null;

            ResourcePiece piece = availablePieces[0];
            availablePieces.RemoveAt(0);

            return piece.Detach(resourceType);
        }

        private IEnumerator TransitionToNextStage()
        {
            var completedStage = CurrentStage;

            if (completedStage.playTransitionAnimation && completedStage.stageVisual != null)
            {
                Debug.Log($"Resource: Playing transition animation for {completedStage.stageName}");
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
                Debug.Log("Resource: Fully depleted");

                if (completedStage.destroyOnComplete)
                {
                    EventBus.Publish(new ResourceDepletedEvent { Resource = gameObject });
                    Destroy(gameObject, 0.1f);
                }
            }
        }

        private IEnumerator PlayTransitionAnimation(ResourceStage stage)
        {
            if (stage.stageVisual == null) yield break;

            Transform visual = stage.stageVisual.transform;
            Quaternion startRotation = visual.localRotation;
            Quaternion endRotation = startRotation * Quaternion.Euler(stage.transitionRotation);

            float elapsed = 0f;
            while (elapsed < stage.transitionDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / stage.transitionDuration;

                // Ease out curve for natural falling motion
                t = 1f - Mathf.Pow(1f - t, 2f);

                visual.localRotation = Quaternion.Slerp(startRotation, endRotation, t);
                yield return null;
            }

            visual.localRotation = endRotation;
        }
    }
}