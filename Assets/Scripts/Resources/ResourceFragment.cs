using System;
using System.Collections;
using Assets.Scripts.Core;
using Assets.Scripts.Shared.Enums;
using Assets.Scripts.Shared.Pooling;
using UnityEngine;
using UnityEngine.AI;

namespace Assets.Scripts.Resources
{
    public class ResourceFragment : MonoBehaviour
    {
        [Header("Pickup Settings")]
        [SerializeField] private float pickupDistance = 1.5f;
        [SerializeField] private float pickupAnimationDuration = 0.25f;
        [SerializeField] private float dropAnimationDuration = 0.2f;

        [Header("Carry Settings")]
        [SerializeField] private float stackHeight = 0.5f;
        [SerializeField] private float carryScale = 0.4f;

        [Header("Physics Settings")]
        [SerializeField] private float launchForce = 3f;
        [SerializeField] private float launchAngle = 45f;
        [SerializeField] private float maxDistance = 3f;
        [SerializeField] private float settleTime = 2f;

        private EResourceType resourceType;
        private int value;
        private VisualKey visualKey;

        // State flags
        private bool isPickedUp = false;
        private bool isAnimating = false;
        private bool isLaunched = false;
        private bool isDropping = false;

        // Components
        private Rigidbody rb;
        private Collider col;
        private NavMeshObstacle navObstacle;
        private Coroutine currentCoroutine;

        // Carry state
        private int assignedStackIndex = -1;
        private Transform assignedCarryPoint;

        // Launch tracking
        private Vector3 launchStartPosition;
        private float launchTimer;

        #region Properties

        public EResourceType Type => resourceType;
        public int Value => value;

        /// <summary>
        /// Visual key including category (resource type), variant ID, and piece type.
        /// Used for proper pooling of different visual variants.
        /// </summary>
        public VisualKey VisualKey => visualKey;

        public float PickupDistance => pickupDistance;
        public bool IsPickedUp => isPickedUp;
        public bool IsAnimating => isAnimating;
        public bool IsDropping => isDropping;
        public bool CanBePickedUp => !isPickedUp && !isAnimating && !isDropping && IsSettled();

        #endregion

        #region Setup

        /// <summary>
        /// Initialize the fragment for use (called when spawned or retrieved from pool).
        /// </summary>
        public void Setup(EResourceType type, int resourceValue, Vector3 position, NavMeshObstacle obstacle = null, Collider collider = null, Transform parent = null)
        {
            resourceType = type;
            value = resourceValue;
            col = collider;

            // Reset state
            isPickedUp = false;
            isAnimating = false;
            isLaunched = false;
            isDropping = false;
            assignedStackIndex = -1;
            assignedCarryPoint = null;
            launchTimer = 0f;

            // Set transform
            transform.SetParent(parent);
            transform.SetPositionAndRotation(position, Quaternion.identity);
            transform.localScale = Vector3.one;

            // Cache NavMeshObstacle reference (passed from piece)
            navObstacle = obstacle;

            // Setup physics
            SetupPhysics();
        }

        /// <summary>
        /// Set the visual key for this fragment (used for pooling).
        /// </summary>
        public void SetVisualKey(VisualKey key) => visualKey = key;

        private void SetupPhysics()
        {
            // Add Rigidbody
            rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.linearDamping = 0.5f;
            rb.angularDamping = 0.5f;
            rb.isKinematic = false;
        }

        #endregion

        #region Launch

        /// <summary>
        /// Launch the fragment in a random direction.
        /// </summary>
        public void Launch()
        {
            if (rb == null) return;

            isLaunched = true;
            launchStartPosition = transform.position;
            launchTimer = 0f;

            Vector3 launchDir = CalculateLaunchDirection();
            rb.AddForce(launchDir * launchForce, ForceMode.Impulse);
            rb.AddTorque(UnityEngine.Random.insideUnitSphere * launchForce * 0.3f, ForceMode.Impulse);
        }

        private Vector3 CalculateLaunchDirection()
        {
            float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
            Vector3 horizontal = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            float upward = Mathf.Tan(launchAngle * Mathf.Deg2Rad);
            return (horizontal + Vector3.up * upward).normalized;
        }

        private void Update()
        {
            if (!isLaunched || isPickedUp || rb == null || rb.isKinematic) return;

            launchTimer += Time.deltaTime;

            // Check distance constraint
            float horizontalDist = Vector3.Distance(
                new Vector3(launchStartPosition.x, 0, launchStartPosition.z),
                new Vector3(transform.position.x, 0, transform.position.z)
            );

            // Stop if too far or time elapsed
            if (horizontalDist > maxDistance || launchTimer >= settleTime) StopMovement();
        }

