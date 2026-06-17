# Publishable Runtime Rewrite Plan

Last updated: 2026-06-17

## Progress At A Glance (Private Workspace)

This table reflects **actual status** in the current private Unity project (`C:\Users\sjkim\Turn Based Strategy`). Detailed session notes live in `Assets/Docs/Rewrite/CURSOR_HANDOFF.md`.

| Phase | Status | Notes |
|-------|--------|-------|
| 0 — Baseline | **Done** | Private workspace baseline preserved; smoke tests recorded during migration |
| 1 — Public rewrite workspace | **Not started** | Migration uses **gated cutover in the private workspace** first; separate public workspace still future |
| 2 — Public runtime spec | **Done** | `PUBLIC_RUNTIME_SPEC.md`, `PUBLIC_RUNTIME_ACCEPTANCE.md` exist |
| 3 — Runtime scaffold | **Done** | `com.windy.srpg.runtime`, `Assets/Game/Runtime/**` |
| 4 — Board and cells | **Done** | `BoardCell`, `SquareBoardCell`, occupancy, highlighting |
| 5 — Pathfinding | **Done** | `DijkstraPathfinder`; game uses via `CustomDijkstraPathfinding` adapter |
| 6 — Units and actions | **Done** | `BattleUnit`, `BattleAction`, unit turn states |
| 7 — Board state flow | **Done** | `BoardState*` types; game states dispatch to runtime board |
| 8 — Players and AI | **Done** | Human/AI player controllers, `AiDecisionAction` |
| 9 — Retarget game scripts | **Done (private workspace)** | Runtime owns turn loop, input, movement, battle start, win/lose; framework sync via bridges |
| 10 — Rewire scene and prefabs | **Done (smoke-tested)** | Active tiles use `BattleSquareCell`; `SampleSquare` / dual-cell mirror removed from active prefabs |
| 11 — Remove framework references | **Not started** | ~35 game scripts + 3 anchor components still depend on `TbsFramework`; asmdef still references `com.crookedhead.tbsf` |
| 11B — Consolidate code root | **Not started** | `Assets/Game/Scripts` and `Assets/Game/Runtime` still split |
| 11C — Complexity / bridge cleanup | **Not started** | Large hotspot files and migration bridges remain |
| 12 — Publication audit | **Not started** | Baseline inventory only: `Assets/Docs/Publication/PHASE12_AUDIT_BASELINE.md` |
| 13 — Public repo packaging | **Not started** | |

**Current position:** Phase 10 complete in the private workspace. **Phase 11 is next** — remove remaining `TbsFramework` usage and bridge components so the project can compile without `Assets/TBS Framework`.

**Important:** Gameplay authority has largely moved to `Windy.Srpg.Runtime`, but the project **still compiles against and ships with** `Assets/TBS Framework` in the private workspace. Do not delete the framework folder until Phase 11 is complete and smoke-tested.

## Migration Strategy (Revised)

The original plan assumed an immediate **two-workspace** split (Phase 1: duplicate project, delete framework, implement runtime from spec only).

In practice, migration proceeded as **gated cutover in the private workspace**:

1. Build and prove the replacement runtime (`Assets/Game/Runtime`) while the framework remains available locally.
2. Move gameplay **authority** to the runtime in small slices (turn loop, input, units, cells, prefabs), each compile-green and smoke-tested.
3. Keep thin **framework anchor** components (`FrameworkCellGridAnchor`, `FrameworkSquareAnchor`, `FrameworkUnitAnchor`) on scene objects for registries and pathfinding tokens until Phase 11 removes them.
4. Only after Phase 11–12 pass in the private workspace, create or sync the public rewrite workspace (Phase 1 + 13).

This is slower than a big-bang flip but avoids the failed cell-bridge collapse documented in `CURSOR_HANDOFF.md`.


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

- [CustomCellGrid](/c:/Users/sjkim/Turn%20Based%20Strategy/Assets/Game/Scripts/Grid/CustomCellGrid.cs) — scene host (`IBattleBoard`); legacy registry via `FrameworkCellGridAnchor`
- [CustomUnit](/c:/Users/sjkim/Turn%20Based%20Strategy/Assets/Game/Scripts/Units/CustomUnit.cs) — gameplay unit (`IBattleUnit`); legacy registry via `FrameworkUnitAnchor`
- [CustomAbility](/c:/Users/sjkim/Turn%20Based%20Strategy/Assets/Game/Scripts/Abilities/CustomAbility.cs) — `BattleAction` (does not inherit framework `Ability`)
- [BattleSquareCell](/c:/Users/sjkim/Turn%20Based%20Strategy/Assets/Game/Scripts/Grid/BattleSquareCell.cs) — active tile host (`SquareBoardCell`); legacy token via `FrameworkSquareAnchor`
- `Assets/Game/Scripts/Grid/States/*.cs` — game-owned grid states; dispatch to runtime board
- `Assets/Game/Scripts/AI/*.cs`
- `Assets/Game/Scripts/UI/*.cs`
- [SampleUnit](/c:/Users/sjkim/Turn%20Based%20Strategy/Assets/Scenes/SampleUnit.cs) — scene unit prefab script (still in active use)

