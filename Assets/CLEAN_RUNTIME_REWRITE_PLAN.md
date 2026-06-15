# Publishable Runtime Rewrite Plan

Last updated: 2026-06-10

## Purpose

This plan is for producing a public, publishable version of the project that complies with the vendor's written condition:

- the published project must be entirely decoupled
- the published project must contain no original source files from `TBS Framework`
- the published project must contain no proprietary framework code
- the published project must be self-contained and runnable by another person after downloading and importing it into Unity

This document is therefore not just a technical migration plan.

It is a combined:

- provenance plan
- contamination-avoidance plan
- runtime rewrite plan
- publication audit plan

## Important Boundary

This plan is designed to maximize publishability and comply with the vendor's request.

It is **not** a legal guarantee by itself.

The practical target is:

- no original framework code in the public repo
- no framework assets in the public repo unless separately permitted
- no copied comments, copied text, copied file layout, or copied implementation bodies
- a new runtime written as project-owned code
- no private or unpublished runtime dependency required to open or run the project

## Final Deliverable Requirement

The final public project must be:

- a complete Unity project
- importable by another person without requesting old framework files from the vendor
- runnable without hidden runtime code outside the repo
- free of any "bring your own framework package" setup step

Private notes may exist during development, but the shipped project itself must not depend on any private files.

## Non-Negotiable Compliance Rules

These rules are mandatory if the public repo is meant to be publishable.

1. Do not publish anything from `Assets/TBS Framework`.
2. Do not move framework source files into a new folder and call that a rewrite.
3. Do not preserve framework script `.meta` files in the public runtime.
4. Do not copy framework code, comments, XML docs, README text, or sample scripts into the public runtime.
5. Do not reuse framework namespaces, asmdef names, package names, or distinctive type names in the final public runtime.
6. Do not publish framework example art, prefabs, scenes, or materials unless they are replaced or separately cleared.
7. Do not write the new runtime by keeping the framework folder open as an implementation reference.
8. Do not include private comparison files, reverse-engineering notes, or framework-derived snippets in the public repo.

## Required Evidence To Keep

Keep these records in a private folder outside the future public repo:

1. the vendor reply granting publication freedom only for a fully decoupled project
2. a dated note explaining the rewrite process used
3. a list of which folders were excluded from publication
4. a list of which new runtime files were authored from scratch

This is not for public release. It is for provenance if questions come up later.

## Two-Workspace Model

Use two workspaces from this point onward.

### Private Workspace

Purpose:

- keep the current project running
- preserve the old framework locally
- allow private behavior verification during planning

Contents:

- current full Unity project
- `Assets/TBS Framework`
- private notes
- vendor email record

This workspace is never published.

### Public Rewrite Workspace

Purpose:

- hold only publishable code and assets
- contain the replacement runtime
- become the future public repo
- become the full runnable Unity project that other people can actually import and use

Contents:

- only game-owned code and data
- newly authored runtime files
- replacement assets
- public documentation
- every code file required to compile and run the project

This workspace must not contain `Assets/TBS Framework`.

This workspace must also not depend on:

- hidden local files
- unpublished code archives
- manually requested vendor packages
- private helper DLLs not committed to the repo

## Clean Implementation Rule

For the strongest publishability story, implementation should happen in a workspace where the framework folder is absent.

The ideal process is:

1. create a public rewrite copy of the project
2. remove `Assets/TBS Framework` from that copy before writing the new runtime
3. implement only from the public behavior specification and your own game-owned files

If you want the strictest version of this process, the actual runtime implementation should be done by a separate implementer who only receives:

- the public behavior spec
- the game-owned scripts
- the scene/prefab requirements
- the acceptance checklist

That is stronger than having the same person both inspect the old framework and implement the new runtime.

## Public Repo Rules

The future public repo may include:

- `Assets/Game`
- `Assets/Scenes`
- game-owned presets, save code, UI code, catalog data, localization data
- newly authored runtime code
- replacement art that you own or have permission to redistribute
- any project-owned packages or support files required for Unity import/build

The future public repo must exclude:

- `Assets/TBS Framework`
- framework example assets
- framework example scenes
- vendor readmes
- any derived comparison spreadsheets or internal migration notes based on the framework

The future public repo must also not require:

