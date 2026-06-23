# Turn Based Strategy â€” Architecture Summary

Last updated: 2026-06-23

This document describes the **current** battle architecture after the single-layer merge (Phases 8dâ€“9). The scene layer is canonical: one `CellGrid`, one `Unit`, one `Cell` per battle â€” no parallel runtime mirror grid.

For migration history and smoke-test gates, see `[SINGLE_LAYER_REWRITE_NOTES.md](SINGLE_LAYER_REWRITE_NOTES.md)`.

---

## 1. Architecture overview

### Ownership model


| Responsibility                                          | Owner                                         | Namespace                   |
| ------------------------------------------------------- | --------------------------------------------- | --------------------------- |
| Battle host, turn loop, state machine, deployment       | `CellGrid` (partial)                          | `Windy.Srpg.Game.Grid`      |
| Unit stats, combat, movement, inventory, progression    | `Unit`                                        | `Windy.Srpg.Game.Units`     |
| Tile geometry, occupancy, highlights, tile input events | `Cell`                                        | `Windy.Srpg.Game.Grid`      |
| Human / AI turn drivers                                 | `Player` hierarchy                            | `Windy.Srpg.Game.Players`   |
| Per-unit actions (move, attack, â€¦)                      | `Ability`                                     | `Windy.Srpg.Game.Abilities` |
| Central board input                                     | `GameplayInputController`                     | `Windy.Srpg.Game.UI`        |
| Campaign persistence                                    | `CampaignSaveManager` / `CampaignSaveFactory` | `Windy.Srpg.Game.Campaign`  |


### What changed in the merge

Previously, battle logic was split between **scene** objects (`CellGrid`, `Unit`) and **runtime mirror** objects (`RuntimeGrid`, `GridUnit`) connected by sync bridges and `IGridContext` / `IGridUnit` cast-through APIs. That dual layer is **removed**. All active code paths now use scene types directly:

- Input â†’ `GameplayInputController` â†’ `CellGrid.HandleScene*()` â†’ `CellGridState`
- Pathfinding â†’ `Unit` + `DijkstraPathfinding` on scene `Cell` graph
- Turn order â†’ `RoundRobinBattleFlow.ResolveStart/ResolveTurn(CellGrid)`
- AI â†’ `AiPlayer` â†’ `AiTurnRunner` with `(IBattlePlayer, Unit, CellGrid)`
- Player ownership â†’ `IBattlePlayer.Owns(Unit)` (not mirror units)

Namespaces were consolidated under `Windy.Srpg.Game.*` (Phase 9). A few UI MonoBehaviours remain in the global namespace for historical scene references (`TurnCounterUI`, `ActionMenuUI`, etc.).

### Assembly

- `**com.windy.srpg.game`** â€” all code under `Assets/Game/Code/` (`rootNamespace: Windy.Srpg.Game`)
- `**com.windy.srpg.game.scenes**` â€” scene scripts under `Assets/Scenes/` (e.g. `SampleUnit`)

Game data catalogs load from `Assets/StreamingAssets/gdata.json` via `CatalogResourceLoader`.

---

## 2. Battle lifecycle (scene load â†’ turn end)

```
Awake (CellGrid)
  â”œâ”€ EnsureSceneCellAnchors()
  â”œâ”€ PrepareFriendlyDeploymentFromSave()     â† CampaignSaveData â†’ DeploymentSlots
  â””â”€ WireSceneGridEvents()

Start (CellGrid)
  â”œâ”€ [pre-battle] RequestFrameworkInitialize() â†’ EnterBlockedInputState()
  â””â”€ [immediate]  RequestFrameworkInitializeAndStart() â†’ StartBattle()

InitializeBattleScene()
  â”œâ”€ Discover IBattleTurnPlayer + Player under PlayersParent
  â”œâ”€ Collect scene Cells; IBattleSceneUnitSource registers Units
  â””â”€ SceneLevelLoadingDone

StartBattle()
  â”œâ”€ RoundRobinTurnPlan plan = RoundRobinBattleFlow.ResolveStart(CellGrid)
  â”œâ”€ SyncBattleStartFromPlan(plan)         â† currentPlayerNumber, unit OnTurnStart
  â””â”€ KickCurrentScenePlayer()              â† IBattleTurnPlayer.PlayTurn(CellGrid)

Human turn:  HumanPlayer.Play(CellGrid) â†’ EnterWaitingState()
AI turn:     AiPlayer.Play(CellGrid) â†’ EnterAiTurnState() â†’ AiTurnRunner coroutine

End turn:    CellGrid.RequestEndTurn()
  â”œâ”€ ExecuteSceneEndTurn()
  â”œâ”€ RoundRobinBattleFlow.ResolveTurn(CellGrid) â†’ next RoundRobinTurnPlan
  â”œâ”€ IBattleEndCondition.Evaluate(CellGrid) â†’ BattleOutcome
  â””â”€ KickCurrentScenePlayer() or GameOver
```

`GUIController.Awake()` wires `PreBattleUIController.Initialize(CellGrid)` and `GameplayInputController.Initialize(CellGrid)` so UI and input share the same grid reference.

---

## 3. Parameter and data flow (detailed)

### 3.1 Human input â†’ grid state machine

**Central gate:** When `GameplayInputController.IsCentralizedSceneInputActive` is true, `Cell.OnMouseDown` / `Unit.OnMouseEnter` return early; all board input goes through the controller.