**Removed from active use (Phase 10):**

- ~~`SampleSquare`~~ — deleted; was framework-derived sample cell on prefabs
- ~~`RuntimeSampleSquareCell`~~ — deleted; dual-cell mirror replaced by `BattleSquareCell`
- ~~`CustomSquare`~~ — deleted; superseded by `BattleSquareCell` + anchor pattern

The implementation target is not "match the framework."

The target is:

- make these game-owned scripts work against the new runtime
- keep the current scene behavior intact
- then remove all remaining framework dependencies (Phase 11)

## Concrete Rewrite Phases

## Phase 0 - Record The Vendor Condition And Freeze The Baseline

**Status: Done (private workspace)**

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

**Status: Not started** (deferred until Phase 11–12 pass in private workspace)

### Actions

1. duplicate the project into a new rewrite workspace
2. remove `Assets/TBS Framework` from the rewrite workspace
3. remove any framework-only comparison or migration artifacts from the rewrite workspace
4. add `Assets/Docs/Rewrite/` and `Assets/Docs/Publication/`

### Expected Result

The rewrite workspace should not compile yet, but it should already be free of vendor source files and be positioned to become the final self-contained public project.

## Phase 2 - Write The Public Runtime Spec

**Status: Done**

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

**Status: Done**

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

**Status: Done**

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

**Status: Done**

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

**Status: Done**

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

**Status: Done**

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

**Status: Done**

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

**Status: Done (private workspace)** — runtime authority proven; framework bridge sync remains until Phase 11.

### Completed retargeting (smoke-tested in private workspace)

| Slice | Result |
|-------|--------|
| Pathfinding | Game uses `Windy.Srpg.Runtime.Pathfinding.DijkstraPathfinder` via `CustomDijkstraPathfinding` |
| Turn loop / end turn | `BattleBoard.EndCurrentTurn` owns player kick; legacy sync only |
| Battle start | `StartBattleViaRuntimeBoard` + `BeginBattleFromHost` |
| Human input / movement | Runtime board routes selection, cell click, pending move, right-click |
| Win / lose | Runtime outcome routing smoke-tested |
| Grid `: CellGrid` drop | `CustomCellGrid : MonoBehaviour, IBattleBoard` + `FrameworkCellGridAnchor` token |
| Units `: Unit` drop | `CustomUnit : MonoBehaviour, IBattleUnit` + `FrameworkUnitAnchor` token |
| Abilities | `CustomAbility : BattleAction` (never framework `Ability`) |
| Grid states | Direct `CustomCellGridState` dispatch; `CustomCellGridEndTurnRouter` for legacy `EndTurn` only |

### Actions (original plan — all addressed in private workspace)

Change game-owned scripts to use the new runtime.

Priority order:

1. pathfinding — **done**
2. cells — **done** (runtime mirror + Phase 10 prefab collapse)
3. units — **done**
4. actions — **done**
5. board and board states — **done**
6. players and AI — **done**
7. scene sample scripts — **done** (SampleSquare removed; SampleUnit remains)
8. UI bridge code — **done** (uses game/runtime types; some methods still accept framework `Cell`/`Unit`)

### Important Rule

Do not drag old framework names forward unless there is no practical reason to rename them.

This includes:

- namespaces
- asmdef references
- runtime base class names

## Phase 10 - Rewire Scene And Prefabs

**Status: Done (smoke-tested)** — active scene prefabs rewired; framework anchor components remain on tiles/units/grid.

### Active Assets

- `Assets/Scenes/test.unity`
- `Assets/Scenes/FriendlyUnit.prefab`
- `Assets/Scenes/EnemyUnit.prefab`
- `Assets/Scenes/Square.prefab`
- `Assets/Scenes/Wall.prefab`

### Completed (2026-06-17)

1. Collapsed dual-cell prefabs to single host: `BattleSquareCell` (`SquareBoardCell`) + baked `FrameworkSquareAnchor` + `BattleSquareCellHighlighter`
2. Removed `SampleSquare`, `RuntimeSampleSquareCell`, `CustomSquare` from active prefabs and scenes assembly
3. Rewired `test.unity` serialized references (~400 prefab override / stripped-component updates)
4. Deployment slots recover bindings via `DeploymentSlot.EnsureRegistryCellBinding()`

### Private-workspace acceptance (met)

- `test.unity` loads without missing tile scripts
- Pre-battle deploy, movement highlights, cell click, skills/borders, AI pathfinding, win/lose — smoke-tested OK

### Publication acceptance (not met — deferred to Phase 11)

The original Phase 10 acceptance criteria assumed framework removal in the same step. That was incorrect for the gated approach:

- ~~the current scene runs only on the new runtime~~ — **partial:** runtime owns gameplay; framework anchors still required
- ~~no framework runtime scripts are attached anywhere~~ — **false:** `FrameworkCellGridAnchor`, `FrameworkSquareAnchor`, `FrameworkUnitAnchor` remain
- ~~no asset in active use references files absent from the public repo~~ — **false:** `com.windy.srpg.game` still references `com.crookedhead.tbsf`

Those items move to **Phase 11** acceptance.

### Actions (original list)

1. replace missing runtime components with the new runtime components — **done**
2. reassign serialized references — **done**
3. verify deployment slots, unit spawning, and UI links — **done (smoke-tested)**
4. replace any framework-derived art or sample assets still in active use — **partial** (sample cell scripts removed; framework folder and anchor bases remain)

## Phase 11 - Remove TBS Framework Dependencies

**Status: Not started** — **this is the current phase.**

Game **namespaces** already use `Windy.Srpg.Game.*` and `Windy.Srpg.Runtime.*`. Phase 11 is not primarily a namespace rename; it is **eliminating compile-time and scene-time dependency on `Assets/TBS Framework`**.

See also `Assets/Docs/Publication/PHASE12_AUDIT_BASELINE.md` for the current contamination inventory.

### What still depends on the framework (today)

| Category | Examples |
|----------|----------|
| Asmdef reference | `com.windy.srpg.game` → `com.crookedhead.tbsf` |
| Anchor inheritance | `FrameworkCellGridAnchor : CellGrid`, `FrameworkSquareAnchor : Square`, `FrameworkUnitAnchor : Unit` |
| End-turn hook | `CustomCellGridEndTurnRouter : CellGrid.CellGridState` |
| Game script imports | ~35 files with `using TbsFramework.*` (mostly `Cell`, `Unit`, `Player`, `CellGrid` types) |
| On-disk folder | `Assets/TBS Framework/` (required for private workspace compile today) |

`Assets/Game/Runtime/**` and `Assets/Scenes/*.cs` are already **free** of `TbsFramework` imports.

### Recommended slice order (Phase 11)

1. Replace framework type usage in gameplay code with runtime interfaces (`IBattleCell`, `IBattleUnit`, `IBattleBoard`) and registry helpers
2. Move pathfinding edge maps and occupancy fully to runtime cell/coordinate keys
3. Remove `FrameworkSquareAnchor` when cell registry and graph no longer need framework `Square`/`Cell`
4. Remove `FrameworkUnitAnchor` when unit registry is runtime-owned
5. Remove `FrameworkCellGridAnchor` and `CustomCellGridEndTurnRouter` when init/turn/end-game no longer touch `CellGrid`
6. Remove `com.crookedhead.tbsf` from game/scenes asmdef references
7. Delete `Assets/TBS Framework` and verify compile + full smoke test

### Namespace coverage (already using `Windy.Srpg.Game.*`)

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

- no game or scenes code references `TbsFramework` — **not met** (~35 game scripts remain)
- no game or scenes asmdef references `com.crookedhead.tbsf` — **not met**
- no scene/prefab component inherits from framework `CellGrid`, `Square`, or `Unit` — **not met** (three anchor types)
- `Assets/TBS Framework` can be deleted without breaking the active scene — **not met**

### Prep work already done (does not satisfy Phase 11)

- Asmdef **filenames** renamed to `com.windy.srpg.game.asmdef` / `com.windy.srpg.game.scenes.asmdef` (assembly names already matched; GUIDs preserved)
- `RuntimeSampleSquareHighlighter` renamed to `BattleSquareCellHighlighter` (Phase 10 naming cleanup)

## Phase 11B - Consolidate To One Public Code Root

**Status: Not started**

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

**Status: Not started** (defer major file-splitting until Phase 11 framework removal is stable)

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

**Status: Not started** (baseline only: `Assets/Docs/Publication/PHASE12_AUDIT_BASELINE.md`)

This phase is mandatory before publication.

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

**Status: Not started**

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

**As of 2026-06-17:** Phases 0–10 are complete in the private workspace. **None of the publication checklist items below are fully met yet** — Phase 11 must finish first.

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

1. preserve the vendor reply privately — **done**
2. write `PUBLIC_RUNTIME_SPEC.md` — **done**
3. write `PUBLIC_RUNTIME_ACCEPTANCE.md` — **done**
4. scaffold `com.windy.srpg.runtime` — **done**
5. implement board and cells — **done**
6. implement pathfinding — **done**
7. implement units and actions — **done**
8. implement board states — **done**
9. implement players and AI — **done**
10. retarget game-owned scripts (gated cutover in private workspace) — **done**
11. rewire scene and prefabs (Phase 10) — **done (smoke-tested)**
12. **remove TBS Framework dependencies (Phase 11)** — **next**
13. consolidate public code into one root folder (Phase 11B)
14. complexity / bridge cleanup (Phase 11C)
15. run the publication audit (Phase 12)
16. create the public rewrite workspace without framework (Phase 1, deferred)
17. package the public repo (Phase 13)

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
