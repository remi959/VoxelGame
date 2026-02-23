// ============================================================================
// WaitForCraftState.cs - Wait for bench to craft the part
// ============================================================================
using Assets.Scripts.Buildings;
using Assets.Scripts.Core;
using Assets.Scripts.NPCs.Units;
using Assets.Scripts.NPCs.States.Crafting;
using UnityEngine;
using Assets.Scripts.Core.Services;

namespace Assets.Scripts.NPCs.States.Building
{
    /// <summary>
    /// Request crafting and wait near the bench output.
    /// Transitions to PickUpCraftedPartState when part is ready.
    /// 
    /// Uses CraftingWorkContext to persist state across MoveToTargetState transitions,
    /// preventing duplicate craft requests on re-entry.
    /// </summary>
    public class WaitForCraftState : BuildingStateBase
    {
        /// <summary>
        /// Crafting context persists across state transitions.
        /// </summary>
        private CraftingWorkContext CraftingContext => worker.CraftingContext;

        public WaitForCraftState(Worker worker, StateMachine stateMachine) 
            : base(worker, stateMachine) { }

        public override void Enter()
        {
            var bench = Context.CurrentBench;
            var slot = Context.CurrentSlot;

            if (bench == null || slot == null)
            {
                DebugManager.LogWarning("WaitForCraftState: Missing bench or slot");
                AbandonBuilding();
                return;
            }

            // Check if craft already completed while we were moving
            if (CraftingContext.IsCraftComplete)
            {
                DebugManager.LogState("WaitForCraftState: Craft already complete, picking up part");
                Context.CurrentPart = CraftingContext.CompletedPart;
                CraftingContext.ResetForNextCraft();
                stateMachine.SetState<PickUpCraftedPartState>();
                return;
            }

            // Only request craft once (CraftingContext persists across state transitions)
            if (!CraftingContext.CraftRequested)
            {
                DebugManager.LogState($"WaitForCraftState: Requesting craft for slot {slot.Index}");

                CraftingContext.TargetBench = bench;
                CraftingContext.CraftRequested = bench.RequestCraft(slot.SourcePart, OnPartCrafted);

                if (!CraftingContext.CraftRequested)
                {
                    DebugManager.LogWarning("WaitForCraftState: Craft request failed");
                    CraftingContext.Clear();
                    AbandonBuilding();
                    return;
                }
            }

            // If already at output, just wait
            if (CraftingContext.ArrivedAtOutput)
            {
                worker.Motor.Stop();
                DebugManager.LogState("WaitForCraftState: Already at output, waiting for craft...");
                return;
            }

            // Move to output position using MoveToTargetState
            MoveToOutput();
        }

        private void MoveToOutput()
        {
            var bench = Context.CurrentBench;

            // Add a small random offset to prevent multiple NPCs from blocking each other
            // at the exact same position
            Vector3 targetPos = bench.OutputPosition;
            float offsetRadius = 0.5f;
            Vector3 randomOffset = new Vector3(
                Random.Range(-offsetRadius, offsetRadius),
                0f,
                Random.Range(-offsetRadius, offsetRadius)
            );
            targetPos += randomOffset;

            var moveState = stateMachine.GetState<MoveToTargetState>();
            moveState.SetTarget(targetPos, OnArrivedAtOutput, OnMoveToOutputFailed);
            stateMachine.SetState<MoveToTargetState>();
        }

        private void OnMoveToOutputFailed()
        {
            DebugManager.LogWarning("WaitForCraftState: Failed to reach output, abandoning building");
            CraftingContext.Clear();
            AbandonBuilding();
        }

        private void OnArrivedAtOutput()
        {
            CraftingContext.ArrivedAtOutput = true;

            // Check if craft completed while we were moving
            if (CraftingContext.IsCraftComplete)
            {
                DebugManager.LogState("WaitForCraftState: Craft completed during move, picking up");
                Context.CurrentPart = CraftingContext.CompletedPart;
                CraftingContext.ResetForNextCraft();
                stateMachine.SetState<PickUpCraftedPartState>();
                return;
            }

            // Re-enter this state to wait for craft callback
            // This time, ArrivedAtOutput is true so we won't move again
            stateMachine.SetState<WaitForCraftState>();
        }

        private void OnPartCrafted(CraftedPart part)
        {
            // Guard: only process if we're actually waiting for a craft
            // This prevents issues if the callback fires after we've moved on
            if (!CraftingContext.CraftRequested || CraftingContext.IsCraftComplete)
            {
                DebugManager.LogWarning($"WaitForCraftState: Ignoring stale OnPartCrafted callback (CraftRequested={CraftingContext.CraftRequested}, IsCraftComplete={CraftingContext.IsCraftComplete})");
                return;
            }

            if (part == null)
            {
                DebugManager.LogWarning("WaitForCraftState: Craft produced null part");
                CraftingContext.Clear();
                AbandonBuilding();
                return;
            }

            DebugManager.LogState("WaitForCraftState: Part crafted!");

            // Set target info on the part so it can be recovered if dropped
            if (Context.CurrentBlueprint != null && Context.CurrentSlot != null)
            {
                part.SetTarget(Context.CurrentBlueprint, Context.CurrentSlot.Index);
            }

            // Store in context - the state will pick it up on next Enter() or Update()
            CraftingContext.CompletedPart = part;

            // Only transition if we're in the right state category (Building states)
            // The callback might fire while in MoveToTargetState (moving to output)
            // In that case, OnArrivedAtOutput will handle the transition
            var currentCategory = stateMachine.CurrentCategory;
            bool canTransition = currentCategory == StateCategory.Building || 
                                 (currentCategory == StateCategory.Movement && CraftingContext.ArrivedAtOutput);

            if (CraftingContext.ArrivedAtOutput && canTransition)
            {
                Context.CurrentPart = part;
                CraftingContext.ResetForNextCraft();
                stateMachine.SetState<PickUpCraftedPartState>();
            }
            else
            {
                DebugManager.LogState($"WaitForCraftState: Part stored, will pick up on arrival (ArrivedAtOutput={CraftingContext.ArrivedAtOutput}, Category={currentCategory})");
            }
        }

        public override void Update()
        {
            // Check if craft completed (callback might have fired)
            if (CraftingContext.IsCraftComplete && CraftingContext.ArrivedAtOutput)
            {
                DebugManager.LogState("WaitForCraftState: Update detected craft complete, transitioning");
                Context.CurrentPart = CraftingContext.CompletedPart;
                CraftingContext.ResetForNextCraft();
                stateMachine.SetState<PickUpCraftedPartState>();
                return;
            }

            // Safety check: if benchmark is gone, abort
            if (Context.CurrentBlueprint == null || 
                Context.CurrentBlueprint.IsComplete)
            {
                DebugManager.LogState("WaitForCraftState: Blueprint gone while waiting");
                CraftingContext.Clear();
                AbandonBuilding();
                return;
            }

            // Debug: Log state if waiting for a while (every 60 frames)
            if (CraftingContext.CraftRequested && !CraftingContext.IsCraftComplete && Time.frameCount % 60 == 0)
            {
                DebugManager.LogState($"WaitForCraftState: Still waiting (ArrivedAtOutput={CraftingContext.ArrivedAtOutput}, CraftRequested={CraftingContext.CraftRequested})");
            }
        }

        public override void Exit()
        {
            // Don't clear CraftingContext here - it needs to persist across
            // MoveToTargetState transitions. It's cleared by:
            // - CraftingContext.ResetForNextCraft() when craft completes
            // - CraftingContext.Clear() when building is abandoned
            // - Worker.StopBuilding() when interrupted
        }
    }
}