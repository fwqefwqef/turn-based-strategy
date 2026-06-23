# Single-Layer Rewrite Notes

Last updated: 2026-06-17

## Status

| Item | Value |
|------|--------|
| **Baseline commit** | `88bf25f` (`cleanup`) |
| **WIP stash** | `stash@{0}` — `single-layer Phase 1-6 WIP (pre-baseline test)` |
| **Active phase** | **Phase 8 in progress** — turn plans use scene `Unit` (8a applied); smoke test pending |
| **Rule** | One phase → compile → **smoke test** → commit. Never skip the smoke test. |

To recover the abandoned slice (for reference only):

```powershell
git stash show -p stash@{0}   # read diff
git stash apply stash@{0}     # re-apply on top of baseline (expect conflicts)
```

---

## Goal

One owner per responsibility:

| Responsibility | Target type | Namespace (eventual) |
|----------------|-------------|----------------------|
| Battle flow / turns | `CellGrid` | `Windy.Srpg.Game.Grid` |
| Unit state / combat / move | `Unit` | `Windy.Srpg.Game.Units` |
| Tile / occupancy / highlight | `Cell` | `Windy.Srpg.Game.Grid` |
| AI / abilities / UI inputs | talk to `CellGrid`, `Unit`, `Player` directly |

**Delete when done:** duplicate mirrors (`GridUnit`, `RuntimeGrid`, `RuntimeGridState*`) and cast-through contracts (`IGridContext`, `IGridUnit` as default API).

---

## Baseline architecture (HEAD ΓÇö do not break until Phase 5)

```
Human input ΓåÆ CellGrid (scene states)
           ΓåÆ RuntimeGrid (mirror states when ShouldRouteHumanMovementThroughRuntime)
           ΓåÆ MoveAbility / Unit

Pathfinding (human + AI):
  Unit.GetAvailableDestinations / FindPath / CachePaths
    ΓåÆ if TryUseRuntimePathAuthority ΓåÆ GridUnit (mirror)  ΓåÉ still authoritative today
    ΓåÆ else scene graph on Unit

Pending move + action menu:
  Unit._pendingMove / PreviewCell (scene)
  GetActingCellForPendingActions may compare with GridUnit.PreviewCell when runtime-routed

AI turn:
  RuntimeGrid.KickCurrentTurnPlay ΓåÆ AiPlayer.PlayTurn(CellGrid)
  AiTurnRunner on scene Unit (may still pass IGridContext / cast from GridUnit in places)

Turn loop:
  ShouldRouteTurnLoopThroughRuntime = true ΓåÆ ProcessRuntimeRoutedEndTurn / RuntimeGrid.EndCurrentTurn
```

---

## Why the monolithic slice failed (2026-06-17)

The stashed WIP changed **~30 files at once** and combined:

1. `IGridContext` / `IGridUnit` ΓåÆ `CellGrid` / `Unit` on `BattleAction` and AI
2. Removal of `TryUseRuntimePathAuthority` / `TryUseRuntimeMovementAuthority` (scene-only pathfinding)
3. Removal of runtime pending-move commit and mirror sync helpers
4. Occupancy / `IsTaken` simplification

That broke three symptoms together:

| Symptom | Likely cause in WIP |
|---------|---------------------|
| Attack missing from action menu | Acting-cell / range queries no longer matched preview destination after mirror desync |
| Skills unselectable | Same range / `CanUseSkillFromPreview` acting cell |
| Enemies don't move | `GetAvailableDestinations` empty on scene path while AI no longer delegated to `GridUnit` |

**Lesson:** collapsing **types** and collapsing **authority** in one diff is unsafe. Types can move first; path/move authority stays on the hybrid until parity is proven.

---

## Smoke test gate (run after every phase)

Use the same scene (`test.unity` or your battle scene):

1. **Deploy / start battle** ΓÇö friendlies and enemies both appear.
2. **Human move** ΓÇö blue reachable tiles show; click destination; preview animates.
3. **Action menu** ΓÇö Attack visible when adjacent to enemy; Skill opens with at least one usable entry; Wait/Cancel work.
4. **Attack or Wait** ΓÇö turn ends cleanly.
5. **AI turn** ΓÇö at least one enemy moves (or attacks if in range).
6. **Second human turn** ΓÇö no stuck input / blocked state.

If any step fails, **stop**, fix or revert the phase ΓÇö do not start the next phase.

---

