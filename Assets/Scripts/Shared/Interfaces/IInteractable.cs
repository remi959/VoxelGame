using Assets.Scripts.NPCs.Units;
using Assets.Scripts.Shared.Enums;
using UnityEngine;

namespace Assets.Scripts.Shared.Interfaces
{
    /// <summary>
    /// Interface for objects that can be interacted with by workers.
    /// Provides a unified way to handle resources, buildings, storage, etc.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// The type of interaction this object supports.
        /// </summary>
        InteractionType InteractionType { get; }

        /// <summary>
        /// Get the position where the worker should stand to interact.
        /// </summary>
        /// <param name="workerTransform">The worker's transform for position-aware calculations.</param>
        /// <returns>The world position for interaction.</returns>
        Vector3 GetInteractionPosition(Transform workerTransform);

        /// <summary>
        /// Check if the worker can interact with this object.
        /// </summary>
        /// <param name="worker">The worker attempting to interact.</param>
        /// <returns>True if interaction is possible.</returns>
        bool CanInteract(Worker worker);

        /// <summary>
        /// Execute the interaction.
        /// </summary>
        /// <param name="worker">The worker performing the interaction.</param>
        void OnInteract(Worker worker);
    }
}
