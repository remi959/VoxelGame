# Code Review - VoxelGame

**Reviewer**: Claude (Senior-Level)
**Date**: 2026-02-22
**Branch**: `dev`
**Scope**: Full codebase under `Assets/Scripts/`

---

## Executive Summary

This is a well-structured Unity RTS/colony-sim project with resource gathering, building construction, and combat. The codebase demonstrates solid engineering fundamentals: feature-based folder organization, event-driven architecture, centralized NPC updates, and a clear source-of-truth model for resource data.

However, there is meaningful over-engineering in several areas. The project has infrastructure for networking/replay, modding support, and multiplayer that appears speculative given the current single-player state. The command buffer, deterministic tick system, and data-driven state factory add indirection without clear present-day consumers. Several systems have layers of abstraction that obscure otherwise straightforward logic.

**Summary of verdicts:**
- 11 findings rated **KEEP** (complexity justified)
- 11 findings rated **SIMPLIFY** (unnecessary or premature complexity)
- 3 findings rated **CAUTION** (correct approach but with bugs or risks)

---

## 1. Architecture

### 1.1 Feature-Based Folder Organization — **KEEP**

```
Assets/Scripts/
├── Core/          (Services, Events, Input, Camera)
├── Features/      (Buildings, Resources, Units, Commands)
└── Shared/        (Interfaces, Enums, Utilities, Pooling)
```

This is the right organization for a game of this size. Features are isolated, shared code is minimal, and core infrastructure is centralized. The `Data/`, `Events/`, `Scripts/` sub-organization within each feature is consistent and predictable.

### 1.2 ServiceLocator — **SIMPLIFY**

**File**: `Core/Services/ServiceLocator.cs`

Every service that registers with `ServiceLocator` also exposes a `static Instance` property (e.g., `EconomyService.Instance`, `ConstructionService.Instance`, `WorkQueueService.Instance`). The codebase uses `.Instance` directly throughout — the `ServiceLocator` is never called for resolution at runtime in gameplay code.

This means two DI systems coexist and neither is used exclusively. The ServiceLocator adds a registration step without providing its benefits (testability, swappable implementations) because callers bypass it entirely.

**Recommendation**: Pick one. Either:
- Remove `ServiceLocator` and keep the static `.Instance` pattern (simpler, matches actual usage)
- Remove all `.Instance` properties and use `ServiceLocator.Get<T>()` consistently (more testable, but requires refactoring all call sites)

The static `.Instance` pattern is fine for a single-player Unity game. The ServiceLocator would matter if you were writing unit tests with mocked services, but there are no tests in the project.

### 1.3 EventBus — **KEEP**

**File**: `Core/Events/EventBus.cs`

