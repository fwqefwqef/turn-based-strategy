# Cursor Handoff — Framework Rewrite Work Log

Last updated: 2026-06-16  
Workspace: `C:\Users\sjkim\Turn Based Strategy`  
Prior chat transcript: `C:\Users\sjkim\.cursor\projects\c-Users-sjkim-Turn-Based-Strategy\agent-transcripts\235f9eff-6e30-49c4-8ef9-729c8c24e36a\235f9eff-6e30-49c4-8ef9-729c8c24e36a.jsonl`

This document summarizes work done inside Cursor toward decoupling the Unity SRPG from the proprietary `TBS Framework`. It is written for the next agent (Codex) to continue without re-discovering context.

---

## Goal

Incrementally transfer gameplay authority from the framework (`Assets/TBS Framework`, `CustomCellGrid`, `CustomUnit`, framework states/abilities/AI) to the project-owned runtime (`Assets/Game/Runtime`, namespace `Windy.Srpg.Runtime.*`, assembly `com.windy.srpg.runtime`).

The long-term target is a fully self-contained, publishable project per `Assets/CLEAN_RUNTIME_REWRITE_PLAN.md`. The **current near-term strategy** is **gated cutover + shadow harness**, not a big-bang flip.

---

## Strategy (current, post-rollback)

After a failed earlier attempt (see below), the approach was revised:

1. **Keep the framework authoritative** for now (input, turn loop, abilities, AI, win/lose).
2. **Prove runtime parity** on the live board before handing over any authority.
3. **Flip ownership in small slices**, each behind a dev toggle and smoke-tested.
4. **Do not collapse cell identity yet.** Prefabs already carry a runtime cell mirror (`RuntimeSampleSquareCell` alongside framework `SampleSquare`). Build runtime ownership of turn loop / board states / units first; defer cell-layer collapse.
5. **Use a shadow harness** toward the eventual input/turn-loop flip: run real runtime state logic non-authoritatively, compare decisions to the framework, only flip when click-for-click parity is proven.

---

## Prior Failed Attempt (do not repeat)

An earlier Cursor session (Composer 2.5) attempted a cell-layer bridge collapse:

- Added `GameSquareCell`, `LegacySquareCellBridge`, `CellInterop`
- Changed `DeploymentSlot.cell` typing

**Result:** Scene broke — units could not move (no blue range), clicking tiles deselected units. User restored a backup.

**User also reported:** "Player 1 (enemy) wins as soon as the game starts" during that attempt (occupancy/deployment regression).

**Action taken:** User rolled back. Those bridge files were **deleted** and are **not** in the repo:

- `Assets/Game/Scripts/Grid/GameSquareCell.cs` (deleted)
- `Assets/Game/Scripts/Grid/LegacySquareCellBridge.cs` (deleted)
- `Assets/Game/Scripts/Grid/CellInterop.cs` (deleted)

**Lesson:** Do not force cell identity collapse or change serialized cell references without a gated plan and smoke test. The existing prefab mirror pattern is intentional.

---

## Key Discovery That Changed The Plan

`Assets/Scenes/Square.prefab` and `Assets/Scenes/Wall.prefab` already contain **both**:

- Framework cell: `SampleSquare` (TBS Framework)
- Runtime cell mirror: `RuntimeSampleSquareCell` (`Assets/Scenes/RuntimeSampleSquareCell.cs` — trivial subclass of `SquareBoardCell`)

So the scene already has dual cell components on the same GameObjects. The runtime mirror sync path is:

```csharp
// CustomCellGrid.RuntimeMirror.cs / CustomUnit.RuntimeMirror.cs
BoardCell runtimeCell = cell.GetComponent<BoardCell>();
```

Framework ↔ runtime linking is **same GameObject, different components**, not a bridge type.

---

## Work Completed In Cursor

### Gate A — Runtime reachable parity diagnostic (read-only)

