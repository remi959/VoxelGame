using Assets.Scripts.Core;
using Assets.Scripts.Core.Services;
using Assets.Scripts.NPCs.Units;
using Assets.Scripts.Resources;
using Assets.Scripts.Shared.Interfaces;

namespace Assets.Scripts.NPCs.States
{
    public class DepositingState : IState
    {
        private readonly Worker worker;
        private readonly StateMachine stateMachine;

        public StateCategory Category => StateCategory.Gathering;

        private Resource lastResource;
        private StoragePoint targetStorage;

        public DepositingState(Worker worker, StateMachine stateMachine)
        {
            this.worker = worker;
            this.stateMachine = stateMachine;
        }

        public void SetLastResource(Resource resource) => lastResource = resource;

        public void SetStoragePoint(StoragePoint storage) => targetStorage = storage;

        public void Enter()
        {
            worker.Motor.Stop();

            if (targetStorage == null)
            {
                DebugManager.LogWarning("DepositingState: No storage point set!");
                stateMachine.SetState<WorkerIdleState>();
                return;
            }

            DebugManager.LogState($"DepositingState: Depositing {worker.CarriedAmount} {worker.CarriedType}");

            // Deposit resources
            if (worker.CarriedAmount > 0)
            {
                targetStorage.Deposit(worker.CarriedType, worker.CarriedAmount);
                worker.ClearInventory();
            }

            // Use the Worker's helper method to decide next action
            worker.HandleResourceDepletedOrGone(lastResource);
        }

        public void Update() { }

        public void Exit()
        {
            lastResource = null;
            targetStorage = null;
        }
    }
}