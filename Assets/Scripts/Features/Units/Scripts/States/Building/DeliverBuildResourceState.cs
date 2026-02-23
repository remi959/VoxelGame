// ============================================================================
// DeliverBuildResourcesState.cs - Deliver resources to crafting bench
// ============================================================================
using Assets.Scripts.Buildings;
using Assets.Scripts.Core;
using Assets.Scripts.Core.Services;
using Assets.Scripts.NPCs.Units;

namespace Assets.Scripts.NPCs.States.Building
{
    /// <summary>
    /// Deliver carried resources to the crafting bench.
    /// Transitions back to GatherBuildResourcesState if more needed, 
    /// or to WaitForCraftState if bench has enough.
    /// </summary>
    public class DeliverBuildResourcesState : BuildingStateBase
    {
        private bool isMoving;

        public DeliverBuildResourcesState(Worker worker, StateMachine stateMachine) 
            : base(worker, stateMachine) { }

        public override void Enter()
        {
            worker.Motor.Stop();
            isMoving = false;

            var bench = Context.CurrentBench;

            if (bench == null)
            {
                DebugManager.LogWarning("DeliverBuildResourcesState: No bench");
                AbandonBuilding();
                return;
            }

            if (worker.CarriedAmount <= 0)
            {
                DebugManager.LogWarning("DeliverBuildResourcesState: Nothing to deliver");
                // Go back to gathering
                stateMachine.SetState<GatherBuildResourcesState>();
                return;
            }

            DebugManager.LogState($"DeliverBuildResourcesState: Delivering {worker.CarriedAmount} {worker.CarriedType}");

            // Move to bench
            MoveToBench();
        }

        private void MoveToBench()
        {
            isMoving = true;

            var bench = Context.CurrentBench;
            var moveState = stateMachine.GetState<MoveToTargetState>();
            moveState.SetTarget(bench.transform.position, OnArrivedAtBench);
            stateMachine.SetState<MoveToTargetState>();
        }

        private void OnArrivedAtBench()
        {
            isMoving = false;

            var bench = Context.CurrentBench;
            var slot = Context.CurrentSlot;

            if (bench == null || slot == null)
            {
                DebugManager.LogWarning("DeliverBuildResourcesState: Bench or slot gone");
                AbandonBuilding();
                return;
            }

            // Deposit resources
            bench.DepositResources(worker.CarriedType, worker.CarriedAmount);
            worker.ClearInventory();

            DebugManager.LogState("DeliverBuildResourcesState: Resources deposited");

            // Check if bench needs more
            var missing = bench.GetMissingResources(slot.SourcePart);

            if (missing.Count == 0)
            {
                // All resources delivered, start crafting
                stateMachine.SetState<WaitForCraftState>();
            }
            else
            {
                // Need more resources
                stateMachine.SetState<GatherBuildResourcesState>();
            }
        }

        public override void Update()
        {
            // Movement handled by MoveToTargetState
        }

        public override void Exit()
        {
            isMoving = false;
        }
    }
}