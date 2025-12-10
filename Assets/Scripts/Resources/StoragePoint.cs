using System.Collections.Generic;
using Assets.Scripts.Events;
using Assets.Scripts.Shared.Enums;
using UnityEngine;

namespace Assets.Scripts.Resources
{
    public class StoragePoint : MonoBehaviour
    {
        private static List<StoragePoint> allStoragePoints = new();

        [Header("Storage Settings")]
        [SerializeField] private EResourceType acceptedResourceType;
        [SerializeField] private bool acceptAllTypes = false;

        public EResourceType AcceptedType => acceptedResourceType;
        public bool AcceptsAllTypes => acceptAllTypes;

        private void OnEnable() { if (!allStoragePoints.Contains(this)) allStoragePoints.Add(this); }
        private void OnDisable() => allStoragePoints.Remove(this);


        /// <summary>
        /// Find the nearest storage point that accepts the given resource type.
        /// </summary>
        public static StoragePoint FindNearest(Vector3 position, EResourceType resourceType)
        {
            StoragePoint nearest = null;
            float nearestDistance = float.MaxValue;

            foreach (var storage in allStoragePoints)
            {
                if (storage == null) continue;

                // Check if this storage accepts the resource type
                if (!storage.AcceptsType(resourceType)) continue;

                float distance = Vector3.Distance(position, storage.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = storage;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Find any storage point that accepts the given resource type.
        /// </summary>
        public static StoragePoint FindAny(EResourceType resourceType)
        {
            foreach (var storage in allStoragePoints)
                if (storage != null && storage.AcceptsType(resourceType)) return storage;

            return null;
        }

        /// <summary>
        /// Get all storage points that accept the given resource type.
        /// </summary>
        public static List<StoragePoint> FindAll(EResourceType resourceType)
        {
            List<StoragePoint> matching = new();
            foreach (var storage in allStoragePoints)
                if (storage != null && storage.AcceptsType(resourceType)) matching.Add(storage);

            return matching;
        }

        /// <summary>
        /// Check if this storage point accepts the given resource type.
        /// </summary>
        public bool AcceptsType(EResourceType type) => acceptAllTypes || acceptedResourceType == type;

        public void Deposit(EResourceType type, int amount)
        {
            if (!AcceptsType(type)) { Debug.LogWarning($"StoragePoint: Cannot deposit {type}, only accepts {acceptedResourceType}"); return; }

            Debug.Log($"StoragePoint: Deposited {amount} {type}");

            EventBus.Publish(new ResourceDepositedEvent
            {
                Amount = amount,
                ResourceType = (int)type
            });
        }
    }
}