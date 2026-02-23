// ============================================================================
// WorkQueueService.cs - Centralized work coordination
// ============================================================================
using System;
using System.Collections.Generic;
using Assets.Scripts.Buildings.Construction;
using Assets.Scripts.Core;
using Assets.Scripts.Core.Services;
using Assets.Scripts.NPCs.Units;
using Assets.Scripts.Shared;
using UnityEngine;

namespace Assets.Scripts.Buildings
{
    /// <summary>
    /// Coordinates worker task assignments across all construction sites.
    /// 
    /// Responsibilities:
    /// - Maintain priority queue of construction tasks
    /// - Assign workers to optimal tasks
    /// - Balance workload across sites
    /// - Handle worker availability changes
    /// 
    /// Benefits over per-worker discovery:
    /// - Workers don't all run to the same building
    /// - Can implement priority (military buildings first)
    /// - Can implement distance optimization
    /// - Can reserve tasks to prevent conflicts
    /// </summary>
    public class WorkQueueService : MonoBehaviour
    {
        public static WorkQueueService Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private float taskAssignmentInterval = 0.5f;
        [SerializeField] private int maxTasksPerFrame = 5;

        // Workers waiting for tasks
        private readonly Queue<WorkerTaskRequest> pendingRequests = new();

        // Active task assignments
        private readonly Dictionary<int, TaskAssignment> activeAssignments = new();

        // Task assignment timing
        private float lastAssignmentTime;

        /// <summary>
        /// Request a task for a worker.
        /// </summary>
        public struct WorkerTaskRequest
        {
            public Worker Worker;
            public int WorkerId;
            public int PlayerId;
            public Vector3 Position;
            public float RequestTime;
        }

        /// <summary>
        /// An active task assignment.
        /// </summary>
        public struct TaskAssignment
        {
            public int WorkerId;
            public string SiteId;
            public TaskType Type;
            /// <summary>
            /// Time.time value when the task was assigned.
            /// </summary>
            public float AssignedAt;
        }

        public enum TaskType
        {
            Construction,
            ResourceDelivery,
            Repair
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            ServiceLocator.Register(this);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                ServiceLocator.Unregister<WorkQueueService>();
            }
        }

        private void Update()
        {
            // Periodically process task queue
            if (Time.time - lastAssignmentTime >= taskAssignmentInterval)
            {
                lastAssignmentTime = Time.time;
                ProcessTaskQueue();
            }
        }

        // ========== Public API ==========

        /// <summary>
        /// Request a construction task for an idle worker.
        /// Returns immediately; task assigned via callback or event.
        /// </summary>
        public void RequestTask(Worker worker, int playerId, Action<BuildingSite> onTaskAssigned)
        {
            if (worker == null) return;

            int workerId = worker.GetInstanceID();

            // Check if already has a task
            if (activeAssignments.ContainsKey(workerId))
            {
                Debug.LogWarning($"WorkQueueService: Worker {workerId} already has a task");
                return;
            }

            // Try immediate assignment
            var site = FindOptimalSite(worker.transform.position, playerId);

            if (site != null && site.CanAssignWorker)
            {
                AssignWorkerToSite(worker, site);
                onTaskAssigned?.Invoke(site);
                return;
            }

            // Queue for later assignment
            pendingRequests.Enqueue(new WorkerTaskRequest
            {
                Worker = worker,
                WorkerId = workerId,
                PlayerId = playerId,
                Position = worker.transform.position,
                RequestTime = Time.time
            });
        }

        /// <summary>
        /// Notify that a worker has completed or abandoned their task.
        /// </summary>
        public void ReleaseTask(Worker worker)
        {
            if (worker == null) return;

            int workerId = worker.GetInstanceID();

            if (activeAssignments.TryGetValue(workerId, out var assignment))
            {
                // Notify construction service
                var site = ConstructionService.Instance.GetSite(assignment.SiteId);
                site?.UnassignWorker(workerId);

                activeAssignments.Remove(workerId);
            }
        }

        /// <summary>
        /// Get current task assignment for a worker.
        /// </summary>
        public TaskAssignment? GetAssignment(Worker worker)
        {
            if (worker == null) return null;

            int workerId = worker.GetInstanceID();

            if (activeAssignments.TryGetValue(workerId, out var assignment))
            {
                return assignment;
            }

            return null;
        }

        // ========== Task Processing ==========

        private void ProcessTaskQueue()
        {
            int processed = 0;

            while (pendingRequests.Count > 0 && processed < maxTasksPerFrame)
            {
                var request = pendingRequests.Dequeue();

                // Validate worker still exists and is idle
                if (request.Worker == null) continue;
                if (activeAssignments.ContainsKey(request.WorkerId)) continue;

                // Find best site
                var site = FindOptimalSite(request.Position, request.PlayerId);

                if (site != null && site.CanAssignWorker)
                {
                    AssignWorkerToSite(request.Worker, site);
                }
                else
                {
                    // Re-queue if no site available (with timeout)
                    if (Time.time - request.RequestTime < 30f)
                    {
                        // Re-add to back of queue
                        pendingRequests.Enqueue(request);
                    }
                    // Otherwise, request expires
                }

                processed++;
            }
        }

        private BuildingSite FindOptimalSite(Vector3 workerPosition, int playerId)
        {
            BuildingSite nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var site in ConstructionService.Instance.GetSitesNeedingWorkers())
            {
                if (site.OwnerPlayerId != playerId) continue;
                if (!site.CanAssignWorker) continue;

                float dist = Vector3.Distance(workerPosition, site.Position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = site;
                }
            }

            return nearest;
        }

        private void AssignWorkerToSite(Worker worker, BuildingSite site)
        {
            int workerId = worker.GetInstanceID();

            // Create assignment
            var assignment = new TaskAssignment
            {
                WorkerId = workerId,
                SiteId = site.SiteId,
                Type = TaskType.Construction,
                AssignedAt = Time.time
            };

            activeAssignments[workerId] = assignment;

            // Notify construction service
            ConstructionService.Instance.OnWorkerStartedConstruction(site.SiteId, workerId);

            // Start worker on building task
            var blueprint = site.SceneObject?.GetComponent<BuildingBlueprint>();
            if (blueprint != null)
            {
                worker.StartBuilding(blueprint);
            }

            Debug.Log($"WorkQueueService: Assigned worker {workerId} to {site.SiteId}");
        }
    }
}