| Step | Caller       | Method                                       | Key parameters  | Callee                                                                         |
| ---- | ------------ | -------------------------------------------- | --------------- | ------------------------------------------------------------------------------ |
| 1    | Unity Update | `GameplayInputController.Update()`           | â€”               | polls input                                                                    |
| 2    | Controller   | `UpdateMouseHover()` / keyboard hover        | â€”               | sets `hoveredCell`, `hoveredUnit`                                              |
| 3    | Controller   | `ApplyHoverTarget(Cell cell, Unit unit)`     | scene refs      | `unit.RaiseSceneHighlightEvent()` or `cell.RaiseSceneHighlightEvent()`         |
| 4    | Unit/Cell    | highlight event                              | `Unit` / `Cell` | `CellGrid` handlers â†’ `TryDispatchUnitHighlighted` / `TryDispatchCellSelected` |
| 5    | Controller   | `TryHandleSelectCommand()`                   | â€”               | see below                                                                      |
| 6    | Controller   | `cellGrid.HandleSceneUnitClicked(Unit unit)` | `Unit`          | `TryDispatchUnitClicked(unit)`                                                 |
| 7    | State        | `CellGridState.OnUnitClicked(Unit unit)`     | `Unit`          | e.g. `EnterSelectedState(unit)`                                                |


**Select (single click / X):**

```
TryHandleSelectCommand()
  if TryOpenTurnInfoFromDoubleSelect()     // 0.35s window, same target
    â†’ TurnCounterUI.RequestShow()
  else if hoveredUnit != null
    â†’ TurnCounterUI.RequestHide()
    â†’ cellGrid.HandleSceneUnitClicked(hoveredUnit)
  else if hoveredCell != null
    â†’ TurnCounterUI.RequestHide()
    â†’ cellGrid.HandleSceneCellClicked(hoveredCell)
```

**Turn info eligibility** (`ShouldOpenTurnInfoForHoveredUnit/Cell`):

- `CellGrid.CurrentState` is `CellGridStateWaitingForInput`
- `cellGrid.IsHumanTurn && !cellGrid.IsPreBattlePhase`
- Unit path: finished friendly (`unit.PlayerNumber == CurrentPlayerNumber && unit.IsFinishedForTurn`)
- Cell path: empty cell (no unit on tile)

**Cancel (Z / right-click equivalent):**

```
TryHandleCancelCommand()
  â†’ cellGrid.ProcessSceneRightClick()
    â†’ currentState.OnRightClick()   // usually EnterWaitingState()
```

**Inspect (S):** `UnitInspectPanelUI.TryOpenInspectForUnit(Unit)` â€” independent of grid state machine.

### 3.2 Unit selection â†’ MoveAbility â†’ pending move â†’ action menu


| Step | Type flow                       | Description                                                                                                          |
| ---- | ------------------------------- | -------------------------------------------------------------------------------------------------------------------- |
| 1    | `Unit` â†’ `CellGrid`             | `CellGridStateWaitingForInput.OnUnitClicked(Unit)` calls `cellGrid.EnterSelectedState(Unit)`                         |
| 2    | `CellGrid` â†’ state              | `new UnitSelectedState(CellGrid, Unit, IEnumerable<Ability>)` via `unit.GetAbilities()`                              |
| 3    | `UnitSelectedState` â†’ abilities | For each `Ability` on unit: `OnActionSelected(CellGrid)`, `DisplayAction(CellGrid)`; input forwarded to all          |
| 4    | `MoveAbility.Display`           | `Unit.GetAvailableDestinations(List<Cell>)` â†’ marks reachable `Cell`s                                                |
| 5    | Click destination               | `MoveAbility.HandleCellClicked(Cell, CellGrid)`                                                                      |
| 6    | Path                            | `Unit.FindPath(List<Cell>, Cell destination)` â†’ `IList<Cell>` via `DijkstraPathfinding`                              |
| 7    | State                           | `cellGrid.EnterPendingMoveConfirmState(MoveAbility)` â†’ `CellGridStateMovePendingConfirm`                             |
| 8    | Preview                         | `Unit.PreviewMove(Cell destination, IList<Cell> path)` sets internal `PendingMove` (occupancy **not** committed yet) |
| 9    | UI                              | `MoveAbility.ShowActionMenu(CellGrid)` â†’ `ActionMenuUI` (attack / skill / item / trade / wait)                       |


**Wait in place:** Re-click selected unit â†’ `MoveAbility.OnSelectedUnitClicked(CellGrid)` â†’ `Unit.BeginPendingMoveInPlace()` â†’ pending confirm without path animation.

**Confirm move after action:**

```
Unit.ConfirmPendingMove(bool consumeAllRemainingMovement)
  OR CellGrid.CommitPendingMoveOnSceneUnit(Unit, bool)
    â†’ updates Unit.Cell, Cell.CurrentUnits, MovementPoints
    â†’ fires movement / occupancy notifications
```

**Attack from pending move:**

```
MoveAbility.BeginAttackTargeting(CellGrid)
  â†’ GetAttackableEnemiesFromPreview(CellGrid) : List<Unit>
  â†’ user picks Unit target
  â†’ Unit.AttackHandler(Unit target)
  â†’ Unit.ConfirmPendingMove()
```

Acting position during pending move uses `Unit.PreviewCell` (where the unit *will* be after confirm), so range checks for skills/attacks match the preview destination.

### 3.3 AI turn â†’ AiPlayer â†’ evaluators â†’ actions


