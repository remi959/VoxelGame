// ============================================================================
// GatherBuildResourcesState.cs - Gather resources from storage for building
// ============================================================================
using Assets.Scripts.Core;
using Assets.Scripts.Core.Services;
using Assets.Scripts.NPCs.Units;
using Assets.Scripts.Resources;
using Assets.Scripts.Shared.Enums;
using UnityEngine;

namespace Assets.Scripts.NPCs.States.Building
{
    /// <summary>
    /// Gather resources from storage to deliver to crafting bench.
    /// Transitions to DeliverBuildResourcesState when resources acquired.
    /// </summary>
    public class GatherBuildResourcesState : BuildingStateBase
    {
        private EResourceType neededType;
        private int neededAmount;
        private bool isMoving;

        public GatherBuildResourcesState(Worker worker, StateMachine stateMachine) 
            : base(worker, stateMachine) { }

        public override void Enter()
        {
            worker.Motor.Stop();
            isMoving = false;

            var bench = Context.CurrentBench;
            var slot = Context.CurrentSlot;

            if (bench == null || slot == null)
            {
                DebugManager.LogWarning("GatherBuildResourcesState: Missing bench or slot");
                AbandonBuilding();
                return;
            }

            // Get missing resources for current part
            var missing = bench.GetMissingResources(slot.SourcePart);

            if (missing.Count == 0)
            {
                // No more resources needed
                DebugManager.LogState("GatherBuildResourcesState: Bench has all resources");
                stateMachine.SetState<WaitForCraftState>();
                return;
            }

            // Find first missing resource type
            foreach (var kvp in missing)
            {
                neededType = kvp.Key;
                neededAmount = kvp.Value;
                break;
            }

            DebugManager.LogState($"GatherBuildResourcesState: Need {neededAmount} {neededType}");

            // Find storage with this resource (owned by the worker's player)
            Context.TargetStorage = StoragePoint.FindNearest(worker.transform.position, neededType, worker.OwnerPlayerId);

            if (Context.TargetStorage == null)
            {
                DebugManager.LogWarning($"GatherBuildResourcesState: No storage found for {neededType} owned by player {worker.OwnerPlayerId}");
                AbandonBuilding();
                return;
            }

            // Check if storage has resources
            if (Context.TargetStorage.GetAmount(neededType) <= 0)
            {
                DebugManager.LogWarning($"GatherBuildResourcesState: Storage has no {neededType}");
                AbandonBuilding();
                return;
            }

            // Move to storage
            MoveToStorage();
        }

        private void MoveToStorage()
        {
            isMoving = true;

            var moveState = stateMachine.GetState<MoveToTargetState>();
            moveState.SetTarget(Context.TargetStorage.transform.position, OnArrivedAtStorage);
            stateMachine.SetState<MoveToTargetState>();
        }

        private void OnArrivedAtStorage()
        {
            isMoving = false;

            if (Context.TargetStorage == null)
            {
                DebugManager.LogWarning("GatherBuildResourcesState: Storage disappeared");
                AbandonBuilding();
                return;
            }

            // Withdraw resources
            int amountToTake = Mathf.Min(neededAmount, worker.CarryCapacity);
            int actuallyTaken = Context.TargetStorage.Withdraw(neededType, amountToTake);

            if (actuallyTaken > 0)
            {
                worker.AddToInventory(neededType, actuallyTaken);
                DebugManager.LogState($"GatherBuildResourcesState: Took {actuallyTaken} {neededType}");

                // Go deliver
                stateMachine.SetState<DeliverBuildResourcesState>();
            }
            else
            {
                DebugManager.LogWarning($"GatherBuildResourcesState: Failed to withdraw {neededType}");
                AbandonBuilding();
            }
        }

        public override void Update()
        {
            // Movement is handled by MoveToTargetState
        }

        public override void Exit()
        {
            isMoving = false;
            // Note: Context.TargetStorage is NOT cleared here because
            // the callback (OnArrivedAtStorage) still needs it after
            // transitioning to MoveToTargetState. It's cleared when:
            // - Context.Clear() is called (building complete/abandoned)
            // - A new storage is assigned in Enter()
        }
    }
}