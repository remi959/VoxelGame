// ============================================================================
// WorldResourcePickup.cs - Dropped resource pickup in the world
// ============================================================================
using Assets.Scripts.NPCs.Units;
using Assets.Scripts.Shared.Enums;
using Assets.Scripts.Shared.Interfaces;
using UnityEngine;

namespace Assets.Scripts.Resources
{
    /// <summary>
    /// Represents a resource that has been dropped in the world and can be picked up.
    /// This is used for resources that workers drop when interrupted or when building
    /// construction is cancelled.
    /// </summary>
    public class WorldResourcePickup : MonoBehaviour, IInteractable
    {
        [Header("Pickup Settings")]
        [SerializeField] private float pickupDistance = 1.5f;

        private EResourceType resourceType;
        private int amount;
        private bool isPickedUp;

        #region Properties

        /// <summary>
        /// Type of resource in this pickup.
        /// </summary>
        public EResourceType ResourceType => resourceType;

        /// <summary>
        /// Amount of resources in this pickup.
        /// </summary>
        public int Amount => amount;

        /// <summary>
        /// Whether this pickup has been collected.
        /// </summary>
        public bool IsPickedUp => isPickedUp;

        /// <summary>
        /// Distance at which workers can pick this up.
        /// </summary>
        public float PickupDistance => pickupDistance;

        /// <summary>
        /// Whether this pickup can be collected.
        /// </summary>
        public bool CanBePickedUp => !isPickedUp && amount > 0;

        #endregion

        #region IInteractable Implementation

        public InteractionType InteractionType => InteractionType.Pickup;

        public Vector3 GetInteractionPosition(Transform workerTransform) => transform.position;

        public bool CanInteract(Worker worker)
        {
            if (worker == null) return false;
            return CanBePickedUp && !worker.IsInventoryFull;
        }

        public void OnInteract(Worker worker)
        {
            if (!CanInteract(worker)) return;

            // Add resources to worker inventory
            worker.AddToInventory(resourceType, amount);
            
            // Mark as picked up
            isPickedUp = true;
            
            // Destroy or return to pool
            Destroy(gameObject);
        }

        #endregion

        #region Setup

        /// <summary>
        /// Initialize the pickup with resource data.
        /// </summary>
        public void Setup(EResourceType type, int resourceAmount)
        {
            resourceType = type;
            amount = resourceAmount;
            isPickedUp = false;
        }

        /// <summary>
        /// Create a world pickup at a position.
        /// </summary>
        public static WorldResourcePickup Create(EResourceType type, int amount, Vector3 position)
        {
            var go = new GameObject($"WorldPickup_{type}_{amount}");
            go.transform.position = position;
            
            var pickup = go.AddComponent<WorldResourcePickup>();
            pickup.Setup(type, amount);
            
            // Add a simple collider for interaction detection
            var collider = go.AddComponent<SphereCollider>();
            collider.radius = 0.5f;
            collider.isTrigger = true;
            
            return pickup;
        }

        #endregion
    }
}