| Step | Method                                                                                    | Parameters                                                                                              |
| ---- | ----------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------- |
| 1    | `CellGrid.KickCurrentScenePlayer()`                                                       | â€”                                                                                                       |
| 2    | `IBattleTurnPlayer.PlayTurn(CellGrid grid)`                                               | `CellGrid`                                                                                              |
| 3    | `AiPlayer.Play(CellGrid)`                                                                 | enters `CellGridStateAiTurn`                                                                            |
| 4    | `AiPlayer.SelectUnits(CellGrid)`                                                          | `IReadOnlyList<Unit>` via `MovementFreedomUnitSelection` or `AiTurnOrdering`                            |
| 5    | `AiTurnRunner.ExecuteTurn(IBattlePlayer, IEnumerable<Unit>, CellGrid, Action onComplete)` | per unit loop                                                                                           |
| 6    | `unit.GetComponentsInChildren<AiDecisionAction>()`                                        | each decision component                                                                                 |
| 7    | `InitializeDecision(IBattlePlayer, Unit, CellGrid)`                                       | binds player, unit, grid                                                                                |
| 8    | `Precalculate(...)` / `ShouldExecute(...)`                                                | evaluator setup                                                                                         |
| 9    | `ExecuteDecision(...)` â†’ coroutine                                                        | e.g. `MoveToPositionAIAction`                                                                           |
| 10   | Move                                                                                      | `CellEvaluator.Evaluate(Cell, Unit, Player, CellGrid)` â†’ best `Cell`; `MoveAbility.AIExecute(CellGrid)` |
| 11   | Attack                                                                                    | `UnitEvaluator.Evaluate(Unit, Unit, Player, CellGrid)` â†’ best target; `unit.AttackHandler(Unit)`        |
| 12   | Done                                                                                      | `onComplete()` â†’ `cellGrid.RequestEndTurn()`                                                            |


### 3.4 Campaign save â†’ pre-battle deployment â†’ battle units


| Step      | Method                                                                                      | Data                                                  |
| --------- | ------------------------------------------------------------------------------------------- | ----------------------------------------------------- |
| Load      | `CampaignSaveManager.Load()`                                                                | `CampaignSaveData` (JSON on disk)                     |
| Seed      | `CampaignSaveFactory.EnsureStarterOwnedUnits(CampaignSaveData, IEnumerable<UnitPreset>)`    | merges starter presets not already in save            |
| Roster    | `GetResolvedDeploymentRosterForCurrentScene(CampaignSaveData, int slotCount)`               | `string[]` unit IDs                                   |
| Apply     | `ApplyFriendlyDeployment(DeploymentSlot[], CampaignSaveData, IReadOnlyList<string> roster)` | per slot: `OwnedUnitSaveData` + optional `UnitPreset` |
| Configure | `Unit.ConfigureFromOwnedUnitSaveData(OwnedUnitSaveData, UnitPreset)`                        | stats, inventory, skills, passives                    |
| Register  | `RegisterDeploymentUnitForBattle(Unit, Cell)`                                               | `CellGrid` registry + occupancy                       |


**Pre-battle UI edits:**

```
PreBattleUIController
  â†’ cellGrid.SetDeploymentRoster(IEnumerable<string>)
  â†’ cellGrid.ReplaceDeploymentSlotUnit(int slotIndex, string unitId)
  â†’ cellGrid.SwapDeploymentSlots(int, int)   // board swap mode
    â†’ ApplyStagedDeploymentRoster(...)
    â†’ DeploymentRosterChanged event
```

**Persist:** `cellGrid.SaveDeploymentRosterChanges()` â†’ `CampaignSaveManager.Save(CampaignSaveData)`. Battle start may auto-save via `TryPersistOwnedUnitSave()` â†’ `CampaignSaveFactory.CreateFromOwnedUnits(IEnumerable<Unit>, ...)`.

### 3.5 Turn loop and battle end

```
CellGrid.ExecuteSceneEndTurn(bool isNetworkInvoked)
  â†’ EnterBlockedInputState()
  â†’ EndUnitsForCurrentPlayerTurn()          // marks units finished, cleanup
  â†’ RoundRobinTurnPlan = RoundRobinBattleFlow.ResolveTurn(CellGrid)
       inputs:  IEnumerable<IBattlePlayer> from GetOrderedPlayers()
                IEnumerable<Unit> from GetAllUnits()
       outputs: IBattlePlayer NextPlayer, IReadOnlyList<Unit> PlayableUnits
  â†’ BattleOutcome = IBattleEndCondition.Evaluate(CellGrid)
       default: RoundRobinBattleFlow.EvaluateLastSideStanding(...)
  â†’ CommitTurnTransition(plan)              // currentPlayerNumber, RoundCount++, TurnStarted
  â†’ KickCurrentScenePlayer() or SyncStateToGameOver()
```

Configurable adapters on the `CellGrid` GameObject:

- `RoundRobinTurnResolver` : `IBattleTurnResolver`
- `LastSideStandingCondition` : `IBattleEndCondition`
- `SceneUnitGenerator` : `IBattleSceneUnitSource`

### 3.6 UI modal stack and input blocking

`GameplayModalUI` maintains an active modal stack. When `BlocksGameplayInput` is true, `GameplayInputController` skips board hover/select. Modals that participate in keyboard navigation (`ParticipatesInKeyboardNavigation`) receive arrow-key focus via the controller.

Cancel on a visible modal: `GameplayModalUI.TryCancelTopmostActiveModal()` (used when gameplay input is blocked).

