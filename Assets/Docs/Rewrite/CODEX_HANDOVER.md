# Codex Handover

Last updated: 2026-06-18
Workspace: `C:\Users\sjkim\Turn Based Strategy`

## Purpose

This document is a practical handoff for continuing Phase `11C` of the publishable runtime rewrite.

It is written for another coding agent to resume immediately without re-discovering:

- what the current code layout is
- what was already merged or renamed
- what still needs cleanup
- what is safe to change mechanically
- where the next real smoke-test boundaries are

This is not the full historical log. For older migration history, see:

- `Assets/CLEAN_RUNTIME_REWRITE_PLAN.md`
- `Assets/Docs/Rewrite/CURSOR_HANDOFF.md`

Important: `CURSOR_HANDOFF.md` contains useful history, but many paths and class names in it are now outdated because the code root was aggressively consolidated afterward.

## Current Rewrite Status

High-level status:

- Phase `11` is effectively complete in the active private workspace
- Phase `11B` is complete: active code is under one root, `Assets/Game/Code`
- Phase `11C` is in progress: complexity reduction, merge-first cleanup, clearer ownership
- Phase `12` publication audit has not started for real yet

Current guiding principle for `11C`:

- do not split files just because they are large
- merge migration-era fragments when they are really one subsystem
- only split when a merged file becomes harder to understand

The user explicitly prefers:

- coherent large files over lots of tiny migration shards
- meaningful names over transitional names
- behavior/ownership cleanup over cosmetic file shuffling

## Current Authoritative Code Layout

The active codebase is now under:

- `Assets/Game/Code/*`

Do not treat old paths like `Assets/Game/Runtime/*` or `Assets/Game/Scripts/*` as authoritative. Some tooling/editor tabs may still point there, but the live code now compiles from `Assets/Game/Code`.

## Major Naming Decisions Already Applied

The most important recent rename:

- gameplay-side unit remains `Unit`
- board/runtime-side unit is now `BoardUnit`
- board/runtime-side unit interface is now `IBoardUnit`

Reason:

- `BattleUnit` vs `Unit` was too ambiguous
- `BoardUnit` clearly means the board/runtime-side mirror/owner

This rename has already been applied across the active code root and project file.

## What Was Recently Done In Phase 11C

### 1. Restored battle-flow logs

Battle-flow and victory logs were restored in the scene/runtime authority points:

- `Assets/Game/Code/Grid/CellGrid.SceneBattle.cs`
- `Assets/Game/Code/Grid/BattleBoard.cs`

Current logs include:

- battle start
- player turn advance
- player turn start/end
- victory condition trigger with winner/defeated player ids

### 2. Collapsed old `Unit` runtime/scene bridge fragments

Merged into:

- `Assets/Game/Code/Units/Unit.BoardSync.cs`

Removed:

- `Unit.RuntimeMirror.cs`
- `Unit.SceneInterop.cs`

What `Unit.BoardSync.cs` now owns:

- runtime mirror push/pull
- board cell sync
- pending move sync
- runtime path conversion helpers
- occupancy helpers tied to the runtime mirror
- scene lookup helpers used by board-sync logic

### 3. Collapsed old `CellGrid` sync fragments

Merged into:

- `Assets/Game/Code/Grid/CellGrid.SceneBattle.cs`
- `Assets/Game/Code/Grid/CellGrid.BattleHost.cs`
- `Assets/Game/Code/Grid/CellGrid.SceneModel.cs`
- `Assets/Game/Code/Grid/CellGrid.RuntimeInput.cs`
- `Assets/Game/Code/Grid/CellGrid.RuntimeStateBridge.cs`
- `Assets/Game/Code/Grid/CellGrid.RuntimeBattleFlow.cs`

Removed older narrow fragments such as:

- `CellGrid.ActionSync.cs`
- `CellGrid.UnitSync.cs`
- `CellGrid.BattleBootstrap.cs`
- `CellGrid.BattleLifecycle.cs`
- `CellGrid.Lifecycle.cs`
- `CellGrid.GridInput.cs`
- `CellGrid.CellRegistry.cs`
- `CellGrid.Occupancy.cs`
- `CellGrid.UnitRegistry.cs`
- `CellGrid.LegacyGridBridge.cs`
- `CellGrid.SceneRegistry.cs`
- `CellGrid.RuntimeMirror.cs`

The goal was to replace a pile of migration shards with a smaller set of coherent owners.

## Current `CellGrid` File Responsibilities

