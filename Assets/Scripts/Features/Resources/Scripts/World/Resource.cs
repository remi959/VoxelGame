using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.Core;
using Assets.Scripts.Core.Events;
using Assets.Scripts.Core.Services;
using Assets.Scripts.Events;
using Assets.Scripts.NPCs.Units;
using Assets.Scripts.Shared.Enums;
using Assets.Scripts.Shared.Interfaces;
using UnityEngine;

namespace Assets.Scripts.Resources
{
    /// <summary>
    /// Result of checking work progress on a resource.
    /// </summary>
    public enum WorkResult
    {
        /// <summary>Work is still in progress, keep polling.</summary>
        InProgress,
        /// <summary>Fragment is ready to be collected.</summary>
        FragmentReady,
        /// <summary>Stage completed without fragment (e.g., chopping phase done).</summary>
        StageComplete,
        /// <summary>Resource is depleted, no more work possible.</summary>
        Depleted,
        /// <summary>Worker is waiting (queued behind another worker).</summary>
        Waiting,
        /// <summary>Work session not found or invalid.</summary>
        Invalid
    }

    /// <summary>
    /// Tracks a single worker's progress on a resource.
    /// This is a struct to avoid heap allocations - stored in a dictionary by worker ID.
    /// </summary>
    public struct WorkSession
    {
        public int WorkerId;
        public float StartTime;
        public float Duration;
        public Transform WorkerTransform;
        public bool IsComplete;
        public bool IsWaiting;
        public ResourceFragment ResultFragment;
        public bool StageTransitionPending;

        public readonly float Progress => IsComplete ? 1f : Mathf.Clamp01((Time.time - StartTime) / Duration);
        public readonly bool IsTimeComplete => Time.time >= StartTime + Duration;
    }

    public class Resource : MonoBehaviour, IInteractable
    {
        [Header("Resource Settings")]
        [SerializeField] private EResourceType resourceType;
        [SerializeField] private ResourceStage[] stages;
        [SerializeField] private EHarvestOrder harvestOrder = EHarvestOrder.Random;

        private int currentStageIndex = 0;
        private int piecesDetachedThisStage = 0;
        private List<ResourcePiece> availablePieces = new();
        private List<ResourcePiece> allPiecesCache = new();  // Cached to avoid allocations
        private GameObject resourceVisual;
        private bool isInitialized = false;

        // ===== Polling-based work tracking (replaces coroutines) =====
        // Keyed by worker instance ID for O(1) lookup
        private readonly Dictionary<int, WorkSession> activeSessions = new();
        
        // Workers waiting for their turn (non-yielding stages or no pieces available)
        private readonly List<int> waitingWorkerIds = new();

        // Track if a non-yielding stage is being worked (only one worker at a time)
        private bool isNonYieldingStageInProgress = false;
        private int nonYieldingStageWorkerId = 0;

        // Stage transition coroutine (only for animations, not work timing)
        private Coroutine stageTransitionCoroutine;
        private bool isTransitioning = false;

        #region Properties

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

        private ResourceStage CurrentStage => currentStageIndex < stages.Length ? stages[currentStageIndex] : null;

        #endregion

        #region IInteractable Implementation

        public InteractionType InteractionType => InteractionType.Resource;

        public Vector3 GetInteractionPosition(Transform workerTransform) => GetWorkPositionFor(workerTransform);

        public bool CanInteract(Worker worker) => !IsDepleted;

        public void OnInteract(Worker worker) => worker.GatherFrom(this);

        #endregion

        #region Lifecycle

