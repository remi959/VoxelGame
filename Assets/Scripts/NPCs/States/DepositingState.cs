using Assets.Scripts.Core;
using Assets.Scripts.NPCs.Units;
using Assets.Scripts.Resources;
using Assets.Scripts.Shared.Interfaces;

namespace Assets.Scripts.NPCs.States
{
    public class DepositingState : IState
    {
        private readonly Worker worker;
        private readonly StateMachine stateMachine;

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

            // Check if we should go back for more
            if (lastResource != null && !lastResource.IsDepleted)
            {
                DebugManager.LogState("DepositingState: Going back for more resources");
                worker.GatherFrom(lastResource);
            }
            else
            {
                DebugManager.LogState("DepositingState: Resource depleted or gone, going idle");
                stateMachine.SetState<WorkerIdleState>();
            }
        }

        public void Update() { }

        public void Exit()
        {
            lastResource = null;
            targetStorage = null;
        }
    }
}