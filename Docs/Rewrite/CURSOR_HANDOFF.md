# Cursor Handoff — Framework Rewrite Work Log

Last updated: 2026-06-17 (Phase 10 smoke-tested; Phase 11A asmdef + highlighter rename)  
Workspace: `C:\Users\sjkim\Turn Based Strategy`  
Prior chat transcript: `C:\Users\sjkim\.cursor\projects\c-Users-sjkim-Turn-Based-Strategy\agent-transcripts\235f9eff-6e30-49c4-8ef9-729c8c24e36a\235f9eff-6e30-49c4-8ef9-729c8c24e36a.jsonl`

This document summarizes work done inside Cursor toward decoupling the Unity SRPG from the proprietary `TBS Framework`. It is written for the next agent (Codex) to continue without re-discovering context.

---

## Goal

Incrementally transfer gameplay authority from the framework (`Assets/TBS Framework`, `CellGrid`, `Unit`, framework states/abilities/AI) to the project-owned runtime (`Assets/Game/Runtime`, namespace `Windy.Srpg.Runtime.*`, assembly `com.windy.srpg.runtime`).

The long-term target is a fully self-contained, publishable project per `Assets/CLEAN_RUNTIME_REWRITE_PLAN.md`. The **current near-term strategy** is **gated cutover + shadow harness**, not a big-bang flip.

---

## Strategy (current, post-rollback)

After a failed earlier attempt (see below), the approach was revised:

1. **Keep the framework authoritative** for now (input, turn loop, abilities, AI, win/lose).
2. **Prove runtime parity** on the live board before handing over any authority.
3. **Flip ownership in small slices**, each behind a dev toggle and smoke-tested.
4. ~~**Do not collapse cell identity yet.**~~ **Phase 10 done:** prefabs use single `BattleSquareCell` + baked `FrameworkSquareAnchor`; `SampleSquare` / `CustomSquare` / `RuntimeSampleSquareCell` removed from game prefabs and scenes assembly.
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

## Key Discovery That Changed The Plan (historical → superseded by Phase 10)

Previously `Assets/Scenes/Square.prefab` and `Assets/Scenes/Wall.prefab` carried **both** framework `SampleSquare` and runtime `RuntimeSampleSquareCell` on the same GameObject.

**Phase 10 (2026-06-17):** collapsed to a single tile host:

```
BattleSquareCell (SquareBoardCell / IBattleCell)  ← gameplay + input routing
FrameworkSquareAnchor (Square / Cell)             ← registry + pathfinding token (baked on prefab)
RuntimeSampleSquareHighlighter                    ← overlays + skill area borders (renamed BattleSquareCellHighlighter)
```

Resolution helpers on `CellGrid`:

- `ResolveBattleSquareFromRegistryCell(Cell)` → `GetComponent<BattleSquareCell>()`
- `ResolveRegistryCellFromBattleCell(IBattleCell)` → `BattleSquareCell.LegacyCell` (anchor)

Mouse input: `BattleSquareCell` gates like old `CustomSquare` — when `ShouldSuppressFrameworkSceneInput`, runtime `BoardCell` click; else anchor `RaiseCellClicked`.

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

- `Assets/Game/Scripts/Abilities/MoveAbility.cs`
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

- `Assets/Game/Scripts/Grid/CellGrid.cs`
  - Added serialized dev toggle: `useRuntimeMovementExecution` (default **off**)
  - Public getter: `UseRuntimeMovementExecution`

- `Assets/Game/Runtime/Units/BattleUnit.cs`
  - Added `AnimateAlongPathVisual(...)` — **pure-visual** local-space path animation
  - Supports cancellation via `Func<bool> isCancelled` and camera follow via `Action<Vector3> onFrame`
  - Does **not** mutate occupancy, cell, or movement points

- `Assets/Game/Scripts/Units/Unit.SceneInterop.cs`
  - Added `TryBuildRuntimeMovementPath(IList<Cell> frameworkPath, out List<BoardCell> orderedRuntimePath)`
  - Converts framework path (destination-first) to origin-to-destination runtime `BoardCell` list
  - Added `using System.Collections.Generic;` (fixed CS0246 compile error)

- `Assets/Game/Scripts/Units/Unit.cs`
  - `Move(...)`: when toggle on + human turn + valid runtime path → `AnimateAlongPathVisual`
  - `PreviewMovementAnimation(...)`: same conditional for human preview walk, with framework cancellation predicate and camera callback
  - Falls back to framework animation when toggle off or preconditions fail

