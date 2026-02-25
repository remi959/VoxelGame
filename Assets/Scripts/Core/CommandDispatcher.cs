using Assets.Scripts.Events;
using Assets.Scripts.NPCs;
using Assets.Scripts.NPCs.Jobs;
using Assets.Scripts.NPCs.Modules;
using UnityEngine;

namespace Assets.Scripts.Core
{
    /// <summary>
    /// Translates player commands (move, interact) into Jobs for the selected pawns.
    /// This is the bridge between player input and the NPC job system.
    /// Separated from SelectionManager so that selection and commanding are independent concerns.
    /// </summary>
    public class CommandDispatcher : MonoBehaviour
    {
        private void OnEnable()
        {
            EventBus.Subscribe<MoveCommandEvent>(OnMoveCommand);
            EventBus.Subscribe<InteractCommandEvent>(OnInteractCommand);
            EventBus.Subscribe<StopCommandEvent>(OnStopCommand);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<MoveCommandEvent>(OnMoveCommand);
            EventBus.Unsubscribe<InteractCommandEvent>(OnInteractCommand);
            EventBus.Unsubscribe<StopCommandEvent>(OnStopCommand);
        }

        private void OnMoveCommand(MoveCommandEvent e)
        {
            foreach (Selectable selected in SelectionManager.Instance.Selected)
            {
                if (selected.TryGetComponent<Pawn>(out var pawn))
                {
                    var job = new Job(JobDef.MoveTo, targetPosition: e.Destination);
                    pawn.Jobs.StartJob(job);
                }
            }
        }

        private void OnInteractCommand(InteractCommandEvent e)
        {
            foreach (Selectable selected in SelectionManager.Instance.Selected)
            {
                if (selected.TryGetComponent<Pawn>(out var pawn))
                {
                    var job = new Job(JobDef.Interact, targetObject: e.Target);
                    pawn.Jobs.StartJob(job);
                }
            }
        }

        private void OnStopCommand(StopCommandEvent e)
        {
            foreach (Selectable selected in SelectionManager.Instance.Selected)
            {
                if (selected.TryGetComponent<Pawn>(out var pawn))
                    pawn.Jobs.EndCurrentJob();
            }
        }
    }
}