---

## 4. Grid state machine

All states live in `Windy.Srpg.Game.Grid.States` (`CellGridStates.All.cs`). Transitions are driven by `CellGrid.Enter*State()` methods in `CellGrid.cs`.


| State                             | Entered when                             | Input behavior                                        |
| --------------------------------- | ---------------------------------------- | ----------------------------------------------------- |
| `CellGridStateWaitingForInput`    | Human turn start, cancel from sub-states | Select friendly unfinished units                      |
| `UnitSelectedState`               | Friendly unit selected                   | All unit `Ability` components display; move targeting |
| `CellGridStateMovePendingConfirm` | Move destination chosen                  | Action menu; attack/skill/item flows                  |
| `CellGridStateAiTurn`             | AI turn                                  | Blocked for human; AI coroutine runs                  |
| `CellGridStateBlockInput`         | Pre-battle, combat presentation, modals  | No end-turn; input blocked                            |
| `PreBattleDeploymentSwapState`    | Deployment swap mode                     | Slot swap on board                                    |
| `CellGridStateGameOver`           | Victory/defeat                           | Terminal                                              |
| `CellGridStateRemotePlayerTurn`   | Network placeholder                      | Blocked                                               |


Every state implements `IRightClickHandler.OnRightClick()` for cancel/back behavior.

---

## 5. Class reference

Paths are under `Assets/Game/Code/` unless noted.

### Abilities (`Windy.Srpg.Game.Abilities`)

All ability types live in `**Ability.cs**` (base class, execution helpers, and typed bases).

#### What `Ability` is

`Ability` is an abstract `MonoBehaviour` child of a `Unit`. The grid state machine talks only to `Ability` â€” there is no separate action base class and no runtime/scene split.

Each unit typically has **multiple** sibling abilities (e.g. `MoveAbility`, `AttackRangeHighlightAbility`, `AttackAbility`). When the unit is selected, `UnitSelectedState` broadcasts input and lifecycle events to **every** `Ability` on that unit in parallel.

Units discover abilities via `Unit.GetAbilities()` â†’ `GetComponentsInChildren<Ability>()`.

#### Two usage modes (same component, different entry points)


| Mode                 | Who drives it           | Entry                                                    | Typical use                                                                         |
| -------------------- | ----------------------- | -------------------------------------------------------- | ----------------------------------------------------------------------------------- |
| **Reactive**         | Grid state machine      | `OnActionSelected`, `DisplayAction`, `OnCellClicked`, â€¦  | Human turn: highlights, pending move, action menu                                   |
| **Active execution** | Caller runs a coroutine | `AIExecute(CellGrid)` / `HumanExecute` / `RemoteExecute` | AI move (`MoveToPositionAIAction` sets `Destination`, then `moveAbility.AIExecute`) |


Reactive mode does **not** call `Act()`. Active execution wraps `Act()` in `AbilityExecutionFlow` with grid state transitions (block input, return to selected state, etc.).

#### Class table


