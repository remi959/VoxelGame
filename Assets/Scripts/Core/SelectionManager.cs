using System.Collections.Generic;
using Assets.Scripts.Events;
using Assets.Scripts.NPCs.Modules;
using UnityEngine;

namespace Assets.Scripts.Core
{
    /// <summary>
    /// Tracks which Selectables are currently selected by the player.
    /// Pure selection state — command dispatching is handled by CommandDispatcher.
    /// Works with any Selectable (pawns, buildings, resources in the future).
    /// </summary>
    public class SelectionManager : MonoBehaviour
    {
        public static SelectionManager Instance { get; private set; }

        private readonly List<Selectable> selected = new();
        public IReadOnlyList<Selectable> Selected => selected;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnEnable()
        {
            Debug.Log("[SelectionManager] OnEnable — subscribing to events");
            EventBus.Subscribe<NPCSelectedEvent>(OnSelected);
            EventBus.Subscribe<NPCDeselectedEvent>(OnDeselected);
            EventBus.Subscribe<SelectionClearedEvent>(OnSelectionCleared);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<NPCSelectedEvent>(OnSelected);
            EventBus.Unsubscribe<NPCDeselectedEvent>(OnDeselected);
            EventBus.Unsubscribe<SelectionClearedEvent>(OnSelectionCleared);
        }

        private void OnSelected(NPCSelectedEvent e)
        {
            bool hasSelectable = e.NPC.TryGetComponent<Selectable>(out var selectable);
            Debug.Log($"[SelectionManager] OnSelected received for: {e.NPC.name}, has Selectable: {hasSelectable}");
            if (!hasSelectable) return;

            if (!e.AddToSelection)
                ClearSelection();

            if (!selected.Contains(selectable))
            {
                selected.Add(selectable);
                selectable.Select();
            }
        }

        private void OnDeselected(NPCDeselectedEvent e)
        {
            if (!e.NPC.TryGetComponent<Selectable>(out var selectable)) return;

            if (selected.Remove(selectable))
                selectable.Deselect();
        }

        private void OnSelectionCleared(SelectionClearedEvent e) => ClearSelection();

        private void ClearSelection()
        {
            foreach (var selectable in selected)
                selectable.Deselect();

            selected.Clear();
        }
    }
}