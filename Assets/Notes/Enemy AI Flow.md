# Enemy AI Flow

This document describes the current code path for enemy AI, from turn start through movement and action selection.

Main implementation files:

- `Assets/Game/Code/Players/AiPlayer.cs`
- `Assets/Game/Code/Players/AiBattlePlayerController.cs`
- `Assets/Game/Code/AI/AiDecisionAction.cs`
- `Assets/Game/Code/AI/MoveToPositionAIAction.cs`
- `Assets/Game/Code/AI/AttackAIAction.cs`
- `Assets/Game/Code/AI/AiBehaviorUtility.cs`
- `Assets/Game/Code/AI/AiCombatPlanner.cs`
- `Assets/Game/Code/AI/Evaluators/DamageCellEvaluator.cs`
- `Assets/Game/Code/Units/UnitPreset.cs`

## Big Picture

Enemy AI is component-driven.

Each AI-controlled unit has child components called `AiDecisionAction`s. The default unit setup creates a `Brain` child object with:

- `MoveToPositionAIAction`
- `AttackAIAction`
- `DamageCellEvaluator`
- `DamageUnitEvaluator`

The current default flow is:

```text
AI player turn starts
-> choose all current-player units in an order
-> for each unit
   -> run MoveToPositionAIAction if it should execute
   -> run AttackAIAction if it should execute
-> when all units are processed, request end turn
```

The AI does not have one giant "enemy brain" class. The turn runner only says, "for each unit, run its decision components." The movement decision and attack decision each decide whether they should do anything.

## AI Turn Entry

The battle loop eventually calls `IBattleTurnPlayer.PlayTurn(CellGrid grid)` for the current player.

For AI, there are two related entry points:

- `AiPlayer.Play(CellGrid cellGrid)`
- `AiBattlePlayerController.PlayTurn(CellGrid grid)`

`AiPlayer` is the main behavior. It:

1. Calls `cellGrid.EnterAiTurnState(this)`.
2. Builds an ordered list of units with `SelectUnits(cellGrid)`.
3. Starts `AiTurnRunner.ExecuteTurn(...)`.
4. Passes `cellGrid.RequestEndTurn()` as the completion callback.

`AiBattlePlayerController` is a wrapper style controller that also finds the local `AiPlayer`, enters AI state, selects units, then calls the same `AiTurnRunner`.

## AI State

The grid enters `CellGridStateAiTurn`.

This state mainly blocks normal human input and provides debug behavior:

- It can store cell debug info from movement scoring.
- It can store unit debug info from combat plan scoring.
- In debug mode, clicking a scored cell/unit can print its AI metadata.

The actual decision-making is not inside `CellGridStateAiTurn`; it is inside the AI decision components.

## Unit Turn Ordering

`AiPlayer.SelectUnits()` chooses which units act this turn.

If the `AiPlayer` GameObject has a custom `UnitSelection` component, it uses that component.

If there is no custom selector, it uses:

```csharp
AiTurnOrdering.OrderByMovementFreedom(...)
```

That orders units by how many traversable neighbor cells their current cell has. Units with more immediate movement freedom act earlier.

This is a deterministic ordering based on the current board and LINQ ordering. It does not currently do tactical ordering like "healers first" or "finish kills first."

## Decision Runner

`AiTurnRunner.ExecuteTurn(...)` is the shared per-turn executor.

For each ordered unit:

1. It gets all child `AiDecisionAction` components with:

```csharp
unit.GetComponentsInChildren<AiDecisionAction>(true)
```

2. For each decision action, in Unity component order:

```text
InitializeDecision(...)
ShouldExecute(...)
if should execute:
    Precalculate(...)
    ExecuteDecision(...)
CleanUpDecision(...)
```

3. After every unit has run every decision action, it invokes `onTurnCompleted`, which normally calls:

```csharp
cellGrid.RequestEndTurn()
```

The default order is important: movement usually happens before attacking because `Unit.Reset()` adds `MoveToPositionAIAction` before `AttackAIAction`.

## AI Mode Data

Enemy AI mode is stored on `UnitPreset`.

```csharp
public UnitActionAiMode ActionAiMode = UnitActionAiMode.Attack;
public UnitMovementAiMode MovementAiMode = UnitMovementAiMode.Move;
public int WaitGroupId = 0;
```

### Action Modes

`UnitActionAiMode` currently has:

- `Attack`
- `Heal`

`Attack` means the planner builds offensive plans.