**Important design note:** Human movement primarily uses `PreviewMovementAnimation` (pending-move preview system), not `Move` directly. Both paths were wired.

---

### Gate D — Movement flip smoke test

**User result:** "Toggle off and toggle on behavior is the same. I see no issues."

Toggle location: `CellGrid` component in scene — field `useRuntimeMovementExecution`.

---

### Codex backup commit (`8a1314d`) — Movement ability runtime expansion

Codex extended the movement migration beyond animation-only into **runtime board states + pending-move model + human input routing**.

**Runtime additions:**

- `BoardStateUnitMovePendingConfirm` — pending destination, `BeginPendingMove` / `BeginPendingMoveInPlace` on enter, cancel/right-click/wait handling
- `BattleUnit` pending move API — `BeginPendingMove`, `BeginPendingMoveInPlace`, `ConfirmPendingMove`, `CancelPendingMove`, `HasPendingMove`, runtime snapshots for shadow eval
- `BattleBoard.ProcessUnitClick/ProcessCellClick/ProcessRightClick/ConfirmPendingMoveWait` — authoritative runtime input dispatch (when routed)
- `CellGrid.ShouldRouteHumanMovementThroughRuntime` — `useRuntimeMovementExecution && IsHumanTurn`
- `CellGrid.ApplyLegacyStateFromRuntime` — applies framework state without re-mirroring runtime (prevents loops)
- `CellGrid.RuntimeMirror` — `ProcessRuntime*` methods, legacy↔runtime state builders, shadow compare helpers (parity logging infrastructure retained)

**Framework bridge changes:**

- `CellGridStateWaitingForInput` / `UnitSelectedState` — when toggle **on**, runtime decides selection/cell-click/right-click; framework UI/abilities follow via `ApplyLegacyStateFromRuntime`
- `MoveAbility` — pending wait/right-click consult runtime when toggle on
- `Unit.ConfirmPendingMove` — when toggle on, commits via `TryCommitPendingMoveViaRuntime` (runtime owns occupancy/MP, framework synced back)
- `Unit.Move` — when toggle on, can commit direct moves via runtime path authority

**Shadow parity (user-reported):**

- Right-click deselect from selected unit: **`[RuntimeShadow] RightClick ... RESULT: MATCH`**

**Cursor follow-up (2026-06-17):**

Codex had wired runtime routing **unconditionally** (even with toggle off). Restored gated cutover:

- Toggle **off** → legacy framework input + shadow logging (same as pre-Codex behavior)
- Toggle **on** → runtime routes human selection/move/pending confirm
- `CancelPendingMove` / `BeginPendingMoveInPlace` now sync runtime `BattleUnit` pending state
- `TryUseRuntimePathAuthority` gated behind same toggle (was applying runtime commits to AI/direct moves always)

**Cursor slice — runtime-owned reachable/path highlighting (2026-06-17):**

When `ShouldRouteHumanMovementThroughRuntime` is true, `MoveAbility` draws blue reachable tiles and hover path via `BoardCell.ApplyHighlight` / `BattleSquareCellHighlighter` instead of framework cell mark methods. `CellGrid.ClearAllCellHighlights()` clears both layers on state enter.

**Cursor slice — pending-move + routing parity on toggle-ON path (2026-06-17):**

- `ShadowComparePendingMoveWait/RightClick` wired on wait and cancel-restore (both toggles)
- `CompareRuntimeStateDecision` logs shadow-eval vs live `ProcessRuntime*` on toggle-ON routing (selection, cell click, right-click, pending wait/right-click)
- Flag: `LogRuntimeRoutingParity` (default true)

**Cursor slice — pending-state attack routing (2026-06-17):**

- `GetActingCellForPendingActions` — when toggle ON, attack/skill queries use `ResolveRuntimeActingCell` (runtime `BattleUnit.PreviewCell` → legacy `Cell`); toggle OFF uses `PreviewCell`
- Attack menu visibility, attackable enemy list, weapon legality, attack preview cells, and action menu anchor all use acting cell
- Runtime attack-range highlighting via `CellHighlightKind.Attack` when toggle ON (framework `MarkAsAttackPreview` when OFF)
- `ComparePendingAttackables` + `LogPendingAttackParity` log framework vs runtime attackable sets when action menu opens and attack targeting begins