## Phased merge plan

### Phase 0 ΓÇö Baseline confirmation (no code)

- [x] Run smoke test gate on `88bf25f`.
- [x] Baseline confirmed working (2026-06-17).

**Exit:** all six smoke checks pass.

---

### Phase 1 ΓÇö Turn-player surface only Γ£à (code done ΓÇö smoke test pending)

**Intent:** Call sites pass `CellGrid` into player turn hooks without changing path authority or action contracts.

| Touch | Change |
|-------|--------|
| `IBattleTurnPlayer`, `BattlePlayerController`, `Player` | `PlayTurn(CellGrid)` / `BindToGrid(CellGrid)` |
| `RuntimeGrid` | `KickCurrentTurnPlay` / `BeginCurrentTurn` / `BindToGrid` pass `SceneGrid`, not `RuntimeGrid` |
| `AiBattlePlayerController` | `PlayTurn(CellGrid)` + `RequestEndTurn()` on scene grid |

**Do not:** remove `IGridContext`, change `BattleAction`, or touch `TryUseRuntime*`.

**Exit:** smoke test gate + compile.

---

### Phase 2 ΓÇö AI ordering on scene `Unit` Γ£à

**Intent:** AI turn runner orders and executes scene `Unit` lists; `CellGrid` is the execution context.

| Touch | Change |
|-------|--------|
| `AiTurnRunner` | Overloads for `IEnumerable<Unit>` + `CellGrid` |
| `AiTurnOrdering` | `OrderByMovementFreedom(units, cellGrid)` on scene units |
| `AiPlayer` | Single `ExecuteTurn(cellGrid)` path; no `RuntimeGrid` passed to AI actions |
| `MovementFreedomUnitSelection` | Sort `Unit` directly (no `GridUnit` hop) |
| `AiBattlePlayerController` | Select/order via `cellGrid.GetCurrentPlayerUnits()` |

**Do not:** remove runtime path delegation from `Unit.CombatAndMovement`.

**Exit:** smoke test gate ΓÇö **AI move is the critical check for this phase**.

**Smoke checklist:**

- [x] Compiles in Unity
- [x] Smoke 1ΓÇô6 (see gate above)

---

### Phase 3 ΓÇö `BattleAction` scene types (keep runtime path delegation) Γ£à

**Intent:** Replace `IGridContext` / `IGridUnit` on the **public** action API with `CellGrid` / `Unit`. Keep a thin `ResolveCellGrid` only if something still passes `RuntimeGrid`.

| Touch | Change |
|-------|--------|
| `BattleAction.cs` | `InitializeAction(Unit)`, callbacks take `CellGrid`; drop `IBattleAction` when nothing implements it externally |
| `Ability.cs` | Public `OnCellClicked` ΓåÆ protected `HandleCellClicked`; `UnitReference => OwnerUnit` |
| `MoveAbility`, `AttackAbility` | Rename overrides to `Handle*` pattern |
| `GridUnit.Initialize` | Still calls `action.InitializeAction(sceneUnit)` |

**Do not:** remove `TryUseRuntimePathAuthority`, `SyncMirroredRuntimeNow`, or runtime pending-move commit yet.

**Exit:** smoke test gate ΓÇö **action menu attack + skills are the critical checks**.

**Smoke checklist:**

- [x] Compiles in Unity
- [x] Smoke 1ΓÇô6 (see gate above)

---

### Phase 4 ΓÇö Scene pathfinding parity (read-only proof) Γ£à

**Intent:** Prove scene-only pathfinding matches mirror **before** switching authority.

| Touch | Change |
|-------|--------|
| `RuntimeParityDiagnostics` | On unit select / AI precalc: compare scene `ComputeAvailableDestinationsSceneOnly` vs `GridUnit.GetAvailableDestinations`; sample path compare |
| `Unit.CombatAndMovement` | Scene-only helpers extracted; uses `DijkstraPathfinder` (not legacy reverse-path adapter); traverse/occupy rules match `GridUnit`; graph edges + path cost aligned |
| `CellGrid.Scene` | `RebuildSceneCellOccupancy` refreshes `IsTaken` after mirror sync |

**Do not:** remove `TryUseRuntimePathAuthority` yet.

**Exit:** parity logs `[RuntimeParity] MATCH` for human select and AI units; smoke test still passes with delegation **on**.

**Verify in Unity console:**