        private void StopMovement()
        {
            if (rb == null || rb.isKinematic) return;

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            isLaunched = false;

            // Disable NavMeshObstacle so NPCs can walk to pickup
            if (navObstacle != null) navObstacle.enabled = false;

            // Snap to ground
            if (Physics.Raycast(transform.position + Vector3.up, Vector3.down, out RaycastHit hit, 10f))
                transform.position = hit.point + Vector3.up * 0.05f;
        }

        private bool IsSettled()
        {
            if (rb == null) return true;
            if (rb.isKinematic) return true;
            return rb.linearVelocity.magnitude < 0.1f;
        }

        #endregion

        #region Pickup

        public void PickUp(Transform carryPoint, int stackIndex)
        {
            if (isPickedUp || isDropping) return;

            // Stop any existing animation
            if (currentCoroutine != null)
            {
                StopCoroutine(currentCoroutine);
                currentCoroutine = null;
            }

            isPickedUp = true;
            isAnimating = true;
            isLaunched = false;
            assignedStackIndex = stackIndex;
            assignedCarryPoint = carryPoint;

            DebugManager.LogGathering($"Fragment: PickUp called, stackIndex={stackIndex}, key={visualKey}");

            // Disable NavMeshObstacle
            if (navObstacle != null) navObstacle.enabled = false;

            // Remove physics components
            if (rb != null) { Destroy(rb); rb = null; }
            if (col != null) { Destroy(col); col = null; }

            float yOffset = stackIndex * stackHeight;
            Vector3 targetLocalPos = new(0, yOffset, 0);

            currentCoroutine = StartCoroutine(SmoothPickupAnimation(carryPoint, targetLocalPos));
        }

        private IEnumerator SmoothPickupAnimation(Transform carryPoint, Vector3 targetLocalPos)
        {
            transform.GetPositionAndRotation(out Vector3 startWorldPos, out Quaternion startWorldRot);
            transform.SetParent(carryPoint);

            // Calculate target scale accounting for parent scale
            Vector3 targetScale = Vector3.one * carryScale;
            if (carryPoint.lossyScale.x != 0 && carryPoint.lossyScale.y != 0 && carryPoint.lossyScale.z != 0)
            {
                targetScale = new Vector3(
                    carryScale / carryPoint.lossyScale.x,
                    carryScale / carryPoint.lossyScale.y,
                    carryScale / carryPoint.lossyScale.z
                );
            }

            Vector3 startLocalScale = transform.localScale;
            float elapsed = 0f;

            while (elapsed < pickupAnimationDuration)
            {
                if (this == null || transform == null) yield break;

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / pickupAnimationDuration);

                // Ease out cubic
                float smoothT = 1f - Mathf.Pow(1f - t, 3f);

                Vector3 targetWorldPos = carryPoint.TransformPoint(targetLocalPos);
                Quaternion targetWorldRot = carryPoint.rotation;

                transform.SetPositionAndRotation(Vector3.Lerp(startWorldPos, targetWorldPos, smoothT), Quaternion.Slerp(startWorldRot, targetWorldRot, smoothT));
                transform.localScale = Vector3.Lerp(startLocalScale, targetScale, smoothT);

                yield return null;
            }

            // Snap to final position
            transform.SetLocalPositionAndRotation(targetLocalPos, Quaternion.identity);
            transform.localScale = targetScale;

            isAnimating = false;
            currentCoroutine = null;
        }

        public void UpdateStackPosition(int newStackIndex)
        {
            if (!isPickedUp) return;

            assignedStackIndex = newStackIndex;
            float yOffset = newStackIndex * stackHeight;

            if (!isAnimating) transform.localPosition = new Vector3(0, yOffset, 0);
        }

        public void ForceCompletePickup()
        {
            if (currentCoroutine != null)
            {
                StopCoroutine(currentCoroutine);
                currentCoroutine = null;
            }

            if (isPickedUp && assignedCarryPoint != null && assignedStackIndex >= 0)
            {
                float yOffset = assignedStackIndex * stackHeight;
                transform.SetLocalPositionAndRotation(new Vector3(0, yOffset, 0), Quaternion.identity);
                transform.localScale = Vector3.one * carryScale;
            }

            isAnimating = false;
        }

        #endregion

        #region Drop

        /// <summary>
        /// Drop the fragment with animation, then return to pool or destroy.
        /// </summary>
        public void Drop(Action onDropComplete = null)
        {
            if (isDropping) return;

            ForceCompletePickup();
            isDropping = true;
            isPickedUp = false;

            currentCoroutine = StartCoroutine(DropAnimation(onDropComplete));
        }

        private IEnumerator DropAnimation(Action onComplete)
        {
            transform.SetParent(null);

            Vector3 startScale = transform.localScale;
            float elapsed = 0f;

            while (elapsed < dropAnimationDuration)
            {
                if (this == null) yield break;

                elapsed += Time.deltaTime;
                float t = elapsed / dropAnimationDuration;
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);

                yield return null;
            }

            onComplete?.Invoke();

            // Return to pool or destroy
            Destroy(gameObject);
        }

        #endregion
    }
}