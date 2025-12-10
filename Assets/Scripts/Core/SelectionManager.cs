using System.Collections.Generic;
using Assets.Scripts.Events;
using Assets.Scripts.NPCs;
using UnityEngine;

namespace Assets.Scripts.Core
{
    public class SelectionManager : MonoBehaviour
    {
        public static SelectionManager Instance { get; private set; }

        private readonly List<NPCBase> selectedNPCs = new();
        public IReadOnlyList<NPCBase> SelectedNPCs => selectedNPCs;

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
            EventBus.Subscribe<NPCSelectedEvent>(OnNPCSelected);
            EventBus.Subscribe<NPCDeselectedEvent>(OnNPCDeselected);
            EventBus.Subscribe<SelectionClearedEvent>(OnSelectionCleared);
            EventBus.Subscribe<MoveCommandEvent>(OnMoveCommand);
            EventBus.Subscribe<InteractCommandEvent>(OnInteractCommand);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<NPCSelectedEvent>(OnNPCSelected);
            EventBus.Unsubscribe<NPCDeselectedEvent>(OnNPCDeselected);
            EventBus.Unsubscribe<SelectionClearedEvent>(OnSelectionCleared);
            EventBus.Unsubscribe<MoveCommandEvent>(OnMoveCommand);
            EventBus.Unsubscribe<InteractCommandEvent>(OnInteractCommand);
        }

        private void OnNPCSelected(NPCSelectedEvent e)
        {
            if (!e.NPC.TryGetComponent<NPCBase>(out var npc)) return;

            if (!e.AddToSelection) ClearSelection();

            if (!selectedNPCs.Contains(npc))
            {
                selectedNPCs.Add(npc);
                npc.OnSelected();
            }
        }

        private void OnNPCDeselected(NPCDeselectedEvent e)
        {
            if (!e.NPC.TryGetComponent<NPCBase>(out var npc)) return;

            if (selectedNPCs.Remove(npc)) npc.OnDeselected();
        }

        private void OnSelectionCleared(SelectionClearedEvent e) => ClearSelection();

        private void ClearSelection()
        {
            foreach (var npc in selectedNPCs) npc.OnDeselected();

            selectedNPCs.Clear();
        }

        private void OnMoveCommand(MoveCommandEvent e)
        {
            foreach (var npc in selectedNPCs) npc.MoveTo(e.Destination);
        }

        private void OnInteractCommand(InteractCommandEvent e)
        {
            foreach (var npc in selectedNPCs) npc.InteractWith(e.Target);
        }
    }
}