**Purpose:** Compare framework vs runtime reachable destination sets on the live board without mutating game state.

**Created:**

- `Assets/Game/Scripts/Diagnostics/RuntimeParityDiagnostics.cs`
  - `LogReachableParity` (default `true`)
  - `CompareReachable(frameworkUnit, frameworkCells, frameworkReachable)`
  - Logs `[RuntimeParity] ... RESULT: MATCH|MISMATCH`

**Modified:**

- `Assets/Game/Scripts/Abilities/CustomMoveAbility.cs`
  - Calls `RuntimeParityDiagnostics.CompareReachable(...)` after framework computes `availableDestinations` in `OnAbilitySelected`.

**Initial mismatches found:**

1. Runtime included the unit's **origin cell** in reachable set; framework did not.
2. Runtime reused a **stale path cache** across selections.
3. Minor occupancy semantics differences (surfaced during investigation).

---

### Gate B — Reachable parity fixes (runtime model aligned to framework)

**Modified:**

- `Assets/Game/Runtime/Units/BattleUnit.cs`
  - `GetAvailableDestinations`: skip `cell == CurrentCell` (exclude origin), matching framework behavior.

- `Assets/Game/Scripts/Diagnostics/RuntimeParityDiagnostics.cs`
  - Force `runtimeUnit.CachePaths(runtimeCells)` before comparison so runtime doesn't use stale cache.

**Result:** User confirmed all subsequent parity logs show **`RESULT: MATCH`** for both friendly and enemy units across selections, MP budgets, and post-combat states.

---

### Gate C — First ownership flip: runtime-driven human movement animation

**Purpose:** Runtime `BattleUnit` drives the **visual walk only** (preview + commit animation). Framework retains all state commits (occupancy, movement points, cell assignment, cancellation).

**Modified:**

- `Assets/Game/Scripts/Grid/CustomCellGrid.cs`
  - Added serialized dev toggle: `useRuntimeMovementExecution` (default **off**)
  - Public getter: `UseRuntimeMovementExecution`

- `Assets/Game/Runtime/Units/BattleUnit.cs`
  - Added `AnimateAlongPathVisual(...)` — **pure-visual** local-space path animation
  - Supports cancellation via `Func<bool> isCancelled` and camera follow via `Action<Vector3> onFrame`
  - Does **not** mutate occupancy, cell, or movement points

- `Assets/Game/Scripts/Units/CustomUnit.SceneInterop.cs`
  - Added `TryBuildRuntimeMovementPath(IList<Cell> frameworkPath, out List<BoardCell> orderedRuntimePath)`
  - Converts framework path (destination-first) to origin-to-destination runtime `BoardCell` list
  - Added `using System.Collections.Generic;` (fixed CS0246 compile error)

- `Assets/Game/Scripts/Units/CustomUnit.cs`
  - `Move(...)`: when toggle on + human turn + valid runtime path → `AnimateAlongPathVisual`
  - `PreviewMovementAnimation(...)`: same conditional for human preview walk, with framework cancellation predicate and camera callback
  - Falls back to framework animation when toggle off or preconditions fail

**Important design note:** Human movement primarily uses `PreviewMovementAnimation` (pending-move preview system), not `Move` directly. Both paths were wired.

---

### Gate D — Movement flip smoke test

**User result:** "Toggle off and toggle on behavior is the same. I see no issues."

Toggle location: `CustomCellGrid` component in scene — field `useRuntimeMovementExecution`.

---

### Shadow Harness — Selection parity (in progress, awaiting smoke test)

**Context:** User chose to pursue the **input/turn-loop flip** eventually, but agreed to the **shadow harness** path because runtime board states/AI are skeletal. An immediate input flip would break the game (runtime `BoardStateUnitSelected` has no cell-click movement; `BoardStateAiTurn` is empty; no runtime ability/AI/end-turn logic).

**Approach:** Framework stays authoritative. On each relevant framework input event, evaluate what the runtime **would** decide using real runtime state classes, log parity, change nothing visible.