`Heal` means the planner first tries to build healing plans for allies. If it finds any valid healing plan, it uses healing only. If no healing plan exists, it falls back to normal attack planning.

### Movement Modes

`UnitMovementAiMode` currently has:

- `Move`
- `Wait`
- `WaitGroup`
- `NotMove`

Movement and action are both gated by `AiBehaviorUtility`.

`Move`:

- Movement is allowed.
- Action is allowed.

`NotMove`:

- Movement is not allowed.
- Action is still allowed.
- The preset application also sets `MovementPoints = 0`, so displayed range reflects that this unit should not move.

`Wait`:

- The unit stays inactive until triggered.
- It triggers if it can make an offensive plan from any reachable cell.
- It also triggers permanently if it takes damage.

`WaitGroup`:

- The unit belongs to a linked waiting group by `WaitGroupId`.
- If there is only one living unit in the group, it behaves like `Wait`.
- If there are multiple living units in the group, at least two group units must be able to make an offensive plan from reachable cells.
- Once that condition is true, all units in that wait group are triggered.
- If any unit in the group takes damage, all matching living group units are triggered.

## Wait AI Trigger Details

The important methods are:

- `AiBehaviorUtility.ShouldAllowMovement(...)`
- `AiBehaviorUtility.ShouldAllowAction(...)`
- `AiBehaviorUtility.EnsureWaitTriggered(...)`
- `AiBehaviorUtility.EnsureWaitGroupTriggered(...)`
- `Unit.WakeWaitingAiOnDamage()`

For a `Wait` unit, triggering asks:

```text
Can this unit make any offensive plan from any reachable destination?
```

That is checked by:

```csharp
AiCombatPlanner.HasAnyOffensivePlanFromReachableCells(...)
```

There is a subtle movement rule:

```text
If the unit is triggered but already has an offensive plan from its current cell,
it does not move.
```

That prevents a waiting enemy from stepping away when the player is already in its current attack range. The later attack decision can still execute.

For a `WaitGroup`, the group trigger checks all allied units that:

- are alive,
- are not excluded from battle,
- have the same `WaitGroupId`,
- have `MovementAiMode == WaitGroup`.

If two or more matching units can threaten from reachable cells, the whole group wakes.

## Movement Decision

Movement is handled by `MoveToPositionAIAction`.

### Initialize

`InitializeAction(...)` calls:

```csharp
unit.GetComponent<MoveAbility>()?.OnActionSelected(cellGrid);
```

Then it prepares dictionaries for scoring every cell on the board.

### ShouldExecute

Movement only runs if:

- the unit exists,
- the unit has `MoveAbility`,
- the unit has a current cell,
- `AiBehaviorUtility.ShouldAllowMovement(...)` returns true.

If movement is blocked by AI mode, it sets `topDestination` to the current cell and returns false.

Then it gathers every `CellEvaluator` component on the same GameObject as the movement action. The default evaluator is `DamageCellEvaluator`.

For every cell on the board:

```text
score = sum(evaluator.Evaluate(cell) * evaluator.Weight)
```

The result is stored in `cellScoresDict`.

After all cells are scored, the action restricts candidates to:

```csharp
unit.GetAvailableDestinations(allCells)
```

This is important: the evaluator scores the whole map, but the movement decision can only select reachable destinations.

### Current Default Cell Evaluator

`DamageCellEvaluator` asks:

```csharp
AiCombatPlanner.EvaluateBestPlanScore(unit, player, grid, candidateCell)
```

In plain English:

```text
How good is the best combat or healing action this unit could take if it stood on this cell?
```

That means movement is combat-aware. It is not simply "move toward nearest player unit." It tries to move to a cell where the unit's best available plan has the best score.

### Choosing a Destination

After reachable cells are scored:

1. Pick the reachable cell with the highest score.
2. Compare it to the score of the unit's current cell.
3. If the best reachable score is higher than the current cell score, move toward that best cell.
4. If not, try a fallback pursuit destination.
5. If neither is useful, do not move.

The fallback pursuit logic searches for any standable cell that would allow a plan, then chooses:

1. the lowest path cost,
2. then the highest attack score as a tiebreaker.

This lets a unit advance toward an attack position even if the immediate reachable score does not beat its current score.

### Path Trimming

During `Precalculate(...)`, the action finds a path to `topDestination`.

It walks the path until movement points run out. If `ShouldMoveAllTheWay` is true, it chooses the furthest reachable cell along that path. If `ShouldMoveAllTheWay` is false, it chooses the highest-scoring cell along the reachable part of the path.

