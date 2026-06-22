# Turn Based Strategy - Architecture Summary

Last reviewed: 2026-06-23

## Project Status

- The playable scene is now owned by code under `Assets/Game/Code`.
- The old `TBS Framework` folder is no longer part of the runtime dependency chain for this repo.
- The game still uses a scene/runtime split internally, but both layers now live in the same project codebase and are meant to be publishable together.
- The scene currently supports:
  - readable JSON catalog loading
  - readable CSV text loading
  - readable campaign save loading/writing
  - pre-battle roster selection and deployment swapping
  - turn-based combat with movement preview, action menus, items, trade, skills, area spells, inspect UI, EXP, and level-up

## Important Data Files

- Unified gameplay data:
  - `Assets/StreamingAssets/gdata.json`
- Text/localization table:
  - `Assets/StreamingAssets/game_text.csv`
- Persistent campaign save:
  - `Application.persistentDataPath/campaign_save.json`
- Friendly preset assets:
  - `Assets/Scenes/friendly preset/`
- Enemy preset assets:
  - `Assets/Scenes/enemy preset/`

## Code Layout

Main code root:

- `Assets/Game/Code/`

Main folders:

- `Abilities`
- `AI`
- `Buffs`
- `Camera`
- `Campaign`
- `Catalogs`
- `Diagnostics`
- `Grid`
- `Inventory`
- `Localization`
- `Passives`
- `Pathfinding`
- `Players`
- `Progression`
- `Skills`
- `UI`
- `Units`
- `WorldUI`

## Core Ownership Model

The project currently has two main battle layers:

1. Scene layer
   - Owns Unity scene objects, authored references, deployment slots, visible units, scene UI, and scene state transitions.
   - Main type:
     - `Assets/Game/Code/Grid/CellGrid.cs`
     - plus partials:
       - `CellGrid.Scene.cs`
       - `CellGrid.PreBattle.cs`
       - `CellGrid.Runtime.cs`

2. Runtime layer
   - Owns runtime turn order, runtime state machine, mirrored unit/cell collections, and battle outcome evaluation.
   - Main type:
     - `Assets/Game/Code/Grid/RuntimeGrid.cs`

That means:

- `CellGrid` is the Unity host and orchestration shell.
- `RuntimeGrid` is the runtime battle machine.
- `Unit` is the scene/gameplay unit aggregate.
- `GridUnit` is the runtime mirror for unit state that the runtime grid reads/writes.

## Main Entry Points

### Scene bootstrap

- `Assets/Game/Code/Grid/CellGrid.Scene.cs`

Main startup path:

1. `CellGrid.Awake()`
   - `EnsureSceneCellAnchors()`
   - `PrepareFriendlyDeploymentFromSave()`
   - `ResolveRuntimeGrid()`
   - `WireLegacyGridEvents()`
   - `SubscribeToExistingCells()`
2. `CellGrid.Start()`
   - if pre-battle is enabled:
     - initialize scene without starting battle
     - show deployment slots
     - enter blocked input state
   - otherwise:
     - initialize battle scene
     - start battle via runtime grid

### Runtime battle start

- `CellGrid.StartBattleViaRuntimeGrid()`

Main work:

1. `SyncRuntimeMirrorNow()`
2. `RoundRobinBattleFlow.ResolveStart(this)`
3. `SyncBattleStartFromPlan(...)`
4. `PrepareRuntimeTurnStartForPlan(...)`
5. `runtimeGrid.BeginBattleFromHost(...)`
6. `ApplyRuntimeTurnStartToScenePlayableUnits()`
7. `runtimeGrid.KickCurrentTurnPlay()`

## Scene Layer Details

### `CellGrid`

Main responsibilities:

- initialize scene battle objects
- load and apply pre-battle deployment from save
- own the scene state machine (`CellGridState`)
- mirror scene state into `RuntimeGrid`
- mirror runtime decisions back into the scene
- route scene clicks/hover into runtime when direct runtime scene input is active
- handle campaign save staging/flushing
- track deployment slot selection and visibility
- rebuild occupancy and evaluate battle end conditions