**Modified — runtime side:**

- `Assets/Game/Runtime/Board/States/BoardState.cs`
  - Added virtual `SelectedUnit` property (default `null`) for shadow readback

- `Assets/Game/Runtime/Board/States/BoardStateUnitSelected.cs`
  - Overrides `SelectedUnit => selectedUnit`

- `Assets/Game/Runtime/Board/BattleBoard.cs`
  - Added `ShadowMode` property
  - `SetState`: suppresses `StateChanged` event when `ShadowMode` is true
  - Added `ShadowEvaluateUnitClickFromWaiting(BattleUnit clickedUnit)`:
    - Saves/restores current state
    - Sets `ShadowMode = true`
    - Instantiates `BoardStateWaitingForInput`, calls `OnUnitClicked`
    - Returns `currentState.SelectedUnit`
    - Restores everything; no commit

- `Assets/Game/Runtime/Units/BattleUnit.cs`
  - `Select()` / `Deselect()`: no-op when `Board.ShadowMode` is true (prevents visual/event side effects during shadow eval)

**Modified — framework bridge side:**

- `Assets/Game/Scripts/Diagnostics/RuntimeParityDiagnostics.cs`
  - Added `LogSelectionParity` (default `true`)
  - Added `CompareSelection(...)` → logs `[RuntimeShadow] Selection ... RESULT: MATCH|MISMATCH`

- `Assets/Game/Scripts/Grid/CustomCellGrid.RuntimeMirror.cs`
  - Added `ShadowCompareSelection(clickedUnit, frameworkSelectedUnit)`
  - Calls `SyncRuntimeMirrorNow()` first, then `runtimeBoard.ShadowEvaluateUnitClickFromWaiting(...)`, then diagnostic compare

- `Assets/Game/Scripts/Grid/States/CustomCellGridStateWaitingForInput.cs`
  - Before framework selection logic, calls `_cellGrid.ShadowCompareSelection(...)`

**Smoke test requested but not yet reported by user:**

During human turn, click units and watch Console for `[RuntimeShadow] Selection`:

| Action | Expected |
|--------|----------|
| Click own un-acted unit | Both select same unit → MATCH |
| Click enemy unit | Both `<none>` → MATCH |
| Re-click selected unit | MATCH (framework may deselect; shadow only covers waiting-for-input click for now) |
| Click finished unit | Both `<none>` → MATCH |

Nothing visible should change — shadow is observe-only.

---

## Existing Infrastructure (pre-Cursor, still in use)

These were already present and are relied on by the work above:

| Area | Location | Role |
|------|----------|------|
| Runtime board | `Assets/Game/Runtime/Board/BattleBoard.cs` | Turn-loop engine; `sceneInputEnabled` defaults **off** (dormant) |
| Runtime units | `Assets/Game/Runtime/Units/BattleUnit.cs` | Full movement/pathfinding model |
| Runtime cells | `Assets/Game/Runtime/Board/SquareBoardCell.cs`, prefab `RuntimeSampleSquareCell` | Board cell mirror on prefabs |
| Framework mirror sync | `CustomCellGrid.RuntimeMirror.cs`, `CustomUnit.RuntimeMirror.cs` | Push framework state → runtime (cell, MP, turn state, player index, battle started) |
| Runtime board states | `Assets/Game/Runtime/Board/States/*` | **Skeletal** — selection only, no movement/abilities/AI |
| Runtime players | `HumanBattlePlayerController`, `AiBattlePlayerController` | **Stubs** — no AI decision logic |
| Turn flow | `RoundRobinBattleFlow.cs` | Runtime turn resolution exists but is not driving the live game |

---

## Files Touched In Cursor (summary)

### Created

- `Assets/Game/Scripts/Diagnostics/RuntimeParityDiagnostics.cs`

### Modified