| Class                                                                  | Role                                                                                                                                                                                               |
| ---------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Ability`                                                              | Base MonoBehaviour: grid-facing lifecycle (`OnActionSelected`, `OnCellClicked`, â€¦), protected implementor hooks (`HandleCellClicked`, `Display`, â€¦), and coroutine execution (`Act`, `AIExecute`). |
| `AbilityExecutionMode`                                                 | Enum: `HumanLocal`, `RemoteInvocation`, `AiLocal`.                                                                                                                                                 |
| `AbilityExecutionFlow`                                                 | Static coroutine wrapper: before â†’ `Act()` â†’ after.                                                                                                                                                |
| `MoveActionBase`, `AttackActionBase`, `AttackRangeHighlightActionBase` | Optional typed bases extending `Ability` (shared helpers; no current concrete subclasses).                                                                                                         |
| `MoveAbility` (+ `MoveAbility.PendingActions`)                         | Primary human turn driver: movement preview, pending confirm, action menu, attack/skill/item/trade subflows.                                                                                       |
| `AttackAbility`                                                        | Standalone attack flow (also used by AI).                                                                                                                                                          |
| `AttackRangeHighlightAbility`                                          | Highlights in-range enemies while a unit is selected.                                                                                                                                              |


#### Grid â†” ability notification

`CellGrid` notifies abilities on turn boundaries and unit destruction via `NotifyAbilities(Unit, Action<Ability>)`, which calls `unit.GetAbilities()` and invokes the matching hook on each component.

### AI (`Windy.Srpg.Game.AI`, `.AI.Actions`, `.AI.Evaluators`)


| Class                    | Role                                                                                                       |
| ------------------------ | ---------------------------------------------------------------------------------------------------------- |
| `AiDecisionAction`       | Abstract AI decision component; init / precalc / execute / cleanup with `(IBattlePlayer, Unit, CellGrid)`. |
| `AiTurnRunner`           | Runs ordered `AiDecisionAction` list for one unit.                                                         |
| `AiTurnOrdering`         | Sorts AI units by movement-freedom heuristic.                                                              |
| `AiDebugInfo`            | Debug overlay metadata for tile scoring.                                                                   |
| `AIAction`               | Bridges `AiDecisionAction` to legacy `Player`-typed execute API.                                           |
| `AttackAIAction`         | Picks best in-range enemy via `DamageUnitEvaluator`; calls `Unit.AttackHandler`.                           |
| `MoveToPositionAIAction` | Picks best reachable cell via `DamageCellEvaluator`; calls `MoveAbility.AIExecute`.                        |
| `CellEvaluator`          | Abstract tile scorer for movement AI.                                                                      |
| `DamageCellEvaluator`    | Damage-at-cell heuristic.                                                                                  |
| `UnitEvaluator`          | Abstract unit scorer for attack AI.                                                                        |
| `DamageUnitEvaluator`    | Normalized dry-attack damage scoring.                                                                      |


### Buffs (`Windy.Srpg.Game.Buffs`)


| Class                                | Role                                                       |
| ------------------------------------ | ---------------------------------------------------------- |
| `BuffData`                           | Serializable buff definition (stats, duration, effect id). |
| `Buff`                               | Runtime buff instance on a unit.                           |
| `UnitBuffList`                       | Active buff collection; turn start/end hooks.              |
| `BuffRegistry`, `BuffEffectRegistry` | ID â†’ definition / effect factory.                          |
| `BuffEffectBase`, `IP_BuffEffect`    | Buff effect plugin contract.                               |
| `BuiltInBuffCatalog`                 | Registers built-in buff effects at startup.                |


### Camera (`Windy.Srpg.Game.CameraControl`)


| Class                      | Role                                                                        |
| -------------------------- | --------------------------------------------------------------------------- |
| `GameplayCameraController` | Battle camera pan/focus; follows pending-move preview and combat sequences. |


### Campaign (`Windy.Srpg.Game.Campaign`)


| Class                     | Role                                                                                  |
| ------------------------- | ------------------------------------------------------------------------------------- |
| `CampaignSaveData`        | Root save document (owned units, roster, gold, storage).                              |
| `OwnedUnitSaveData`       | Per-unit persistent state (stats, inventory ids, skill/passive ids).                  |
| `SavedInventoryEntryData` | One inventory stack entry in save data.                                               |
| `CampaignSaveManager`     | Load/save JSON to `Application.persistentDataPath`.                                   |
| `CampaignSaveFactory`     | Create/merge saves from `Unit` / `UnitPreset`; roster normalization; starter seeding. |


### Catalogs (`Windy.Srpg.Game.Catalogs`)

All types live in `JsonCatalogLoader.cs`.


| Class                                                                                          | Role                                                                                                                                     |
| ---------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------- |
| `CatalogResourceLoader`                                                                        | Static loader: `LoadItemCatalog()`, `LoadSkillCatalog()`, `LoadPassiveCatalog()`, `LoadBuffCatalog()` from `StreamingAssets/gdata.json`. |
| `GameDataCatalogResource`                                                                      | Root JSON document wrapper.                                                                                                              |
| `ItemCatalogResource`, `SkillCatalogResource`, `PassiveCatalogResource`, `BuffCatalogResource` | Section containers with `ToRuntimeDefinitions()`.                                                                                        |
| `*CatalogEntry` types                                                                          | Serializable JSON rows (weapons, skills, passives, buffs, consumables, â€¦) mapped to runtime `*Data` objects.                             |


### Grid (`Windy.Srpg.Game.Grid`, `.Grid.States`)


| Class                                        | Role                                                                                                   |
| -------------------------------------------- | ------------------------------------------------------------------------------------------------------ |
| `CellGrid`                                   | **Main partial** â€” public API, events, state transitions, pending-move commit, deferred destroy queue. |
| `CellGrid.Scene`                             | **Partial** â€” Unity lifecycle, scene registry, turn loop, input dispatch, occupancy rebuild.           |
| `CellGrid.PreBattle`                         | **Partial** â€” campaign I/O, deployment slots, roster staging.                                          |
| `Cell`                                       | Grid tile: coordinates, neighbours, `CurrentUnits`, highlights, click/hover events.                    |
| `CellHighlightKind`                          | Enum for overlay kinds (reachable, path, selected, â€¦).                                                 |
| `CellHighlighterBehaviour`                   | Abstract highlight renderer API.                                                                       |
| `CellHighlighter`                            | Concrete tile highlighter (fills, cursor border, preview borders).                                     |
| `CellTilePreviewUtility`                     | Skill preview highlight/border helpers.                                                                |
| `CellOverlaySpriteFitter`                    | Fits overlay sprites to tile bounds.                                                                   |
| `DeploymentSlot`                             | Pre-battle slot marker on a `Cell`; selection visuals.                                                 |
| `SceneUnitGenerator`                         | `IBattleSceneUnitSource` â€” discovers unit transforms in scene hierarchy.                               |
| `RoundRobinBattleFlow`                       | Static turn-order and last-side-standing victory logic.                                                |
| `RoundRobinTurnPlan`                         | Value type: `(IBattlePlayer NextPlayer, IReadOnlyList<Unit> PlayableUnits)`.                           |
| `BattleOutcome`                              | Value type: finished flag + winning/defeated player id lists.                                          |
| `GridQueries`                                | Player ordering and lookup helpers.                                                                    |
| `BattleFlowContracts.cs`                     | Defines `IBattleSceneUnitSource`, `IBattleTurnResolver`, `IBattleEndCondition`.                        |
| `RoundRobinTurnResolver`                     | MonoBehaviour adapter implementing `IBattleTurnResolver`.                                              |
| `LastSideStandingCondition`                  | MonoBehaviour adapter implementing `IBattleEndCondition`.                                              |
| `UnitAddedEventArgs`, `BattleEndedEventArgs` | Event payload types on `CellGrid`.                                                                     |


**States (`CellGridStates.All.cs`):** `CellGridState`, `CellGridStateWaitingForInput`, `CellGridStateBlockInput`, `CellGridStateGameOver`, `CellGridStateRemotePlayerTurn`, `CellGridStateAiTurn`, `CellGridStateMovePendingConfirm`, `UnitSelectedState`, `PreBattleDeploymentSwapState`, `IRightClickHandler`.

### Inventory (`Windy.Srpg.Game.Inventory`)


| Class                                                             | Role                                                |
| ----------------------------------------------------------------- | --------------------------------------------------- |
| `ItemData`                                                        | Base item definition.                               |
| `WeaponData`, `AccessoryData`, `ConsumableData`                   | Typed items with stat modifiers or charges.         |
| `Item`                                                            | Runtime inventory entry (item id + charges).        |
| `UnitInventory`                                                   | Unit's equipped/carried items.                      |
| `ItemRegistry`, `ConsumableEffectRegistry`, `UnitPassiveRegistry` | Item and effect lookup.                             |
| `BuiltInItemCatalog`                                              | Registers built-in consumable/passive item effects. |


### Localization (`Windy.Srpg.Game.Localization`)


| Class             | Role                                                         |
| ----------------- | ------------------------------------------------------------ |
| `GameTextCatalog` | Localized string lookup and `Format(key, fallback, params)`. |


### Passives (`Windy.Srpg.Game.Passives`)


| Class                                      | Role                                              |
| ------------------------------------------ | ------------------------------------------------- |
| `PassiveData`                              | Passive definition (cost, stat mods, effect id).  |
| `Passive`, `UnitPassiveList`               | Runtime passive instances; unique vs equip lists. |
| `PassiveRegistry`, `PassiveEffectRegistry` | Passive lookup and effect factory.                |
| `PassiveEffectBase`, `IP_PassiveEffect`    | Passive effect plugin contract.                   |
| `BuiltInPassiveCatalog`                    | Registers built-in passive effects.               |
| `StartingPassiveEntry`, `PassiveListKind`  | Preset/save passive list types.                   |


### Pathfinding (`Windy.Srpg.Game.Pathfinding`, `.Pathfinding.Algorithms`)


| Class                 | Role                                                                                 |
| --------------------- | ------------------------------------------------------------------------------------ |
| `IPathfinder`         | Generic pathfinder interface over weighted graphs.                                   |
| `DijkstraPathfinder`  | Generic Dijkstra implementation.                                                     |
| `GridPath<TNode>`     | Path result container.                                                               |
| `DijkstraPathfinding` | Adapter: `Dictionary<Cell, Dictionary<Cell, float>>` edges â†’ `Cell` paths for units. |


### Players (`Windy.Srpg.Game.Players`, `.Players.AI`)


| Class                          | Role                                                              |
| ------------------------------ | ----------------------------------------------------------------- |
| `IBattlePlayer`                | `PlayerId`, `IsHumanControlled`, `Owns(Unit)`.                    |
| `IBattleTurnPlayer`            | Extends with `BindToGrid(CellGrid)`, `PlayTurn(CellGrid)`.        |
| `BattlePlayerController`       | MonoBehaviour base implementing `IBattleTurnPlayer`.              |
| `Player`                       | Abstract turn player; `Play(CellGrid)` implemented by subclasses. |
| `HumanPlayer`                  | Human turn â†’ `cellGrid.EnterWaitingState()`.                      |
| `AiPlayer`                     | AI turn â†’ `EnterAiTurnState` + `AiTurnRunner` coroutine.          |
| `HumanBattlePlayerController`  | Thin human controller (`IsHumanControlled == true`).              |
| `AiBattlePlayerController`     | AI controller using `AiTurnRunner` directly.                      |
| `UnitSelection`                | Abstract per-turn unit ordering strategy.                         |
| `MovementFreedomUnitSelection` | Orders units by traversable-neighbour count.                      |


### Skills (`Windy.Srpg.Game.Skills`)


| Class                                                                       | Role                                                                 |
| --------------------------------------------------------------------------- | -------------------------------------------------------------------- |
| `SkillData`                                                                 | Skill definition (targeting, range, effect id, MP cost).             |
| `Skill`                                                                     | Runtime skill instance on a unit.                                    |
| `UnitSkillList`                                                             | Unit's known skills and per-turn usage tracking.                     |
| `SkillRegistry`, `SkillEffectRegistry`                                      | Skill lookup and effect factory.                                     |
| `SkillContext`, `ISkillEffect`, `IHealingSkillEffect`, `IAttackSkillEffect` | Skill execution context and effect contracts (`SkillRegistries.cs`). |
| `BuiltInSkillCatalog`                                                       | Registers built-in skill effects.                                    |


### UI (`Windy.Srpg.Game.UI` + global namespace)


| Class                     | Namespace  | Role                                                                                |
| ------------------------- | ---------- | ----------------------------------------------------------------------------------- |
| `GameplayInputController` | Game.UI    | Central input: hover, select/cancel/inspect, keyboard nav, double-select turn info. |
| `GameplayModalUI`         | Game.UI    | Modal stack; blocks gameplay input; keyboard navigation.                            |
| `GUIController`           | Game.UI    | Scene bootstrap: wires pre-battle UI + input to `CellGrid`.                         |
| `PreBattleUIController`   | Game.UI    | Pre-battle roster/deployment panels and board swap mode.                            |
| `CanvasClampManager`      | Game.UI    | Positions world-anchored UI on canvas.                                              |
| `UnitInspectPanelUI`      | Game.UI    | Unit inspection panel (S key).                                                      |
| `UnitInspectEntryListUI`  | Game.UI    | Scrollable inspect list with keyboard nav.                                          |
| `UnitHoverStripUI`        | Game.UI    | Compact hover stat strip.                                                           |
| `UnitStatsHoverDisplay`   | Game.UI    | Detailed hover stats panel.                                                         |
| `CombatSequenceUI`        | Game.UI    | Animated combat presentation overlay.                                               |
| `ExperienceGainHUD`       | Game.UI    | Post-combat XP display.                                                             |
| `LevelUpUI`               | Game.UI    | Level-up stat allocation.                                                           |
| `TurnCounterUI`           | *(global)* | Turn info modal; **double-select** to open; End Turn â†’ `RequestEndTurn()`.          |
| `ActionMenuUI`            | *(global)* | Pending-move action menu (`MoveAbility.IActionMenuUI`).                             |
| `AttackPreviewUI`         | Game.UI    | Combat preview panel during pending attack.                                         |
| `SkillMenuUI`             | Game.UI    | Skill picker during pending move.                                                   |
| `AreaConfirmUI`           | Game.UI    | Area-skill confirmation.                                                            |
| `InventoryMenuUI`         | *(global)* | Item use menu during pending move.                                                  |
| `TradeMenuUI`             | *(global)* | Unit-to-unit trade during pending move.                                             |
| `UIController`            | *(global)* | Legacy simple turn counter text display.                                            |

### Units (`Windy.Srpg.Game.Units`)

Unit gameplay is split across multiple files of the same `partial class Unit`. There is no separate `UnitHandler` component and no `unit.Handler` proxy layer anymore.

| File / type | Role |
|-------|------|
| `Unit.cs` | Core unit state: serialized stats/loadout, derived properties, events, pending-move state, editor setup, visual marking helpers, `Ensure*` helpers, and shared state consumed by other gameplay systems. |
| `Unit.Behavior.cs` | Main behavior implementation for the same `Unit` class: initialization, scene input handlers, save/preset application, combat flow, movement preview/commit, pathfinding, occupancy/grid binding, XP gain, and level-up sequencing. |
| `UnitPreset`, `UnitPresetOverride` | Template and per-instance overrides. |
| `UnitTurnStateKind` | Per-turn visual state enum. |
| `UnitDisplacementUtility` | Push/pull displacement helper. |
| `UnitCombatSupportTypes.cs` | Shared combat types such as `ResolvedAttackProfile`, `DamageChangeContext`, and combat hook interfaces. |
| `UnitExperienceSupportTypes.cs` | Shared progression types such as `ExperienceCalculator`, `ExperienceAwardResult`, `LevelUpPresentation`, and `LevelUpGainCalculator`. |
| `*EventArgs` in `UnitSceneEventArgs.cs` | Event payload types. |

**`Unit.cs` section map** (`#region CTRL+F:` blocks):