**Smoke test (toggle ON):** move adjacent to enemy → action menu shows Attack → enter attack targeting → red preview cells + defending enemy highlight → Console `[RuntimeShadow] Pending attackables … RESULT: MATCH`

**Still framework-only:** ~~skill/heal/item/trade pending actions~~ **implemented** (acting cell wired; item menu uses acting-cell anchor; execution still framework-owned)

**Cursor slice — pending-state skill/heal/item/trade acting cell (2026-06-17):**

- Skill menu visibility, valid targets, range preview, area projection, weapon legality, and skill preview UI all use `GetActingCellForPendingActions`
- Trade menu visibility, adjacent partner query, trade-range highlights use acting cell
- Inventory menu anchor uses acting cell
- Runtime highlights when toggle ON: skill range (`Attack`/`Support` by targeting mode), trade adjacent tiles (`Support`)
- Cancel skill/trade targeting on acting-cell click (same as attack)

**Smoke test (toggle ON):** move to preview tile → verify Heal/Skill/Item/Trade buttons match pre-migration when in range → enter each targeting mode → cancel via acting-cell click

---

### Shadow Harness — Selection / move parity

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

- `Assets/Game/Scripts/Grid/CellGrid.RuntimeMirror.cs`
  - Added `ShadowCompareSelection(clickedUnit, frameworkSelectedUnit)`
  - Calls `SyncRuntimeMirrorNow()` first, then `runtimeBoard.ShadowEvaluateUnitClickFromWaiting(...)`, then diagnostic compare

- `Assets/Game/Scripts/Grid/States/CellGridStateWaitingForInput.cs`
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
| Framework mirror sync | `CellGrid.RuntimeMirror.cs`, `Unit.RuntimeMirror.cs` | Push framework state → runtime (cell, MP, turn state, player index, battle started) |
| Runtime board states | `Assets/Game/Runtime/Board/States/*` | **Skeletal** — selection only, no movement/abilities/AI |
| Runtime players | `HumanBattlePlayerController`, `AiBattlePlayerController` | **Stubs** — no AI decision logic |
| Turn flow | `RoundRobinBattleFlow.cs` | Runtime turn resolution exists but is not driving the live game |

---

## Files Touched In Cursor (summary)

### Created

- `Assets/Game/Scripts/Diagnostics/RuntimeParityDiagnostics.cs`

### Modified

- `Assets/Game/Scripts/Abilities/MoveAbility.cs`
- `Assets/Game/Scripts/Grid/CellGrid.cs`
- `Assets/Game/Scripts/Grid/CellGrid.RuntimeMirror.cs`
- `Assets/Game/Scripts/Grid/States/CellGridStateWaitingForInput.cs`
- `Assets/Game/Scripts/Units/Unit.cs`
- `Assets/Game/Scripts/Units/Unit.SceneInterop.cs`
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
| ~~`useRuntimeMovementExecution`~~ | ~~`CellGrid` (Inspector)~~ | **removed** | Runtime routing is now always on |
| `ShouldRouteHumanMovementThroughRuntime` | `CellGrid` (code) | derived | `IsHumanTurn` |
| `LogReachableParity` | `RuntimeParityDiagnostics` | true | `[RuntimeParity]` reachable logs |
| `LogSelectionParity` | `RuntimeParityDiagnostics` | true | `[RuntimeShadow]` selection logs (toggle off path) |
| `LogRightClickParity` | `RuntimeParityDiagnostics` | true | `[RuntimeShadow]` right-click logs (toggle off path) |
| `LogSelectedMoveParity` | `RuntimeParityDiagnostics` | true | `[RuntimeShadow]` selected-state unit/cell click logs (toggle off path) |
| `LogPendingMoveParity` | `RuntimeParityDiagnostics` | true | `[RuntimeShadow]` pending wait/right-click logs |
| `LogPendingAttackParity` | `RuntimeParityDiagnostics` | true | `[RuntimeShadow]` pending attackable enemy parity |
| `LogRuntimeRoutingParity` | `RuntimeParityDiagnostics` | true | `[RuntimeShadow]` live routing decisions (move/end-turn) |
| `LogTurnLoopParity` | `RuntimeParityDiagnostics` | true | `[RuntimeShadow]` end-turn plan + post-turn player sync |
| `LogMirroredBoardStateParity` | `RuntimeParityDiagnostics` | true | `[RuntimeShadow]` framework vs runtime board state after mirror/bridged input |

