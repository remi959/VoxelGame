using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Events;
using Assets.Scripts.NPCs.Modules;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Scripts.Core
{
    /// <summary>
    /// Handles drag-box selection visuals and detection.
    /// Works with the Selectable registry — any Selectable in the scene can be box-selected.
    /// </summary>
    public class SelectionBox : MonoBehaviour
    {
        [Header("Selection Box Visuals")]
        [SerializeField] private RectTransform selectionBoxUI;

        [Header("Settings")]
        [SerializeField] private Color boxColor = new(0.8f, 0.8f, 0.95f, 0.25f);
        [SerializeField] private Color borderColor = new(0.8f, 0.8f, 0.95f, 0.8f);

        private Camera mainCamera;
        private readonly List<GameObject> selectionBuffer = new();

        private void OnValidate()
        {
            if (selectionBoxUI == null)
                Debug.LogWarning($"[{nameof(SelectionBox)}] Missing selectionBoxUI reference");
        }

        private void Awake()
        {
            mainCamera = Camera.main;

            if (selectionBoxUI != null)
                selectionBoxUI.gameObject.SetActive(false);
        }

        public void UpdateSelectionBox(Vector2 startPos, Vector2 currentPos)
        {
            if (selectionBoxUI == null) return;

            if (!selectionBoxUI.gameObject.activeSelf)
                selectionBoxUI.gameObject.SetActive(true);

            float width = currentPos.x - startPos.x;
            float height = currentPos.y - startPos.y;

            selectionBoxUI.anchoredPosition = new Vector2(
                startPos.x + width / 2,
                startPos.y + height / 2
            );

            selectionBoxUI.sizeDelta = new Vector2(Mathf.Abs(width), Mathf.Abs(height));
        }

        public void FinishSelection(Vector2 startPos, Vector2 endPos)
        {
            if (selectionBoxUI != null)
                selectionBoxUI.gameObject.SetActive(false);

            SelectUnitsInBox(startPos, endPos);
        }

        private void SelectUnitsInBox(Vector2 startPos, Vector2 endPos)
        {
            selectionBuffer.Clear();

            Rect selectionRect = new(
                Mathf.Min(startPos.x, endPos.x),
                Mathf.Min(startPos.y, endPos.y),
                Mathf.Abs(endPos.x - startPos.x),
                Mathf.Abs(endPos.y - startPos.y)
            );

            bool addToSelection = Keyboard.current.shiftKey.isPressed;

            if (!addToSelection)
                EventBus.Publish(new SelectionClearedEvent());

            Selectable[] allSelectables = Selectable.All.ToArray();

            foreach (Selectable selectable in allSelectables)
            {
                Vector3 screenPos = mainCamera.WorldToScreenPoint(selectable.transform.position);

                if (screenPos.z > 0 && selectionRect.Contains(screenPos))
                    selectionBuffer.Add(selectable.gameObject);
            }

            foreach (GameObject obj in selectionBuffer)
                EventBus.Publish(new NPCSelectedEvent { NPC = obj, AddToSelection = true });
        }
    }
}