- [x] Select a human unit ΓåÆ `[RuntimeParity] MATCH (human-select)`
- [x] End turn ΓåÆ AI units log `[RuntimeParity] MATCH (ai-precalc)`
- [x] Smoke 1ΓÇô6 still pass (user confirmed gameplay + parity)

---

### Phase 5 ΓÇö Transfer path / move authority to scene `Unit` ✅

**Intent:** After Phase 4 proved parity, flip the switch: the scene `Unit` is authoritative for pathfinding and movement execution. The runtime `GridUnit` mirror becomes **push-only** (it receives scene state but never drives it).

| Touch | Change |
|-------|--------|
| `Unit.CombatAndMovement` | `GetAvailableDestinations` / `CachePaths` / `FindPath` drop the `TryUseRuntimePathAuthority` branch ΓåÆ scene-only compute |
| `Unit.CombatAndMovement` | `Move()` removes the runtime `BeginPendingMove` / `AnimateAlongPathVisual` / `ConfirmPendingMove` block ΓåÆ scene animate + occupancy + mirror push |
| `Unit.CombatAndMovement` | `ConfirmPendingMove` / `PreviewMovementAnimation` drop runtime branches; `BeginPendingMoveInPlace` pushes mirror via `ResolveRuntimeUnit()` |
| `Unit.SceneBinding` | `SyncMirroredRuntimeNow` is now push-only; deleted `PullRuntimeStateToScene`, `ApplySceneSyncFromRuntimeMoveCommit`, `ShouldPullSceneFromRuntimeMirror`, `TryUseRuntimePathAuthority`, `TryUseRuntimeMovementAuthority`, `TryBuildSceneMovementPath` |
| `CellGrid` | `ProcessRuntimeRoutedPendingMoveCommit` commits on scene `Unit` then pushes mirror; `TryCommitPendingMoveFromPendingAction` delegates to it |

**Do not:** remove human input state routing (`ShouldRouteHumanMovementThroughRuntime` click routing) yet ΓÇö that is the turn-loop / state collapse (Phase 6/7). Keep `SyncMirroredRuntimePendingMove` / `TryBuildRuntimeMovementPath` (still used to mirror pending moves).

**Exit:** AI units move; human move/preview/confirm works; parity logs still `MATCH`.

**Smoke checklist:**

- [x] Compiles in Unity
- [x] Human move/preview/confirm; AI move/attack
- [x] Parity logs `MATCH`

---

### Phase 6 — Single occupancy on `Cell` ✅

**Smoke checklist:**

- [x] Compiles in Unity
- [x] Smoke 1–6
- [x] `[RuntimeParity] MATCH` on select + AI turn (all 3 enemies, post-move positions)

---

### Phase 7 — Collapse `RuntimeGrid` turn / input shadow ✅ (7a complete)

**Intent:** `CellGrid` states own flow; `RuntimeGrid` stops driving parallel state machine.

**Rollback note (2026-06-17):** Phase 7 was attempted and reverted after AI stopped moving. Restart with incremental flag changes — do not disable all `ShouldRoute*ThroughRuntime` flags at once.

**Phase 7a (applied):** turn loop only — keep human movement routed through runtime mirror.

| Touch | Change |
|-------|--------|
| `CellGrid.cs` | `ShouldRouteTurnLoopThroughRuntime => false`; `ShouldSyncFlowStateThroughRuntimeGrid` gates `Enter*State`; `RequestEndTurn` → `ExecuteSceneEndTurn`; scene combat notify hooks |
| `CellGrid.Scene.cs` | `StartBattleViaSceneAuthority` when turn loop not routed; scene end turn syncs mirror after transition |
| `CellGridStates.All.cs` | **Unchanged** — human click routing early-returns stay until 7b |
| `CellGrid.Runtime.cs` | **Unchanged** — runtime path kept for rollback / future 7b |

**Still routed through runtime (7a):**

- `ShouldRouteHumanMovementThroughRuntime` — human select / pending-move clicks
- `ShouldRouteBattleOutcomeThroughRuntime` — outcome evaluation

**Smoke checklist:**

- [x] Compiles in Unity
- [x] Smoke 1 — battle start (`Game started via scene grid`)
- [x] Smoke 2 — human move + reachable tiles
- [x] Smoke 3 — action menu attack + skill
- [x] Smoke 4 — end action / turn
- [x] Smoke 5 — AI move
- [x] Smoke 6 — second human turn
- [x] User confirmed: works flawlessly

