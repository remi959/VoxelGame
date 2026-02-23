// ============================================================================
// BuildingSite.cs - Runtime data for a building under construction
// ============================================================================
using System;
using System.Collections.Generic;
using Assets.Scripts.Core;
using Assets.Scripts.Economy;
using Assets.Scripts.Shared.Enums;
using UnityEngine;

namespace Assets.Scripts.Buildings.Construction
{
    /// <summary>
    /// Runtime data representing a building under construction.
    /// This is pure DATA - no Unity lifecycle, no MonoBehaviour.
    /// 
    /// The corresponding MonoBehaviour (BuildingBlueprint) references this data.
    /// </summary>
    public class BuildingSite
    {
        // ========== Identity ==========

        /// <summary>
        /// Unique identifier for this site (for networking, saves, etc.)
        /// </summary>
        public readonly string SiteId;

        /// <summary>
        /// The building being constructed.
        /// </summary>
        public readonly BuildingDefinitionSO Definition;

        /// <summary>
        /// Player who owns this building.
        /// </summary>
        public readonly int OwnerPlayerId;

        // ========== Placement ==========

        /// <summary>
        /// World position of the building.
        /// </summary>
        public readonly Vector3 Position;

        /// <summary>
        /// Rotation of the building.
        /// </summary>
        public readonly Quaternion Rotation;

        /// <summary>
        /// Time.time value when the building was placed.
        /// </summary>
        public readonly float PlacedAt;

        // ========== Resources ==========

        /// <summary>
        /// Resource reservation token (before construction starts).
        /// </summary>
        public ResourceReservation Reservation { get; private set; }

        /// <summary>
        /// Resources delivered to construction site.
        /// </summary>
        public Dictionary<EResourceType, int> ResourcesDelivered { get; } = new();

        /// <summary>
        /// Resources required for completion.
        /// </summary>
        public Dictionary<EResourceType, int> ResourcesRequired { get; } = new();

        // ========== Construction State ==========

        /// <summary>
        /// Current construction state.
        /// </summary>
        public BuildingSiteState State { get; private set; }

        /// <summary>
        /// Current construction phase (0-indexed).
        /// </summary>
        public int CurrentPhaseIndex { get; private set; }

        /// <summary>
        /// Number of parts completed.
        /// </summary>
        public int PartsCompleted { get; private set; }

        /// <summary>
        /// Total parts to complete.
        /// </summary>
        public int TotalParts { get; private set; }

        /// <summary>
        /// Overall progress (0.0 to 1.0).
        /// </summary>
        public float Progress => TotalParts > 0 ? (float)PartsCompleted / TotalParts : 0f;

        /// <summary>
        /// Is construction complete?
        /// </summary>
        public bool IsComplete => State == BuildingSiteState.Complete;

        // ========== Workers ==========

        /// <summary>
        /// IDs of workers assigned to this site.
        /// </summary>
        public HashSet<int> AssignedWorkerIds { get; } = new();

        /// <summary>
        /// Maximum workers that can work simultaneously.
        /// </summary>
        public int MaxWorkers => Definition?.maxWorkers ?? 4;

        /// <summary>
        /// Can another worker be assigned?
        /// </summary>
        public bool CanAssignWorker => AssignedWorkerIds.Count < MaxWorkers && !IsComplete;

        // ========== Events ==========

        public event Action<BuildingSiteState, BuildingSiteState> OnStateChanged;
        public event Action<float> OnProgressChanged;
        public event Action<int> OnWorkerAssigned;
        public event Action<int> OnWorkerUnassigned;

        // ========== GameObject Reference ==========

        /// <summary>
        /// Reference to the scene object (set by ConstructionService).
        /// </summary>
        public GameObject SceneObject { get; set; }

        // ========== Constructor ==========

        public BuildingSite(
            string siteId,
            BuildingDefinitionSO definition,
            int ownerPlayerId,
            Vector3 position,
            Quaternion rotation,
            ResourceReservation reservation)
        {
            SiteId = siteId;
            Definition = definition;
            OwnerPlayerId = ownerPlayerId;
            Position = position;
            Rotation = rotation;
            PlacedAt = Time.time;
            Reservation = reservation;

            State = BuildingSiteState.PlannedAwaitingResources;
            CurrentPhaseIndex = 0;
            PartsCompleted = 0;

            // Initialize resource tracking
            var costs = definition.GetTotalCosts();
            foreach (var kvp in costs)
            {
                ResourcesRequired[kvp.Key] = kvp.Value;
                ResourcesDelivered[kvp.Key] = 0;
            }
        }

        // ========== State Transitions ==========

        public void SetState(BuildingSiteState newState)
        {
            if (State == newState) return;

            var oldState = State;
            State = newState;
            OnStateChanged?.Invoke(oldState, newState);
        }

        public void SetTotalParts(int total) => TotalParts = total;

        public void IncrementPartsCompleted()
        {
            PartsCompleted++;
            OnProgressChanged?.Invoke(Progress);

            if (PartsCompleted >= TotalParts) SetState(BuildingSiteState.Complete);
        }

        // ========== Worker Management ==========

        public bool AssignWorker(int workerId)
        {
            if (!CanAssignWorker) return false;
            if (AssignedWorkerIds.Contains(workerId)) return false;

            AssignedWorkerIds.Add(workerId);
            OnWorkerAssigned?.Invoke(workerId);

            // First worker starts construction
            if (State == BuildingSiteState.ResourcesReserved) SetState(BuildingSiteState.UnderConstruction);

            return true;
        }

        public bool UnassignWorker(int workerId)
        {
            if (!AssignedWorkerIds.Contains(workerId)) return false;

            AssignedWorkerIds.Remove(workerId);
            OnWorkerUnassigned?.Invoke(workerId);

            return true;
        }

        // ========== Resource Delivery ==========

        public void DeliverResource(EResourceType type, int amount)
        {
            if (!ResourcesDelivered.ContainsKey(type)) ResourcesDelivered[type] = 0;

            ResourcesDelivered[type] += amount;
        }

        public int GetRemainingResourceNeed(EResourceType type)
        {
            int required = ResourcesRequired.TryGetValue(type, out int r) ? r : 0;
            int delivered = ResourcesDelivered.TryGetValue(type, out int d) ? d : 0;
            return Mathf.Max(0, required - delivered);
        }
    }

    /// <summary>
    /// States a building site can be in.
    /// </summary>
    public enum BuildingSiteState
    {
        /// <summary>
        /// Site placed but resources not yet reserved.
        /// </summary>
        PlannedAwaitingResources,

        /// <summary>
        /// Resources reserved, waiting for workers.
        /// </summary>
        ResourcesReserved,

        /// <summary>
        /// Workers actively constructing.
        /// </summary>
        UnderConstruction,

        /// <summary>
        /// Manually paused by player.
        /// </summary>
        Paused,

        /// <summary>
        /// Construction complete.
        /// </summary>
        Complete,

        /// <summary>
        /// Cancelled - resources should be refunded.
        /// </summary>
        Cancelled
    }
}