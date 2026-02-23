using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Shared.Utilities
{
    /// <summary>
    /// Collection of performance optimization patterns and utilities.
    /// </summary>
    public static class PerformanceUtility
    {
        #region Cached WaitForSeconds

        private static readonly Dictionary<float, WaitForSeconds> waitCache = new();

        /// <summary>
        /// Get a cached WaitForSeconds to avoid GC allocations in coroutines.
        /// </summary>
        /// <param name="seconds">Duration to wait</param>
        /// <returns>Cached WaitForSeconds instance</returns>
        public static WaitForSeconds GetWait(float seconds)
        {
            // Round to 2 decimal places to increase cache hits
            seconds = Mathf.Round(seconds * 100f) / 100f;

            if (!waitCache.TryGetValue(seconds, out var wait))
            {
                wait = new WaitForSeconds(seconds);
                waitCache[seconds] = wait;
            }

            return wait;
        }

        /// <summary>
        /// Clear the wait cache (call when memory is tight or on scene unload).
        /// </summary>
        public static void ClearWaitCache() => waitCache.Clear();

        #endregion

        #region Cached WaitForEndOfFrame / WaitForFixedUpdate

        // These are singletons - Unity reuses them anyway, but explicit caching is clearer
        private static readonly WaitForEndOfFrame cachedEndOfFrame = new();
        private static readonly WaitForFixedUpdate cachedFixedUpdate = new();

        /// <summary>
        /// Get cached WaitForEndOfFrame (singleton pattern).
        /// </summary>
        public static WaitForEndOfFrame EndOfFrame => cachedEndOfFrame;

        /// <summary>
        /// Get cached WaitForFixedUpdate (singleton pattern).
        /// </summary>
        public static WaitForFixedUpdate FixedUpdate => cachedFixedUpdate;

        #endregion

        #region Common Wait Durations (Pre-cached)

        // Pre-cache common wait durations to avoid dictionary lookup
        private static readonly WaitForSeconds wait01 = new(0.1f);
        private static readonly WaitForSeconds wait025 = new(0.25f);
        private static readonly WaitForSeconds wait05 = new(0.5f);
        private static readonly WaitForSeconds wait1 = new(1f);
        private static readonly WaitForSeconds wait2 = new(2f);

        /// <summary>WaitForSeconds(0.1f) - 100ms</summary>
        public static WaitForSeconds Wait100ms => wait01;

        /// <summary>WaitForSeconds(0.25f) - 250ms</summary>
        public static WaitForSeconds Wait250ms => wait025;

        /// <summary>WaitForSeconds(0.5f) - 500ms</summary>
        public static WaitForSeconds Wait500ms => wait05;

        /// <summary>WaitForSeconds(1f) - 1 second</summary>
        public static WaitForSeconds Wait1s => wait1;

        /// <summary>WaitForSeconds(2f) - 2 seconds</summary>
        public static WaitForSeconds Wait2s => wait2;

        #endregion
    }
}

// ============================================================================
// PATTERN: Non-allocating GetComponentsInChildren
// ============================================================================
/*
// BEFORE (allocates new array each call):
var pieces = resourceVisual.GetComponentsInChildren<ResourcePiece>();
foreach (var piece in pieces) { ... }

// AFTER (reuses list):
private List<ResourcePiece> allPiecesCache = new();

private void CacheAllPieces()
{
    allPiecesCache.Clear();
    if (resourceVisual == null) return;
    
    // This overload reuses the list instead of allocating
    resourceVisual.GetComponentsInChildren(allPiecesCache);
}
*/

// ============================================================================
// PATTERN: Avoid repeated SetDestination calls
// ============================================================================
/*
// BEFORE (calls SetDestination every frame):
public void Update()
{
    float distance = Vector3.Distance(worker.transform.position, target.position);
    if (distance > pickupDistance)
        worker.Motor.SetDestination(target.position);
}

// AFTER (tracks if destination was set):
private bool hasSetDestination = false;
private Vector3 lastTargetPosition;

public void Update()
{
    if (!target.CanBePickedUp)
    {
        worker.Motor.Stop();
        hasSetDestination = false;
        return;
    }

    // Only recalculate if target moved significantly
    if (hasSetDestination && Vector3.Distance(lastTargetPosition, target.position) < 0.1f)
        return;

    float distance = Vector3.Distance(worker.transform.position, target.position);
    if (distance > pickupDistance)
    {
        worker.Motor.SetDestination(target.position);
        lastTargetPosition = target.position;
        hasSetDestination = true;
    }
    else
    {
        // Arrived - do pickup
        hasSetDestination = false;
    }
}
*/

// ============================================================================
// PATTERN: Disable Update when not needed
// ============================================================================
/*
// In ResourceFragment.cs:

private void Awake()
{
    // Start disabled - only enable when launched
    enabled = false;
}

public void Launch()
{
    enabled = true;  // Enable Update
    // ... launch logic
}

private void StopMovement()
{
    // ... stop logic
    enabled = false;  // Disable Update - no longer needed
}

private void Update()
{
    // This now only runs when the fragment is actively moving
    if (!isLaunched || rb == null || rb.isKinematic)
    {
        enabled = false;
        return;
    }
    // ... movement tracking
}
*/

// ============================================================================
// PATTERN: Avoid ToArray() allocations
// ============================================================================
/*
// BEFORE (allocates array):
NPCBase[] allNPCs = NPCBase.All.ToArray();
foreach (NPCBase npc in allNPCs) { ... }

// AFTER (iterate directly):
foreach (NPCBase npc in NPCBase.All) { ... }
*/

// ============================================================================
// PATTERN: Use sqrMagnitude for distance comparisons
// ============================================================================
/*
// BEFORE (calculates square root):
float distance = Vector3.Distance(a, b);
if (distance < threshold) { ... }

// AFTER (avoids square root):
float distanceSqr = (a - b).sqrMagnitude;
if (distanceSqr < threshold * threshold) { ... }

// Or use the utility method:
if (SpatialUtility.IsWithinDistance(a, b, threshold)) { ... }
*/

// ============================================================================
// PATTERN: Cache layer masks at initialization
// ============================================================================
/*
// BEFORE (looks up every time):
private void ProcessClick()
{
    if (Physics.Raycast(ray, out hit, Mathf.Infinity, 
        LayerMask.GetMask("Ground", "NPC")))  // Allocates string array!
    {
        if (hit.collider.gameObject.layer == LayerMask.NameToLayer("NPC"))  // String lookup!
        { ... }
    }
}

// AFTER (cached):
// Use LayerManager.SelectableMask and LayerManager.IsNPC() instead
// See LayerManager.cs for implementation
*/

// ============================================================================
// PATTERN: Object pooling for frequently created objects
// ============================================================================
/*
// For ResourceFragment, consider using Unity's ObjectPool<T> or a custom pool:

public class FragmentPool
{
    private Queue<ResourceFragment> pool = new();
    private GameObject prefab;
    
    public ResourceFragment Get()
    {
        if (pool.Count > 0)
        {
            var fragment = pool.Dequeue();
            fragment.gameObject.SetActive(true);
            return fragment;
        }
        return Object.Instantiate(prefab).GetComponent<ResourceFragment>();
    }
    
    public void Return(ResourceFragment fragment)
    {
        fragment.gameObject.SetActive(false);
        pool.Enqueue(fragment);
    }
}
*/