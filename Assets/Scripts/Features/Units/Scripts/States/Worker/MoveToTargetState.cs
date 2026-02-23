using Assets.Scripts.Core;
using Assets.Scripts.Core.Services;
using Assets.Scripts.Data;
using Assets.Scripts.Shared.Interfaces;
using UnityEngine;

namespace Assets.Scripts.NPCs.States
{
    public class MoveToTargetState : IState
    {
        private readonly NPCBase npc;
        private readonly StateMachine stateMachine;
        private readonly float arrivalDistance;

        public StateCategory Category => StateCategory.Movement;

        private Vector3? targetPosition;
        private System.Action onArrived;
        private System.Action onFailed;  // Called when path fails or stuck too long
        private bool isActive;
        
        // Stuck handling
        private int stuckRetryCount;
        private const int MaxStuckRetries = 3;
        private float stuckWaitTimer;
        private bool isWaitingAfterStuck;
        private int stuckCycleCount;  // Track full stuck cycles (retry + wait)
        private const int MaxStuckCycles = 2;  // After 2 full cycles, force arrival
        
        // Movement timeout
        private float movementTimer;
        private const float MaxMovementTime = 30f; // Max time to reach destination

        public MoveToTargetState(NPCBase npc, StateMachine stateMachine, float? arrivalDistance = null)
        {
            this.npc = npc;
            this.stateMachine = stateMachine;
            // Use provided distance or fall back to settings
            this.arrivalDistance = arrivalDistance ?? NPCSettingsSO.Instance.DefaultArrivalDistance;
        }

        public void SetTarget(Vector3 position, System.Action onArrivedCallback, System.Action onFailedCallback = null)
        {
            if (targetPosition.HasValue)
                Debug.LogWarning("MoveToTargetState: Overwriting target before previous arrival!");
            targetPosition = position;
            onArrived = onArrivedCallback;
            onFailed = onFailedCallback;
        }

        public void Enter()
        {
            isActive = true;
            stuckRetryCount = 0;
            stuckWaitTimer = 0f;
            isWaitingAfterStuck = false;
            stuckCycleCount = 0;
            movementTimer = 0f;
            DebugManager.LogState($"MoveToTargetState: Moving to {targetPosition}");
            
            if (targetPosition.HasValue)
            {
                // Subscribe to motor events
                npc.Motor.OnArrived += HandleArrival;
                npc.Motor.OnPathFailed += HandlePathFailed;
                npc.Motor.OnStuck += HandleStuck;
                
                npc.Motor.SetDestination(targetPosition.Value);
            }
            else
            {
                DebugManager.LogWarning("MoveToTargetState: Entered with no target position set.");
                onFailed?.Invoke();
            }
        }

        public void Update()
        {
            if (!isActive || !targetPosition.HasValue) return;

            // Handle waiting after being stuck
            if (isWaitingAfterStuck)
            {
                stuckWaitTimer -= Time.deltaTime;
                if (stuckWaitTimer <= 0f)
                {
                    isWaitingAfterStuck = false;
                    stuckRetryCount = 0;
                    DebugManager.LogState("MoveToTargetState: Resuming after stuck wait");
                    npc.Motor.SetDestination(targetPosition.Value);
                }
                return;
            }

            // Track movement time
            movementTimer += Time.deltaTime;
            
            // Timeout check - if moving too long, force arrival at closest position
            if (movementTimer >= MaxMovementTime)
            {
                DebugManager.LogWarning($"MoveToTargetState: Movement timeout after {MaxMovementTime}s, forcing arrival");
                HandleArrival();
                return;
            }

            // Diagnostic: Log movement status periodically (every 120 frames = ~2 sec)
            if (Time.frameCount % 120 == 0)
            {
                var motor = npc.Motor;
                float dist = Vector3.Distance(npc.transform.position, targetPosition.Value);
                DebugManager.LogState($"MoveToTargetState: IsMoving={motor.IsMoving}, HasPath={motor.HasPath}, Dist={dist:F2}, Target={targetPosition.Value}");
            }

            // Fallback distance check in case events don't fire
            // Uses sqrMagnitude for performance
            float distanceSqr = (npc.transform.position - targetPosition.Value).sqrMagnitude;
            float arrivalDistSqr = arrivalDistance * arrivalDistance;

            if (distanceSqr <= arrivalDistSqr)
            {
                HandleArrival();
            }
        }

        private void HandleArrival()
        {
            if (!isActive) return;
            
            // Store callback before clearing
            var callback = onArrived;

            // Clear state data
            Cleanup();

            // Invoke callback (which will transition to next state)
            DebugManager.LogState("MoveToTargetState: Arrived at destination");
            callback?.Invoke();
        }

        private void HandlePathFailed()
        {
            if (!isActive) return;
            
            DebugManager.LogWarning("MoveToTargetState: Path failed!");

            // Store callbacks before Cleanup() clears them
            var failCallback = onFailed;
            var arrivedCallback = onArrived;

            Cleanup();

            // Let calling state handle the failure; fall back to onArrived if no failure handler
            if (failCallback != null)
            {
                failCallback.Invoke();
            }
            else
            {
                arrivedCallback?.Invoke();
            }
        }

        private void HandleStuck()
        {
            if (!isActive) return;
            
            stuckRetryCount++;
            
            if (stuckRetryCount >= MaxStuckRetries)
            {
                stuckCycleCount++;
                
                // After too many stuck cycles, give up and fail the path
                // This lets the calling state handle the failure appropriately
                if (stuckCycleCount >= MaxStuckCycles)
                {
                    DebugManager.LogWarning($"MoveToTargetState: NPC stuck after {stuckCycleCount} cycles, giving up");
                    HandlePathFailed();
                    return;
                }
                
                // After max retries, wait a moment before trying again
                // This allows blocking NPCs time to move away
                DebugManager.LogWarning($"MoveToTargetState: NPC stuck after {stuckRetryCount} retries (cycle {stuckCycleCount}), waiting 1.5s...");
                isWaitingAfterStuck = true;
                stuckWaitTimer = 1.5f;
                stuckRetryCount = 0;  // Reset retry count for next cycle
                npc.Motor.Stop();
            }
            else
            {
                DebugManager.LogWarning($"MoveToTargetState: NPC stuck (retry {stuckRetryCount}/{MaxStuckRetries}), recalculating...");
                
                // Try to recalculate path
                if (targetPosition.HasValue)
                {
                    npc.Motor.SetDestination(targetPosition.Value);
                }
            }
        }

        public void Exit()
        {
            DebugManager.LogState("MoveToTargetState: Exiting state");
            Cleanup();
        }

        private void Cleanup()
        {
            isActive = false;
            targetPosition = null;
            onArrived = null;
            onFailed = null;
            stuckRetryCount = 0;
            stuckWaitTimer = 0f;
            isWaitingAfterStuck = false;
            stuckCycleCount = 0;
            movementTimer = 0f;

            // Unsubscribe from motor events
            if (npc?.Motor != null)
            {
                npc.Motor.OnArrived -= HandleArrival;
                npc.Motor.OnPathFailed -= HandlePathFailed;
                npc.Motor.OnStuck -= HandleStuck;
            }
        }
    }
}