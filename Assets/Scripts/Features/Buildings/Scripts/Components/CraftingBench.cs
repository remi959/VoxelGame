using System.Collections.Generic;
using Assets.Scripts.Core;
using Assets.Scripts.Core.Services;
using Assets.Scripts.Shared;
using Assets.Scripts.Shared.Enums;
using UnityEngine;

namespace Assets.Scripts.Buildings
{
    /// <summary>
    /// Crafting bench where workers construct building parts from resources.
    /// </summary>
    public class CraftingBench : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float craftingSpeedMultiplier = 1f;
        [SerializeField] private Transform outputPoint;
        [SerializeField] private int maxStoragePerType = 100;

        private Dictionary<EResourceType, int> storedResources = new();
        private CraftRequest currentRequest;
        private float craftingProgress;
        private bool isCrafting;
        private Queue<CraftRequest> craftQueue = new();

        public bool IsCrafting => isCrafting;
        public Vector3 OutputPosition => outputPoint != null ? outputPoint.position : transform.position + transform.forward;

        private struct CraftRequest
        {
            public BuildingPart Part;
            public System.Action<CraftedPart> OnComplete;
        }

        #region Lifecycle

        private void Awake()
        {
            if (outputPoint == null)
            {
                var go = new GameObject("OutputPoint");
                outputPoint = go.transform;
                outputPoint.SetParent(transform);
                outputPoint.localPosition = Vector3.forward * 1.5f;
            }

            foreach (EResourceType type in System.Enum.GetValues(typeof(EResourceType)))
                storedResources[type] = 0;

            // Start with Update disabled — re-enable when work is queued
            enabled = false;
        }

        private void OnEnable()
        {
            EntityRegistry<CraftingBench>.Register(this);
        }

        private void OnDisable()
        {
            EntityRegistry<CraftingBench>.Unregister(this);
        }

        private void Update()
        {
            if (isCrafting)
            {
                craftingProgress += Time.deltaTime * craftingSpeedMultiplier;
                if (craftingProgress >= currentRequest.Part.ConstructionTime)
                    CompleteCrafting();
            }
            else if (craftQueue.Count > 0)
            {
                TryStartNext();
            }
        }

        #endregion

        #region Static

        public static CraftingBench FindNearest(Vector3 position)
        {
            return EntityRegistry<CraftingBench>.FindNearest(position);
        }

        #endregion

        #region Resources

        public void DepositResources(EResourceType type, int amount)
        {
            storedResources[type] = Mathf.Min(storedResources[type] + amount, maxStoragePerType);
            DebugManager.LogInventory($"CraftingBench: Deposited {amount} {type}. Total: {storedResources[type]}");
        }

        public int GetStoredAmount(EResourceType type)
        {
            return storedResources.TryGetValue(type, out int amt) ? amt : 0;
        }

        public bool HasResourcesFor(BuildingPart part)
        {
            foreach (var cost in part.ResourceCosts)
                if (GetStoredAmount(cost.resourceType) < cost.amount)
                    return false;
            return true;
        }

        public Dictionary<EResourceType, int> GetMissingResources(BuildingPart part)
        {
            var missing = new Dictionary<EResourceType, int>();
            foreach (var cost in part.ResourceCosts)
            {
                int stored = GetStoredAmount(cost.resourceType);
                if (stored < cost.amount)
                    missing[cost.resourceType] = cost.amount - stored;
            }
            return missing;
        }

        private bool ConsumeResourcesFor(BuildingPart part)
        {
            if (!HasResourcesFor(part)) return false;
            foreach (var cost in part.ResourceCosts)
                storedResources[cost.resourceType] -= cost.amount;
            return true;
        }

        #endregion

        #region Crafting

        public bool RequestCraft(BuildingPart part, System.Action<CraftedPart> onComplete)
        {
            if (part == null) return false;

            craftQueue.Enqueue(new CraftRequest { Part = part, OnComplete = onComplete });
            enabled = true;  // Ensure Update runs while work is queued
            DebugManager.LogState($"CraftingBench: Queued {part.gameObject.name}");
            return true;
        }

        private void TryStartNext()
        {
            if (craftQueue.Count == 0) return;

            var request = craftQueue.Peek();
            if (HasResourcesFor(request.Part))
            {
                craftQueue.Dequeue();
                StartCrafting(request);
            }
        }

        private void StartCrafting(CraftRequest request)
        {
            if (!ConsumeResourcesFor(request.Part)) return;

            currentRequest = request;
            craftingProgress = 0f;
            isCrafting = true;

            DebugManager.LogState($"CraftingBench: Started crafting {request.Part.gameObject.name}");
        }

        private void CompleteCrafting()
        {
            var request = currentRequest;

            // Spawn crafted part
            GameObject partObj = Instantiate(request.Part.gameObject, OutputPosition, Quaternion.identity);
            partObj.name = $"CraftedPart_{request.Part.gameObject.name}";

            // Swap BuildingPart for CraftedPart
            var bp = partObj.GetComponent<BuildingPart>();
            var crafted = partObj.AddComponent<CraftedPart>();
            crafted.Initialize(request.Part, OutputPosition);
            if (bp != null) Destroy(bp);

            currentRequest = default;
            craftingProgress = 0f;
            isCrafting = false;

            // Disable Update when idle (re-enabled by RequestCraft)
            if (craftQueue.Count == 0) enabled = false;

            DebugManager.LogState($"CraftingBench: Completed {request.Part.gameObject.name}");
            request.OnComplete?.Invoke(crafted);
        }

        #endregion
    }
}