This is the current intended shape.

### `CellGrid.cs`

Primary public surface and core shared state:

- public events
- battle/pre-battle flags
- common queries
- state transitions
- high-level routing toggles

### `CellGrid.BattleHost.cs`

Owns scene host lifecycle and battle host entrypoints:

- Unity lifecycle methods
- battle boot/start entrypoints
- scene event wiring
- non-runtime input dispatch helpers

### `CellGrid.SceneModel.cs`

Owns scene-authored board/unit model plumbing:

- registry cell conversion helpers
- scene unit registration
- scene cell anchor setup
- scene occupancy rebuild

### `CellGrid.SceneBattle.cs`

Owns scene-side battle execution and registry-driven battle loop:

- scene registry data
- turn-start/turn-end sync
- scene battle start
- battle outcome application
- battle action notifications
- scene cell/unit input callbacks

### `CellGrid.RuntimeInput.cs`

Owns runtime-routed human input:

- `IBattleBoardSceneInputCoordinator`
- runtime scene click/right-click/hover handlers
- runtime decision application back into legacy/gameplay state

### `CellGrid.RuntimeStateBridge.cs`

Owns runtime board/state mirroring:

- runtime board resolution
- state mirroring
- runtime collection mirroring
- runtime mirror refresh
- selected/pending runtime state construction

### `CellGrid.RuntimeBattleFlow.cs`

Owns runtime-routed battle flow:

- runtime battle outcome evaluation
- combat presentation begin/end routing
- post-combat recovery
- runtime end-turn processing
- acting-cell resolution for pending attacks/skills

### `CellGrid.CampaignSave.cs`

Owns campaign save reads/writes and deployment persistence.

### `CellGrid.Deployment.cs`

Owns pre-battle deployment slots, roster application, deployment save staging, and related helper logic.

### `CellGrid.DeferredDestroy.cs`

Owns deferred destroy queue behavior.

## Current `Unit` File Responsibilities

### `Unit.cs`

Still very large and still a major hotspot. It currently owns:

- stats
- combat logic
- movement execution hooks
- sprite/preset setup
- save identity
- progression
- inventory/skills/passives/buffs integration
- some runtime-aware movement/turn logic

This file still needs future cleanup, but it is not currently in the most fragile state compared to the just-split `CellGrid` runtime bridge.

### `Unit.BoardSync.cs`

Owns runtime-board sync and occupancy interop.

### `Unit.ExperienceFlow.cs`

Owns experience and level-up related flows.

### `Unit.LegacyUnitSurface.cs`

Contains legacy compatibility surface/events still needed by the current gameplay/UI layer.

## Build Status

Latest mechanical verification:

```powershell
dotnet build com.windy.srpg.game.csproj
```

Status:

- succeeds

Current warnings are mostly:

- obsolete `GetAllCells()` call sites
- deprecated Unity object-finding overloads
- unused legacy events in `Unit.LegacyUnitSurface.cs`

These warnings are known and not new regressions from the recent restructuring.

## What Needs To Be Done Next

### Immediate next step

Run a Unity smoke test after the latest `CellGrid.Runtime*` breakup.

This is the current real behavior boundary.

Reason:

- `CellGrid.RuntimeMirror.cs` was one of the densest behavior hubs
- it was split into three files
- compile success is not enough to prove scene/runtime input behavior still matches

Suggested smoke test focus:

1. pre-battle UI opens normally
2. select units still works
3. switch deployment still works
4. battle start still works
5. selecting a friendly unit still enters move selection
6. pending move preview still works
7. right click still cancels appropriately
8. attack preview and combat still work
9. AI still takes turns
10. battle end still resolves and logs correctly

### If smoke test passes

Continue `11C` cleanup with these priorities:

1. Replace obsolete `GetAllCells()` call sites with `GetAllBoardCells()`
2. Decide whether `CellGrid.SceneBattle.cs` and `CellGrid.BattleHost.cs` should stay separate or merge further
3. Review whether `CellGrid.SceneModel.cs` should absorb more small scene/registry helpers
4. Audit `Unit.cs` for merge/split opportunities based on coherent subsystem ownership, not file size alone
5. Reduce leftover compatibility-only members if clearly no longer needed

### If smoke test fails

First inspect these files:

