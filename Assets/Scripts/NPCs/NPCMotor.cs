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

        public bool IsMoving => agent.hasPath && agent.remainingDistance > stoppingDistance;
        public Vector3 Destination => agent.destination;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            agent.speed = moveSpeed;
            agent.stoppingDistance = stoppingDistance;
        }

        public void SetDestination(Vector3 destination)
        {
            if (NavMesh.SamplePosition(destination, out NavMeshHit hit, 10f, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
        }

        public void Stop()
        {
            if (agent != null) agent.ResetPath();
        }

        public void SetSpeed(float speed)
        {
            agent.speed = speed;
        }
    }
}