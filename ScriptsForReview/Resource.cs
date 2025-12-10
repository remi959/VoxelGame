using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.Core;
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
        [SerializeField] private EHarvestOrder harvestOrder = EHarvestOrder.Random;

        private int currentStageIndex = 0;
        private int piecesDetachedThisStage = 0;
        private List<ResourcePiece> availablePieces = new();
        private GameObject resourceVisual;
        private bool isInitialized = false;

        // Track active workers to prevent conflicts
        private Dictionary<object, Coroutine> activeWorkers = new();

        // Track if a non-yielding stage is being worked (only one worker at a time)
        private bool isNonYieldingStageInProgress = false;
        private object nonYieldingStageWorker = null;

        // Workers waiting for pieces to become available
        private List<WaitingWorker> waitingWorkers = new();

        private struct WaitingWorker
        {
            public System.Action<ResourceFragment> Callback;
            public Transform WorkerTransform;
            public object WorkerKey;
        }

        public EResourceType Type => resourceType;
        public bool IsDepleted => currentStageIndex >= stages.Length;
        public Vector3 WorkPosition => transform.position;
        public bool HasMoreStages => currentStageIndex < stages.Length - 1;
        public EHarvestOrder HarvestOrder => harvestOrder;

        /// <summary>
        /// Returns true if the current stage yields pieces (allows multiple workers).
        /// </summary>
        public bool CurrentStageYieldsPieces => CurrentStage != null && CurrentStage.yieldsPieces;

        /// <summary>
        /// Returns the number of available pieces in the current stage.
        /// </summary>
        public int AvailablePieceCount => availablePieces.Count;

        private ResourceStage CurrentStage =>
            currentStageIndex < stages.Length ? stages[currentStageIndex] : null;

        private void Start()
        {
            if (!isInitialized && stages != null && stages.Length > 0)
            {
                if (resourceVisual == null && transform.childCount > 0)
                {
                    resourceVisual = transform.GetChild(0).gameObject;
                }
                InitializeStage(0);
            }
        }

        public void InitializeTree(EResourceType type, GameObject visual, float chopTime, float harvestTimePerPiece, Vector3 fallRotation, float fallDuration, EHarvestOrder order = EHarvestOrder.Random)
        {
            resourceType = type;
            resourceVisual = visual;
            harvestOrder = order;

            stages = new ResourceStage[2];

            stages[0] = new ResourceStage
            {
                stageName = "Chopping",
                workTimePerPiece = chopTime,
                yieldsPieces = false,
                instantPickupLastPiece = false,
                playTransitionAnimation = true,
                transitionDuration = fallDuration,
                transitionRotation = fallRotation,
                destroyOnComplete = false
            };

            stages[1] = new ResourceStage
            {
                stageName = "Harvesting",
                workTimePerPiece = harvestTimePerPiece,
                yieldsPieces = true,
                instantPickupLastPiece = true,
                playTransitionAnimation = false,
                destroyOnComplete = true
            };

            isInitialized = true;
            InitializeStage(0);
        }

        public void InitializeSimple(EResourceType type, GameObject visual, float workTimePerPiece = 1f, bool destroyOnComplete = true, EHarvestOrder order = EHarvestOrder.Random)
        {
            resourceType = type;
            resourceVisual = visual;
            harvestOrder = order;

            stages = new ResourceStage[1];
            stages[0] = new ResourceStage
            {
                stageName = "Harvest",
                workTimePerPiece = workTimePerPiece,
                yieldsPieces = true,
                instantPickupLastPiece = true,
                playTransitionAnimation = false,
                destroyOnComplete = destroyOnComplete
            };

            isInitialized = true;
            InitializeStage(0);
        }

        private void InitializeStage(int stageIndex)
        {
            currentStageIndex = stageIndex;
            piecesDetachedThisStage = 0;
            isNonYieldingStageInProgress = false;
            nonYieldingStageWorker = null;

            if (CurrentStage != null && CurrentStage.yieldsPieces)
            {
                RefreshAvailablePieces();

                // Notify waiting workers that pieces are now available
                NotifyWaitingWorkers();
            }
            else
            {
                availablePieces.Clear();
            }

            DebugManager.LogGathering($"Resource: Initialized stage {stageIndex} ({CurrentStage?.stageName}), yields pieces: {CurrentStage?.yieldsPieces}, pieces available: {availablePieces.Count}");
        }

        private void RefreshAvailablePieces()
        {
            availablePieces.Clear();

            if (resourceVisual == null)
            {
                DebugManager.LogWarning("Resource: resourceVisual is null, cannot find pieces!");
                return;
            }

            var pieces = resourceVisual.GetComponentsInChildren<ResourcePiece>();
            foreach (var piece in pieces)
            {
                if (!piece.IsDetached)
                {
                    availablePieces.Add(piece);
                }
            }

            // Only shuffle for random order - other orders are determined at detach time
            if (harvestOrder == EHarvestOrder.Random)
            {
                ShuffleList(availablePieces);
            }

            DebugManager.LogGathering($"Resource: Found {availablePieces.Count} harvestable pieces");
        }

        /// <summary>
        /// Start working on this resource. Each worker gets their own work session.
        /// For non-yielding stages, only one worker can work at a time.
        /// Additional workers will wait until pieces become available.
        /// </summary>
        public void StartWorking(System.Action<ResourceFragment> onFragmentReady, Transform workerTransform = null)
        {
            if (IsDepleted || CurrentStage == null)
            {
                onFragmentReady?.Invoke(null);
                return;
            }

            // Use the callback as a unique key for this worker
            object workerKey = onFragmentReady;

            // Stop any existing work for this worker
            if (activeWorkers.TryGetValue(workerKey, out var existingCoroutine))
            {
                if (existingCoroutine != null)
                    StopCoroutine(existingCoroutine);
                activeWorkers.Remove(workerKey);
            }

            // Remove from waiting list if present
            waitingWorkers.RemoveAll(w => w.WorkerKey == workerKey);

            // Check if this is a non-yielding stage
            if (!CurrentStage.yieldsPieces)
            {
                // Non-yielding stage: only one worker can work at a time
                if (isNonYieldingStageInProgress)
                {
                    // Another worker is already doing this stage, add to waiting list
                    DebugManager.LogGathering($"Resource: Worker queued - non-yielding stage already in progress");

                    waitingWorkers.Add(new WaitingWorker
                    {
                        Callback = onFragmentReady,
                        WorkerTransform = workerTransform,
                        WorkerKey = workerKey
                    });
                    return;
                }

                // This worker will handle the non-yielding stage
                isNonYieldingStageInProgress = true;
                nonYieldingStageWorker = workerKey;
            }
            else
            {
                // Yielding stage: check if there are enough pieces for this worker
                int workersOnThisStage = activeWorkers.Count;
                if (workersOnThisStage >= availablePieces.Count && availablePieces.Count > 0)
                {
                    // Not enough pieces for another worker, add to waiting list
                    DebugManager.LogGathering($"Resource: Worker queued - not enough pieces ({availablePieces.Count} pieces, {workersOnThisStage} workers)");

                    waitingWorkers.Add(new WaitingWorker
                    {
                        Callback = onFragmentReady,
                        WorkerTransform = workerTransform,
                        WorkerKey = workerKey
                    });
                    return;
                }
            }

            var coroutine = StartCoroutine(WorkRoutine(onFragmentReady, workerTransform, workerKey));
            activeWorkers[workerKey] = coroutine;
        }

        /// <summary>
        /// Stop a specific worker from working.
        /// </summary>
        public void StopWorking(System.Action<ResourceFragment> workerCallback = null)
        {
            if (workerCallback != null)
            {
                object workerKey = workerCallback;

                // Remove from waiting list
                waitingWorkers.RemoveAll(w => w.WorkerKey == workerKey);

                // Stop active work
                if (activeWorkers.TryGetValue(workerKey, out var coroutine))
                {
                    if (coroutine != null)
                        StopCoroutine(coroutine);
                    activeWorkers.Remove(workerKey);

                    // If this was the non-yielding stage worker, clear the flag
                    if (nonYieldingStageWorker == workerKey)
                    {
                        isNonYieldingStageInProgress = false;
                        nonYieldingStageWorker = null;

                        // Try to assign another waiting worker to the non-yielding stage
                        TryAssignWaitingWorker();
                    }
                }
            }
            else
            {
                // Stop all workers (legacy behavior)
                foreach (var kvp in activeWorkers)
                {
                    if (kvp.Value != null)
                        StopCoroutine(kvp.Value);
                }
                activeWorkers.Clear();
                waitingWorkers.Clear();
                isNonYieldingStageInProgress = false;
                nonYieldingStageWorker = null;
            }
        }

        private void NotifyWaitingWorkers()
        {
            if (waitingWorkers.Count == 0) return;

            DebugManager.LogGathering($"Resource: Notifying {waitingWorkers.Count} waiting workers");

            // Copy the list since StartWorking might modify it
            var workersToNotify = new List<WaitingWorker>(waitingWorkers);
            waitingWorkers.Clear();

            foreach (var worker in workersToNotify)
            {
                // Re-queue them through StartWorking which will properly assign them
                StartWorking(worker.Callback, worker.WorkerTransform);
            }
        }

        private void TryAssignWaitingWorker()
        {
            if (waitingWorkers.Count == 0) return;

            var worker = waitingWorkers[0];
            waitingWorkers.RemoveAt(0);

            DebugManager.LogGathering($"Resource: Assigning waiting worker to stage");
            StartWorking(worker.Callback, worker.WorkerTransform);
        }

        private IEnumerator WorkRoutine(System.Action<ResourceFragment> onFragmentReady, Transform workerTransform, object workerKey)
        {
            DebugManager.LogGathering($"Resource: Working on stage {CurrentStage.stageName}...");

            ResourceFragment fragment = null;

            if (CurrentStage.yieldsPieces && availablePieces.Count > 0)
            {
                bool isLastPiece = availablePieces.Count == 1;
                bool skipWorkTime = isLastPiece && CurrentStage.instantPickupLastPiece;

                if (!skipWorkTime)
                {
                    yield return new WaitForSeconds(CurrentStage.workTimePerPiece);
                }
                else
                {
                    DebugManager.LogGathering("Resource: Instant pickup for last piece!");
                    yield return null;
                }

                // Double-check pieces still available (another worker might have taken it)
                if (availablePieces.Count > 0)
                {
                    fragment = DetachNextPiece(workerTransform);
                    piecesDetachedThisStage++;

                    DebugManager.LogGathering($"Resource: Detached piece, remaining: {availablePieces.Count}");

                    if (availablePieces.Count == 0)
                    {
                        yield return StartCoroutine(TransitionToNextStage());
                    }
                }
                else
                {
                    DebugManager.LogGathering("Resource: No pieces available (taken by another worker)");
                }
            }
            else
            {
                // Non-yielding stage
                yield return new WaitForSeconds(CurrentStage.workTimePerPiece);
                DebugManager.LogGathering($"Resource: Stage {CurrentStage.stageName} complete, transitioning...");

                // Clear the non-yielding stage flag before transition
                isNonYieldingStageInProgress = false;
                nonYieldingStageWorker = null;

                yield return StartCoroutine(TransitionToNextStage());
            }

            // Remove from active workers
            activeWorkers.Remove(workerKey);

            onFragmentReady?.Invoke(fragment);
        }

        private ResourcePiece GetNextPieceFor(Transform workerTransform)
        {
            if (availablePieces.Count == 0) return null;

            switch (harvestOrder)
            {
                case EHarvestOrder.Closest:
                    return GetClosestPiece(workerTransform);
                case EHarvestOrder.ClosestEndFirst:
                    return GetClosestEndPiece(workerTransform);
                case EHarvestOrder.Sequential:
                    return availablePieces[0];
                case EHarvestOrder.ReverseSequential:
                    return availablePieces[availablePieces.Count - 1];
                case EHarvestOrder.Random:
                default:
                    return availablePieces[0];
            }
        }

        private ResourceFragment DetachNextPiece(Transform workerTransform = null)
        {
            if (availablePieces.Count == 0) return null;

            ResourcePiece piece = null;

            switch (harvestOrder)
            {
                case EHarvestOrder.Closest:
                    piece = GetClosestPiece(workerTransform);
                    break;
                case EHarvestOrder.ClosestEndFirst:
                    piece = GetClosestEndPiece(workerTransform);
                    break;
                case EHarvestOrder.Sequential:
                    piece = availablePieces[0];
                    break;
                case EHarvestOrder.ReverseSequential:
                    piece = availablePieces[availablePieces.Count - 1];
                    break;
                case EHarvestOrder.Random:
                default:
                    piece = availablePieces[0];
                    break;
            }

            if (piece != null)
            {
                availablePieces.Remove(piece);
                return piece.Detach(resourceType);
            }

            return null;
        }

        /// <summary>
        /// Get the closest piece that is at either end of the stack (highest or lowest in local space).
        /// Uses local Y position relative to the resource visual, so it works even after the tree falls.
        /// </summary>
        private ResourcePiece GetClosestEndPiece(Transform workerTransform)
        {
            if (availablePieces.Count == 0) return null;
            if (availablePieces.Count == 1) return availablePieces[0];

            // Find the top-most and bottom-most pieces based on LOCAL position
            // This preserves the original stack order even after the tree falls
            ResourcePiece topPiece = null;
            ResourcePiece bottomPiece = null;
            float highestLocalY = float.MinValue;
            float lowestLocalY = float.MaxValue;

            foreach (var piece in availablePieces)
            {
                if (piece == null) continue;

                // Use local position relative to the resource visual (or parent)
                float localY;
                if (resourceVisual != null)
                {
                    // Get position relative to the visual container
                    localY = resourceVisual.transform.InverseTransformPoint(piece.transform.position).y;
                }
                else
                {
                    // Fallback to local position in hierarchy
                    localY = piece.transform.localPosition.y;
                }

                if (localY > highestLocalY)
                {
                    highestLocalY = localY;
                    topPiece = piece;
                }

                if (localY < lowestLocalY)
                {
                    lowestLocalY = localY;
                    bottomPiece = piece;
                }
            }

            // Safety checks
            if (topPiece == null) return bottomPiece;
            if (bottomPiece == null) return topPiece;
            if (topPiece == bottomPiece) return topPiece;

            // If no worker transform, default to top piece (tip of fallen tree)
            if (workerTransform == null) return topPiece;

            // Return the end piece that's closest to the worker (using world position for distance)
            float distToTop = Vector3.Distance(workerTransform.position, topPiece.transform.position);
            float distToBottom = Vector3.Distance(workerTransform.position, bottomPiece.transform.position);

            return distToTop <= distToBottom ? topPiece : bottomPiece;
        }

        private ResourcePiece GetClosestPiece(Transform workerTransform)
        {
            if (availablePieces.Count == 0) return null;

            // If no worker transform provided, fall back to first piece
            if (workerTransform == null)
            {
                return availablePieces[0];
            }

            ResourcePiece closest = null;
            float closestDistance = float.MaxValue;

            foreach (var piece in availablePieces)
            {
                if (piece == null || piece.IsDetached) continue;

                float distance = Vector3.Distance(workerTransform.position, piece.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = piece;
                }
            }

            return closest != null ? closest : availablePieces[0];
        }

        /// <summary>
        /// Get the position where a worker should move to work on this resource.
        /// For yielding stages, returns the position of the next piece to harvest.
        /// For non-yielding stages, returns the resource's base position.
        /// </summary>
        public Vector3 GetWorkPositionFor(Transform workerTransform)
        {
            if (IsDepleted || CurrentStage == null)
            {
                return transform.position;
            }

            // For non-yielding stages (like chopping), work at base position
            if (!CurrentStage.yieldsPieces)
            {
                return transform.position;
            }

            // For yielding stages, find the piece position
            if (availablePieces.Count == 0)
            {
                return transform.position;
            }

            ResourcePiece targetPiece = null;

            switch (harvestOrder)
            {
                case EHarvestOrder.Closest:
                    targetPiece = GetClosestPiece(workerTransform);
                    break;
                case EHarvestOrder.Sequential:
                    targetPiece = availablePieces[0];
                    break;
                case EHarvestOrder.ReverseSequential:
                    targetPiece = availablePieces[availablePieces.Count - 1];
                    break;
                case EHarvestOrder.Random:
                default:
                    targetPiece = availablePieces[0];
                    break;
            }

            if (targetPiece != null)
            {
                return targetPiece.transform.position;
            }

            return transform.position;
        }

        /// <summary>
        /// Check if the current stage yields pieces (useful for determining work behavior).
        /// </summary>
        public bool DoesCurrentStageYieldPieces()
        {
            return CurrentStage != null && CurrentStage.yieldsPieces;
        }


        private IEnumerator TransitionToNextStage()
        {
            var completedStage = CurrentStage;

            if (completedStage.playTransitionAnimation && resourceVisual != null)
            {
                DebugManager.LogGathering($"Resource: Playing transition animation");
                yield return StartCoroutine(PlayTransitionAnimation(completedStage));
            }

            currentStageIndex++;

            if (currentStageIndex < stages.Length)
            {
                InitializeStage(currentStageIndex);
            }
            else
            {
                DebugManager.LogGathering($"Resource: Fully depleted");

                if (completedStage.destroyOnComplete)
                {
                    EventBus.Publish(new ResourceDepletedEvent { Resource = gameObject });
                    Destroy(gameObject, 0.5f);
                }
            }
        }

        private IEnumerator PlayTransitionAnimation(ResourceStage stage)
        {
            Transform visual = resourceVisual.transform;
            Quaternion startRot = visual.localRotation;
            Quaternion endRot = startRot * Quaternion.Euler(stage.transitionRotation);

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