- `Assets/Game/Scripts/Abilities/CustomMoveAbility.cs`
- `Assets/Game/Scripts/Grid/CustomCellGrid.cs`
- `Assets/Game/Scripts/Grid/CustomCellGrid.RuntimeMirror.cs`
- `Assets/Game/Scripts/Grid/States/CustomCellGridStateWaitingForInput.cs`
- `Assets/Game/Scripts/Units/CustomUnit.cs`
- `Assets/Game/Scripts/Units/CustomUnit.SceneInterop.cs`
- `Assets/Game/Runtime/Units/BattleUnit.cs`
- `Assets/Game/Runtime/Board/BattleBoard.cs`
- `Assets/Game/Runtime/Board/States/BoardState.cs`
- `Assets/Game/Runtime/Board/States/BoardStateUnitSelected.cs`

### Deleted (during plan reorder, not re-added)

- `Assets/Game/Scripts/Grid/GameSquareCell.cs`
- `Assets/Game/Scripts/Grid/LegacySquareCellBridge.cs`
- `Assets/Game/Scripts/Grid/CellInterop.cs`

---

## Dev Toggles And Diagnostic Flags

| Flag | Location | Default | Purpose |
|------|----------|---------|---------|
| `useRuntimeMovementExecution` | `CustomCellGrid` (Inspector) | off | Runtime animates human movement walk |
| `LogReachableParity` | `RuntimeParityDiagnostics` | true | `[RuntimeParity]` reachable logs |
| `LogSelectionParity` | `RuntimeParityDiagnostics` | true | `[RuntimeShadow]` selection logs |

Consider gating selection logs behind the same Inspector toggle if Console noise is a problem.

---

## What Is NOT Done Yet

1. **Selection shadow smoke test** — implemented, user has not yet reported results.
2. **Shadow harness for other input behaviors:**
   - Deselect / right-click
   - Cell click → move (reachable highlighting + path preview + commit)
   - Ability invocation (attack, skill, item, trade, wait)
   - End turn / turn transitions
   - AI decisions
   - Win/lose evaluation
3. **Runtime-owned reachable/path highlighting** — deferred; would validate runtime cell rendering via `BoardCell.ApplyHighlight`.
4. **Runtime-owned move commit** — occupancy/cell/MP authority transfer; higher risk.
5. **Input/turn-loop authority flip** — enable `BattleBoard.sceneInputEnabled`, disable framework input loop. **Blocked** until shadow parity is proven across all behaviors.
6. **Cell identity collapse** — explicitly deferred; prefab dual-component mirror stays.
7. **Phases 10–13** of `CLEAN_RUNTIME_REWRITE_PLAN.md` (full scene rewire, namespace rename, publication audit) — not started.

---

## Recommended Next Steps For Codex

### Immediate

1. Confirm project compiles cleanly.
2. Run **selection shadow smoke test** (see table above). Fix any MISMATCH before proceeding.
3. If MATCH across cases, extend shadow harness to **deselect/right-click** and **cell-click while unit selected** (compare framework `CustomUnitSelectedState` + `CustomMoveAbility` decisions vs runtime).

### Short term (shadow buildout order)

1. Selection parity ✅ (implemented, verify)
2. Deselect / right-click parity
3. Move-on-click parity (reachable set already proven; add decision parity for destination choice)
4. Ability menu / action parity
5. End-turn / turn-loop parity
6. AI parity (runtime `BoardStateAiTurn` + `AiBattlePlayerController` need real logic)
7. Win/lose parity

### Medium term (ownership flips, each gated + smoke-tested)

1. Runtime-owned reachable/path highlighting (cosmetic, low risk)
2. Runtime-owned move commit (occupancy/MP — use parity diagnostics heavily)
3. Full input/turn-loop flip (only after shadow proves click-for-click parity)

### Do not do without explicit user approval