| Section marker | Contents |
|----------------|----------|
| Events / runtime state / serialized fields | Stats, loadout fields, turn-state flags, scene events, pending-move state |
| Loadout / equipment / stat modifiers | Weapons, accessories, buff/passive stat mods |
| Save serialization / preset helper data | Save capture helpers, preset-facing utility, identity helpers |
| Visual marking / editor setup | Turn tints, `MarkAs*`, `Reset()` ability/AI wiring |
| Movement / pending move preview / pathfinding | Shared pending-move structs and state accessors |

**`Unit.Behavior.cs`** is the operational side of the class. Call sites invoke behavior directly on `Unit`, for example `unit.Initialize()`, `unit.AttackHandler(...)`, `unit.PreviewMove(...)`, and `unit.GrantExperience(...)`.

### World UI (`Windy.Srpg.Game.UI` in `WorldUI/` folder)

| Class                      | Role                                                  |
| -------------------------- | ----------------------------------------------------- |
| `UnitWorldHealthBar`       | Per-unit world-space HP/MP bar.                       |
| `UnitWorldHealthBarSystem` | Creates/updates health bars for all registered units. |


### Scene scripts (`Assets/Scenes/`)


| Class        | Role                                                                   |
| ------------ | ---------------------------------------------------------------------- |
| `SampleUnit` | Default scene unit subclass; applies turn-state tint colors on `Unit`. |


