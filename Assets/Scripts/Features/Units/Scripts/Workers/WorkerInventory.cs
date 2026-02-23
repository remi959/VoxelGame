// ============================================================================
// WorkerInventory.cs - Worker inventory management component
// ============================================================================
using System.Collections.Generic;
using Assets.Scripts.Resources;
using Assets.Scripts.Shared.Enums;
using UnityEngine;

namespace Assets.Scripts.NPCs.Units
{
    /// <summary>
    /// Manages a worker's inventory of resources.
    /// 
    /// Features:
    /// - Tracks resource type and amount
    /// - Manages visual fragments attached to carry point
    /// - Supports stacking of fragments
    /// </summary>
    public class WorkerInventory : MonoBehaviour
    {
        [Header("Inventory Settings")]
        [SerializeField] private int capacity = 10;
        [SerializeField] private Transform carryPoint;

        [Header("Stack Settings")]
        [SerializeField] private float stackHeight = 0.3f;

        // Current inventory state
        private EResourceType resourceType;
        private int amount;

        // Visual fragments being carried
        private readonly List<ResourceFragment> fragments = new();

        #region Properties

        /// <summary>
        /// Maximum capacity of this inventory.
        /// </summary>
        public int Capacity => capacity;

        /// <summary>
        /// Current amount of resources carried.
        /// </summary>
        public int Amount => amount;

        /// <summary>
        /// Type of resource currently being carried.
        /// </summary>
        public EResourceType ResourceType => resourceType;

        /// <summary>
        /// Whether the inventory is at capacity.
        /// </summary>
        public bool IsFull => amount >= capacity;

        /// <summary>
        /// Whether the inventory is empty.
        /// </summary>
        public bool IsEmpty => amount <= 0;

        /// <summary>
        /// The transform where fragments are visually attached.
        /// </summary>
        public Transform CarryPoint => carryPoint;

        /// <summary>
        /// Number of visual fragments being carried.
        /// </summary>
        public int FragmentCount => fragments.Count;

        #endregion

        #region Lifecycle

        private void Awake()
        {
            // If no carry point assigned, create one as child
            if (carryPoint == null)
            {
                var carryPointGO = new GameObject("CarryPoint");
                carryPointGO.transform.SetParent(transform);
                carryPointGO.transform.localPosition = new Vector3(0, 1.5f, 0.3f);
                carryPoint = carryPointGO.transform;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Add a resource fragment to the inventory.
        /// </summary>
        public void Add(ResourceFragment fragment)
        {
            if (fragment == null) return;

            // First fragment sets the type
            if (amount == 0)
            {
                resourceType = fragment.Type;
            }
            else if (fragment.Type != resourceType)
            {
                Debug.LogWarning($"WorkerInventory: Cannot mix resource types ({resourceType} vs {fragment.Type})");
                return;
            }

            amount += fragment.Value;
            fragments.Add(fragment);

            // Attach fragment to carry point
            AttachFragment(fragment);
        }

        /// <summary>
        /// Add resources without a physical fragment.
        /// </summary>
        public void Add(EResourceType type, int resourceAmount)
        {
            if (resourceAmount <= 0) return;

            if (amount == 0)
            {
                resourceType = type;
            }
            else if (type != resourceType)
            {
                Debug.LogWarning($"WorkerInventory: Cannot mix resource types ({resourceType} vs {type})");
                return;
            }

            amount += resourceAmount;
        }

        /// <summary>
        /// Remove a specific amount from the inventory.
        /// </summary>
        /// <returns>Amount actually removed.</returns>
        public int Remove(int removeAmount)
        {
            int actualRemove = Mathf.Min(removeAmount, amount);
            amount -= actualRemove;

            if (amount <= 0)
            {
                Clear();
            }

            return actualRemove;
        }

        /// <summary>
        /// Consume up to the specified amount of a specific resource type.
        /// Only consumes if the inventory contains the matching type.
        /// </summary>
        /// <param name="type">The resource type to consume.</param>
        /// <param name="maxAmount">Maximum amount to consume.</param>
        /// <returns>Amount actually consumed (0 if wrong type or empty).</returns>
        public int ConsumeUpTo(EResourceType type, int maxAmount)
        {
            // Can only consume if we have the matching type
            if (amount <= 0 || resourceType != type)
                return 0;

            int toConsume = Mathf.Min(maxAmount, amount);
            amount -= toConsume;

            if (amount <= 0)
            {
                Clear();
            }

            return toConsume;
        }

        /// <summary>
        /// Clear the entire inventory with drop animations.
        /// </summary>
        public void Clear()
        {
            // Drop all fragments with animation (will destroy themselves after animation)
            foreach (var fragment in fragments)
            {
                if (fragment != null)
                {
                    fragment.Drop();
                }
            }
            fragments.Clear();

            amount = 0;
            resourceType = EResourceType.Wood; // Reset to default
        }

        /// <summary>
        /// Get all carried fragments.
        /// </summary>
        public IReadOnlyList<ResourceFragment> GetFragments() => fragments;

        #endregion

        #region Private Methods

        private void AttachFragment(ResourceFragment fragment)
        {
            if (carryPoint == null) return;

            // Use the fragment's PickUp method for proper animation and scaling
            int stackIndex = fragments.Count - 1;
            fragment.PickUp(carryPoint, stackIndex);
        }

        #endregion
    }
}
