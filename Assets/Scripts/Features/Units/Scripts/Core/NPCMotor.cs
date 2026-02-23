using System;
using Assets.Scripts.Data;
using UnityEngine;
using UnityEngine.AI;

namespace Assets.Scripts.NPCs
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class NPCMotor : MonoBehaviour
    {
        private NavMeshAgent agent;

        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float stoppingDistance = 0.5f;

        // Events for state machine integration
        public event Action OnArrived;
        public event Action OnPathFailed;
        public event Action OnStuck;

        // Stuck detection
        private Vector3 lastPosition;
        private float stuckTimer;
        private bool wasMoving;
        private bool hasPath;
        private Vector3 currentTargetPosition;  // Track actual target for stuck detection
        private float notMovingTimer;  // Track how long we've been not moving but far from target

        public bool IsMoving => agent.hasPath && !agent.pathPending && agent.remainingDistance > stoppingDistance;
        public bool HasPath => hasPath;
        public Vector3 Destination => agent.destination;
        public NavMeshAgent Agent => agent;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            agent.speed = moveSpeed;
            agent.stoppingDistance = stoppingDistance;
            lastPosition = transform.position;
        }

        private void Update()
        {
            CheckArrival();
            CheckStuck();
        }

        private void CheckArrival()
        {
            bool currentlyMoving = IsMoving;

            // Check for arrival (was moving, now stopped at destination)
            if (wasMoving && !currentlyMoving && hasPath)
            {
                // Verify we're actually close to the target, not just stopped
                float distToTarget = Vector3.Distance(transform.position, currentTargetPosition);
                if (distToTarget <= stoppingDistance * 3f) // Allow some tolerance
                {
                    hasPath = false;
                    notMovingTimer = 0f;
                    OnArrived?.Invoke();
                }
                // If not close, we might be stuck - let CheckStuck handle it
            }

            // Check for path failure
            if (hasPath && agent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                hasPath = false;
                notMovingTimer = 0f;
                OnPathFailed?.Invoke();
            }
            
            // Check for partial path (can't reach destination fully)
            // This happens when destination is blocked or unreachable
            if (hasPath && !agent.pathPending && agent.pathStatus == NavMeshPathStatus.PathPartial)
            {
                // If we're close to where the partial path ends and close to actual target
                float distToTarget = Vector3.Distance(transform.position, currentTargetPosition);
                if (!currentlyMoving && agent.remainingDistance <= stoppingDistance * 2f && distToTarget <= stoppingDistance * 3f)
                {
                    hasPath = false;
                    notMovingTimer = 0f;
                    OnArrived?.Invoke();
                }
            }
            
            // Detect being stuck: not moving, has path, but far from target
            if (hasPath && !currentlyMoving && !agent.pathPending)
            {
                float distToTarget = Vector3.Distance(transform.position, currentTargetPosition);
                if (distToTarget > stoppingDistance * 3f)
                {
                    notMovingTimer += Time.deltaTime;
                    // If stuck for more than 1 second, fire OnStuck
                    if (notMovingTimer >= 1f)
                    {
                        notMovingTimer = 0f;
                        OnStuck?.Invoke();
                    }
                }
                else
                {
                    notMovingTimer = 0f;
                }
            }
            else
            {
                notMovingTimer = 0f;
            }

            wasMoving = currentlyMoving;
        }

        private void CheckStuck()
        {
            if (!IsMoving)
            {
                stuckTimer = 0f;
                lastPosition = transform.position;
                return;
            }

            var settings = NPCSettingsSO.Instance;
            float movementThreshold = settings.StuckMovementThreshold;
            float stuckTime = settings.StuckDetectionTime;

            // Check if we've moved enough
            if (Vector3.Distance(transform.position, lastPosition) < movementThreshold)
            {
                stuckTimer += Time.deltaTime;
                if (stuckTimer >= stuckTime)
                {
                    stuckTimer = 0f;
                    OnStuck?.Invoke();
                }
            }
            else
            {
                stuckTimer = 0f;
            }

            lastPosition = transform.position;
        }

        public void SetDestination(Vector3 destination)
        {
            currentTargetPosition = destination;
            notMovingTimer = 0f;
            
            if (NavMesh.SamplePosition(destination, out NavMeshHit hit, 10f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
                hasPath = true;
                stuckTimer = 0f;
            }
            else
            {
                OnPathFailed?.Invoke();
            }
        }

        public void Stop()
        {
            if (agent != null)
            {
                agent.ResetPath();
                hasPath = false;
                notMovingTimer = 0f;
            }
        }

        public void SetSpeed(float speed) => agent.speed = speed;

        /// <summary>
        /// Clear all event subscriptions. Call when NPC is being destroyed or reset.
        /// </summary>
        public void ClearEventSubscriptions()
        {
            OnArrived = null;
            OnPathFailed = null;
            OnStuck = null;
        }
    }
}