Important fields in `CellGrid.cs`:

- `currentState`
  - active scene state machine state
- `runtimeGrid`
  - paired runtime battle machine
- `battleStarted`
  - true after pre-battle ends and combat begins
- `selectedPreBattleDeploymentSlotIndex`
  - currently selected deployment slot in swap mode
- `selectedPreBattleDeploymentUnit`
  - unit currently selected for slot swap mode
- `stagedDeploymentRosterUnitIds`
  - working deployment roster for this scene before save commit
- `hasUnsavedDeploymentRosterChanges`
  - whether pre-battle roster differs from saved roster
- `cachedCampaignSave`
  - in-memory working copy of the save
- `campaignSaveDirty`
  - delayed save flush flag
- `occupancyRevision`
  - increments when occupancy changes so path caches can invalidate
- `suppressSceneToRuntimeStateMirror`
  - prevents scene/runtime feedback loops while applying runtime-driven scene state

Important booleans:

- `enablePreBattleUi`
- `startBattleImmediatelyWithCurrentRoster`
- `autoCreateOwnedUnitSaveIfMissing`
- `overwriteOwnedUnitSaveOnGameStarted`
- `ShouldRouteHumanMovementThroughRuntime`
- `ShouldRouteAiMovementThroughRuntime`
- `ShouldRouteTurnLoopThroughRuntime`
- `ShouldRouteBattleOutcomeThroughRuntime`
- `UsesRuntimeDirectSceneInput`

### `CellGrid.PreBattle`

Main responsibilities:

- seed/load owned units from save
- resolve roster against deployment slots
- stage roster edits without immediately overwriting the save
- apply friendly deployment into scene placeholders
- swap deployment slot contents during pre-battle
- compact/normalize roster for save use

Important methods:

- `PrepareFriendlyDeploymentFromSave()`
- `LoadSeededCampaignSave()`
- `GetDeploymentRosterForPreBattle()`
- `GetPreBattleDeploymentSlotCells()`
- `GetPreferredPreBattleDeploymentCell()`
- `HandlePreBattleDeploymentSlotClicked(...)`
- `ReplaceDeploymentSlotUnit(...)`
- `ClearDeploymentSlotUnit(...)`
- `SaveDeploymentRosterChanges()`

### `CellGrid.Runtime`

Main responsibilities:

- keep `RuntimeGrid` collections in sync with scene units/cells/players
- translate scene states into runtime states
- translate runtime state back to scene state when needed
- route human scene input directly into runtime-side handlers
- process runtime-owned end-turn, pending-move, combat, and battle-outcome flow

Important fields:

- `runtimeGrid`
- `runtimeGridCollectionsDirty`

Important methods:

- `SyncRuntimeMirrorNow()`
- `RefreshRuntimeGridCollections()`
- `UpdateRuntimeGridMetadata()`
- `ApplyRuntimeDrivenState(...)`
- `MirrorSceneStateToRuntimeGrid(...)`
- `ProcessRuntimeRoutedEndTurn()`
- `ProcessRuntimeRoutedBattleOutcomeEvaluation()`

## Runtime Layer Details

### `RuntimeGrid`

Main responsibilities:

- own runtime battle state machine (`RuntimeGridState`)
- own current player index
- decide turn transitions with `RoundRobinBattleFlow`
- drive human/AI runtime states
- expose runtime-side click/hover handlers
- maintain mirrored collections of cells, units, and players

Important fields:

- `cells`
- `units`
- `players`
- `currentState`
- `currentPlayerIndex`
- `battleStarted`
- `sceneInputEnabled`
- `SceneInputCoordinator`

Important methods:

- `BeginBattleFromHost(...)`
- `SetState(...)`
- `EndCurrentTurn(...)`
- `EvaluateBattleOutcome()`
- `KickCurrentTurnPlay()`
- `SetMirroredCollections(...)`
- `ProcessSceneRightClick()`
- `ConfirmPendingMoveWait()`
- `ConfirmPendingMoveAfterCombat(...)`

### `RoundRobinBattleFlow`

