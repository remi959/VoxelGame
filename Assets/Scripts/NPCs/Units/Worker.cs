using System.Collections.Generic;
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
        private List<ResourceFragment> carriedFragments = new();

        public int CarriedAmount => carriedAmount;
        public int CarryCapacity => carryCapacity;
        public EResourceType CarriedType => carriedType;
        public bool IsInventoryFull => carriedAmount >= carryCapacity;

        protected override void Awake()
        {
            base.Awake();
            npcName = "Worker";

            if (carryPoint == null)
            {
                GameObject carryObj = new GameObject("CarryPoint");
                carryPoint = carryObj.transform;
                carryPoint.SetParent(transform);
                carryPoint.localPosition = new Vector3(0, 1.2f, 0.2f);
                carryPoint.localRotation = Quaternion.identity;
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

        public override void InteractWith(GameObject target)
        {
            var resource = target.GetComponent<Resource>();
            if (resource == null)
            {
                resource = target.GetComponentInParent<Resource>();
            }

            if (resource != null && !resource.IsDepleted)
            {
                GatherFrom(resource);
                return;
            }

            var fragment = target.GetComponent<ResourceFragment>();
            if (fragment != null && fragment.CanBePickedUp)
            {
                PickUpFragment(fragment, null);
                return;
            }

            base.InteractWith(target);
        }

        public void GatherFrom(Resource resource)
        {
            if (resource == null || resource.IsDepleted)
            {
                Debug.Log("Worker: Cannot gather from null or depleted resource");
                return;
            }

            currentResource = resource;

            var gatherState = stateMachine.GetState<GatheringState>();
            gatherState.SetTarget(resource);

            var moveState = stateMachine.GetState<MoveToTargetState>();
            moveState.SetTarget(resource.WorkPosition, () =>
            {
                stateMachine.SetState<GatheringState>();
            });

            stateMachine.SetState<MoveToTargetState>();
        }

        public void PickUpFragment(ResourceFragment fragment, Resource sourceResource)
        {
            if (fragment == null) return;

            if (sourceResource == null)
            {
                sourceResource = currentResource;
            }

            var pickupState = stateMachine.GetState<PickUpFragmentState>();
            pickupState.SetTarget(fragment, sourceResource);

            stateMachine.SetState<PickUpFragmentState>();
        }

        public void ReturnToStorage()
        {
            // Find nearest storage that accepts our resource type
            StoragePoint storage = StoragePoint.FindNearest(transform.position, carriedType);

            if (storage == null)
            {
                Debug.LogWarning($"No storage point found for {carriedType}!");
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

        public void AddToInventory(ResourceFragment fragment)
        {
            if (fragment == null)
            {
                Debug.LogWarning("Worker: Tried to add null fragment to inventory");
                return;
            }

            carriedType = fragment.Type;
            carriedAmount = Mathf.Min(carriedAmount + fragment.Value, carryCapacity);

            int stackIndex = carriedFragments.Count;

            Debug.Log($"Worker: Adding fragment to inventory. Stack index: {stackIndex}");

            fragment.PickUp(carryPoint, stackIndex);
            carriedFragments.Add(fragment);

            Debug.Log($"Worker: Now carrying {carriedAmount}/{carryCapacity} {carriedType} ({carriedFragments.Count} visual items)");
        }

        public void AddToInventory(EResourceType type, int amount)
        {
            carriedType = type;
            carriedAmount = Mathf.Min(carriedAmount + amount, carryCapacity);
            Debug.Log($"Worker: Now carrying {carriedAmount}/{carryCapacity} {carriedType}");
        }

        public void ClearInventory()
        {
            Debug.Log($"Worker: Clearing inventory. Dropping {carriedFragments.Count} fragments.");

            foreach (var fragment in carriedFragments)
            {
                if (fragment != null)
                {
                    fragment.Drop();
                }
            }
            carriedFragments.Clear();
            carriedAmount = 0;

            Debug.Log("Worker: Inventory cleared");
        }

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
    }
}