**Next slice (7b):** flip `ShouldRouteHumanMovementThroughRuntime`; remove runtime-routing early-returns in `CellGridStates.All.cs`.

### Phase 7b — Scene-owned human input ✅

| Touch | Change |
|-------|--------|
| `CellGrid.cs` | `ShouldRouteHumanMovementThroughRuntime => false`; scene-only `EnterSelectedState` / `EnterPendingMoveConfirmState`; pending-attack commit uses scene combat notify |
| `CellGridStates.All.cs` | Removed runtime-routing early-returns — clicks dispatch to scene state handlers |
| `GameplayInputController` | **Unchanged** — already falls back to `HandleSceneUnitClicked` when runtime input inactive |

**Smoke checklist:**

- [x] User confirmed working (committed as `998d022`)

---

### Phase 8 — Delete mirrors and rename (8a applied — smoke test pending)

**Phase 8a (this slice):** turn plans and battle outcome on scene types.

| Touch | Change |
|-------|--------|
| `RoundRobinBattleFlow.cs` | `RoundRobinTurnPlan.PlayableUnits` is `IReadOnlyList<Unit>`; resolve scene units from `CellGrid` |
| `CellGrid.Scene.cs` | Drop `IGridUnit` hop in playable-units accessor |
| `CellGrid.cs` | `ShouldRouteBattleOutcomeThroughRuntime => false` |

**Still present (later slices):**

- `GridUnit` / `RuntimeGrid` mirror components and `CellGrid.Runtime.cs` sync bridge
- `IGridContext` / `IGridUnit` on AI evaluators and legacy adapters
- `Windy.Srpg.Runtime.*` namespaces on scene-owned types

**Smoke checklist:**

- [ ] Compiles in Unity
- [ ] Smoke 1–6 (see gate above)

**Next slices:**

- **8b:** Remove `RuntimeGrid` state machine + dead routing in `CellGrid.Runtime.cs`
- **8c:** Remove `GridUnit` mirror and `Unit.SceneBinding` push sync
- **8d:** Delete `IGridContext` / `IGridUnit`; namespace cleanup

---

## Phase checklist (copy per commit)

```
Phase N: [name]
- [ ] Code complete
- [ ] Compiles in Unity
- [ ] Smoke 1 ΓÇö battle start
- [ ] Smoke 2 ΓÇö human move + reachable tiles
- [ ] Smoke 3 ΓÇö action menu attack + skill
- [ ] Smoke 4 ΓÇö end action / turn
- [ ] Smoke 5 ΓÇö AI move
- [ ] Smoke 6 ΓÇö second human turn
- [ ] Commit
```

---

## Files usually involved (by phase)

| Phase | Primary files |
|-------|----------------|
| 1 | `Player.cs`, `AiPlayer.cs`, `AiBattlePlayerController.cs`, `RuntimeGrid.cs`, `CellGrid.Scene.cs` |
| 2 | `AiDecisionAction.cs`, `AIAction.cs`, `AiPlayer.cs`, `MoveToPositionAIAction.cs` |
| 3 | `BattleAction.cs`, `Ability.cs`, `MoveAbility*.cs`, `AttackAbility.cs`, `GridUnit.cs` |
| 4 | `Unit.CombatAndMovement.cs`, `GridUnit.cs`, `RuntimeParityDiagnostics.cs` |
| 5 | `Unit.CombatAndMovement.cs`, `Unit.SceneBinding.cs`, `MoveAbility.PendingActions.cs`, `CellGrid.Runtime.cs` |
| 6 | `Cell.cs`, `Unit.SceneBinding.cs`, `CellGrid.Scene.cs` |
| 7 | `CellGrid.cs`, `CellGrid.Runtime.cs`, `CellGridStates.All.cs`, `RuntimeGridStates.All.cs` |
| 8 | Wide deletion pass |

---

## Remaining seams at baseline (HEAD)

- `Unit` vs `GridUnit` ΓÇö duplicate occupancy, pending move, pathfinding delegation
- `CellGrid` vs `RuntimeGrid` ΓÇö dual state machines + turn kick
- `IGridUnit` / `IGridContext` ΓÇö embedded in actions, AI, `RoundRobinTurnPlan`
- `Windy.Srpg.Runtime.*` namespaces on scene-owned types

These are **expected** until Phases 5ΓÇô8. Do not ΓÇ£clean them up early.ΓÇ¥