Zero-allocation, generic static event system. The implementation is clean and the design avoids dictionary lookups by using `EventHolder<T>`. This is a well-known Unity pattern that earns its complexity: it decouples features (Buildings don't reference Units directly), enables easy addition of new listeners, and has zero GC overhead.

### 1.4 Dual Event Systems — **SIMPLIFY**

**File**: `Features/Resources/Scripts/Economy/EconomyService.cs:58-61`

`EconomyService` exposes both C# events (`OnResourceChanged`, `OnReservationCreated`, etc.) *and* publishes to the `EventBus`. This is redundant.

```csharp
// C# events
public event Action<int, EResourceType, int, int> OnResourceChanged;

// Also publishes to EventBus
EventBus.Publish(new ResourceChangedEvent { ... });
```

Any listener using the C# event is tightly coupled to `EconomyService` anyway. If you have an EventBus, use it consistently.

**Recommendation**: Remove the `Action` events from `EconomyService` and use `EventBus` exclusively. This eliminates the dual-subscription confusion and keeps the event routing consistent.

### 1.5 NPCBase Static Registry + EntityRegistry Overlap — **SIMPLIFY**

**File**: `Features/Units/Scripts/Core/NPCBase.cs:31-41`

`NPCBase` maintains its own `static HashSet<NPCBase> allNPCs` while the project also has `EntityRegistry<T>` — a generic static registry used by `CompletedBuilding`, `CraftingBench`, `StoragePoint`, etc.

NPCs are the only entity type that doesn't use `EntityRegistry<T>`. This is an inconsistency. `NPCBase.All` returns `IEnumerable<NPCBase>` which also means LINQ usage is possible in hot paths (the `using System.Linq` import is present in NPCBase.cs).

**Recommendation**: Register NPCs with `EntityRegistry<NPCBase>` like every other entity, and remove the custom static collection. This unifies spatial queries across all entity types.

---

## 2. Over-Engineering (YAGNI/KISS Analysis)

### 2.1 Command Buffer + GameTick Deterministic System — **SIMPLIFY**

**Files**: `Features/Commands/Scripts/CommandBuffer.cs`, `Core/Services/GameTick.cs`

This is the most significant instance of speculative architecture. The `CommandBuffer` implements:
- Tick-scheduled command execution
- Command history for replay/rollback
- Deterministic ordering by player ID
- SortedList-based tick processing

The `GameTick` system provides deterministic tick counting for networking.

**The problem**: There is no networking, no replay system, and no multiplayer. `CommandBuffer` has registered handlers but commands are not routed through it consistently — most gameplay actions go directly through the EventBus or method calls (e.g., `Worker.GatherFrom()`, `Worker.StartBuilding()`). The `BuildingCommand.cs` publishes via EventBus, not the CommandBuffer.

The entire command infrastructure exists for a hypothetical future use case. Meanwhile, it adds:
- An extra `MonoBehaviour` running in `Update()`
- A parallel command routing path that conflicts with the EventBus
- `GameTick.Current` calls scattered throughout code that could use `Time.time`

**Recommendation**: Remove `CommandBuffer` and `GameTick` until networking is actually being implemented. The EventBus already handles command routing. When networking becomes a real requirement, the command pattern can be introduced with full knowledge of the actual networking library's constraints.

### 2.2 StateFactory + StateConfigSO + WorkerStateConfig — **SIMPLIFY**

**Files**: `Features/Units/Scripts/Core/StateFactory.cs`, `Features/Units/Data/StateConfigSO.cs`, `Features/Units/Scripts/Workers/WorkerStateConfig.cs`

Three files collaborate to create states for the state machine:
1. `StateConfigSO` — ScriptableObject listing state types
2. `WorkerStateConfig.CreateDefault()` — Code that builds a `StateConfigSO` at runtime
3. `StateFactory` — Switch statement mapping `StateType` enum to constructors
4. `StateFactory.SetStateByType()` — A second switch statement duplicating the mapping

Additionally, `StateFactory` has a `customCreators` dictionary for "modding support" that is never used.

The actual state machine initialization in `Worker.cs:87-97` does this:
```csharp
var config = stateConfig != null ? stateConfig : WorkerStateConfig.CreateDefault();
var factory = new StateFactory(this, stateMachine, interactionDistance);
factory.InitializeFromConfig(config);
```

This is three layers of indirection to do what could be:
```csharp
stateMachine.AddState(new WorkerIdleState(this));
stateMachine.AddState(new GatheringState(this, stateMachine));
// ... etc
stateMachine.SetState<WorkerIdleState>();
```

The data-driven approach would make sense if designers needed to configure different worker types via the Inspector, but `WorkerStateConfig.CreateDefault()` shows that even the code path creates a hardcoded configuration.

**Recommendation**: Initialize states directly in `Worker.InitializeStateMachine()` and `OffensiveNPCBase.InitializeStateMachine()`. Remove `StateFactory`, `StateConfigSO`, and `WorkerStateConfig`. If you need different unit types later, you can extract a factory *at that point* with knowledge of the actual variation needed.

### 2.3 StateFactory Modding Support — **SIMPLIFY**

**File**: `Features/Units/Scripts/Core/StateFactory.cs:202-226`

```csharp
private static readonly Dictionary<StateType, Func<StateFactory, IState>> customCreators = new();

public static void RegisterCustomCreator(StateType type, Func<StateFactory, IState> creator) { ... }
public static void UnregisterCustomCreator(StateType type) { ... }
public static void ClearCustomCreators() { ... }
```

This static dictionary and three public methods exist for a modding system that doesn't exist. There are zero callers of `RegisterCustomCreator` in the codebase. This is textbook YAGNI.

**Recommendation**: Remove entirely. Modding support requires far more infrastructure than a state factory hook, and the actual modding API would look different once real requirements exist.

### 2.4 BuildingDefinitionSO Prerequisite System — **SIMPLIFY**

**File**: `Features/Buildings/Data/BuildingDefinitionSO.cs:179-197`

```csharp
public BuildingDefinitionSO[] requiredBuildings;

public bool ArePrerequisitesMet(System.Func<BuildingDefinitionSO, bool> playerBuildingChecker)
```

A tech-tree prerequisite system. There is one building type in the game. There are no callers of `ArePrerequisitesMet()`. This is speculative design for a feature that doesn't exist yet.

**Recommendation**: Remove `requiredBuildings` and `ArePrerequisitesMet()`. Add them back when a tech tree is actually being designed.

### 2.5 WorkQueueService Task Scoring — **SIMPLIFY**

**File**: `Features/Buildings/Scripts/Components/WorkQueueService.cs:245-268`

```csharp
private float CalculateSiteScore(BuildingSite site, Vector3 workerPosition)
{
    float score = 0f;
    float distanceScore = Mathf.Max(0f, 100f - distance);
    score += distanceScore * 1f;
    float progressScore = site.Progress * 50f;
    score += progressScore * 0.5f;
    float workerRatio = (float)site.AssignedWorkerIds.Count / site.MaxWorkers;
    float workerScore = (1f - workerRatio) * 30f;
    score += workerScore * 0.8f;
    // TODO: building type priority
}
```

A multi-factor weighted scoring function with magic numbers and commented-out priority logic. For the current game state (likely a few buildings at most), "nearest available site" would suffice. The weights (1.0, 0.5, 0.8) are arbitrary and untested.

**Recommendation**: Replace with simple nearest-site-with-capacity logic. Extract the scoring function later when there's enough gameplay to tune the weights against real playtesting feedback.

### 2.6 UnitCommands Without Consumers — **SIMPLIFY**

**File**: `Features/Commands/Scripts/UnitCommands.cs`

Six command classes (`MoveUnitCommand`, `StopUnitCommand`, `GatherResourceCommand`, `DepositResourcesCommand`, `BuildStructureCommand`, `AttackTargetCommand`) are defined but have no registered handlers in `CommandBuffer`. The actual gameplay uses direct method calls (`worker.GatherFrom()`, `worker.MoveTo()`).

These are data classes for a command system that isn't wired up.

**Recommendation**: Remove until the command buffer is actually used. The command classes can be reintroduced when there's a real consumer.

---

## 3. Bugs and Correctness Issues

### 3.1 EntityRegistry Shared Buffer Thread Safety — **CAUTION**

**File**: `Shared/EntityRegistry.cs:15,105-121`

```csharp
private static readonly List<T> sharedBuffer = new();

public static List<T> FindAllInRange(Vector3 position, float range, ...)
{
    sharedBuffer.Clear();
    // ... populate ...
    return sharedBuffer;
}
```

`FindAllInRange` returns a *shared static list*. If a caller stores the reference and another call is made, the original reference is mutated. This is documented as "do not store reference" in `SpatialHash.Query()` but **not** documented in `EntityRegistry.FindAllInRange()`.

More critically, if `FindAllInRange` is called from inside a loop that also calls `FindAllInRange` (nested queries), the inner call will corrupt the outer results.

**Recommendation**: Add a clear comment warning callers, or better, accept a `List<T>` parameter to fill (like `SpatialHash.Query`'s second overload does). This is a latent bug waiting to happen.

### 3.2 ResourceStorageAggregator Uses Reflection — **CAUTION**

**File**: `Features/Resources/Scripts/Economy/ResourceStorageAggregator.cs:150-166`

```csharp
private void SetStorageAcceptAllTypes(StoragePoint storage, bool acceptAll)
{
    var field = typeof(StoragePoint).GetField("acceptAllTypes",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    field?.SetValue(storage, acceptAll);
}
```

Reflection to set private fields is fragile — renaming the field breaks this silently (the `?.` swallows the null). The comment even acknowledges this: *"In production, you'd use a prefab or a factory method."*

**Recommendation**: Add a public `SetOwner(int id)` method and a public `SetAcceptAllTypes(bool)` method to `StoragePoint`. StoragePoint already has serialized fields for these; adding setters is trivial and eliminates the reflection.

### 3.3 EconomyService Reservation Expiration in Update() — **CAUTION**

**File**: `Features/Resources/Scripts/Economy/EconomyService.cs:324-351`

Every frame, the expiration check:
1. Copies all reservation keys to a list
2. Sorts them
3. Iterates to find expired ones
4. Cancels them

This runs every frame even when there are zero reservations. The sort is for "deterministic order" — but reservation cancellation is idempotent and order doesn't matter for a single-player game.

**Recommendation**: Early-return when `reservations.Count == 0`. Remove the sort. If deterministic ordering becomes necessary for networking, add it at that point.

---

## 4. Performance

### 4.1 NPCUpdateManager — **KEEP**

**File**: `Core/Services/NPCUpdateManager.cs`

Centralized update loop instead of per-NPC `Update()`. This is a well-known Unity optimization that matters at scale (100+ NPCs). The deferred add/remove during iteration is correctly implemented.

### 4.2 SpatialHash — **KEEP**

**File**: `Shared/Utilities/SpatialHash.cs`

Proper spatial hashing implementation with cell-based queries, reusable result lists, and XZ-plane distance checks. Used for building placement validation where Physics queries would allocate. The implementation is clean and the API is well-documented.

### 4.3 BuildingHolder.GetPartsSorted() Allocates Every Call — **SIMPLIFY**

**File**: `Features/Buildings/Scripts/Components/BuildingHolder.cs:23-35`

```csharp
public List<BuildingPart> GetPartsSorted()
{
    var parts = new List<BuildingPart>(GetComponentsInChildren<BuildingPart>());
    parts.Sort(...);
    return parts;
}
```

This allocates a new list and calls `GetComponentsInChildren` every time. It's called by `BuildingDefinitionSO.GetPartsSorted()`, `GetPartCount()`, `GetTotalCosts()`, and `GetTotalConstructionTime()`. Building parts don't change at runtime — this should be cached.

**Recommendation**: Cache the sorted parts list on first access (lazy initialization). Invalidate only in `OnValidate()` for editor changes.

### 4.4 CraftingBench.Update() When Not Crafting — **SIMPLIFY**

**File**: `Features/Buildings/Scripts/Components/CraftingBench.cs:61-73`

```csharp
private void Update()
{
    if (isCrafting) { ... }
    else if (craftQueue.Count > 0) { TryStartNext(); }
}
```

The `Update()` runs every frame even when the bench is idle (not crafting, empty queue). For a rarely-used per-building component, this is minor, but it's easy to fix.

**Recommendation**: Disable the component when idle, re-enable when a craft is requested. Or move crafting progress to the centralized update manager.

### 4.5 BuildingDefinitionSO.GetTotalCosts() Allocates — **SIMPLIFY**

**File**: `Features/Buildings/Data/BuildingDefinitionSO.cs:127-131`

```csharp
public Dictionary<EResourceType, int> GetTotalCosts()
{
    return Holder?.GetTotalCosts() ?? new Dictionary<EResourceType, int>();
}
```

Every call to `GetTotalCosts()` creates a new Dictionary via `BuildingHolder.GetTotalCosts()`. This is called during placement validation (20Hz), reservation creation, and UI updates. The costs for a building definition never change at runtime.

**Recommendation**: Cache the result. Building definitions are immutable ScriptableObjects.

---

## 5. Unity-Specific Issues

### 5.1 BuildingSettingsSO Singleton via Resources.Load — **KEEP** (with caveat)

**File**: `Features/Buildings/Data/BuildingSettingsSO.cs:69-94`

```csharp
public static BuildingSettingsSO Instance
{
    get
    {
        if (_instance == null)
        {
            _instance = Resources.Load<BuildingSettingsSO>("Config/BuildingSettings");
            if (_instance == null)
            {
                _instance = CreateInstance<BuildingSettingsSO>();
            }
        }
        return _instance;
    }
}
```

This is a standard pattern for config ScriptableObjects. The fallback `CreateInstance` ensures the game doesn't crash if the asset is missing. However, this doesn't use the `ServiceLocator` or follow the same pattern as other services — it's a third singleton approach in the codebase (alongside `ServiceLocator` and `.Instance` on MonoBehaviours).

**Caveat**: Ensure the asset exists at `Resources/Config/BuildingSettings`. The silent fallback to defaults could mask a missing asset in builds.

### 5.2 CraftedPart Coroutine-Based Animations — **KEEP**

**File**: `Features/Buildings/Scripts/Components/CraftedPart.cs:93-116`

The pickup/place animations use coroutines. For one-shot tweening on individual objects, coroutines are appropriate and simpler than a centralized animation system. The `StopCoroutine` call before starting new ones prevents overlapping animations.

### 5.3 NPCBase.OnEnable Race Condition — **CAUTION**

**File**: `Features/Units/Scripts/Core/NPCBase.cs:119-123`

```csharp
if (NPCUpdateManager.Instance != null)
{
    NPCUpdateManager.Instance.Register(this);
}
```

If `NPCUpdateManager` hasn't been initialized when an NPC is enabled (scene load order), the NPC silently fails to register and will never get `UpdateNPC()` calls. There's no retry mechanism.

**Recommendation**: Use `ServiceLocator` for registration (which could queue registrations), or ensure initialization order via Script Execution Order settings, or have `NPCUpdateManager.Awake()` find and register existing NPCs.

---

## 6. Code Quality and Maintainability

### 6.1 Comment Quality — **KEEP**

The codebase has consistently good XML documentation comments on public APIs, with design rationale in class-level summaries. Comments like "Why this exists:" blocks in `SpatialHash.cs` and `CommandBuffer.cs` explain architectural intent clearly. This is above-average for a game project.

### 6.2 Excessive File Headers — **SIMPLIFY**

Most files start with:
```csharp
// ============================================================================
// FileName.cs - Brief description
// ============================================================================
```

This is noise. The file name is already the file name. The brief description duplicates the class-level XML doc. These banners add maintenance burden (keeping the description in sync) with no searchability benefit.

**Recommendation**: Remove the banner comments. The XML doc `<summary>` on the class is sufficient.

### 6.3 Namespace Inconsistency — **SIMPLIFY**

Namespaces don't match folder structure consistently:

| File Location | Namespace |
|---|---|
| `Features/Buildings/Data/BuildingSettingsSO.cs` | `Assets.Scripts.Data` |
| `Features/Buildings/Data/BuildingDefinitionSO.cs` | `Assets.Scripts.Buildings` |
| `Features/Buildings/Events/BuildingEvents.cs` | `Assets.Scripts.Events` |
| `Features/Resources/Scripts/Economy/EconomyService.cs` | `Assets.Scripts.Economy` |
| `Features/Units/Scripts/Core/NPCBase.cs` | `Assets.Scripts.NPCs` |

The namespace `Assets.Scripts` prefix is also unconventional for Unity projects (most use the project name or a company namespace).

**Recommendation**: Align namespaces with folder paths. Use a project-level namespace root (e.g., `VoxelGame.Buildings`, `VoxelGame.Units`). This prevents confusion when navigating between files and folders.

### 6.4 IHoverTarget Interface Coupling — **KEEP**

**File**: `Shared/Interfaces/IHoverTarget.cs`

Clean interface with struct-based `HoverAction` for zero-allocation action descriptions. The `ExecuteAction(string actionId)` pattern decouples UI from implementation. Three implementors (`CompletedBuilding`, `BuildingBlueprint`, `StoragePoint`) use it consistently. This is a good abstraction — it solves a real present problem (contextual UI actions on different world objects).

### 6.5 IInteractable Interface — **KEEP**

**File**: `Shared/Interfaces/IInteractable.cs`

Implemented by `Resource`, `StoragePoint`, `BuildingBlueprint`, and `WorldResourcePickup`. The worker's `InteractWith()` method uses `TryGetComponent<IInteractable>()` for dispatch. This is the right level of abstraction — it avoids a growing chain of `if (target.GetComponent<Resource>()) ... else if (target.GetComponent<StoragePoint>()) ...` checks.

---

## 7. Coupling Concerns

### 7.1 WorkQueueService Direct ConstructionService Coupling — **SIMPLIFY**

**File**: `Features/Buildings/Scripts/Components/WorkQueueService.cs:162,227,286`

```csharp
var site = ConstructionService.Instance.GetSite(assignment.SiteId);
foreach (var site in ConstructionService.Instance.GetSitesNeedingWorkers())
ConstructionService.Instance.OnWorkerStartedConstruction(site.SiteId, workerId);
```

`WorkQueueService` directly calls `ConstructionService.Instance` three times. Given that the project uses EventBus for decoupling elsewhere, this tight coupling is inconsistent.

**Recommendation**: Either use EventBus for worker-to-construction communication (consistent with the rest of the architecture), or accept the direct coupling but document the dependency explicitly.

### 7.2 NPCBase References WorkerIdleState — **SIMPLIFY**

**File**: `Features/Units/Scripts/Core/NPCBase.cs:192`

```csharp
moveState.SetTarget(destination, () => stateMachine.SetState<WorkerIdleState>());
```

The *base* class references a specific subclass state (`WorkerIdleState`). This means non-worker NPCs (combat units) would transition to a worker-specific idle state after movement, which is incorrect.

**Recommendation**: Make `MoveTo()` abstract or use a generic idle state, or have each subclass override `MoveTo()` with its own completion state.

---

## 8. Testing and Debugging

### 8.1 DebugManager Conditional Logging — **KEEP**

**File**: `Core/Services/DebugManager.cs`

Category-based logging (`LogState`, `LogGathering`, `LogInventory`, etc.) with toggleable categories. This is essential for a game with many interacting systems. The `[Conditional]` attribute approach (if used) or runtime checks avoid string allocations when logging is off.

### 8.2 No Unit Tests — **Observation**

There are no test assemblies or test files in the project. The architecture (ServiceLocator, interfaces, pure data classes like `BuildingSite`, `ResourceReservation`) could support testing, but no tests exist.

This is common in Unity game projects and not necessarily a problem at this stage, but it means the speculative "testability" justification for patterns like ServiceLocator is theoretical.

---

## 9. Summary of Recommendations

### High Priority (Simplify Now)

| # | Finding | Action |
|---|---------|--------|
| 2.1 | CommandBuffer + GameTick | Remove until networking is real |
| 2.2 | StateFactory + StateConfigSO | Replace with direct state initialization |
| 2.6 | UnitCommands without consumers | Remove dead code |
| 3.2 | Reflection in ResourceStorageAggregator | Add public setters to StoragePoint |
| 7.2 | NPCBase references WorkerIdleState | Fix incorrect state transition |

### Medium Priority (Improve When Touching)

| # | Finding | Action |
|---|---------|--------|
| 1.2 | Dual DI (ServiceLocator + .Instance) | Pick one pattern |
| 1.4 | Dual events (C# events + EventBus) | Use EventBus exclusively |
| 1.5 | NPCBase static registry vs EntityRegistry | Use EntityRegistry consistently |
| 4.3 | BuildingHolder allocations | Cache sorted parts |
| 4.5 | BuildingDefinitionSO.GetTotalCosts() | Cache result |
| 6.3 | Namespace inconsistency | Align with folder paths |

### Low Priority (Nice to Have)

| # | Finding | Action |
|---|---------|--------|
| 2.4 | Prerequisite system with no callers | Remove unused code |
| 2.5 | WorkQueueService scoring complexity | Simplify to nearest-available |
| 3.1 | EntityRegistry shared buffer | Add caller warning or parameter |
| 3.3 | Reservation expiration every frame | Early-return on empty |
| 6.2 | File header banners | Remove noise comments |

---

## 10. What's Done Well

These are aspects that should be preserved and extended:

1. **Source-of-truth model** for resources (StoragePoint -> Aggregator -> EconomyService) is clean and prevents data duplication.
2. **Polling-based work system** in `Resource.cs` avoids coroutine complexity and supports multiple concurrent workers.
3. **StateCategory enum** for O(1) state queries instead of type-checking is a smart optimization.
4. **Feature-based organization** with consistent `Data/Events/Scripts` subfolders.
5. **Zero-allocation EventBus** with proper subscribe/unsubscribe lifecycle.
6. **SpatialHash** for placement validation instead of Physics queries.
7. **Per-worker BuildingWorkContext** allowing multiple workers to build simultaneously without shared state conflicts.
8. **IHoverTarget** providing a clean abstraction for contextual UI without type-checking.
9. **Centralized NPCUpdateManager** eliminating per-MonoBehaviour Update overhead.
10. **BuildingBlueprint slot system** with dropped-part recovery — thoughtful edge case handling.
