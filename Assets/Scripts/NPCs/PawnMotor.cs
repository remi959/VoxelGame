using UnityEngine;
using UnityEngine.AI;

namespace Assets.Scripts.NPCs
{
    /// <summary>
    /// Handles NPC locomotion via NavMeshAgent.
    /// Pure movement — no awareness of jobs, selection, or game logic.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class PawnMotor : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float stoppingDistance = 0.5f;

        private NavMeshAgent agent;

        public bool IsMoving => agent.pathPending || (agent.hasPath && agent.remainingDistance > stoppingDistance);
        public Vector3 Destination => agent.destination;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            agent.speed = moveSpeed;
            agent.stoppingDistance = stoppingDistance;
        }

        /// <summary>
        /// Navigate to a world position. Snaps to nearest valid NavMesh point.
        /// Returns true if a valid destination was found.
        /// </summary>
        public bool SetDestination(Vector3 destination)
        {
            if (NavMesh.SamplePosition(destination, out NavMeshHit hit, 10f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
                return true;
            }

            return false;
        }

        public void Stop()
        {
            if (agent != null)
                agent.ResetPath();
        }

        public void SetSpeed(float speed)
        {
            agent.speed = speed;
        }
    }
}
