using UnityEngine;

namespace Assets.Scripts.Events
{
    // Selection Events
    public struct NPCSelectedEvent
    {
        public GameObject NPC;
        public bool AddToSelection; // For shift-click multi-select
    }

    public struct NPCDeselectedEvent
    {
        public GameObject NPC;
    }

    public struct SelectionClearedEvent { }

    // Command Events
    public struct MoveCommandEvent
    {
        public Vector3 Destination;
    }

    public struct StopCommandEvent { }

    public struct GatherCommandEvent
    {
        public GameObject Resource;
    }

    public struct InteractCommandEvent
    {
        public GameObject Target;
    }

    public struct ResourceDepletedEvent
    {
        public GameObject Resource;
    }

    public struct ResourceDepositedEvent
    {
        public int Amount;
        public int ResourceType; // Cast from ResourceType enum
    }

    // NPC Lifecycle Events
    public struct NPCSpawnedEvent
    {
        public GameObject NPC;
    }

    public struct NPCDiedEvent
    {
        public GameObject NPC;
    }
}