        private void Start()
        {
            if (!isInitialized && stages != null && stages.Length > 0)
            {
                // Default to this GameObject so animations affect all children
                if (resourceVisual == null) resourceVisual = gameObject;

                CacheAllPieces();
                InitializeStage(0);
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// General-purpose initialization that accepts pre-built stages.
        /// Use this when initializing from a ResourceDefinitionSO.
        /// </summary>
        public void Initialize(EResourceType type, GameObject visual, ResourceStage[] resourceStages, EHarvestOrder order = EHarvestOrder.Random)
        {
            resourceType = type;
            resourceVisual = visual;
            harvestOrder = order;
            stages = resourceStages;

            isInitialized = true;
            CacheAllPieces();
            InitializeStage(0);
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
            CacheAllPieces();
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
            CacheAllPieces();
            InitializeStage(0);
        }

        /// <summary>
        /// Cache all pieces once at initialization to avoid repeated GetComponentsInChildren calls.
        /// </summary>
        private void CacheAllPieces()
        {
            allPiecesCache.Clear();

            if (resourceVisual == null) { DebugManager.LogWarning("Resource: resourceVisual is null, cannot cache pieces!"); return; }

            // Use non-allocating overload
            resourceVisual.GetComponentsInChildren(allPiecesCache);
            DebugManager.LogGathering($"Resource: Cached {allPiecesCache.Count} total pieces");
        }

        private void InitializeStage(int stageIndex)
        {
            currentStageIndex = stageIndex;
            piecesDetachedThisStage = 0;
            isNonYieldingStageInProgress = false;
            nonYieldingStageWorkerId = 0;
            isTransitioning = false;

            if (CurrentStage != null && CurrentStage.yieldsPieces)
            {
                RefreshAvailablePieces();

                // Promote waiting workers now that pieces are available
                PromoteWaitingWorkers();
            }
            else availablePieces.Clear();

            DebugManager.LogGathering($"Resource: Initialized stage {stageIndex} ({CurrentStage?.stageName}), yields pieces: {CurrentStage?.yieldsPieces}, pieces available: {availablePieces.Count}");
        }

        private void RefreshAvailablePieces()
        {
            availablePieces.Clear();

            // Use cached pieces instead of GetComponentsInChildren
            foreach (var piece in allPiecesCache)
                if (piece != null && !piece.IsDetached) availablePieces.Add(piece);

            // Only shuffle for random order - other orders are determined at detach time
            if (harvestOrder == EHarvestOrder.Random) ShuffleList(availablePieces);

            DebugManager.LogGathering($"Resource: Found {availablePieces.Count} harvestable pieces");
        }

        #endregion

        #region Polling-Based Work System

        /// <summary>
        /// Begin a work session for a worker. Call once when starting to gather.
        /// Returns true if work started, false if worker must wait or resource is depleted.
        /// </summary>
        /// <param name="workerId">Worker's instance ID (worker.GetInstanceID())</param>
        /// <param name="workerTransform">Worker's transform for position-based calculations</param>
        public bool BeginWork(int workerId, Transform workerTransform)
        {
            if (IsDepleted || CurrentStage == null)
            {
                DebugManager.LogGathering($"Resource: Cannot begin work - depleted or no stage");
                return false;
            }

            // Cancel any existing session for this worker
            CancelWork(workerId);

            // Check if worker must wait
            if (!CurrentStage.yieldsPieces)
            {
                // Non-yielding stage: only one worker at a time
                if (isNonYieldingStageInProgress && nonYieldingStageWorkerId != workerId)
                {
                    DebugManager.LogGathering($"Resource: Worker {workerId} queued - non-yielding stage in progress");
                    QueueWorker(workerId, workerTransform);
                    return true; // Session created, but in waiting state
                }

                isNonYieldingStageInProgress = true;
                nonYieldingStageWorkerId = workerId;
            }
            else
            {
                // Yielding stage: check if there are enough pieces
                int activeWorkerCount = 0;
                foreach (var kvp in activeSessions)
                {
                    if (!kvp.Value.IsWaiting) activeWorkerCount++;
                }
                
                if (activeWorkerCount >= availablePieces.Count && availablePieces.Count > 0)
                {
                    DebugManager.LogGathering($"Resource: Worker {workerId} queued - not enough pieces");
                    QueueWorker(workerId, workerTransform);
                    return true; // Session created, but in waiting state
                }
            }

            // Calculate work duration
            float duration = CurrentStage.workTimePerPiece;

            // Check for instant pickup on last piece
            if (CurrentStage.yieldsPieces && availablePieces.Count == 1 && CurrentStage.instantPickupLastPiece)
            {
                duration = 0f;
                DebugManager.LogGathering($"Resource: Instant pickup for last piece!");
            }

            // Create work session
            var session = new WorkSession
            {
                WorkerId = workerId,
                StartTime = Time.time,
                Duration = duration,
                WorkerTransform = workerTransform,
                IsComplete = false,
                IsWaiting = false,
                ResultFragment = null,
                StageTransitionPending = false
            };

            activeSessions[workerId] = session;
            DebugManager.LogGathering($"Resource: Worker {workerId} began work on stage {CurrentStage.stageName}");
            return true;
        }

        /// <summary>
        /// Check work progress for a worker. Call this every frame from the gathering state.
        /// </summary>
        /// <param name="workerId">Worker's instance ID</param>
        /// <param name="fragment">Output: the fragment if ready, null otherwise</param>
        /// <returns>Current work status</returns>
        public WorkResult CheckWorkProgress(int workerId, out ResourceFragment fragment)
        {
            fragment = null;

            if (!activeSessions.TryGetValue(workerId, out var session))
            {
                return WorkResult.Invalid;
            }

            // Worker is waiting in queue
            if (session.IsWaiting)
            {
                return WorkResult.Waiting;
            }

            // Stage transition is pending (animation playing)
            if (session.StageTransitionPending)
            {
                // Check if transition is complete
                if (!isTransitioning)
                {
                    // Transition complete, worker should re-begin work
                    return WorkResult.StageComplete;
                }
                return WorkResult.InProgress;
            }

            // Already collected fragment
            if (session.IsComplete && session.ResultFragment == null)
            {
                return WorkResult.Depleted;
            }

            // Fragment already ready (was collected previously)
            if (session.IsComplete && session.ResultFragment != null)
            {
                fragment = session.ResultFragment;
                return WorkResult.FragmentReady;
            }

            // Check if work time is complete
            if (!session.IsTimeComplete)
            {
                return WorkResult.InProgress;
            }

            // Work time complete - process result
            return ProcessWorkComplete(workerId, ref session, out fragment);
        }

        /// <summary>
        /// Process the completion of work for a session.
        /// </summary>
        private WorkResult ProcessWorkComplete(int workerId, ref WorkSession session, out ResourceFragment fragment)
        {
            fragment = null;

            if (CurrentStage == null)
            {
                session.IsComplete = true;
                activeSessions[workerId] = session;
                return WorkResult.Depleted;
            }

            if (CurrentStage.yieldsPieces && availablePieces.Count > 0)
            {
                // Detach a piece and create fragment
                fragment = DetachNextPiece(session.WorkerTransform);
                piecesDetachedThisStage++;

                session.IsComplete = true;
                session.ResultFragment = fragment;
                activeSessions[workerId] = session;

                DebugManager.LogGathering($"Resource: Worker {workerId} detached piece, remaining: {availablePieces.Count}");

                // Check if stage is now complete
                if (availablePieces.Count == 0)
                {
                    session.StageTransitionPending = true;
                    activeSessions[workerId] = session;
                    StartStageTransition();
                }

                return WorkResult.FragmentReady;
            }
            else if (!CurrentStage.yieldsPieces)
            {
                // Non-yielding stage complete
                session.IsComplete = true;
                session.StageTransitionPending = true;
                activeSessions[workerId] = session;

                // Clear non-yielding stage lock
                isNonYieldingStageInProgress = false;
                nonYieldingStageWorkerId = 0;

                DebugManager.LogGathering($"Resource: Worker {workerId} completed non-yielding stage");
                StartStageTransition();

                return WorkResult.StageComplete;
            }
            else
            {
                // No pieces available (shouldn't happen normally)
                session.IsComplete = true;
                activeSessions[workerId] = session;
                return WorkResult.Depleted;
            }
        }

        /// <summary>
        /// Collect the fragment from a completed work session.
        /// Call after CheckWorkProgress returns FragmentReady.
        /// </summary>
        public ResourceFragment CollectFragment(int workerId)
        {
            if (!activeSessions.TryGetValue(workerId, out var session))
            {
                return null;
            }

            var fragment = session.ResultFragment;
            
            // Clear the fragment from session (it's been collected)
            session.ResultFragment = null;
            activeSessions[workerId] = session;

            return fragment;
        }

        /// <summary>
        /// Cancel a work session for a worker.
        /// </summary>
        public void CancelWork(int workerId)
        {
            if (activeSessions.TryGetValue(workerId, out var session))
            {
                // If this was the non-yielding stage worker, release the lock
                if (nonYieldingStageWorkerId == workerId)
                {
                    isNonYieldingStageInProgress = false;
                    nonYieldingStageWorkerId = 0;
                    PromoteWaitingWorkers();
                }

                activeSessions.Remove(workerId);
            }

            waitingWorkerIds.Remove(workerId);
        }

        /// <summary>
        /// Check if a worker has an active work session.
        /// </summary>
        public bool HasActiveSession(int workerId)
        {
            return activeSessions.ContainsKey(workerId);
        }

        /// <summary>
        /// Get work progress (0-1) for a worker.
        /// </summary>
        public float GetWorkProgress(int workerId)
        {
            if (activeSessions.TryGetValue(workerId, out var session))
            {
                return session.Progress;
            }
            return 0f;
        }

        /// <summary>
        /// Add a worker to the waiting queue.
        /// </summary>
        private void QueueWorker(int workerId, Transform workerTransform)
        {
            if (!waitingWorkerIds.Contains(workerId))
            {
                waitingWorkerIds.Add(workerId);
            }

            // Create a waiting session
            var session = new WorkSession
            {
                WorkerId = workerId,
                StartTime = Time.time,
                Duration = 0f,
                WorkerTransform = workerTransform,
                IsComplete = false,
                IsWaiting = true,
                ResultFragment = null,
                StageTransitionPending = false
            };

            activeSessions[workerId] = session;
        }

        /// <summary>
        /// Promote waiting workers to active when slots become available.
        /// </summary>
        private void PromoteWaitingWorkers()
        {
            if (waitingWorkerIds.Count == 0) return;

            DebugManager.LogGathering($"Resource: Promoting {waitingWorkerIds.Count} waiting workers");

            // Copy list since BeginWork modifies it
            var workersToPromote = new List<int>(waitingWorkerIds);
            
            foreach (var workerId in workersToPromote)
            {
                if (!activeSessions.TryGetValue(workerId, out var session)) continue;
                if (!session.IsWaiting) continue;

                var workerTransform = session.WorkerTransform;

                // Remove from waiting
                waitingWorkerIds.Remove(workerId);
                activeSessions.Remove(workerId);

                // Try to begin work again
                BeginWork(workerId, workerTransform);
            }
        }

        #endregion

        #region Piece Selection

        private ResourceFragment DetachNextPiece(Transform workerTransform = null)
        {
            if (availablePieces.Count == 0) return null;

            ResourcePiece piece = null;

            piece = harvestOrder switch
            {
                EHarvestOrder.Closest => GetClosestPiece(workerTransform),
                EHarvestOrder.ClosestEndFirst => GetClosestEndPiece(workerTransform),
                EHarvestOrder.Sequential => availablePieces[0],
                EHarvestOrder.ReverseSequential => availablePieces[availablePieces.Count - 1],
                _ => availablePieces[0],
            };

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

                // Get position relative to the visual container
                if (resourceVisual != null) localY = resourceVisual.transform.InverseTransformPoint(piece.transform.position).y;
                else localY = piece.transform.localPosition.y;

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
            if (workerTransform == null) return availablePieces[0];

            ResourcePiece closest = null;
            float closestDistanceSqr = float.MaxValue;

            foreach (var piece in availablePieces)
            {
                if (piece == null || piece.IsDetached) continue;

                // Use sqrMagnitude to avoid sqrt
                float distanceSqr = (workerTransform.position - piece.transform.position).sqrMagnitude;
                if (distanceSqr < closestDistanceSqr)
                {
                    closestDistanceSqr = distanceSqr;
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
            if (IsDepleted || CurrentStage == null) return transform.position;

            // For non-yielding stages (like chopping), work at base position
            if (!CurrentStage.yieldsPieces) return transform.position;

            // For yielding stages, find the piece position
            if (availablePieces.Count == 0) return transform.position;

            ResourcePiece targetPiece = null;

            targetPiece = harvestOrder switch
            {
                EHarvestOrder.Closest => GetClosestPiece(workerTransform),
                EHarvestOrder.ClosestEndFirst => GetClosestEndPiece(workerTransform),
                EHarvestOrder.Sequential => availablePieces[0],
                EHarvestOrder.ReverseSequential => availablePieces[availablePieces.Count - 1],
                _ => availablePieces[0],
            };

            if (targetPiece != null) return targetPiece.transform.position;

            return transform.position;
        }

        /// <summary>
        /// Check if the current stage yields pieces (useful for determining work behavior).
        /// </summary>
        public bool DoesCurrentStageYieldPieces() => CurrentStage != null && CurrentStage.yieldsPieces;

        #endregion

        #region Stage Transitions

        private void StartStageTransition()
        {
            if (stageTransitionCoroutine != null)
            {
                StopCoroutine(stageTransitionCoroutine);
            }
            isTransitioning = true;
            stageTransitionCoroutine = StartCoroutine(TransitionToNextStage());
        }

        private IEnumerator TransitionToNextStage()
        {
            var completedStage = CurrentStage;

            if (completedStage != null && completedStage.playTransitionAnimation && resourceVisual != null)
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

                if (completedStage != null && completedStage.destroyOnComplete)
                {
                    EventBus.Publish(new ResourceDepletedEvent { Resource = gameObject });
                    Destroy(gameObject, 0.5f);
                }
            }

            isTransitioning = false;
            stageTransitionCoroutine = null;
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

        #endregion

        #region Utilities

        private void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        #endregion
    }
}
