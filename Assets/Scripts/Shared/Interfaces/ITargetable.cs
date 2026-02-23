// ============================================================================
// ITargetable.cs - Interface for entities that can be targeted in combat
// ============================================================================
using UnityEngine;

namespace Assets.Scripts.Shared.Interfaces
{
    /// <summary>
    /// Interface for entities that can be targeted by offensive units.
    /// 
    /// Design Notes:
    /// - Allows offensive units to query potential targets
    /// - Supports faction/team filtering
    /// - Separates targeting logic from damage handling
    /// </summary>
    public interface ITargetable
    {
        /// <summary>
        /// The faction/team this entity belongs to.
        /// Used for friend/foe identification.
        /// </summary>
        int FactionId { get; }

        /// <summary>
        /// Whether this entity can currently be targeted.
        /// Returns false if dead, invulnerable, or otherwise untargetable.
        /// </summary>
        bool IsTargetable { get; }

        /// <summary>
        /// Priority for target selection (higher = more important target).
        /// Used when multiple targets are in range.
        /// </summary>
        int TargetPriority { get; }

        /// <summary>
        /// The center point for targeting calculations.
        /// </summary>
        Vector3 TargetPosition { get; }

        /// <summary>
        /// The transform of this targetable entity.
        /// </summary>
        Transform Transform { get; }
    }
}
