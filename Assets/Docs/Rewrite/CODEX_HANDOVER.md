# Codex Handover

Last updated: 2026-06-18
Workspace: `C:\Users\sjkim\Turn Based Strategy`

## Purpose

This is the current handoff for continuing the publishable runtime rewrite from the **actual live codebase**, not from older migration notes.

Use this first when resuming work. Older docs such as `CURSOR_HANDOFF.md` still contain useful history, but some of their class/file names are now outdated after the recent consolidation and rename passes.

## Current Rewrite Status

- Phase `11` is effectively done in the private workspace: active code no longer depends on `Assets/TBS Framework`
- Phase `11B` is done: active code is consolidated under `Assets/Game/Code`
- Phase `11C` is in progress: bridge cleanup, naming cleanup, responsibility cleanup
- Phase `12` audit docs exist and report a clean publication boundary, but public packaging is still future work

## Current Authoritative Code Root

Active code lives under:

- `Assets/Game/Code/*`

Do not use older `Assets/Game/Scripts/*` or `Assets/Game/Runtime/*` paths as the baseline for new work.

## Current Core Naming

The current runtime/gameplay split is:

- `CellGrid` = scene/gameplay host and scene-facing orchestration
- `RuntimeGrid` = runtime turn/state/input authority mirror
- `Cell` = unified tile type
- `Unit` = scene/gameplay unit
- `GridUnit` = runtime-side unit mirror/authority object
- `IGridUnit` = runtime-side unit interface

This replaced older naming such as:

- `BattleBoard`
- `BoardUnit`
- `BattleUnit`
- split cell mirror types

## What Was Verified This Turn

Mechanical verification:

```powershell
dotnet build com.windy.srpg.game.csproj
```

Result:

- build succeeds
- current warnings are only:
  - `DeploymentSlot.cs` obsolete `FindObjectsByType` overload usage
  - `MoveAbility.PendingActions.cs` obsolete `FindObjectOfType<T>()`

No smoke test was required yet for the work done this turn because the changes were bridge-name cleanup only.

## What Was Cleaned Up This Turn

### 1. Bridge vocabulary was renamed away from misleading `Legacy*` names

These names were misleading because they referred to the still-live scene/gameplay side rather than a dead compatibility layer.

Examples now renamed:

- `ApplyLegacyStateFromRuntime` -> `ApplySceneStateFromRuntime`
- `EnterLegacyBlockedInputState` -> `EnterSceneOnlyBlockedInputState`
- `ApplyLegacyEffectsAfterRuntime*` -> `ApplySceneEffectsAfterRuntime*`
- `ApplyLegacyTurnStartFromRuntime` -> `ApplySceneTurnStartFromRuntime`
- `ApplyLegacyTurnEndToCurrentPlayerUnits` -> `ApplySceneTurnEndToCurrentPlayerUnits`
- `ApplyRuntimeTurnStartToLegacyPlayableUnits` -> `ApplyRuntimeTurnStartToScenePlayableUnits`
- `PullRuntimeStateToLegacy` -> `PullRuntimeStateToScene`
- `PushLegacyStateToRuntimeMirror` -> `PushSceneStateToRuntimeMirror`
- `ResolveLegacyTurnStateFromRuntime` -> `ResolveSceneTurnStateFromRuntime`
- `ShouldPullLegacyFromRuntimeMirror` -> `ShouldPullSceneFromRuntimeMirror`
- `ApplyLegacySyncFromRuntimeMoveCommit` -> `ApplySceneSyncFromRuntimeMoveCommit`
- `ApplyLegacySyncAfterRuntimePendingMoveCommit` -> `ApplySceneSyncAfterRuntimePendingMoveCommit`

### 2. `CellGrid.cs` minor cleanup

- removed duplicated class summary line
- fixed a formatting slip around `FindCellByOffset`
- renamed `suppressLegacyToRuntimeStateMirror` -> `suppressSceneToRuntimeStateMirror`

## Current High-Value Files

### Grid / battle flow

- `Assets/Game/Code/Grid/CellGrid.cs`
- `Assets/Game/Code/Grid/CellGrid.Scene.cs`
- `Assets/Game/Code/Grid/CellGrid.Runtime.cs`
- `Assets/Game/Code/Grid/RuntimeGrid.cs`
- `Assets/Game/Code/Grid/RoundRobinBattleFlow.cs`
- `Assets/Game/Code/Grid/BattleFlowSceneAdapters.cs`

