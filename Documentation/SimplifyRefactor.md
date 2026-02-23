# SIMPLIFY Refactor — Implementation Summary

All 11 SIMPLIFY-rated recommendations from the code review (`CodeReview.md`) have been implemented. This document records what changed and why.

---

## Files Deleted

| File | Reason |
|------|--------|
| `Assets/Scripts/Features/Commands/Scripts/CommandBuffer.cs` | Zero registered handlers or consumers anywhere in the codebase |
| `Assets/Scripts/Features/Commands/Scripts/UnitCommands.cs` | Six command classes with zero registered handlers |
| `Assets/Scripts/Core/Services/GameTick.cs` | Speculative networking tick counter; all usages replaced with `Time.time` |
| `Assets/Scripts/Features/Units/Scripts/Core/StateFactory.cs` | Three-layer state-creation indirection replaced with direct inline construction |
| `Assets/Scripts/Features/Units/Data/StateConfigSO.cs` | ScriptableObject only referenced from files being deleted |
| `Assets/Scripts/Features/Units/Scripts/Workers/WorkerStateConfig.cs` | Runtime-built config defeating the data-driven purpose |
| `Assets/Scripts/Features/Units/Scripts/Combat/OffensiveStateConfig.cs` | Companion to WorkerStateConfig with no callers (discovered during grep) |

All associated `.meta` files were also deleted.

---

## Changes by Topic

### 1. Deleted unused command infrastructure

`CommandBuffer` and `UnitCommands` had zero callers. Confirmed via grep before deletion.

---

### 2. Replaced GameTick with Time.time

`GameTick` was designed for deterministic networked ticks that were never implemented. All fields changed from `long` to `float`, all tick arithmetic replaced with `Time.time`.

**`ResourceReservation.cs`**
- `public readonly long ExpiresAt` → `public readonly float ExpiresAt`

**`EconomyService.cs`**
- `GameTick.GetFutureTick(seconds)` → `Time.time + seconds`
- `GameTick.Current >= reservation.ExpiresAt` → `Time.time >= reservation.ExpiresAt`

**`BuildingSite.cs`**
- `public readonly long PlacedAtTick` → `public readonly float PlacedAt`
- Constructor: `PlacedAtTick = GameTick.Current` → `PlacedAt = Time.time`

**`ConstructionService.cs`**
- `GameTick.ToSeconds(GameTick.TicksSince(site.PlacedAtTick))` → `Time.time - site.PlacedAt`

**`WorkQueueService.cs`**
- `TaskAssignment.AssignedTick: long` → `TaskAssignment.AssignedAt: float`

**`UnitData.cs`** *(discovered during grep, not in original plan)*
- `public long CreatedTick` → `public float CreatedAt`
- `public long LastModifiedTick` → `public float LastModifiedAt`

---

### 3. Flattened state machine initialization

`StateFactory` + `StateConfigSO` + `WorkerStateConfig` = three layers of indirection to produce 12 `new XxxState(...)` calls. `OffensiveNPCBase` already did this inline. Worker now matches that pattern.

**`Worker.cs`**
- Removed `[SerializeField] private Data.StateConfigSO stateConfig;` field
- `InitializeStateMachine()` now constructs all states directly:

```csharp
protected override void InitializeStateMachine()
{
    stateMachine = new StateMachine();
    stateMachine.AddState(new WorkerIdleState(this));
    stateMachine.AddState(new MoveToTargetState(this, stateMachine, interactionDistance));
    stateMachine.AddState(new GatheringState(this, stateMachine));
    stateMachine.AddState(new PickUpFragmentState(this, stateMachine));
    stateMachine.AddState(new DepositingState(this, stateMachine));
    stateMachine.AddState(new ClaimSlotState(this, stateMachine));
    stateMachine.AddState(new GatherBuildResourcesState(this, stateMachine));
    stateMachine.AddState(new DeliverBuildResourcesState(this, stateMachine));
    stateMachine.AddState(new WaitForCraftState(this, stateMachine));
    stateMachine.AddState(new PickUpCraftedPartState(this, stateMachine));
    stateMachine.AddState(new DeliverPartState(this, stateMachine));
    stateMachine.AddState(new PlacePartState(this, stateMachine));
    stateMachine.SetState<WorkerIdleState>();
}
```

---

### 4. Fixed MoveToTargetState fallback bug

The old code hardcoded `stateMachine.SetState<WorkerIdleState>()` as a fallback in two places. `OffensiveNPCBase` has no `WorkerIdleState` in its state machine, so it would silently get stuck.

**`MoveToTargetState.cs — Enter() (no target set)**
```csharp
// Before:
stateMachine.SetState<WorkerIdleState>();
// After:
DebugManager.LogWarning("MoveToTargetState: Entered with no target position set.");
onFailed?.Invoke();
```

**`MoveToTargetState.cs — HandlePathFailed() (no onFailed callback)**

Also fixed a secondary bug: `onArrived` was being cleared by `Cleanup()` before it could be invoked. Both callbacks are now captured before `Cleanup()` runs:

```csharp
var failCallback = onFailed;
var arrivedCallback = onArrived;
Cleanup();
if (failCallback != null) failCallback.Invoke();
else arrivedCallback?.Invoke();
```