Default:

```csharp
public bool ShouldMoveAllTheWay = true;
```

### Movement Execution

`Execute(...)` sets:

```csharp
moveAbility.Destination = topDestination;
```

Then it runs:

```csharp
moveAbility.AIExecute(cellGrid)
```

For AI, `MoveAbility.Act(...)` usually performs immediate movement:

1. Resolve the destination as a canonical grid cell.
2. Verify the unit can move there.
3. Find a path.
4. Call `Unit.Move(destination, path)`.
5. Let the base ability flow continue.

## Action Selection

Action selection is handled by `AttackAIAction`.

Despite the name, it handles more than weapon attacks. It can execute:

- weapon attacks,
- single-target offensive skills,
- area offensive skills,
- single-target healing skills,
- area healing skills.

### ShouldExecute

`AttackAIAction` only runs if:

- the unit exists,
- the grid exists,
- the unit can start an action this turn,
- `AiBehaviorUtility.ShouldAllowAction(...)` returns true,
- `AiCombatPlanner.HasAnyPlan(unit, player, grid, unit.Cell)` is true.

This action only considers the unit's current cell. Since movement already ran before attack, the "current cell" is normally the post-move position.

### Precalculate

`AttackAIAction.Precalculate(...)` calls:

```csharp
AiCombatPlanner.TryFindBestPlan(unit, player, cellGrid, unit.Cell, out selectedPlan)
```

The selected plan is cached and used during execution.

### Execute

The selected plan has a kind:

```csharp
AiCombatActionKind.WeaponAttack
AiCombatActionKind.Skill
AiCombatActionKind.AreaSkill
```

Weapon attack:

1. Equip the selected weapon entry if needed.
2. Call `unit.AttackHandler(selectedPlan.PrimaryTarget)`.
3. Wait for combat presentation to finish.

Single-target skill:

1. Use `MoveAbility.ExecuteAiSkill(...)`.
2. This uses the same skill execution path as the player.
3. It confirms pending move as part of the skill flow.

Area skill:

1. Use `MoveAbility.ExecuteAiAreaSkill(...)`.
2. Show a short telegraph highlight if configured.
3. Execute the area skill through the same area-skill path as the player.

After an action, the AI waits briefly:

```csharp
yield return new WaitForSeconds(0.15f);
```

## Combat Planner

`AiCombatPlanner` is the current action-selection brain.

The most important method is:

```csharp
TryFindBestPlan(Unit actor, Player player, CellGrid grid, Cell actingCell, out AiCombatPlan plan)
```

It builds every legal plan from `actingCell`, scores each plan, then chooses the highest score.

### Plan Types

The planner builds:

- weapon attack plans,
- single-target skill attack plans,
- area skill attack plans,
- healing plans when `ActionAiMode == Heal`.

### Heal Mode

If `ActionAiMode == Heal`:

1. Build healing plans for allied units.
2. If any healing plan exists, use only those healing plans.
3. If no healing plan exists, fall back to offensive plans.

Single-target healing:

- target must be an ally,
- target cannot be the actor,
- skill targeting must be `AllyUnit` or `AnyUnit`,
- target must be in range,
- healing amount must be greater than 0.

Area healing:

- area must affect at least one ally,
- it must heal another ally, not only the caster,
- total healing amount must be greater than 0.

### Weapon Attack Plans

For every living enemy, the planner asks the actor:

```csharp
actor.GetWeaponsThatCanAttack(enemy, actingCell)
```

For each legal weapon, it calculates:

- number of hits,
- pursuit attack doubling,
- per-hit normal damage,
- per-hit crit damage,
- hit chance,
- crit chance,
- expected damage,
- whether normal damage kills,
- whether the defender can counterattack.

### Skill Plans

Skill plans start from:

```csharp
actor.SkillList?.Entries
```

A skill must pass:

```csharp
actor.CanUseSkill(skill)
```

Offensive single-target skills require:

- `AttackProfile.Enabled`,
- targeting type `EnemyUnit` or `AnyUnit`,
- target in skill range,
- skill effect, if present, must be usable.

Area offensive skills require:

- `AreaProfile.Enabled`,
- `AttackProfile.Enabled`,
- `AreaProfile.AffectsEnemies`.

Combat arts are handled specially. The planner checks every carried weapon that matches the combat art's required weapon type, builds a profile for each valid weapon, then chooses the weapon with the highest expected damage.

Non-combat-art attack skills build a profile from:

- actor `Magic` or `Strength`,
- skill might,
- actor speed-based accuracy,
- actor luck-based crit,
- skill hit count,
- skill counter-prevention flag.

Skill effects can also modify the attack profile through `IAttackSkillEffect`.

### Area Skill Targeting

The planner first finds possible center cells.

For line areas:

- it checks straight directions from the acting cell,
- it walks from min range to max range,
- it stops a direction when it runs off the board.

For non-line areas:

- it uses all cells within min/max range from the acting cell.

Then it computes affected cells from the area shape and radius.

Targets are filtered by:

- alive units only,
- self immunity,
- ally/enemy area flags,
- skill effect `CanUse(...)` if the skill has an effect id.

### Scoring

Every plan starts with:

```text
score = max(0, expectedDamageOrHealing)
```

Then modifiers are multiplied in:

```text
if projects kill:      score *= 10
if avoids counter:     score *= 1.5
if costs no MP:        score *= 1.1
```

These constants are in `AiCombatPlanner`:

```csharp
KillBoost = 10f
SafeBoost = 1.5f
NoMpBoost = 1.1f
```

For healing plans, `expectedDamage` is actually expected healing. The kill boost is not used for healing because healing plans set `projectsKill` to false.

### Tie Breaking

The planner finds the highest score, then gathers every plan within:

```csharp
ScoreTieTolerance = 0.0001f
```

By default, ties are deterministic:

```text
pick the first top option
```

There is support for random tie-breaking through the `breakTiesRandomly` parameter, but the current default calls pass `false`.

The practical tie order comes from plan-building order:

1. healing plans first, if in heal mode and any healing is possible,
2. weapon attacks,
3. skills in skill-list order,
4. targets in grid/unit list order,
5. area centers in candidate-cell order.

## Movement And Action Interaction

The AI movement and action phases are separate, but movement scoring uses action planning.

Movement asks:

```text
If I stood on this cell, how good would my best action be?
```

Then action asks:

```text
From the cell I actually ended on, what is my best action?
```

This produces the common behavior:

1. Enemy evaluates possible attack/heal positions.
2. Enemy moves to the reachable position that enables the best plan.
3. Enemy attacks, uses a skill, heals, or waits if no plan is available.

If a waiting enemy is triggered and already has an offensive plan from its current tile, it skips movement and attacks from there.

## Debug Mode

`AiPlayer` has a serialized `debugMode` flag.

When debug mode is off, the AI runs automatically.

When debug mode is on:

1. Each unit is selected and the console says to press `N`.
2. Each decision action pauses and the console says to press `A`.
3. Movement debug colors cells according to score.
4. Attack debug highlights the chosen target and stores target metadata.

`CellGridStateAiTurn` lets debug clicks print stored metadata for cells or units.

## Current Dormant/Legacy Hooks

`DamageUnitEvaluator` still exists and is still added by `Unit.Reset()`, but the current default `AttackAIAction` does not use `UnitEvaluator` components. Target/action choice goes through `AiCombatPlanner`.

`DamageCellEvaluator` is active because `MoveToPositionAIAction` asks for `CellEvaluator` components.

If you want custom target selection outside the planner, you would either:

- modify `AiCombatPlanner`,
- write a new `AiDecisionAction`,
- or revive/use `UnitEvaluator` in a custom action.

## Where To Change Behavior

To change unit order:

- Add or edit a `UnitSelection` component on the `AiPlayer`.
- Default code: `AiTurnOrdering.OrderByMovementFreedom`.

To change movement goals:

- Add/edit `CellEvaluator` components on the AI brain object.
- Default active evaluator: `DamageCellEvaluator`.
- Change path commitment behavior in `MoveToPositionAIAction`.

To change attack/heal scoring:

- Edit `AiCombatPlanner.BuildPlan(...)`.
- Main knobs are `KillBoost`, `SafeBoost`, and `NoMpBoost`.

To add a new tactical decision:

- Create a new component inheriting `AiDecisionAction` or `AIAction`.
- Put it on the unit's `Brain` object.
- Its component order relative to `MoveToPositionAIAction` and `AttackAIAction` determines when it runs.

To change wait behavior:

- Edit `AiBehaviorUtility`.
- Damage wake-up is in `Unit.WakeWaitingAiOnDamage()`.

To configure a scene enemy:

- Set `ActionAiMode`, `MovementAiMode`, and `WaitGroupId` on its `UnitPreset`.
- For placed scene enemies, use the map painter/preset override fields where available.