---

## 6. Key interfaces and events

### Player

```csharp
interface IBattlePlayer {
    int PlayerId { get; }
    bool IsHumanControlled { get; }
    bool Owns(Unit unit);
}

interface IBattleTurnPlayer : IBattlePlayer {
    void BindToGrid(CellGrid grid);
    void PlayTurn(CellGrid grid);
}
```

### Battle flow adapters

```csharp
interface IBattleSceneUnitSource {
    IReadOnlyList<Transform> GetInitialUnitTransforms(CellGrid grid);
}
interface IBattleTurnResolver {
    RoundRobinTurnPlan ResolveStart(CellGrid grid);
    RoundRobinTurnPlan ResolveTurn(CellGrid grid);
}
interface IBattleEndCondition {
    BattleOutcome Evaluate(CellGrid grid);
}
```

### Ability hooks

**Grid-facing (public, called by `UnitSelectedState` / `CellGrid`):**

`InitializeAction(Unit)`, `DisplayAction(CellGrid)`, `CleanUpAction(CellGrid)`, `OnActionSelected/Deselected(CellGrid)`, `OnCellClicked/Highlighted/Dehighlighted(Cell, CellGrid)`, `OnUnitClicked/Highlighted/Dehighlighted(Unit, CellGrid)`, `OnTurnStarted/Ended(CellGrid)`, `OnOwnerDestroyed(CellGrid)`.

