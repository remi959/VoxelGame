using System.Collections.Generic;
using Assets.Scripts.Core.Events;
using Assets.Scripts.Core.Services;
using Assets.Scripts.NPCs;
using Assets.Scripts.NPCs.Units;
using UnityEngine;

namespace Assets.Scripts.Core.Input
{
    /// <summary>
    /// Manages NPC selection state and forwards commands to selected units.
    /// 
    /// PERSISTENCE:
    /// This component should be placed as a child of the [Services] GameObject.
    /// PersistentServices handles DontDestroyOnLoad for the entire hierarchy.
    /// </summary>
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
            ServiceLocator.Register(this);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                ServiceLocator.Unregister<SelectionManager>();
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<NPCSelectedEvent>(OnNPCSelected);
            EventBus.Subscribe<NPCDeselectedEvent>(OnNPCDeselected);
            EventBus.Subscribe<SelectionClearedEvent>(OnSelectionCleared);
            EventBus.Subscribe<MoveCommandEvent>(OnMoveCommand);
            EventBus.Subscribe<InteractCommandEvent>(OnInteractCommand);
            EventBus.Subscribe<DropPartCommandEvent>(OnDropPartCommand);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<NPCSelectedEvent>(OnNPCSelected);
            EventBus.Unsubscribe<NPCDeselectedEvent>(OnNPCDeselected);
            EventBus.Unsubscribe<SelectionClearedEvent>(OnSelectionCleared);
            EventBus.Unsubscribe<MoveCommandEvent>(OnMoveCommand);
            EventBus.Unsubscribe<InteractCommandEvent>(OnInteractCommand);
            EventBus.Unsubscribe<DropPartCommandEvent>(OnDropPartCommand);
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

        private void OnDropPartCommand(DropPartCommandEvent e)
        {
            foreach (var npc in selectedNPCs)
            {
                if (npc is Worker worker)
                {
                    worker.DropCarriedPart();
                }
            }
        }
    }
}