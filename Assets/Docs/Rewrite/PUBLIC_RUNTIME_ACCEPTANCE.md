# Public Runtime Acceptance

Last updated: 2026-06-09

## Compile Checkpoint

The first rewrite checkpoint is successful when:

- `com.windy.srpg.runtime` compiles
- the existing game assembly still compiles
- the new runtime is isolated and does not yet break the current scene wiring

## Foundation Runtime Acceptance

The fresh runtime foundation is acceptable when it includes:

- a board manager
- a board cell type
- a square board cell type
- unit base types
- action base types
- board state base types
- player base types
- AI decision base types
- a pathfinder contract and implementation

## Pre-Scene-Rewire Checkpoint

Before touching active prefabs or the active scene, confirm:

- the new runtime currently has a stable home, even if it is only a staging folder such as `Assets/Game/Runtime`
- the new runtime uses `Windy.Srpg.Runtime.*`
- the new runtime assembly is `com.windy.srpg.runtime`
- the current game assembly still builds

## Scene-Rewire Acceptance

The next phase after this document's checkpoint will be ready when:

- runtime foundation compiles cleanly
- the public runtime spec exists
- the public runtime acceptance file exists
- the new runtime has enough structure to replace scene dependencies incrementally

## Publication Layout Acceptance

Before the project is considered publication-ready, confirm:

- the shipped gameplay/runtime code has been consolidated into one public code root such as `Assets/Game/Code`
- the repo no longer depends on a long-term split between `Assets/Game/Scripts` and `Assets/Game/Runtime`
- asmdefs, scene references, and prefab references still work after consolidation