- `Assets/Game/Code/Grid/RoundRobinBattleFlow.cs`

Owns:

- start-turn resolution
- next-player resolution
- “last side standing” win evaluation
- lightweight shared query helpers through `GridQueries`

Important types:

- `RoundRobinTurnPlan`
- `BattleOutcome`

## Unit Architecture

### `Unit`

Main file cluster:

- `Assets/Game/Code/Units/Unit.cs`
- `Assets/Game/Code/Units/Unit.SceneBinding.cs`
- `Assets/Game/Code/Units/Unit.CombatAndMovement.cs`
- `Assets/Game/Code/Units/Unit.ExperienceFlow.cs`

`Unit` is still one of the biggest aggregates in the project.

Responsibilities:

- authored/base stats
- resolved preset + override data
- save identity (`unitId`, `visualId`)
- current HP/MP/level/EXP
- inventory / skills / passives / buffs
- combat stats and combat sequencing
- turn state
- scene/runtime mirror sync through `GridUnit`
- occupancy bookkeeping

Important fields in `Unit.cs`:

- `currentTurnStateKind`
- `cachedPaths`
- `baseHitPoints`, `baseManaPoints`, `baseStrength`, etc.
- `unitId`
- `visualId`
- `preset`
- `presetOverride`
- `startingInventory`
- `startingSkills`
- `startingUniquePassives`
- `startingEquipPassives`
- `resolvedStartingInventory`
- `resolvedStartingSkills`
- `resolvedStartingUniquePassives`
- `resolvedStartingEquipPassives`
- `pendingOwnedUnitSaveData`
- `pendingOwnedUnitVisualPreset`

Important runtime-facing fields in `Unit.SceneBinding.cs`:

- `Cell`
- `MovementPoints`
- `movementPointsStorage`
- `excludedFromBattle`

Important sync methods:

- `SyncMirroredRuntimeNow()`
- `PullRuntimeStateToScene(...)`
- `PrepareRuntimeForTurnStart()`
- `ApplySceneTurnStartFromRuntime()`
- `ApplySceneSyncFromRuntimeMoveCommit(...)`
- `RegisterCellOccupancyList(...)`
- `UnregisterCellOccupancyList(...)`
- `RefreshCellOccupancy(...)`

### `GridUnit`

- `Assets/Game/Code/Units/GridUnit.cs`

Responsibilities:

- runtime-readable unit id / player id / movement points
- runtime turn state
- runtime pending move storage
- runtime pathfinding cache
- occupancy registration against runtime cell occupant lists
- runtime click / hover / select events

Important fields:

- `unitId`
- `playerId`
- `baseMovementPoints`
- `CurrentCell`
- `MovementPointsRemaining`
- `pendingMove`
- `cachedPaths`
- `turnState`
- `Grid`

## Action / Ability Architecture

### `BattleAction`

- `Assets/Game/Code/Abilities/BattleAction.cs`

This is the generic runtime action contract.

Main responsibilities:

- define the shared action lifecycle:
  - initialize
  - can perform
  - execute
  - display
  - cleanup
  - selection/deselection
  - unit/cell callbacks
  - turn start/end callbacks

### `Ability`

- `Assets/Game/Code/Abilities/Ability.cs`

This is the scene-facing adapter on top of `BattleAction`.

Main responsibilities:

- convert generic `IGridContext` / `IGridUnit` calls into strongly typed `CellGrid` / `Unit`
- provide separate execution paths for:
  - human local
  - AI local
  - remote invocation
- centralize inline execution flow through `BattleActionExecutionFlow`

### `MoveAbility`

Main file cluster:

- `Assets/Game/Code/Abilities/MoveAbility.cs`
- `Assets/Game/Code/Abilities/MoveAbility.PendingActions.cs`

This is currently one of the largest “god class” areas in the project.

Responsibilities:

- movement destination/path preview
- pending move shell
- action menu
- attack preview
- skill targeting
- area spell targeting and confirm UI
- inventory/trade UI integration
- pending action cleanup

Important fields:

- `Destination`
- `currentPath`
- `availableDestinations`
- `awaitingAttackTargetSelection`
- `awaitingSkillTargetSelection`
- `awaitingTradeTargetSelection`
- `selectedAttackPreviewTarget`
- `selectedSkillPreviewTarget`
- `selectedTargetingSkill`
- `selectedAreaSkillCenterCell`
- `cachedOccupancyRevision`

Supporting UI contracts in this file:

- `IActionMenuUI`
- `IAttackPreviewUI`
- `ISkillMenuUI`
- `IAreaConfirmUI`
- `IInventoryMenuUI`
- `ITradeMenuUI`

## Input Architecture

### `GameplayInputController`

- `Assets/Game/Code/UI/GameplayInputController.cs`

This is now the central gameplay input coordinator.

Responsibilities:

- detect active control scheme:
  - mouse
  - keyboard
  - controller placeholder
- own hover/focus tile state
- own keyboard UI navigation
- own focus color application to active buttons
- dispatch select / cancel / inspect / range-toggle commands
- block gameplay tile input while modal gameplay UI is active

Important fields:

- `currentScheme`
- `hoveredCell`
- `hoveredUnit`
- `keyboardHoveredCell`
- `repeatingDirectionKey`
- `nextRepeatTime`
- `collectiveEnemyRangeVisible`
- `enemyRangeToggles`

Important behavior:

- in keyboard mode, `keyboardHoveredCell` is treated as the active hover tile and camera focus anchor
- in mouse mode, `hoveredCell` is driven by cursor hover
- when keyboard UI navigation is active, gameplay hover movement is suppressed and button focus owns the arrows

## UI Architecture

Key gameplay UI hosts:

- `PreBattleUIController`
- `GameplayInputController`
- `UIController`
- `TurnCounterUI`
- `CombatSequenceUI`
- `UnitInspectPanelUI`
- `LevelUpUI`

### `PreBattleUIController`

- `Assets/Game/Code/UI/PreBattleUIController.cs`

Responsibilities:

- show root pre-battle menu
- open/close `Select Units` and `Switch Deployment`
- keep save button state and roster status text updated
- rebuild selectable owned-unit buttons
- leave switch-deployment interaction to scene board slots rather than a generated button list

Important field:

- `preferredSelectUnitId`
  - keeps keyboard/button focus on the same unit after a roster toggle

### `UnitInspectPanelUI`

- `Assets/Game/Code/UI/UnitInspectPanelUI.cs`

Responsibilities:

- show detailed unit inspect panel
- handle row focus / entry selection
- drive inspect descriptions for inventory, skills, passives, and stats
- cooperate with keyboard UI navigation

### `LevelUpUI`

- `Assets/Game/Code/UI/LevelUpUI.cs`

Responsibilities:

- show pending level-up stat gains
- own row highlight state and confirm button focus
- expose preferred keyboard focus for the current level-up step

## Save / Data / Text Architecture

### Save

Files:

- `Assets/Game/Code/Campaign/CampaignSaveData.cs`
- `Assets/Game/Code/Campaign/CampaignSaveFactory.cs`
- `Assets/Game/Code/Campaign/CampaignSaveManager.cs`

Responsibilities:

- define readable JSON save payload
- seed starter owned units from presets
- flatten scene units back into owned-unit save data
- normalize deployment roster
- write/read `campaign_save.json`

Important save payload areas:

- `OwnedUnits`
- `DeploymentRosterUnitIds`
- `StorageItems`
- `Gold`

### Catalogs

Files:

- `Assets/Game/Code/Catalogs/JsonCatalogLoader.cs`

Responsibilities:

- load `gdata.json`
- parse unified item/skill/passive/buff catalog blocks
- convert raw catalog entries into runtime definitions

### Text

Files:

- `Assets/Game/Code/Localization/GameTextCatalog.cs`

Responsibilities:

- load `game_text.csv`
- resolve strings by key
- support fallback values
- support simple language column switching
- allow scene-authored TMP text to override code-provided defaults when desired

## Current Flow Summaries

### Scene start to battle start