---

### 5. Removed reflection from ResourceStorageAggregator

`SetStorageAcceptAllTypes()` and `SetStorageOwner()` used `System.Reflection` to write private fields on `StoragePoint`. This bypasses encapsulation and breaks with IL2CPP stripping.

**`StoragePoint.cs`** — Added:
```csharp
public void Initialize(bool acceptsAllTypes, int ownerIdValue)
{
    acceptAllTypes = acceptsAllTypes;
    ownerId = ownerIdValue;
}
```

**`ResourceStorageAggregator.cs`** — Replaced two reflection calls with:
```csharp
standardStorageInstance.Initialize(true, playerId);
```

Both reflection helper methods deleted entirely. `using System.Reflection` removed.

---

### 6. Cached BuildingHolder.GetPartsSorted()

Previously allocated a new `List<BuildingPart>` on every call.

**`BuildingHolder.cs`**
- Added `private List<BuildingPart> _cachedParts;`
- `OnEnable()` sets `_cachedParts = null;` to invalidate on re-enable
- `GetPartsSorted()` returns `_cachedParts` if already computed

---

### 7. Cached BuildingDefinitionSO.GetTotalCosts()

Previously allocated a new `Dictionary<EResourceType, int>` on every call.

**`BuildingDefinitionSO.cs`**
- Added `private Dictionary<EResourceType, int> _cachedTotalCosts;`
- `GetTotalCosts()` returns cached result on subsequent calls
- `OnValidate()` clears `_cachedTotalCosts = null` so changes in the Inspector are reflected

**Note:** `ArePrerequisitesMet()` was listed for deletion in the plan ("zero callers") but was found to have a caller in `BuildingDataProvider.cs`. It was kept.

---

### 8. EconomyService.Update() early return + removed dual events

Two independent fixes applied together:

**Early return:**
```csharp
// Top of Update():
if (reservations.Count == 0) return;
```

**Dual events removed:**
`EconomyService` was firing both C# events (`OnResourceChanged`, etc.) and `EventBus.Publish` for the same data, causing `BuildingDataProvider.CheckAffordabilityChanges()` to run twice per resource change.

- `BuildingDataProvider.cs`: Removed `economyService.OnResourceChanged` subscription entirely; now uses only the EventBus path.
- `EconomyService.cs`: Removed all four C# event declarations (`OnResourceChanged`, `OnReservationCreated`, `OnReservationConfirmed`, `OnReservationCancelled`) and all their invocations.

---

### 9. Disabled CraftingBench.Update() when idle

`Update()` ran every frame even with an empty queue.

**`CraftingBench.cs`**
- `Awake()`: `enabled = false;`
- `RequestCraft()`: `enabled = true;` after enqueue
- `CompleteCrafting()`: `if (craftQueue.Count == 0) enabled = false;`

---

### 10. Simplified WorkQueueService scoring

`FindOptimalSite()` used multi-factor weighted scoring with magic numbers and a TODO comment. Replaced with nearest-distance selection, which is what the scoring approximated anyway.

**`WorkQueueService.cs`**
`CalculateSiteScore()` deleted. `FindOptimalSite()` now:

```csharp
private BuildingSite FindOptimalSite(Vector3 workerPosition, int playerId)
{
    BuildingSite nearest = null;
    float nearestDist = float.MaxValue;
    foreach (var site in ConstructionService.Instance.GetSitesNeedingWorkers())
    {
        if (site.OwnerPlayerId != playerId) continue;
        if (!site.CanAssignWorker) continue;
        float dist = Vector3.Distance(workerPosition, site.Position);
        if (dist < nearestDist) { nearestDist = dist; nearest = site; }
    }
    return nearest;
}
```

---

### 11. Documented EntityRegistry.FindAllInRange shared buffer

**`EntityRegistry.cs`** — Added `<remarks>` warning to `FindAllInRange()`:

```xml
/// <remarks>
/// WARNING: Returns a shared static buffer. Iterate the results immediately after calling.
/// Do NOT store the returned reference — it will be overwritten on the next call.
/// </remarks>
```

---

## Deferred (Not Changed)

These were identified in the review but intentionally left for a separate pass:

- **Namespace consistency** — Requires touching every file and risks `.meta` GUID mismatches.
- **ServiceLocator vs. `.Instance` unification** — Requires auditing scene initialization order.
- **NPCBase static registry vs. EntityRegistry** — Requires coordinating `NPCUpdateManager` and the registry system.
- **File header banners** — Low value, touches every file.
- **`NPCBase.MoveTo()` line 192** — Still hardcodes `WorkerIdleState`, but both subclasses (`Worker` and `OffensiveNPCBase`) fully override `MoveTo()`, so this path is unreachable in practice.

---

## Verification Checklist

1. Unity Editor compiles with zero errors
2. Worker spawns → idles → gathers from a resource → deposits to storage
3. Place a building → workers assign → build parts → construction completes
4. Reservation expiry: place building, wait 60 s, confirm auto-cancel fires
5. CraftingBench: verify `Update()` is disabled until a craft is queued
6. Combat unit (if any): right-click move → unit arrives and returns to combat idle without getting stuck