### Units / movement / runtime sync

- `Assets/Game/Code/Units/Unit.cs`
- `Assets/Game/Code/Units/Unit.CombatAndMovement.cs`
- `Assets/Game/Code/Units/Unit.SceneBinding.cs`
- `Assets/Game/Code/Units/GridUnit.cs`
- `Assets/Game/Code/Units/UnitTurnState.All.cs`

### Ability bridge hotspot

- `Assets/Game/Code/Abilities/MoveAbility.cs`
- `Assets/Game/Code/Abilities/MoveAbility.PendingActions.cs`

## What Still Looks Messy

These are the main remaining complexity hotspots:

### 1. `CellGrid` still owns too much bridge logic

It currently mixes:

- scene lifecycle
- runtime mirror collection sync
- runtime input coordination
- turn flow routing
- battle outcome routing
- pre-battle deployment behavior
- campaign save hooks

This is functional, but still migration-heavy.

### 2. `Unit` is still split across large combat and sync surfaces

The split is currently:

- `Unit.cs`
- `Unit.CombatAndMovement.cs`
- `Unit.SceneBinding.cs`
- `Unit.ExperienceFlow.cs`

This is acceptable for now, but `Unit.SceneBinding.cs` and movement/runtime-commit pieces still read like migration scaffolding.

### 3. `MoveAbility` still carries runtime-routing glue

Pending-move UI flow and runtime scene sync are still partially coordinated from `MoveAbility`, especially around:

- pending move confirm
- right-click recovery
- action menu transitions
- post-combat commit handling

## Recommended Next Phase 11C Slices

These are the next good cleanup targets.

### Slice A: tighten `CellGrid` runtime-state application flow

Goal:

- reduce duplicate "capture runtime decision -> apply scene state -> replay scene effect" patterns

Likely targets:

- `ProcessRuntimeRoutedSceneUnitClick`
- `ProcessRuntimeRoutedSceneCellClick`
- `ProcessRuntimeRoutedSceneRightClick`
- `ApplySceneEffectsAfterRuntime*`

Potential outcome:

- a smaller set of reusable scene-application helpers

### Slice B: reduce `MoveAbility` ownership of runtime bridge callbacks

Goal:

- keep `MoveAbility` focused on movement/action behavior, not on runtime/scene reconciliation orchestration

Likely targets:

- pending move right-click flow
- commit-after-combat flow
- menu close / cancel recovery hooks

Potential outcome:

- more of the "runtime decided X, now reconcile scene state" logic moves into `CellGrid`

### Slice C: decide whether `Unit.SceneBinding.cs` should stay separate

Current question:

- does runtime sync remain best as a dedicated `Unit.SceneBinding.cs`
- or should it merge back into `Unit.CombatAndMovement.cs` / `Unit.cs`

User preference note:

- the user is fine with large files if the grouping is coherent
- they prefer merge-first readability over many tiny files

That means a merge is allowed if the result is genuinely clearer.

## Smoke Test Boundary

A smoke test becomes appropriate after any of these:

- changing runtime-to-scene input routing control flow
- moving pending-move reconciliation out of `MoveAbility`
- merging `CellGrid.Runtime.cs` responsibilities into other grid files
- merging `Unit.SceneBinding.cs` into another unit file

Suggested smoke test once one of those happens:

1. Select a friendly unit
2. Preview move
3. Cancel preview with right click
4. Preview move again and confirm
5. Attack from pending state
6. End turn
7. Let AI move and attack
8. Kill a unit and confirm turn flow continues
9. Open pre-battle UI and verify roster/deployment still behave

## Publication Status

Based on the current private-workspace docs:

- publication audit docs were generated
- `Assets/TBS Framework` is gone from the active project
- active code builds without framework source

Still not done:

- final public packaging pass
- final exclusion sweep for private artifacts
- public README / license / repo polish

## Important Caution

Do not trust older docs that still describe:

- `BoardUnit`
- `BattleBoard`
- split scene/runtime cell identity resolvers
- older `Assets/Game/Runtime` / `Assets/Game/Scripts` layouts

Those are historical, not current.
