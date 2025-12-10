using System.Collections;
using UnityEngine;

namespace Assets.Scripts.Core
{
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private float speed = 10f;
        [SerializeField] private float moveToDuration = 0.5f;

        private Coroutine moveCoroutine;

        public void Move(Vector2 input)
        {
            if (input == Vector2.zero) return;

            Vector3 forward = transform.forward;
            forward.y = 0;
            forward.Normalize();

            Vector3 right = transform.right;
            right.y = 0;
            right.Normalize();

            Vector3 move = right * input.x + forward * input.y;
            transform.position += speed * Time.deltaTime * move;
        }

        public void MoveToPosition(Vector3 targetPosition)
        {
            // Keep camera height constant
            targetPosition.y = transform.position.y;

            if (moveCoroutine != null) StopCoroutine(moveCoroutine);

            moveCoroutine = StartCoroutine(SmoothlyMoveToPosition(targetPosition, moveToDuration));
        }

        public void CancelMovement()
        {
            if (moveCoroutine != null)
            {
                StopCoroutine(moveCoroutine);
                moveCoroutine = null;
            }
        }

        private IEnumerator SmoothlyMoveToPosition(Vector3 targetPosition, float duration)
        {
            Vector3 startPosition = transform.position;
            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                transform.position = Vector3.Lerp(startPosition, targetPosition, elapsedTime / duration);
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            transform.position = targetPosition;
        }
    }
}