- a separate private zip
- a missing framework package
- manual recovery of delisted third-party files
- undocumented local machine setup beyond normal Unity project import

## Naming Rules For The New Runtime

To avoid carrying forward framework identity, the new runtime should use new names.

### Assembly

Use:

- `com.windy.srpg.runtime`

### Runtime Namespace Root

Use:

- `Windy.Srpg.Runtime`

### Game Namespace Root

Use:

- `Windy.Srpg.Game`

### Avoid In Final Public Code

Do not keep:

- `com.crookedhead.tbsf`
- `TbsFramework`
- `CellGrid`
- `Unit`
- `Ability`
- other framework-identifying names when a fresh name is practical

Generic concepts are fine, but the public runtime should clearly read as its own system.

## Unified Code Root Requirement

The final published project should not keep game code in one folder and framework-replacement code in another sibling folder forever.

The publishable end-state should have one clear public code root:

- `Assets/Game/Code`

That root can contain subfolders, but all project C# code that ships as the public gameplay/runtime layer should live under that one code tree.

Temporary rewrite staging is allowed during migration. For example, it is acceptable to build the replacement runtime in:

- `Assets/Game/Runtime`

while the older game-owned scripts still live in:

- `Assets/Game/Scripts`

However, that split is temporary only.

Before publication, the code must be consolidated into one folder structure so the repo reads like one project-owned codebase rather than "game code plus a bolted-on runtime."

## Public Rewrite Folder Layout

Use this temporary implementation layout while the rewrite is in progress:

```text
Assets/
  Game/
    Runtime/
      Core/
      Board/
      Board/States/
      Units/
      Actions/
      Players/
      AI/
      Pathfinding/
      Rendering/
    Scripts/
    Catalog/
  Scenes/
  StreamingAssets/
  Docs/
    Publication/
    Rewrite/
```

The final public folder layout should converge to one code root:

```text
Assets/
  Game/
    Code/
      Runtime/
      Battle/
      Units/
      Actions/
      Players/
      AI/
      UI/
      Campaign/
      Inventory/
      Skills/
      Passives/
      Buffs/
      Localization/
      WorldUI/
    Catalog/
  Scenes/
  StreamingAssets/
  Docs/
    Publication/
    Rewrite/
```

Suggested documentation files:

- `Assets/Docs/Rewrite/PUBLIC_RUNTIME_SPEC.md`
- `Assets/Docs/Rewrite/PUBLIC_RUNTIME_ACCEPTANCE.md`
- `Assets/Docs/Publication/PUBLIC_REPO_BOUNDARY.md`

## Runtime Scope

The rewrite is intentionally limited to what the current game scene needs.

### Required

- square grid cells
- cell occupancy
- unit placement
- turn order
- player turns
- AI turns
- movement range
- pathfinding
- basic combat action flow
- battle state machine
- unit selection and targeting hooks
- pre-battle deployment support
- current canvas and world UI integration points

### Deferred

- networking
- multiplayer
- remote players
- hex grids
- generic example framework tools
- generic map painter
- generic map generator
- framework sample content

## Public Behavior Spec Phase

Before runtime implementation starts, produce a public behavior spec.

That spec must be written only in terms of:

- project behavior
- scene requirements
- game-owned script expectations
- user-facing outcomes

That spec must not contain:

- copied framework code
- copied framework comments
- copied method bodies
- vendor file structure descriptions beyond high-level dependency notes

### Public Behavior Spec Deliverables

Create:

- `Assets/Docs/Rewrite/PUBLIC_RUNTIME_SPEC.md`
- `Assets/Docs/Rewrite/PUBLIC_RUNTIME_ACCEPTANCE.md`

### Public Spec Sections

The spec should document:

1. board model
2. cell responsibilities
3. unit lifecycle
4. action lifecycle
5. turn flow
6. AI contract
7. scene bootstrap
8. save/pre-battle integration points
9. UI integration points
10. acceptance tests

## Current Game-Owned Integration Points

These are the game-owned scripts that define the runtime contract we actually need to satisfy:

- [CustomCellGrid](/c:/Users/sjkim/Turn%20Based%20Strategy/Assets/Game/Scripts/Grid/CustomCellGrid.cs)
- [CustomUnit](/c:/Users/sjkim/Turn%20Based%20Strategy/Assets/Game/Scripts/Units/CustomUnit.cs)
- [CustomAbility](/c:/Users/sjkim/Turn%20Based%20Strategy/Assets/Game/Scripts/Abilities/CustomAbility.cs)
- `Assets/Game/Scripts/Grid/States/*.cs`
- `Assets/Game/Scripts/AI/*.cs`
- `Assets/Game/Scripts/UI/*.cs`
- [SampleUnit](/c:/Users/sjkim/Turn%20Based%20Strategy/Assets/Scenes/SampleUnit.cs)
- [SampleSquare](/c:/Users/sjkim/Turn%20Based%20Strategy/Assets/Scenes/SampleSquare.cs)

The implementation target is not "match the framework."

The target is:

- make these game-owned scripts work against a new runtime
- keep the current scene behavior intact

## Concrete Rewrite Phases

## Phase 0 - Record The Vendor Condition And Freeze The Baseline

### Actions

1. save the vendor reply in a private legal notes folder outside the public repo
2. save a backup of the current project
3. record a smoke test of the current `test` scene
4. record which framework example assets are currently used

### Baseline Smoke Test

Confirm:

- scene opens
- pre-battle UI opens
- roster selection works
- deployment switching works
- battle starts
- units spawn
- movement works
- attack preview works
- skill preview works
- inspect UI works
- save-backed roster loads

### Stop Condition

Do not begin the rewrite if the private baseline is already unstable.

## Phase 1 - Create The Public Rewrite Workspace

### Actions

1. duplicate the project into a new rewrite workspace
2. remove `Assets/TBS Framework` from the rewrite workspace
3. remove any framework-only comparison or migration artifacts from the rewrite workspace
4. add `Assets/Docs/Rewrite/` and `Assets/Docs/Publication/`

### Expected Result

The rewrite workspace should not compile yet, but it should already be free of vendor source files and be positioned to become the final self-contained public project.

## Phase 2 - Write The Public Runtime Spec

### Actions

1. inspect only game-owned scripts and scene requirements
2. write the public runtime contract the new runtime must satisfy
3. define fresh runtime names for every required concept
4. define acceptance checks for each subsystem

### Required Output

- `PUBLIC_RUNTIME_SPEC.md`
- `PUBLIC_RUNTIME_ACCEPTANCE.md`

### Fresh Runtime Name Mapping

Recommended mapping:

- `CellGrid` concept -> `BattleBoard`
- `Cell` concept -> `BoardCell`
- `Unit` concept -> `BattleUnit`
- `Ability` concept -> `BattleAction`
- `AIAction` concept -> `AiDecisionAction`
- `CellGridState` concept -> `BoardState`

These names do not need to match this exact list, but they should be new names.

## Phase 3 - Scaffold The New Runtime

### Actions

Create fresh files under:

- `Assets/Game/Runtime/Core`
- `Assets/Game/Runtime/Board`
- `Assets/Game/Runtime/Board/States`
- `Assets/Game/Runtime/Units`
- `Assets/Game/Runtime/Actions`
- `Assets/Game/Runtime/Players`
- `Assets/Game/Runtime/AI`
- `Assets/Game/Runtime/Pathfinding`
- `Assets/Game/Runtime/Rendering`

Create fresh asmdef:

- `Assets/Game/Runtime/com.windy.srpg.runtime.asmdef`

### Rule

Every runtime file in this folder must be newly authored.

No moved framework scripts.
No copied framework metas.

## Phase 4 - Implement The Board And Cell Layer

### Build

- board cell component
- square cell variant
- neighbor lookup
- occupancy hooks
- cell highlighting hooks

### Acceptance

- cells can discover neighbors
- units can occupy and leave cells
- cells can be highlighted for movement, attack, and deployment

## Phase 5 - Implement Pathfinding

### Build

- pathfinder interface
- Dijkstra-style pathfinder
- path result object
- movement range query support

### Acceptance

- movement range can be computed for a unit
- a path can be generated between reachable cells
- impassable or occupied cells are handled correctly

## Phase 6 - Implement Units And Actions

### Build

- unit base component
- action base component
- move action base
- attack action base
- selection/highlighter integration
- runtime unit states

### Acceptance

- a unit can be selected
- a unit can move
- a unit can execute an attack-style action
- a unit can be marked as finished

## Phase 7 - Implement Board State Flow

### Build