1. `CellGrid.Awake()`
2. `PrepareFriendlyDeploymentFromSave()`
3. `CampaignSaveManager.Load()`
4. `CampaignSaveFactory.EnsureStarterOwnedUnits(...)`
5. roster resolved against current deployment slot count
6. friendly placeholders populated from save
7. `CellGrid.Start()`
8. either:
   - stay in pre-battle UI
   - or call `StartBattleViaRuntimeGrid()`

### Human selection/movement flow

1. `GameplayInputController` decides current control scheme
2. select command resolves hovered unit or hovered cell
3. `CellGrid` enters selected or waiting state
4. `MoveAbility` computes destinations/path preview
5. pending move confirm state opens action menu or wait flow
6. runtime mirror is synced before/after combat and movement commits

### Turn end flow

1. UI calls `CellGrid.RequestEndTurn()`
2. `CellGrid.ProcessRuntimeRoutedEndTurn()`
3. `RuntimeGrid.EndCurrentTurn(...)`
4. `RoundRobinBattleFlow.ResolveTurn(...)`
5. `RuntimeGrid.BeginCurrentTurn(...)`
6. `CellGrid.CommitTurnTransition(...)`
7. scene turn hooks, occupancy refresh, and UI update fire

### Battle outcome flow

1. unit death or turn transition calls `RequestBattleOutcomeEvaluation()`
2. `CellGrid.ProcessRuntimeRoutedBattleOutcomeEvaluation()`
3. `RuntimeGrid.EvaluateBattleOutcome()`
4. `RoundRobinBattleFlow.EvaluateLastSideStanding(...)`
5. `CellGrid` applies scene-side game-over state when finished

## Audit Notes

### Biggest remaining hotspots

1. `Unit` is still a very large aggregate.
   - The partial split helps a little, but conceptually it still owns stats, combat, inventory, save identity, runtime sync, occupancy, and progression.

2. `MoveAbility` is still a very large aggregate.
   - `MoveAbility.PendingActions.cs` in particular contains many UI + targeting + pending-state responsibilities in one place.

3. `CellGrid` is still a large orchestration hub.
   - The partial split is sensible, but it remains the highest-level coordinator for scene state, runtime sync, deployment, save, and battle lifecycle.

### What was cleaned in this pass

- Removed stale switch-deployment generated-button behavior from `PreBattleUIController`.
- Kept switch deployment board-driven instead of list-driven.
- Added boundary comments to the main cross-layer types:
  - `CellGrid`
  - `RuntimeGrid`
  - `Ability`
  - `GameplayInputController`
  - `PreBattleUIController`
  - `Unit`
  - `Unit.SceneBinding`

### What still deserves a future cleanup pass

- split `MoveAbility.PendingActions.cs` by targeting mode or UI responsibility
- reduce `Unit` size by moving more pure save/preset loadout code out of the main class
- keep collapsing old “bridge” naming where the scene/runtime boundary is already stable
- review duplicate UI ownership between:
  - `UIController`
  - `TurnCounterUI`
  - other modal gameplay UI roots

## Current Mental Model

If you are reading the project fresh, start with these files in this order:

1. `Assets/Game/Code/Grid/CellGrid.cs`
2. `Assets/Game/Code/Grid/CellGrid.Scene.cs`
3. `Assets/Game/Code/Grid/CellGrid.PreBattle.cs`
4. `Assets/Game/Code/Grid/CellGrid.Runtime.cs`
5. `Assets/Game/Code/Grid/RuntimeGrid.cs`
6. `Assets/Game/Code/Units/Unit.cs`
7. `Assets/Game/Code/Units/Unit.SceneBinding.cs`
8. `Assets/Game/Code/Abilities/BattleAction.cs`
9. `Assets/Game/Code/Abilities/Ability.cs`
10. `Assets/Game/Code/Abilities/MoveAbility.cs`
11. `Assets/Game/Code/Abilities/MoveAbility.PendingActions.cs`
12. `Assets/Game/Code/UI/GameplayInputController.cs`
13. `Assets/Game/Code/UI/PreBattleUIController.cs`
