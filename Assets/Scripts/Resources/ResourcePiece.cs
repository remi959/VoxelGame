using Assets.Scripts.Shared.Enums;
using UnityEngine;

namespace Assets.Scripts.Resources
{
    public class ResourcePiece : MonoBehaviour
    {
        [Header("Detach Settings")]
        [SerializeField] private float launchForce = 3f;
        [SerializeField] private float launchAngle = 45f;
        [SerializeField] private float maxDistance = 3f;
        [SerializeField] private float stopAfterSeconds = 2f;

        [Header("Fragment Settings")]
        [SerializeField] private int resourceValue = 5;

        private bool isDetached = false;
        private Rigidbody rb;
        private Collider col;
        private float stopTimer;
        private Vector3 startPosition;
        private bool shouldStop = false;

        public bool IsDetached => isDetached;

        void Awake()
        {
            col = TryGetComponent<Collider>(out var collider) ? collider : null;
        }

        public ResourceFragment Detach(EResourceType resourceType)
        {
            if (isDetached) return null;
            isDetached = true;

            // Detach from parent
            transform.SetParent(null);

            // Setup physics
            rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.linearDamping = 0.5f;
            rb.angularDamping = 0.5f;

            // Ensure we have a collider
            if (col == null)
            {
                Debug.LogWarning("ResourcePiece: No collider found, adding BoxCollider.");
                return null;
            }

            // Add fragment component
            ResourceFragment fragment = gameObject.AddComponent<ResourceFragment>();
            fragment.Initialize(resourceType, resourceValue);

            // Calculate launch direction
            startPosition = transform.position;
            Vector3 launchDir = CalculateLaunchDirection();

            // Apply force
            rb.AddForce(launchDir * launchForce, ForceMode.Impulse);
            rb.AddTorque(0.3f * launchForce * Random.insideUnitSphere, ForceMode.Impulse);

            // Start monitoring
            shouldStop = false;
            stopTimer = 0f;

            return fragment;
        }

        private Vector3 CalculateLaunchDirection()
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            Vector3 horizontal = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            float upward = Mathf.Tan(launchAngle * Mathf.Deg2Rad);
            return (horizontal + Vector3.up * upward).normalized;
        }

        private void Update()
        {
            if (!isDetached || shouldStop || rb == null) return;

            stopTimer += Time.deltaTime;

            // Check distance constraint
            float horizontalDist = Vector3.Distance(
                new Vector3(startPosition.x, 0, startPosition.z),
                new Vector3(transform.position.x, 0, transform.position.z)
            );

            // Stop if too far or time elapsed
            if (horizontalDist > maxDistance || stopTimer >= stopAfterSeconds)
            {
                StopMovement();
            }
        }

        private void StopMovement()
        {
            if (shouldStop || rb == null) return;
            shouldStop = true;

            // Only set velocities if not already kinematic
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }

            // Snap to ground
            if (Physics.Raycast(transform.position + Vector3.up, Vector3.down, out RaycastHit hit, 10f))
            {
                transform.position = hit.point + Vector3.up * 0.05f;
            }
        }
    }
}