- `Assets/Game/Code/Grid/CellGrid.RuntimeInput.cs`
- `Assets/Game/Code/Grid/CellGrid.RuntimeStateBridge.cs`
- `Assets/Game/Code/Grid/CellGrid.RuntimeBattleFlow.cs`
- `Assets/Game/Code/Grid/CellGrid.SceneBattle.cs`
- `Assets/Game/Code/Units/Unit.BoardSync.cs`

Most likely failure modes would be:

- scene input no longer reaching the same runtime path
- state mirroring not reconstructing the correct board state
- post-combat or pending-move routing no longer returning to the right gameplay state
- runtime board collection dirty flag not refreshed at the right time

## Specific Cleanup Opportunities

These are good next-step cleanup candidates once the current smoke test passes.

### Candidate A: remove old `GetAllCells()` usages

There are still obsolete calls to:

- `CellGrid.GetAllCells()`

They should be migrated to:

- `CellGrid.GetAllBoardCells()`

This is a good low-risk cleanup slice.

### Candidate B: review `Unit.LegacyUnitSurface.cs`

Known warnings:

- `Unit.UnitAttacked` unused
- `Unit.UnitMoved` unused

Need to verify whether they are truly dead before deletion.

### Candidate C: simplify runtime decision wrappers

There are still several tiny runtime decision methods like:

- `ProcessRuntimeWaitingStateUnitClick`
- `ProcessRuntimeSelectedStateUnitClick`
- `ProcessRuntimeSelectedStateCellClick`
- `ProcessRuntimePendingMoveWait`

These may be fine as-is, but after smoke test it may be worth checking whether some of them should be collapsed into clearer higher-level helpers.

### Candidate D: publication audit preparation

Phase `12` has not meaningfully started.

Eventually need:

- contamination search
- final repo-boundary verification
- check for outdated doc references to old paths
- confirm no hidden private dependency remains

## Important Cautions

### 1. Do not trust old path references blindly

Many older docs still mention:

- `Assets/Game/Runtime/*`
- `Assets/Game/Scripts/*`

The live code is now under:

- `Assets/Game/Code/*`

### 2. Do not re-fragment the code just because files are large

User preference is now explicitly:

- merge related things first
- only split when the split makes semantic sense

### 3. Do not treat compile-green as behavior-green

Recent changes crossed real behavior boundaries in:

- runtime input routing
- runtime state mirroring
- runtime battle flow routing

Unity smoke testing matters here.

### 4. Keep names intuitive

The user cares about names being understandable at a glance.

Example already applied:

- `BattleUnit` -> `BoardUnit`

If a name is ambiguous, rename it if the meaning becomes clearer.

## Recommended Cursor Starting Point

If taking over immediately, the suggested order is:

1. read this file
2. read `Assets/CLEAN_RUNTIME_REWRITE_PLAN.md`
3. run a Unity smoke test on the current scene
4. if scene is stable, do a low-risk cleanup slice:
   - obsolete `GetAllCells()` replacements
5. then continue `11C` using merge-first logic
6. stop only at the next genuine behavior boundary

## Quick File Index

Most relevant current files:

- `Assets/Game/Code/Grid/CellGrid.cs`
- `Assets/Game/Code/Grid/CellGrid.BattleHost.cs`
- `Assets/Game/Code/Grid/CellGrid.SceneModel.cs`
- `Assets/Game/Code/Grid/CellGrid.SceneBattle.cs`
- `Assets/Game/Code/Grid/CellGrid.RuntimeInput.cs`
- `Assets/Game/Code/Grid/CellGrid.RuntimeStateBridge.cs`
- `Assets/Game/Code/Grid/CellGrid.RuntimeBattleFlow.cs`
- `Assets/Game/Code/Grid/BattleBoard.cs`
- `Assets/Game/Code/Units/Unit.cs`
- `Assets/Game/Code/Units/Unit.BoardSync.cs`
- `Assets/Game/Code/Units/BoardUnit.cs`
- `Assets/Game/Code/Abilities/MoveAbility.cs`
- `Assets/Game/Code/Players/AiPlayer.cs`
- `Assets/CLEAN_RUNTIME_REWRITE_PLAN.md`

## Summary

The rewrite is no longer mainly about removing framework references. That part is basically done in the active private workspace.

The active task is now:

- simplify the project-owned runtime/gameplay architecture
- reduce migration-shaped complexity
- preserve behavior while making the codebase readable enough to publish and maintain

At the moment, the biggest freshly changed area is the split of the old runtime mirror bridge into:

- runtime input
- runtime state bridge
- runtime battle flow

That is the next place to validate before pushing further.