---

## What Is NOT Done Yet

1. ~~**Full smoke test with toggle ON** for movement loop~~ — **Done** (user confirmed MATCH).
2. ~~**Shadow logging on toggle ON path**~~ — wired for selection, move, pending wait/right-click, routing, reachable.
3. ~~**Runtime-owned reachable/path highlighting**~~ — **implemented**.
4. **Pending-state attack routing** — **implemented** (acting cell + attack highlights + parity); skill/heal/item/trade still use `PreviewCell`.
5. **End turn runtime routing (human, toggle ON)** — **implemented** (user smoke-tested OK).
6. **Combat post-game / AI counterattack recovery** — **implemented** (`EnterPostCombatGridState`, AI attack wait on player host, game-over during AI turn).
7. **Battle outcome shadow parity** — **done** (user smoke-tested MATCH incl. `Battle ended`).
8. **Win/lose runtime routing (toggle ON)** — **done** (user smoke-tested: shadow + routing MATCH, `TryApplyBattleOutcome` on win).
9. **Framework scene input suppression (toggle ON)** — **implemented + smoke-tested OK** (runtime clicks, framework mouse blocked).
10. **Runtime hover bridge (toggle ON)** — **implemented + smoke-tested OK**.
11. **Mirrored board state parity + pending-move mirror sync** — **implemented** (logs on state mirror + bridged clicks; syncs runtime pending move from framework).
12. **Deferred destroy queue** — **implemented** (combat coroutines finish before `Destroy`; counter-EXP / AI counter-kill stable).
13. **Counterattack EXP + AI turn stall during EXP HUD** — **implemented + smoke-tested OK**.
14. **Input/turn-loop authority flip** — enable `BattleBoard.sceneInputEnabled` without bridge. Blocked until parity proven.
14. **Cell identity collapse** — explicitly deferred.
15. **Phases 10–13** of `CLEAN_RUNTIME_REWRITE_PLAN.md` — not started.

---

## Recommended Next Steps

### Immediate (smoke test — attack slice)

With `useRuntimeMovementExecution` **ON**:

1. Select unit → move adjacent to enemy → action menu at preview tile
2. Attack option visible when in range
3. Click Attack → red attack-range cells + attackable enemies highlighted
4. Click enemy → weapon preview → confirm attack (framework execution unchanged)
5. Click acting cell → cancels attack targeting
6. Console: `[RuntimeShadow] Pending attackables … RESULT: MATCH` on menu open and attack targeting

**Cursor slice — end-turn / turn-loop shadow parity (2026-06-17):**

- `ShadowCompareEndTurn` on `RequestEndTurn` — compares framework vs runtime `RoundRobinBattleFlow.ResolveTurn` + `EvaluateLastSideStanding` before turn executes
- `ShadowCompareCurrentPlayerSync` on framework `TurnEnded` — verifies runtime mirror current player matches after transition
- Flag: `LogTurnLoopParity` (default true)

**Smoke test:** press M or End Turn button → Console `[RuntimeShadow] End turn from player … RESULT: MATCH` (plan parity + routing) then `[RuntimeShadow] Current player sync … RESULT: MATCH`

**Cursor slice — gated runtime end-turn routing (2026-06-17):**

- `ProcessRuntimeRoutedEndTurn` when `ShouldRouteHumanMovementThroughRuntime` — runtime `EndCurrentTurn(kickTurnPlayerPlay: false)` then framework `CommitTurnTransition` (avoids double AI kick)
- `CellGrid.EndUnitsForCurrentPlayerTurn` / `CommitTurnTransition` — shared handoff extracted from `EndTurnExecute`
- `BattleBoard.EndCurrentTurn` ends current-player units; `ShadowEvaluateEndCurrentTurn` for routing parity
- `CompareRuntimeEndTurnRouting` — shadow vs live next player + post-turn state label

### Short term

1. ~~End-turn / turn-loop shadow parity~~ **done** (user smoke-tested MATCH)
2. ~~Gated runtime `RequestEndTurn` routing when toggle ON~~ **done** (user smoke-tested OK)
3. ~~Combat freeze fixes~~ **done** (game-over after human attack; AI counterattack kill)
4. ~~Battle outcome shadow parity~~ **done** (user smoke-tested MATCH)
5. ~~Win/lose runtime routing when toggle ON~~ **done** (user smoke-tested: shadow + routing MATCH)

