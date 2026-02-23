// ============================================================================
// ResourceReservation.cs - Tracks a pending resource transaction
// ============================================================================
using System.Collections.Generic;
using Assets.Scripts.Buildings;

namespace Assets.Scripts.Economy
{
    /// <summary>
    /// Represents a pending resource transaction.
    /// Used for building placement before construction starts.
    /// </summary>
    public class ResourceReservation
    {
        public readonly string Id;
        public readonly int PlayerId;
        public readonly IReadOnlyList<ResourceCost> Costs;
        /// <summary>
        /// Time.time value at which reservation expires.
        /// </summary>
        public readonly float ExpiresAt;

        public bool IsConfirmed { get; private set; }
        public bool IsCancelled { get; private set; }

        public ResourceReservation(string id, int playerId, List<ResourceCost> costs, float expiresAt)
        {
            Id = id;
            PlayerId = playerId;
            Costs = costs;
            ExpiresAt = expiresAt;
        }

        public void Confirm() => IsConfirmed = true;
        public void Cancel() => IsCancelled = true;
    }
}