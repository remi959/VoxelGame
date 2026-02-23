using System.Collections;
using Assets.Scripts.Core;
using UnityEngine;

namespace Assets.Scripts.Buildings
{
    /// <summary>
    /// A crafted building part ready to be carried and placed.
    /// </summary>
    public class CraftedPart : MonoBehaviour
    {
        private BuildingPart sourcePart;
        private Vector3 originalScale;
        private float carryScale;
        private float placeAnimationDuration;

        private bool isPickedUp;
        private bool isPlaced;
        private Coroutine currentAnimation;

        private Rigidbody rb;
        private Collider col;

        // Target tracking for dropped part recovery
        private BuildingBlueprint targetBlueprint;
        private int targetSlotIndex = -1;

        public BuildingPart SourcePart => sourcePart;
        public bool IsPickedUp => isPickedUp;
        public bool IsPlaced => isPlaced;
        public bool CanBePickedUp => !isPickedUp && !isPlaced;

        /// <summary>
        /// The blueprint this part is destined for (set when crafted, cleared when placed).
        /// </summary>
        public BuildingBlueprint TargetBlueprint => targetBlueprint;

        /// <summary>
        /// The slot index this part is destined for (set when crafted, cleared when placed).
        /// </summary>
        public int TargetSlotIndex => targetSlotIndex;

        /// <summary>
        /// Set the target blueprint and slot for this part.
        /// Called when the part is crafted for a specific building slot.
        /// </summary>
        public void SetTarget(BuildingBlueprint blueprint, int slotIndex)
        {
            targetBlueprint = blueprint;
            targetSlotIndex = slotIndex;
        }

        /// <summary>
        /// Clear target info (called when part is successfully placed).
        /// </summary>
        public void ClearTarget()
        {
            targetBlueprint = null;
            targetSlotIndex = -1;
        }

        public void Initialize(BuildingPart source, Vector3 spawnPosition)
        {
            sourcePart = source;
            originalScale = transform.localScale;
            carryScale = source.CarryScale;
            placeAnimationDuration = source.PlaceAnimationDuration;

            transform.localScale = originalScale * carryScale;
            transform.position = spawnPosition;

            col = GetComponent<Collider>();
            if (col == null) col = gameObject.AddComponent<BoxCollider>();

            rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = 1f;
        }

        public void PickUp(Transform carryPoint)
        {
            if (isPickedUp || isPlaced) return;

            if (currentAnimation != null) StopCoroutine(currentAnimation);

            isPickedUp = true;

            if (rb != null) { Destroy(rb); rb = null; }
            if (col != null) col.enabled = false;

            currentAnimation = StartCoroutine(PickupAnimation(carryPoint));
        }

        private IEnumerator PickupAnimation(Transform carryPoint)
        {
            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;
            transform.SetParent(carryPoint);

            float elapsed = 0f;
            float duration = 0.25f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = 1f - Mathf.Pow(1f - elapsed / duration, 3f);

                transform.SetPositionAndRotation(
                    Vector3.Lerp(startPos, carryPoint.position, t),
                    Quaternion.Slerp(startRot, carryPoint.rotation, t)
                );
                yield return null;
            }

            transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            currentAnimation = null;
        }

        public void Place(Vector3 targetPos, Quaternion targetRot, Vector3 targetScale, Transform parent, System.Action onComplete)
        {
            if (!isPickedUp || isPlaced) return;

            if (currentAnimation != null) StopCoroutine(currentAnimation);

            isPlaced = true;
            transform.SetParent(parent);

            currentAnimation = StartCoroutine(PlaceAnimation(targetPos, targetRot, targetScale, onComplete));
        }

        private IEnumerator PlaceAnimation(Vector3 targetPos, Quaternion targetRot, Vector3 targetScale, System.Action onComplete)
        {
            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;
            Vector3 startScale = transform.localScale;

            float elapsed = 0f;
            while (elapsed < placeAnimationDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / placeAnimationDuration;

                transform.SetPositionAndRotation(
                    Vector3.Lerp(startPos, targetPos, t),
                    Quaternion.Slerp(startRot, targetRot, t)
                );
                transform.localScale = Vector3.Lerp(startScale, targetScale, t);
                yield return null;
            }

            transform.SetPositionAndRotation(targetPos, targetRot);
            transform.localScale = targetScale;

            isPickedUp = false;
            if (col != null) col.enabled = true;

            currentAnimation = null;
            onComplete?.Invoke();
        }

        public void Drop()
        {
            if (!isPickedUp) return;

            if (currentAnimation != null) StopCoroutine(currentAnimation);

            transform.SetParent(null);
            isPickedUp = false;

            if (col != null) col.enabled = true;
            rb = gameObject.AddComponent<Rigidbody>();
        }
    }
}