### Medium term

1. ~~Framework input suppression when toggle ON~~ **done** (runtime scene input + framework mouse suppress + bridge)
2. Full input/turn-loop flip after click-for-click parity
3. AI runtime turn authority beyond shadow
4. Publication phases per `CLEAN_RUNTIME_REWRITE_PLAN.md`

### Do not do without explicit user approval

- Force-push, hard reset, or amend commits
- Big-bang enable `BattleBoard.sceneInputEnabled` without shadow parity
- Re-introduce `LegacySquareCellBridge` / cell identity collapse
- Change `DeploymentSlot` serialized cell references without a migration plan
- Delete or rename framework files (still needed for live game)

---

## How To Smoke Test (quick reference)

### Movement animation flip

1. Open battle scene with `CellGrid`.
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
CellGrid                     BattleBoard (same GameObject)
Unit                         BattleUnit (same GameObject)
SampleSquare (Cell)                RuntimeSampleSquareCell (BoardCell)
CellGridState*               BoardState* (skeletal)
MoveAbility, etc.            BattleAction* (partial)
```

Sync direction today: **bidirectional with battle-aware routing** in `Unit.RuntimeMirror.cs`:
- **Pre-battle / deployment:** legacy → runtime via `PushLegacyStateToRuntimeMirror()`.
- **During battle (no pending move):** runtime → legacy via `PullRuntimeStateToLegacy()` — path queries and grid mirror sync read runtime authority.
- **Pending move preview/confirm:** legacy → runtime push until commit; `ApplyLegacySyncFromRuntimeMoveCommit()` pulls cell/MP after runtime confirms.

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
| Compiles cleanly | **Yes** (`dotnet build com.windy.srpg.game.csproj`, 0 errors) |
| Reachable parity | **MATCH** (proven) |
| Movement animation flip | **Implemented + smoke-tested OK** |
| Runtime pending move + input routing | **Implemented by Codex; gated behind toggle (Cursor fix)** |
| Shadow parity (toggle off) | Right-click **MATCH**; selection/cell-click helpers wired |
| Shadow parity (toggle on) | Movement + routing + reachable + pending attack **MATCH** (user smoke-tested) |
| Pending attack routing | **Implemented** (acting cell, attack highlights, parity logs) |
| End-turn runtime routing | **Implemented** (user smoke-tested OK) |
| Turn loop runtime kick | **Implemented** — `EndCurrentTurn` owns player kick |
| Battle start runtime routing | **Implemented** — smoke test pending |
| Grid state lifecycle (game-owned) | **Implemented** — adapter click/`EndTurn` routing remains |
| Combat recovery (game over / AI counter) | **Implemented** |
| Battle outcome shadow | **Done** (user smoke-tested MATCH) |
| Win/lose runtime routing | **Implemented + smoke-tested OK** |
| Framework scene input suppression | **Implemented + smoke-tested OK** |
| Runtime hover bridge | **Implemented + smoke-tested OK** |
| Mirrored board state parity | **Implemented + smoke-tested OK** (all MATCH) |
| Runtime-led EnterSelected/PendingMove | **Implemented + smoke-tested OK** |
| Runtime-direct scene input routing | **Implemented + smoke-tested OK** (unit/cell click) |
| Runtime-direct right-click routing | **Implemented + smoke-tested OK** |
| Deferred destroy queue | **Implemented** |
| Counterattack EXP + AI turn stall | **Implemented + smoke-tested OK** |
| Input/turn-loop flip | **Human scene input authority on runtime board** (framework states legacy-only when toggle ON) |
| Units `: Unit` drop | **Smoke-tested OK** |
| Abilities on `BattleAction` | **Done** — `Ability : BattleAction` (never inherited framework `Ability`); turn/destroy hooks route via `Unit.GetBattleActions()` |

| Phase 10 tile collapse | **Smoke-tested OK** |

**Next action:** **Phase 11** — remove remaining `TbsFramework` dependencies (~35 game scripts, three anchor components, asmdef reference). See `Assets/CLEAN_RUNTIME_REWRITE_PLAN.md` and `Assets/Docs/Publication/PHASE12_AUDIT_BASELINE.md`.

---

## Framework Detachment — Session Progress (2026-06-17)

### Completed this session

| Slice | Change | Files |
|-------|--------|-------|
| Turn loop kick | `BattleBoard.EndCurrentTurn(kick:false)` → legacy sync → `KickCurrentTurnPlay()` | `CellGrid.RuntimeMirror.cs`, `BattleBoard.cs` |
| Battle start | `StartBattleViaRuntimeBoard` + `BeginBattleFromHost(refreshSceneCollections: false)` | `CellGrid.BattleBootstrap.cs`, `BattleBoard.cs` |
| AI turn fix | `SelectRuntimeUnits` uses `board.CurrentPlayerId`; kick after legacy sync | `AiPlayer.cs` |
| **Grid state adapter removed** | Direct dispatch to `currentCustomState`; `CellGridEndTurnRouter` for `EndTurn` only | `CellGrid.GridInput.cs`, `CellGridState.cs`, `CellGrid.cs` |
| **Unit turn-start (partial)** | Runtime `BeginTurn` authoritative; legacy hooks via `ApplyLegacyTurnStartFromRuntime`; `syncUnitTurnHooks:false` on routed transitions | `CellGrid.UnitSync.cs`, `Unit.RuntimeMirror.cs`, `CellGrid.cs`, `BattleUnit.cs` |
| **Unit cell/MP pull (battle)** | `SyncMirroredRuntimeNow` pulls from runtime when `IsBattleStarted` and no pending move; `ApplyLegacySyncFromRuntimeMoveCommit` consolidates post-commit legacy sync | `Unit.RuntimeMirror.cs`, `Unit.cs`, `CellGrid.cs` |
| **Unit turn-end (game-owned)** | End-turn unit hooks route through `GetCurrentPlayerUnits()`; removed framework `UnitState` shim (`CompatibilityUnitState`) | `CellGrid.UnitSync.cs`, `Unit.cs` |
| **Unit `: Unit` drop** | `Unit : MonoBehaviour, IBattleUnit`; thin `FrameworkUnitAnchor : Unit` on same GO for `CellGrid.Units` / `Cell.CurrentUnits`; game-owned Unit surface in `Unit.LegacyUnitSurface.cs`; `CellGrid.AddUnit` override registers anchor | `Unit.cs`, `FrameworkUnitAnchor.cs`, `Unit.LegacyUnitSurface.cs`, `Unit.FrameworkBridge.cs`, `CellGrid.UnitRegistry.cs`, `CellGrid.cs`, `Unit.cs` |
| **Turn-state mirror fix** | Inactive-side units no longer pull `Finished` grey from runtime mirror; only current player syncs acting-state visuals | `Unit.RuntimeMirror.cs` |
| **Action notification routing** | `CellGrid` overrides `NotifyTurnStarted/Ended/OwnerDestroyed` to call `Unit.GetBattleActions()`; fixed `PrepareRuntimeTurnStartForPlan` to resolve `BattleUnit` → `Unit` | `CellGrid.ActionSync.cs`, `CellGrid.UnitSync.cs`, `CellGrid.cs` |
| **Grid `: CellGrid` drop** | `CellGrid : MonoBehaviour, IBattleBoard`; `FrameworkCellGridAnchor : CellGrid` on same GO for registries, initialize, turn transitions; game-owned `InitializeBattleScene()` | `CellGrid.LegacyGridBridge.cs`, `FrameworkCellGridAnchor.cs`, `CellGrid.cs` |
| **Cells `: Square` drop** | `CustomSquare : MonoBehaviour, IBattleCell`; `FrameworkSquareAnchor : Square` on same GO for registries/pathfinding; deployment slot binding recovery; `IBattleCell` resolution via `GetComponent<CustomSquare>()` | `CustomSquare.LegacyCellBridge.cs`, `FrameworkSquareAnchor.cs`, `DeploymentSlot.cs`, `Cell.cs` |
| **Phase 10 — full tile collapse** | Single `BattleSquareCell` host; `SampleSquare`/`CustomSquare`/`RuntimeSampleSquareCell` deleted; prefabs + `test.unity` rewired | `BattleSquareCell.cs`, `BattleSquareCell.LegacyAnchorBridge.cs`, `CellTilePreviewUtility.cs`, `BattleSquareCellHighlighter.cs`, `Square.prefab`, `Wall.prefab`, `test.unity` |

### Smoke test required (Unit `: Unit` drop)

1. Battle start + deployment — units register, occupy cells, win/lose still works
2. Human turn — select unit, move (pending confirm + commit), attack menu, deselect
3. AI turn — move + attack; turn handoff back to human
4. Unit hover / inspect UI (uses `Unit.UnitClicked` / `UnitHighlighted`)
5. Cell occupancy — pathfinding, displacement, obstructable tiles
6. End turn — MP refresh, finished-unit visuals, buff turn hooks
7. Unit death — removal from grid registry and occupancy lists

### Smoke test required (turn-end / turn-state slice — prior)

1. End turn — outgoing units show finished/gray state; pending moves cancel cleanly
2. New turn — MP resets, friendly tint returns, buff/skill turn-start hooks fire
3. AI turn end → human turn start (both players)
4. Unit select/deselect visuals (selected tint, friendly tint) unchanged
5. Battle start first-turn MP/state still correct

### Still anchored on framework

- `CellGrid : MonoBehaviour` + `FrameworkCellGridAnchor : CellGrid` — game-owned scene host; anchor is registry/turn token only
- `Unit : MonoBehaviour` + `FrameworkUnitAnchor : Unit` — game-owned Unit fields/events; anchor is registry token only
- Assembly `com.windy.srpg.game` → `com.crookedhead.tbsf`

---

## Framework Detachment Roadmap (priority order)

Goal: remove `Assets/TBS Framework` from the publishable project. The private workspace keeps it until each slice is proven.

### Remaining framework anchors (today)

| Anchor | Game type | Runtime replacement | Status |
|--------|-----------|---------------------|--------|
| Turn loop / end turn | `CellGrid.EndTurn`, `CommitTurnTransition` | `BattleBoard.EndCurrentTurn` | **Done** — runtime kicks; legacy sync skips duplicate kick |
| Grid board | `CellGrid : MonoBehaviour` + `FrameworkCellGridAnchor : CellGrid` | `BattleBoard` + thin scene host | **Done (smoke-tested)** — host owns init/start; anchor is registry/turn token |
| Units | `FrameworkUnitAnchor : Unit` (registry token) + `Unit : MonoBehaviour` | `BattleUnit` mirror on same GO | **Done (smoke-tested)** — `: Unit` inheritance dropped; runtime owns turn hooks + cell/MP pull |
| Cells | `BattleSquareCell : SquareBoardCell` + baked `FrameworkSquareAnchor : Square` | `BoardCell` only (anchor optional later) | **Done (smoke-tested)** — Phase 10 |
| Abilities | `Ability : BattleAction` | `BattleAction` / `IBattleAction` | **Done** — no framework `Ability` inheritance; grid notifies via `GetBattleActions()` |
| Grid states | `LegacyCellGridStateAdapter : CellGridState` | `BoardState*` + direct `CellGridState` dispatch | **Done** — adapter removed; end-turn router only |
| Lifecycle | `Initialize`, `StartGame` | Game-owned bootstrap | **Partial** — host orchestrates init; anchor resolves `IBattleBoard`/agents from sibling components; `StartLegacyBattle` fallback fixed |

### Recommended slice order

1. ~~**Turn loop**~~ — **Done** (smoke-tested).
2. ~~**Battle start**~~ — **Done** (smoke-tested).
3. ~~**Grid states**~~ — **Done** — adapter removed; smoke-test before unit slice.
4. ~~**Units**~~ — **Done (smoke-tested)** — `: Unit` drop; `FrameworkUnitAnchor` keeps registries; turn-state mirror fix.
5. ~~**Abilities**~~ — **Done** — already `BattleAction`-only; action notification routing via `CellGrid.ActionSync`.
7. ~~**Delete `CellGrid : CellGrid`**~~ — **Done (smoke-tested)**.
8. ~~**Cells `: Square` drop + Phase 10 prefab collapse**~~ — **Done (smoke-tested)**.
9. **Phase 11** — remove TBS Framework dependencies (current phase).
10. **Phase 11B–13** — consolidate code root, bridge cleanup, publication audit, public repo packaging.

### Do not do yet

- Delete `Assets/TBS Framework` folder (still needed for `Unit`, `Cell`, `Ability` bases and `Initialize`).
- Collapse cell identity on prefabs without smoke test. **Phase 10 rewire done; smoke test still required.**
- Big-bang remove `LegacyCellGridStateAdapter` before battle-start path is game-owned. **Done.**
