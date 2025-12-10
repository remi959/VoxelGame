using System.Collections.Generic;
using Assets.Scripts.Core;
using Assets.Scripts.NPCs.States;
using Assets.Scripts.Resources;
using Assets.Scripts.Shared.Enums;
using UnityEngine;

namespace Assets.Scripts.NPCs.Units
{
    public class Worker : NPCBase
    {
        [Header("Gathering Settings")]
        [SerializeField] private int carryCapacity = 20;
        [SerializeField] private float interactionDistance = 2f;

        [Header("Carry Visuals")]
        [SerializeField] private Transform carryPoint;

        private int carriedAmount = 0;
        private EResourceType carriedType;
        private Resource currentResource;
        private readonly List<ResourceFragment> carriedFragments = new();

        #region Properties

        public int CarriedAmount => carriedAmount;
        public int CarryCapacity => carryCapacity;
        public EResourceType CarriedType => carriedType;
        public bool IsInventoryFull => carriedAmount >= carryCapacity;

        #endregion

        #region Lifecycle

        protected override void Awake()
        {
            base.Awake();
            npcName = "Worker";

            if (carryPoint == null)
            {
                GameObject carryObj = new("CarryPoint");
                carryPoint = carryObj.transform;
                carryPoint.SetParent(transform);
                carryPoint.SetLocalPositionAndRotation(new Vector3(0, 1.2f, 0.2f), Quaternion.identity);
            }
        }

        protected override void InitializeStateMachine()
        {
            stateMachine = new StateMachine();

            stateMachine.AddState(new WorkerIdleState(this));
            stateMachine.AddState(new MoveToTargetState(this, stateMachine, interactionDistance));
            stateMachine.AddState(new GatheringState(this, stateMachine));
            stateMachine.AddState(new PickUpFragmentState(this, stateMachine));
            stateMachine.AddState(new DepositingState(this, stateMachine));

            stateMachine.SetState<WorkerIdleState>();
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            ClearInventory();
        }

        private void OnDestroy() => ClearInventory();

        #endregion

        #region Interaction

        public override void InteractWith(GameObject target)
        {
            // Check for StoragePoint first - allow depositing if carrying resources
            if (target.TryGetComponent<StoragePoint>(out var storage))
            {
                if (carriedAmount > 0)
                {
                    if (storage.AcceptsType(carriedType)) { DepositAt(storage); return; }
                    else DebugManager.LogInventory($"Worker: Storage doesn't accept {carriedType}");
                }
                else DebugManager.LogInventory("Worker: Nothing to deposit");

                return;
            }

            // Check for Resource
            if (!target.TryGetComponent<Resource>(out var resource)) resource = target.GetComponentInParent<Resource>();


            if (resource != null && !resource.IsDepleted) { GatherFrom(resource); return; }

            // Check for loose fragment
            var fragment = target.GetComponent<ResourceFragment>();
            if (fragment != null && fragment.CanBePickedUp) { PickUpFragment(fragment, null); return; }

            base.InteractWith(target);
        }

        #endregion

        #region Gathering

        public void GatherFrom(Resource resource)
        {
            if (resource == null || resource.IsDepleted) { DebugManager.LogGathering("Worker: Cannot gather from null or depleted resource"); return; }

            currentResource = resource;

            var gatherState = stateMachine.GetState<GatheringState>();
            gatherState.SetTarget(resource);

            // Get the appropriate work position (piece position for yielding stages)
            Vector3 workPosition = resource.GetWorkPositionFor(transform);

            var moveState = stateMachine.GetState<MoveToTargetState>();
            moveState.SetTarget(workPosition, () =>
            {
                stateMachine.SetState<GatheringState>();
            });

            stateMachine.SetState<MoveToTargetState>();
        }

        public void PickUpFragment(ResourceFragment fragment, Resource sourceResource)
        {
            if (fragment == null) return;

            if (sourceResource == null) sourceResource = currentResource;

            var pickupState = stateMachine.GetState<PickUpFragmentState>();
            pickupState.SetTarget(fragment, sourceResource);

            stateMachine.SetState<PickUpFragmentState>();
        }

        #endregion

        #region Depositing

