using Assets.Scripts.Shared.Interfaces;
using UnityEngine;

namespace Assets.Scripts.NPCs.States
{
    public class WorkerIdleState : IState
    {
        private readonly NPCBase npc;

        public WorkerIdleState(NPCBase npc)
        {
            this.npc = npc;
        }

        public void Enter()
        {
            npc.Motor.Stop();
            Debug.Log($"{npc.name}: Entered Idle state");
        }

        public void Update() { }

        public void Exit() { }
    }
}