**Implementor-facing (protected overrides):**

`Display`, `CleanUp`, `OnAbilitySelected/Deselected`, `HandleCellClicked/Selected/Deselected`, `HandleUnitClicked/Highlighted/Dehighlighted`, `OnTurnStart/End`, `OnUnitDestroyed`, `CanPerformAbility`, `Act(CellGrid, bool isRemote)`.

**Execution:** `ExecuteAction(CellGrid)`, `AIExecute(CellGrid)`, `HumanExecute(CellGrid)`, `RemoteExecute(CellGrid)` â€” modes via `AbilityExecutionMode`.

### CellGrid events (selection)


| Event                                | When                                                 |
| ------------------------------------ | ---------------------------------------------------- |
| `PreBattleStateChanged`              | Pre-battle â†” battle transitions                      |
| `DeploymentRosterChanged`            | Roster edited                                        |
| `UnitAdded`                          | Unit registered to grid                              |
| `LevelInitialized` / `BattleStarted` | Framework ready / battle begun                       |
| `TurnStarted` / `BattleTurnEnded`    | Turn boundaries                                      |
| `BattleEnded`                        | Game over                                            |
| `EmptyCellHighlighted`               | Hover on empty cell (via internal cell hover wiring) |


### Unit events

**Gameplay:** `UnitHealthChanged`, `CombatDestroyed`, `DestroyedInCombat`, `UnitStatsChanged`, `UnitBuffsChanged`, `UnitProgressionChanged`, `GameplaySelected`, `GameplayDeselected`.

**Scene input:** `UnitClicked`, `UnitHighlighted`, `UnitDehighlighted`, `UnitSelected`, `UnitDeselected`, `UnitDestroyed`.

**Static presentation:** `CombatSequenceStarted/Ended`, `CombatCameraFocusRequested/Released`, `PreviewMoveCameraFollowRequested/Released`.

### Cell events

`Clicked`, `Hovered`, `Unhovered` â€” each `Action<Cell>`. Raised by mouse (when input not centralized) or by `RaiseSceneHighlightEvent()` from `GameplayInputController`.

---

## 7. Namespace map

```
Windy.Srpg.Game.Abilities      Ability, MoveAbility, AttackAbility, AbilityExecutionFlow
Windy.Srpg.Game.AI             AiDecisionAction, AiTurnRunner, AiTurnOrdering
Windy.Srpg.Game.AI.Actions     AIAction, AttackAIAction, MoveToPositionAIAction
Windy.Srpg.Game.AI.Evaluators  CellEvaluator, UnitEvaluator, damage variants
Windy.Srpg.Game.Buffs          buff data, runtime, registries
Windy.Srpg.Game.CameraControl  GameplayCameraController
Windy.Srpg.Game.Campaign       save data, manager, factory
Windy.Srpg.Game.Catalogs       JSON catalog DTOs
Windy.Srpg.Game.Grid           CellGrid, Cell, battle flow, deployment
Windy.Srpg.Game.Grid.States    CellGridState*
Windy.Srpg.Game.Inventory      items and UnitInventory
Windy.Srpg.Game.Localization   GameTextCatalog
Windy.Srpg.Game.Passives       passive data, runtime, registries
Windy.Srpg.Game.Pathfinding    IPathfinder, DijkstraPathfinder, GridPath
Windy.Srpg.Game.Pathfinding.Algorithms  DijkstraPathfinding
Windy.Srpg.Game.Players        Player hierarchy, IBattlePlayer
Windy.Srpg.Game.Players.AI     UnitSelection strategies
Windy.Srpg.Game.Skills         skill data, runtime, registries
Windy.Srpg.Game.UI             most UI controllers + world health bars
Windy.Srpg.Game.Units           Unit split across Unit.cs + Unit.Behavior.cs, presets, event args, combat support types, XP support types
(global)                       TurnCounterUI, ActionMenuUI, TradeMenuUI, InventoryMenuUI, UIController
```

---

## 8. Design notes for contributors

1. **Single grid owner** â€” Do not reintroduce a parallel grid or unit type. Extend `CellGrid` / `Unit` / `Cell` directly.
2. **Input** â€” Board clicks go through `GameplayInputController` when active; use `RaiseSceneHighlightEvent` / `HandleSceneUnitClicked`, not raw `OnMouseDown` side paths.
3. **Pending move** â€” Always preview first (`PreviewMove`), commit later (`ConfirmPendingMove`). Combat and menus use preview cell for range.
4. **Occupancy** â€” `Cell.CurrentUnits` + `Unit.RefreshCellOccupancy`; use `CellGrid.ResolveCanonicalCell` when binding units to tiles.
5. **Turn counter** â€” `CellGrid.RoundCount` increments when player 0's turn ends (`OnTurnEnded`).
6. **Turn info UI** â€” Double-select (0.35s) on finished friendly unit or empty waiting cell; single select performs normal grid click.
7. **Abilities** â€” One base class (`Ability`). Add new unit behaviors as `Ability` subclasses on the unit prefab; override protected hooks for reactive input or `Act()` for coroutine execution. Use `GetAbilities()`, not a separate action type.


