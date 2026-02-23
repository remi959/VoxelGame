using System;
using System.Collections.Generic;
using System.Diagnostics;
using Assets.Scripts.Core;
using Assets.Scripts.Core.Services;
using Assets.Scripts.Shared.Interfaces;

namespace Assets.Scripts.NPCs.States
{
    public class StateMachine
    {
        private IState currentState;
        private readonly Dictionary<Type, IState> states = new();

        public IState CurrentState => currentState;

        /// <summary>
        /// Current state's category for O(1) state type queries.
        /// </summary>
        public StateCategory CurrentCategory => currentState?.Category ?? StateCategory.Other;

        public void AddState(IState state) => states[state.GetType()] = state;

        public void SetState<T>() where T : IState
        {
            DebugManager.LogState($"StateMachine: Transitioning to state {typeof(T).Name}");
            if (states.TryGetValue(typeof(T), out var newState))
            {
                currentState?.Exit();
                currentState = newState;
                currentState.Enter();
            }
        }

        public void Update()
        {
            currentState?.Update();
        }

        public T GetState<T>() where T : class, IState
        {
            if (states.TryGetValue(typeof(T), out var state)) return state as T;
            return null;
        }

        public bool IsInState<T>() where T : IState
        {
            return currentState?.GetType() == typeof(T);
        }
    }
}