using System.Collections;
using Assets.Scripts.Shared.Enums;
using UnityEngine;

namespace Assets.Scripts.Resources
{
    public class ResourceFragment : MonoBehaviour
    {
        [SerializeField] private float pickupDistance = 1.5f;
        [SerializeField] private float pickupAnimationDuration = 0.25f;
        [SerializeField] private float dropAnimationDuration = 0.2f;
        [SerializeField] private float stackHeight = 0.5f;
        [SerializeField] private float carryScale = 0.5f;

        private EResourceType resourceType;
        private int value;
        private bool isPickedUp = false;
        private bool isAnimating = false;
        private Rigidbody rb;
        private Collider col;
        private Coroutine currentCoroutine;

        private int assignedStackIndex = -1;
        private Transform assignedCarryPoint;

        public EResourceType Type => resourceType;
        public int Value => value;
        public float PickupDistance => pickupDistance;
        public bool CanBePickedUp => !isPickedUp && !isAnimating && IsSettled();
        public bool IsPickedUp => isPickedUp;
        public bool IsAnimating => isAnimating;

        public void Initialize(EResourceType type, int resourceValue)
        {
            resourceType = type;
            value = resourceValue;
            isPickedUp = false;
            isAnimating = false;
            rb = GetComponent<Rigidbody>();
            col = GetComponent<Collider>();
        }

        private bool IsSettled()
        {
            if (rb == null) return true;
            if (rb.isKinematic) return true;
            return rb.linearVelocity.magnitude < 0.1f;
        }

        public void PickUp(Transform carryPoint, int stackIndex)
        {
            if (isPickedUp) return;

            if (currentCoroutine != null)
            {
                StopCoroutine(currentCoroutine);
                currentCoroutine = null;
            }

            isPickedUp = true;
            isAnimating = true;
            assignedStackIndex = stackIndex;
            assignedCarryPoint = carryPoint;

            Debug.Log($"Fragment: PickUp called, stackIndex={stackIndex}");

            // COMPLETELY remove physics components to prevent any interference
            if (rb != null)
            {
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                Destroy(rb);
                rb = null;
            }

            if (col != null)
            {
                Destroy(col);
                col = null;
            }

            float yOffset = stackIndex * stackHeight;
            Vector3 targetLocalPos = new(0, yOffset, 0);

            Debug.Log($"Fragment: Target local position = {targetLocalPos}");

            currentCoroutine = StartCoroutine(SmoothPickupAnimation(carryPoint, targetLocalPos));
        }

        private IEnumerator SmoothPickupAnimation(Transform carryPoint, Vector3 targetLocalPos)
        {
            // Capture world state BEFORE any changes
            Vector3 startWorldPos = transform.position;
            Quaternion startWorldRot = transform.rotation;
            Vector3 startWorldScale = transform.lossyScale;

            // Parent to carry point
            transform.SetParent(carryPoint);

            // Immediately set to target scale to avoid scale inheritance issues
            Vector3 targetScale = Vector3.one * carryScale;
            
            // Calculate what local scale gives us the desired world scale
            // This accounts for any parent scaling
            if (carryPoint.lossyScale.x != 0 && carryPoint.lossyScale.y != 0 && carryPoint.lossyScale.z != 0)
            {
                targetScale = new Vector3(
                    carryScale / carryPoint.lossyScale.x,
                    carryScale / carryPoint.lossyScale.y,
                    carryScale / carryPoint.lossyScale.z
                );
            }

            // Get current local scale after parenting
            Vector3 startLocalScale = transform.localScale;

            float elapsed = 0f;

            while (elapsed < pickupAnimationDuration)
            {
                if (this == null || transform == null) yield break;

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / pickupAnimationDuration);
                float smoothT = 1f - Mathf.Pow(1f - t, 3f);

                // Calculate current target world position
                Vector3 targetWorldPos = carryPoint.TransformPoint(targetLocalPos);
                Quaternion targetWorldRot = carryPoint.rotation;

                // Interpolate position and rotation in world space
                transform.position = Vector3.Lerp(startWorldPos, targetWorldPos, smoothT);
                transform.rotation = Quaternion.Slerp(startWorldRot, targetWorldRot, smoothT);
                
                // Interpolate scale in local space
                transform.localScale = Vector3.Lerp(startLocalScale, targetScale, smoothT);

                yield return null;
            }

            // Final snap - use local position directly for precision
            transform.localPosition = targetLocalPos;
            transform.localRotation = Quaternion.identity;
            transform.localScale = targetScale;

            isAnimating = false;
            currentCoroutine = null;

            Debug.Log($"Fragment: Attached at local pos {transform.localPosition}");
        }

        public void UpdateStackPosition(int newStackIndex)
        {
            if (!isPickedUp) return;

            assignedStackIndex = newStackIndex;
            float yOffset = newStackIndex * stackHeight;

            if (!isAnimating)
            {
                transform.localPosition = new Vector3(0, yOffset, 0);
            }
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
                transform.localPosition = new Vector3(0, yOffset, 0);
                transform.localRotation = Quaternion.identity;
                transform.localScale = Vector3.one * carryScale;
            }

            isAnimating = false;
        }

        public void Drop()
        {
            ForceCompletePickup();
            currentCoroutine = StartCoroutine(DropAnimation());
        }

        private IEnumerator DropAnimation()
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

            Destroy(gameObject);
        }
    }
}