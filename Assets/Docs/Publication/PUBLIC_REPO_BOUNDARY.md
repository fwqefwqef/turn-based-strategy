# Public Repo Boundary

Last updated: 2026-06-09

## Required

The public repo must contain all code needed to:

- open the project in Unity
- compile the project
- run the active game scene

The public repo should also present the shipped gameplay/runtime code as one project-owned code tree, not as a permanent split between separate "game scripts" and "replacement runtime" folders.

Recommended final code root:

- `Assets/Game/Code`

## Forbidden

The public repo must not require:

- `Assets/TBS Framework`
- vendor source files
- vendor example assets
- hidden local helper files
- private DLLs
- manual retrieval of delisted framework packages

## Practical Rule

If a new contributor cannot clone the repo, open it in Unity, and run it without contacting the vendor, the repo is not ready for publication.

If the code still reads like "old project code in one folder and new framework code in another folder," the repo is also not at its intended final presentation quality yet.