- Force-push, hard reset, or amend commits
- Big-bang enable `BattleBoard.sceneInputEnabled` without shadow parity
- Re-introduce `LegacySquareCellBridge` / cell identity collapse
- Change `DeploymentSlot` serialized cell references without a migration plan
- Delete or rename framework files (still needed for live game)

---

## How To Smoke Test (quick reference)

### Movement animation flip

1. Open battle scene with `CustomCellGrid`.
2. Toggle `useRuntimeMovementExecution` off → move units, verify preview + commit + cancel.
3. Toggle on → repeat; behavior should be identical.
4. User confirmed: **pass**.

### Reachable parity (automatic while playing)

- Watch Console for `[RuntimeParity] ... RESULT: MATCH` when selecting units for movement.
- Should appear on every move ability selection.

### Selection shadow (needs verification)

- Play human turn, click friendly/enemy/finished units.
- Watch for `[RuntimeShadow] Selection ... RESULT: MATCH`.
- Game behavior must be unchanged (shadow is non-authoritative).

---

## Architecture Notes For Continuity

### Dual-runtime bridge pattern

```
Framework (authoritative)          Runtime (mirror → eventual owner)
─────────────────────────          ─────────────────────────────────
CustomCellGrid                     BattleBoard (same GameObject)
CustomUnit                         BattleUnit (same GameObject)
SampleSquare (Cell)                RuntimeSampleSquareCell (BoardCell)
CustomCellGridState*               BoardState* (skeletal)
CustomMoveAbility, etc.            BattleAction* (partial)
```

Sync direction today: **framework → runtime** via `SyncMirroredRuntimeNow()` / lifecycle hooks in `CustomCellGrid.RuntimeMirror.cs` and `CustomUnit.RuntimeMirror.cs`.

### Movement path direction

Framework paths are often **destination-first**. Runtime animation expects **origin-to-destination**. `TryBuildRuntimeMovementPath` handles this conversion.

### Why shadow mode suppresses side effects

If shadow evaluation called real `Select()`/`Deselect()`, it would mutate runtime unit turn states and fire events, corrupting the mirror. `BattleBoard.ShadowMode` gates those side effects so the runtime stays a faithful read-only decision engine during comparison.

### Input double-handling risk

Both framework `Unit.OnMouseDown` and runtime `BattleUnit` click handlers exist on the same prefabs. Enabling `sceneInputEnabled` without disabling framework input will cause **double handling**. Any authority flip must explicitly gate one side off.

---

## Reference Documents

| Document | Path |
|----------|------|
| Project summary | `Assets/SUMMARY.md` |
| Full rewrite plan (13 phases) | `Assets/CLEAN_RUNTIME_REWRITE_PLAN.md` |
| Runtime acceptance criteria | `Assets/Docs/Rewrite/PUBLIC_RUNTIME_ACCEPTANCE.md` |
| Runtime spec | `Assets/Docs/Rewrite/PUBLIC_RUNTIME_SPEC.md` |
| Publication boundary | `Assets/Docs/Publication/PUBLIC_REPO_BOUNDARY.md` |

---

## User Preferences (from Cursor rules)

- **Do not commit** unless explicitly asked.
- **Do not push** unless explicitly asked.
- **Minimize scope** — smallest correct diff; match existing conventions.
- **Stop for smoke tests** at gated cutover boundaries; user validates in Unity.
- User rolled back once due to broken movement — prefer incremental, reversible changes.

---

## Current Status At Handoff

| Item | Status |
|------|--------|
| Compiles cleanly | User confirmed (post IList fix) |
| Reachable parity | **MATCH** (proven) |
| Movement animation flip | **Implemented + smoke-tested OK** |
| Selection shadow harness | **Implemented, smoke test pending** |
| Input/turn-loop flip | **Not started** (blocked on shadow buildout) |
| Cell collapse | **Explicitly deferred** |

**Next action for Codex:** Run selection shadow smoke test, report MATCH/MISMATCH, then continue shadow buildout per recommended order above.
