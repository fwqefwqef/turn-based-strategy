# Public Runtime Spec

Last updated: 2026-06-09

## Goal

Define the fresh runtime that the public Unity project will ship with.

This specification is written from game-owned behavior requirements, not from vendor source.

## Current Scene Target

The first rewrite target is the active battle scene:

- `Assets/Scenes/test.unity`

The replacement runtime only needs to support the behavior that this scene and the current game-owned scripts require.

## Game-Owned Integration Points

The runtime must support the needs of these game-owned scripts:

- `Assets/Game/Scripts/Grid/CellGrid.cs`
- `Assets/Game/Scripts/Units/Unit.cs`
- `Assets/Game/Scripts/Abilities/Ability.cs`
- `Assets/Game/Scripts/Grid/States/*.cs`
- `Assets/Game/Scripts/AI/*.cs`
- `Assets/Game/Scripts/UI/*.cs`
- `Assets/Scenes/SampleUnit.cs`
- `Assets/Scenes/SampleSquare.cs`

## Fresh Runtime Naming

These names are the public rewrite target:

- board manager: `BattleBoard`
- board cell: `BoardCell`
- square cell: `SquareBoardCell`
- runtime unit: `BattleUnit`
- runtime action: `BattleAction`
- runtime move action base: `MoveActionBase`
- runtime attack action base: `AttackActionBase`
- AI action contract: `AiDecisionAction`
- board state base: `BoardState`
- pathfinder contract: `IPathfinder`

## Required Runtime Responsibilities

### Board

The board runtime must:

- own the authoritative list of cells, units, and players
- manage the current turn owner
- manage the current board state
- expose unit lookup by owner
- support turn start and turn end transitions
- provide a simple path for AI turn handoff

### Cells

Cells must:

- represent board position
- know whether they are traversable
- track current occupant units
- expose neighbour lookup
- expose a distance metric suitable for square movement
- expose board-space dimensions for sprite/layout normalization later
- expose highlight hooks for movement, targeting, and deployment previews

### Units

Units must:

- belong to a player
- occupy a cell
- expose movement points
- expose current turn state
- host runtime actions as Unity components
- support selection and end-turn state changes
- support immediate placement on a cell

### Actions

Actions must:

- initialize against their owning unit
- decide whether they can currently perform
- respond to board, cell, and unit interactions
- drive execution through coroutines
- support display and cleanup hooks for previews

### Board States

The board state machine must support:

- blocked input
- waiting for player input
- selected unit flow
- AI turn flow
- game over flow

Additional states may be added later only if current game behavior requires them.

### Pathfinding

Pathfinding must support:

- graph edge traversal with movement cost
- computing all reachable paths from an origin
- computing a path to a destination
- square-grid movement use cases

### Players and AI

The runtime must support:

- human-controlled players
- AI-controlled players
- a reusable AI decision action contract

## Explicitly Deferred

The first public runtime does not need:

- networking
- multiplayer
- remote player turn support
- hex cells
- generic sample content systems
- generic map generator tooling
- generic map painter tooling

## Scene Bootstrap Expectations

The board should be able to bootstrap from scene-authored objects:

- cells already placed in scene
- units already placed in scene
- player controllers already placed in scene

Autocollection is acceptable for the first pass as long as the scene remains editable.

## Pre-Battle Expectations

The fresh runtime does not need to reimplement the whole pre-battle UI immediately, but it must keep room for:

- deployment slot representation on scene
- roster-driven unit placement
- input blocking during pre-battle mode
- unit selection during deployment swap mode

## Publication Boundary

The public runtime must be:

- newly authored
- free of vendor namespaces and asmdef identities
- free of vendor code, comments, and copied implementation bodies
- self-contained inside the public Unity project

## Final Code Layout Expectation

The final public project should present one clear code root for the shipped gameplay/runtime code.

Recommended target:

- `Assets/Game/Code`

During the rewrite, temporary staging in separate folders is acceptable, such as:

- `Assets/Game/Runtime`
- `Assets/Game/Scripts`

But that is not the intended end-state.

Before publication, the replacement runtime and the retargeted game code should be consolidated into one project-owned code tree.

## Initial Implementation Slice

The first implementation slice should produce:

1. runtime asmdef
2. board and cell types
3. pathfinding types
4. unit and action base types
5. board state base types
6. player and AI base types

At that point, the next checkpoint is:

- compile the project
- then load the Unity scene and begin the first scene rewiring tests