- board state base
- waiting-for-input state
- blocked-input state
- selected-unit state
- AI-turn state
- game-over state

Add only states actually needed by the current game.

### Acceptance

- turn flow advances correctly
- state changes drive input behavior
- battle can switch between player and AI control

## Phase 8 - Implement Players And AI

### Build

- human player runtime type
- AI player runtime type
- AI decision action base
- minimal evaluator contracts only if required

### Acceptance

- human-owned units can be selected on the correct turn
- AI-owned units can take a turn
- existing game AI scripts can be retargeted to the new contracts

## Phase 9 - Retarget Game-Owned Scripts

### Actions

Change game-owned scripts to use the new runtime.

Priority order:

1. pathfinding
2. cells
3. units
4. actions
5. board and board states
6. players and AI
7. scene sample scripts
8. UI bridge code

### Important Rule

Do not drag old framework names forward unless there is no practical reason to rename them.

This includes:

- namespaces
- asmdef references
- runtime base class names

## Phase 10 - Rewire Scene And Prefabs

### Active Assets

- `Assets/Scenes/test.unity`
- `Assets/Scenes/FriendlyUnit.prefab`
- `Assets/Scenes/EnemyUnit.prefab`
- `Assets/Scenes/Square.prefab`
- `Assets/Scenes/Wall.prefab`

### Actions

1. replace missing runtime components with the new runtime components
2. reassign serialized references
3. verify deployment slots, unit spawning, and UI links
4. replace any framework-derived art or sample assets still in active use

### Acceptance

- the current scene runs only on the new runtime
- no framework runtime scripts are attached anywhere
- no asset or prefab in active use references files that are absent from the public repo

## Phase 11 - Rename Game Namespaces For Publication

### Actions

Move game namespaces away from `TbsFramework.*`.

Recommended target:

- `Windy.Srpg.Game.*`

### Coverage

- grid
- units
- abilities
- UI
- AI
- campaign
- inventory
- skills
- passives
- buffs
- localization
- world UI

### Acceptance

- no public code references `TbsFramework`
- no public asmdef references `com.crookedhead.tbsf`

## Phase 11B - Consolidate To One Public Code Root

### Actions

Move the publishable gameplay/runtime code into one root such as:

- `Assets/Game/Code`

This includes:

- replacement runtime code
- retargeted gameplay code
- UI code
- campaign/save code
- localization code
- other project-owned systems that are part of the shipped runtime

### Rule

This phase is about folder layout clarity, not just file moves.

After consolidation, the codebase should no longer read as:

- `Assets/Game/Scripts` for old code
- `Assets/Game/Runtime` for new code

It should read as one project-owned code tree.

### Acceptance

- public gameplay code is under one root folder
- no long-term split remains between `Assets/Game/Scripts` and `Assets/Game/Runtime`
- asmdef references still compile after the move
- scene and prefab script references survive the move

## Phase 11C - Complexity Reduction And Legacy Cleanup

This phase exists to prevent the rewrite from becoming "old game code plus a new runtime plus a pile of glue."

The goal is not only publishability.

It is also long-term readability.

### Problem To Avoid

The current private project still works, but several files have become oversized coordination points:

- `CustomUnit.cs`
- `CustomMoveAbility.cs`
- `CustomCellGrid.cs`
- some large UI controllers
- runtime bridge code such as `LegacyRuntimeMirrorBridge`

That kind of growth is survivable in a private prototype, but it should not become the permanent shape of the public codebase.

### Simplicity Rules

1. Do not keep adding new features into giant transitional files if the logic can live in a new focused class.
2. Do not keep both "temporary bridge logic" and "final runtime logic" once the runtime replacement for that area is proven.
3. Do not leave framework-shaped compatibility layers in place longer than needed for migration.
4. Prefer one clear owner for each responsibility:
   - board code owns occupancy, traversability, and path queries
   - unit code owns unit state and combat-relevant stats
   - action code owns action execution rules
   - UI code only displays state and sends commands
   - save code only serializes and restores game state
5. If a class starts acting as a unit system, action system, UI flow controller, and compatibility layer at the same time, split it before adding more behavior.
6. During the active rewrite, prioritize replacing framework-owned behavior with game-owned behavior before doing more file-splitting or cosmetic restructuring.

### Explicit Cleanup Targets

Before publication, review these areas for extraction or deletion:

- defer additional file-splitting work until after the core runtime replacement path is stable in play tests
- keep `CustomCellGrid` from remaining the owner of unrelated startup, deployment, save, and runtime-bridge concerns
- remove `LegacyRuntimeMirrorBridge` once the new runtime no longer needs legacy synchronization
- remove obsolete compatibility members that only existed to ease migration
- remove dead code paths, duplicate board-state refresh logic, and "just in case" hooks that are no longer used

### Recommended End-State

The final public code should read like one intentionally designed SRPG codebase, not a patched migration.

That means:

- small to medium focused classes
- minimal bridge code
- minimal compatibility wrappers
- clear ownership boundaries
- no layers of minor fixes that remain after the real cause was solved

### Acceptance

- no essential gameplay system depends on a legacy bridge that exists only for migration
- oversized hotspot files are reduced or clearly separated into focused helpers/services
- public code reads as one coherent architecture
- new contributors can trace unit flow, board flow, UI flow, and save flow without bouncing through multiple compatibility layers
- migration-only code is deleted once its replacement is active

## Phase 12 - Publication Audit

This phase is mandatory.

### Code Audit Searches

Run searches like:

```powershell
rg -n "TbsFramework|CrookedHead|com\\.crookedhead\\.tbsf|CellGridStateRemotePlayerTurn|RemotePlayer" Assets Packages ProjectSettings -g "*.cs" -g "*.asmdef" -g "*.json" -g "*.md"
```

```powershell
rg -n "copyright|readme|example5|tbs framework|crookedhead" Assets -g "*.txt" -g "*.md" -g "*.cs" -g "*.meta"
```

### Asset Audit

Confirm the public repo does not include:

- framework code
- framework prefabs
- framework scenes
- framework example sprites
- framework materials
- framework readmes

Confirm the public repo does include:

- every script needed to compile
- every prefab needed by the active scene
- every asset needed for the project to open without missing references, unless explicitly documented as optional

### Provenance Audit

Confirm:

- all runtime files are newly authored files
- no framework `.meta` files were reused
- final runtime folder structure is project-owned
- final naming is project-owned

### Self-Containment Audit

Confirm:

- a fresh clone can be opened in Unity without importing any private package
- no README step says "contact vendor for framework"
- no script references a DLL or code folder absent from the repo
- no scene or prefab has a missing script caused by excluded private files

## Phase 13 - Public Repo Packaging

### Include

- game-owned scripts
- newly authored runtime
- owned data files
- owned or cleared replacement assets
- README with setup steps
- license for your own code
- any project settings or package references required to open the Unity project normally

### Exclude

- all vendor framework material
- private legal notes
- private migration notes
- old internal comparison files

## Acceptance Checklist For Publication

The project is only ready for publication when all of these are true:

- the runtime in the public repo is newly authored
- the public repo contains no files from `Assets/TBS Framework`
- the public repo contains no framework example assets
- the public repo contains no vendor namespaces or asmdef names
- the public repo contains all code required to compile and run
- the public repo does not require any hidden or separately requested framework files
- the current scene opens and runs
- pre-battle flow works
- deployment flow works
- battle flow works
- save-backed owned unit loading works
- the repo reads as a project-owned codebase, not a relocated vendor package

## Concrete Execution Order

If we proceed with this rewrite, the safest order is:

1. preserve the vendor reply privately
2. create the public rewrite workspace
3. remove `Assets/TBS Framework` from that workspace
4. write `PUBLIC_RUNTIME_SPEC.md`
5. write `PUBLIC_RUNTIME_ACCEPTANCE.md`
6. scaffold `com.windy.srpg.runtime`
7. implement board and cells
8. implement pathfinding
9. implement units and actions
10. implement board states
11. implement players and AI
12. retarget game-owned scripts
13. rewire scene and prefabs
14. rename public namespaces
15. consolidate public code into one root folder
16. run the publication audit
17. package the public repo

## Recommendation

This rewrite is only worth doing if we follow the publication boundary strictly:

- fresh runtime files
- fresh names
- fresh folder layout
- one unified public code root
- no framework source in the public repo
- no framework assets in the public repo
- no hidden runtime dependency outside the public repo
- public implementation based on game behavior requirements, not framework code lift

If we do that, the result should match the vendor's condition far better than the earlier "decoupling" approach.