        /// <summary>
        /// Deposit resources at a specific storage point.
        /// </summary>
        public void DepositAt(StoragePoint storage)
        {
            if (storage == null || carriedAmount <= 0) { DebugManager.LogInventory("Worker: Cannot deposit - no storage or nothing to deposit"); return; }

            // Clear current resource since we're manually depositing
            currentResource = null;

            var depositState = stateMachine.GetState<DepositingState>();
            depositState.SetLastResource(null); // Don't go back to resource after manual deposit
            depositState.SetStoragePoint(storage);

            var moveState = stateMachine.GetState<MoveToTargetState>();
            moveState.SetTarget(storage.transform.position, () =>
            {
                stateMachine.SetState<DepositingState>();
            });

            stateMachine.SetState<MoveToTargetState>();

            DebugManager.LogInventory($"Worker: Moving to deposit {carriedAmount} {carriedType} at {storage.name}");
        }

        public void ReturnToStorage()
        {
            StoragePoint storage = StoragePoint.FindNearest(transform.position, carriedType);

            if (storage == null)
            {
                DebugManager.LogWarning($"No storage point found for {carriedType}!");
                stateMachine.SetState<WorkerIdleState>();
                return;
            }

            var depositState = stateMachine.GetState<DepositingState>();
            depositState.SetLastResource(currentResource);
            depositState.SetStoragePoint(storage);

            var moveState = stateMachine.GetState<MoveToTargetState>();
            moveState.SetTarget(storage.transform.position, () =>
            {
                stateMachine.SetState<DepositingState>();
            });

            stateMachine.SetState<MoveToTargetState>();
        }

        #endregion

        #region Inventory Management

        /// <summary>
        /// Add a fragment to the worker's inventory with visual attachment.
        /// </summary>
        public void AddToInventory(ResourceFragment fragment)
        {
            if (fragment == null) { DebugManager.LogWarning("Worker: Tried to add null fragment to inventory"); return; }

            carriedType = fragment.Type;
            carriedAmount = Mathf.Min(carriedAmount + fragment.Value, carryCapacity);

            int stackIndex = carriedFragments.Count;

            DebugManager.LogInventory($"Worker: Adding fragment to inventory. Stack index: {stackIndex}, Type: {fragment.VisualKey}");

            fragment.PickUp(carryPoint, stackIndex);
            carriedFragments.Add(fragment);

            DebugManager.LogInventory($"Worker: Now carrying {carriedAmount}/{carryCapacity} {carriedType} ({carriedFragments.Count} visual items)");
        }

        /// <summary>
        /// Add resources to inventory without a physical fragment.
        /// </summary>
        public void AddToInventory(EResourceType type, int amount)
        {
            carriedType = type;
            carriedAmount = Mathf.Min(carriedAmount + amount, carryCapacity);
            DebugManager.LogInventory($"Worker: Now carrying {carriedAmount}/{carryCapacity} {carriedType}");
        }

        /// <summary>
        /// Clear inventory with drop animation (for depositing).
        /// </summary>
        public void ClearInventory()
        {
            DebugManager.LogInventory($"Worker: Clearing inventory. Dropping {carriedFragments.Count} fragments.");

            foreach (var fragment in carriedFragments)
                if (fragment != null && !fragment.IsDropping) fragment.Drop();

            carriedFragments.Clear();
            carriedAmount = 0;

            DebugManager.LogInventory("Worker: Inventory cleared");
        }

        /// <summary>
        /// Get the number of visual fragments being carried.
        /// </summary>
        public int GetFragmentCount() => carriedFragments.Count;

        /// <summary>
        /// Update stack positions after a fragment is removed.
        /// </summary>
        private void ReindexFragments()
        {
            for (int i = 0; i < carriedFragments.Count; i++)
            {
                var fragment = carriedFragments[i];
                if (fragment != null) fragment.UpdateStackPosition(i);
            }
        }

        #endregion

        #region Movement

        public override void MoveTo(Vector3 destination)
        {
            currentResource = null;

            var moveState = stateMachine.GetState<MoveToTargetState>();
            moveState.SetTarget(destination, () =>
            {
                stateMachine.SetState<WorkerIdleState>();
            });

            stateMachine.SetState<MoveToTargetState>();
        }

        #endregion
    }
}