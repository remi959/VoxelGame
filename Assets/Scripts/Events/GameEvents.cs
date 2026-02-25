using UnityEngine;

namespace Assets.Scripts.Events
{
    // ── Selection Events ──────────────────────────────────────────────

    public struct NPCSelectedEvent
    {
        public GameObject NPC;
        public bool AddToSelection;
    }

    public struct NPCDeselectedEvent
    {
        public GameObject NPC;
    }

    public struct SelectionClearedEvent { }

    // ── Player Command Events ─────────────────────────────────────────

    public struct MoveCommandEvent
    {
        public Vector3 Destination;
    }

    public struct StopCommandEvent { }

    public struct InteractCommandEvent
    {
        public GameObject Target;
    }

    // ── Pawn Lifecycle Events ─────────────────────────────────────────

    public struct NPCSpawnedEvent
    {
        public GameObject NPC;
    }

    public struct NPCDiedEvent
    {
        public GameObject NPC;
    }
}