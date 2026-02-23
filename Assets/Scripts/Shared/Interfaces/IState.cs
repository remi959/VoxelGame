using Assets.Scripts.NPCs.States;

namespace Assets.Scripts.Shared.Interfaces
{
    public interface IState
    {
        /// <summary>
        /// Category of this state for efficient O(1) state type queries.
        /// </summary>
        StateCategory Category { get; }

        void Enter();
        void Update();
        void Exit();
    }
}