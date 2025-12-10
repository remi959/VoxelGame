using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Events;
using Assets.Scripts.NPCs;
using Assets.Scripts.Shared.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Scripts.Core
{
    public class SelectionBox : MonoBehaviour
    {
        [Header("Selection Box Visuals")]
        [SerializeField] private RectTransform selectionBoxUI;

        [Header("Settings")]
        [SerializeField] private Color boxColor = new(0.8f, 0.8f, 0.95f, 0.25f);
        [SerializeField] private Color borderColor = new(0.8f, 0.8f, 0.95f, 0.8f);

        private Camera mainCamera;
        private readonly List<GameObject> selectedNPCsBuffer = new();

        private void OnValidate()
        {
            if (selectionBoxUI == null) Debug.LogWarning($"[{nameof(SelectionBox)}] Missing selectionBoxUI reference");
        }

        private void Awake()
        {
            mainCamera = Camera.main;

            // Ensure selection box UI is hidden at start
            if (selectionBoxUI != null) selectionBoxUI.gameObject.SetActive(false);
        }

        public void UpdateSelectionBox(Vector2 startPos, Vector2 currentPos)
        {
            if (selectionBoxUI == null) return;

            if (!selectionBoxUI.gameObject.activeSelf)
                selectionBoxUI.gameObject.SetActive(true);

            // Calculate box dimensions
            float width = currentPos.x - startPos.x;
            float height = currentPos.y - startPos.y;

            // Set position (anchored to bottom-left of box)
            selectionBoxUI.anchoredPosition = new Vector2(
                startPos.x + width / 2,
                startPos.y + height / 2
            );

            // Set size (absolute values for width/height)
            selectionBoxUI.sizeDelta = new Vector2(Mathf.Abs(width), Mathf.Abs(height));
        }

        public void FinishSelection(Vector2 startPos, Vector2 endPos)
        {
            // Hide the selection box
            if (selectionBoxUI != null)
                selectionBoxUI.gameObject.SetActive(false);

            // Get all selectable NPCs
            SelectUnitsInBox(startPos, endPos);
        }

        private void SelectUnitsInBox(Vector2 startPos, Vector2 endPos)
        {
            selectedNPCsBuffer.Clear();

            // Calculate the selection rectangle (handle any drag direction)
            Rect selectionRect = new(
                Mathf.Min(startPos.x, endPos.x),
                Mathf.Min(startPos.y, endPos.y),
                Mathf.Abs(endPos.x - startPos.x),
                Mathf.Abs(endPos.y - startPos.y)
            );

            // Check if shift is held for additive selection
            bool addToSelection = Keyboard.current.shiftKey.isPressed;

            if (!addToSelection)
            {
                EventBus.Publish(new SelectionClearedEvent());
            }

            // Find all NPCs in the scene
            NPCBase[] allNPCs = NPCBase.All.ToArray();

            foreach (NPCBase npc in allNPCs)
            {
                // Convert NPC world position to screen position
                Vector3 screenPos = mainCamera.WorldToScreenPoint(npc.transform.position);

                // Check if NPC is in front of camera and within selection box
                if (screenPos.z > 0 && selectionRect.Contains(screenPos))
                {
                    selectedNPCsBuffer.Add(npc.gameObject);
                }
            }

            // Publish selection event for each NPC found
            foreach (GameObject npc in selectedNPCsBuffer)
            {
                EventBus.Publish(new NPCSelectedEvent
                {
                    NPC = npc,
                    AddToSelection = true // Always additive within a box selection
                });
            }
        }
    }
}