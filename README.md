# Turn Based Strategy

A Unity tactical RPG with grid movement, turn-based combat, character progression, campaign persistence, and chapter-based battles.

## Requirements

- Unity `6000.4.1f1`
- .NET 9 SDK for the standalone development and verification tools
- Open this repository as the Unity project root—the folder containing `Assets/`, `Packages/`, and `ProjectSettings/`

## Current Architecture

Battle logic uses a single scene-backed model:

- `CellGrid` owns battle initialization, deployment, turn flow, input states, and battle results.
- `Unit` owns stats, movement, combat, equipment, skills, passives, buffs, and progression.
- `Cell` owns tile geometry, occupancy, input events, and highlights.
- `Player` implementations drive human and AI turns.
- `Ability` components implement actions such as movement and attacks.
- `GameplayInputController` is the central board-input path.
- `CampaignSaveManager` and `CampaignSaveFactory` persist the roster and unit progression.

The older parallel runtime/mirror grid has been removed. New battle features should extend the scene `CellGrid`, `Unit`, and `Cell` types directly.

## Implemented Systems

- Pending-move previews followed by action confirmation
- Weapon attacks, combat arts, single-target skills, and area skills
- Inventory, equipment, trading, shops, and dropped-item handling
- Class and equip passives
- Five-stat progression: Strength, Magic, Defense, Speed, and Luck; Magic also provides magical defense
- EXP, level-ups, and growth-rate-based stat gains
- Stackable buffs and debuffs with categories, duration refresh, cleansing, crowd control, and damage-over-time processing
- Combat-aware enemy AI with attack/heal action modes and Move, Wait, WaitGroup, and NotMove movement modes
- Pre-battle roster deployment and unit configuration
- Campaign saves, chapter unlocking, replayable chapters, battle results, and victory progression
- Unit preset inheritance with additive per-instance stat and loadout overrides

Death's Door, Black Fog, and multi-tile boss support are planned but are not implemented yet.

## Project Layout

- `Assets/Game/Code` — gameplay code in the `com.windy.srpg.game` assembly
- `Assets/Game/Data/gdata.json` — unified item, skill, passive, and buff catalog
- `Assets/Game/Data/Preset Data (Unit, Tile)` — friendly units, enemies, and tile presets
- `Assets/Scenes/Level` — chapter and free-battle scenes
- `Assets/Notes` — architecture and mechanic documentation
- `Assets/Tools~` — standalone tuning and verification utilities excluded from Unity asset import

The main architecture reference is [`Assets/SUMMARY.md`](Assets/SUMMARY.md). Some newer mechanic details are documented separately under [`Assets/Notes`](Assets/Notes).

## Opening The Project

1. Open this folder in Unity `6000.4.1f1`.
2. Load `Assets/Scenes/OverworldMenu.unity` to enter through chapter selection, or open a battle scene under `Assets/Scenes/Level` directly.
3. Press Play.

Current battle scenes are:

- `Chapter 1.unity`
- `Free Battle 1.unity`
- `Chapter 2.unity`

## Build and Verification

Compile the main gameplay and editor assemblies:

```powershell
dotnet build com.windy.srpg.game.csproj
dotnet build com.windy.srpg.game.editor.csproj --no-restore
```

Run focused checks:

```powershell
dotnet run --project 'Assets/Tools~/DebuffChecks/DebuffChecks.csproj'
dotnet run --project 'Assets/Tools~/PresetOverrideChecks/PresetOverrideChecks.csproj'
```

Run the five-stat growth preview from the command line:

```powershell
dotnet run --project 'Assets/Tools~/StatGainPreview/StatGainPreview.csproj' -- 20 20 20 20 20
```

Launch the interactive Windows Stat Gain Lab with:

```powershell
& 'Assets/Tools~/StatGainLab/Launch Stat Gain Lab.cmd'
```

## License

This project is released under [The Unlicense](LICENSE).
