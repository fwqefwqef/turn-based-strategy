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
- Skills that do not end the unit's action are inherently limited to one use per turn
- Inventory, equipment, trading, shops, and dropped-item handling
- Equipped weapons and accessories can grant skills (`GrantedSkillIds`) and passives (`GrantedPassiveIds`) from `gdata.json`; those grants are removed on unequip and are not saved as learned abilities
- Class and equip passives
- Five-stat progression: Strength, Magic, Defense, Speed, and Luck; Magic also provides magical defense
- EXP, level-ups, and growth-rate-based stat gains
- Stackable buffs and debuffs with categories, duration refresh, cleansing, crowd control, and damage-over-time processing
- Terrain effects with Throne, Forest, Magic Tile, two-round Burning Terrain, and chapter-configured Black Fog
- Mouse/keyboard tile hover strip showing movement cost, terrain effects, duration, and Black Fog depth
- Combat-aware enemy AI with attack/heal action modes and Move, Wait, WaitGroup, and NotMove movement modes
- Pre-battle roster deployment and unit configuration
- Campaign saves, chapter unlocking, replayable chapters, battle results, and victory progression
- Unit preset inheritance with additive per-instance stat and loadout overrides
- Rectangular multi-tile units with footprint-aware movement, occupancy, targeting, terrain, AI, reinforcements, camera focus, and tile-specific targeting indicators

The `colossus` enemy preset is a ready-to-place 3x3 example. Death's Door, Black Fog, buff-backed terrain bonuses and debuffs, terrain-effect presentation, and multi-tile scene behavior are implemented and awaiting broader Play Mode validation.

Death's Door preserves 0 or negative HP and caps movement at 1 until the unit is healed above 0 HP. It does not reduce primary stats or accumulate a lasting weakening penalty.

### Skill authoring

Skill target legality is data-driven. Set `TargetingType` in `Assets/Data/gdata.json` to `Self`, `EnemyUnit`, `AllyUnit`, `AnyUnit`, `Cell`, or `AreaCell`; area skills also use `SelfImmune`, `AreaProfile.AffectsAllies`, and `AreaProfile.AffectsEnemies`. `SkillTargetValidator` applies those rules consistently for player input, previews, execution, and AI.

Custom effects inherit `SkillEffectBase` and normally only implement the parameterless protected `Use()` method. The base class validates and binds the invocation before exposing the guaranteed `User`, `Target`, `Context`, and `Grid` properties. Override parameterless `MeetsAdditionalUseConditions()` only for mechanic-specific rules that cannot be expressed by the catalog, such as requiring missing HP, a completed action, a removable debuff, or sufficient sacrifice HP. Set `RequiresGrid` for displacement effects and `RequiresTarget` for per-target area effects instead of checking either value manually.

Status application is intentionally terse: use `ApplyStatusToTarget`, `ApplyStatusToSelf`, or `ApplyStatusToEnemies`, with the optional `stacks` argument. On-hit effects implement `IP_AttackHitEffect` and use its validated callback arguments directly. Effect implementations should not repeat null, ally/enemy, self, alive, duplicate-area-target, or context-validity checks, and should not call `CanUse` from `Use` or preview calculations.

Buff implementations live in `BuffEffects.cs` and inherit only `BuffEffectBase`, plus the capability interface that makes the effect participate in a particular system. `BuffEffectBase` owns lifecycle validation and exposes guaranteed `Owner`, `Entry`, `Source`, and `Stacks` state. Owner-bound callbacks such as turn-start health, movement caps, and survival guards do not receive redundant owner arguments; their owning lists are the validation boundary. Combat callbacks receive validated context arguments directly from the combat dispatcher.

Effect classes are intended to be edited directly. Prefer straightforward, locally readable implementations: keep short logic inline, avoid extracting a private helper merely to hide a few lines, and minimize call-chain depth. A helper belongs in an effect API when it names a meaningful domain operation or is substantially reused?for example, skill status application and profile-based healing.

Passive implementations live in `PassiveEffects.cs`. `PassiveEffectBase` owns application, removal, owner binding, and turn-hook validation; implementations use the guaranteed `Owner` reference and override parameterless lifecycle hooks only when needed. Combat, healing, stat, and EXP callbacks are dispatched with valid arguments, so passive effects should contain only mechanic-specific conditions rather than repeated null, team, owner, or alive checks.

## Project Layout

- `Assets/Code` — gameplay code in the `com.windy.srpg.game` assembly
- `Assets/Data/gdata.json` — unified item, skill, passive, buff, and terrain-effect catalog
- `Assets/Data/Preset Data (Unit, Tile)` — friendly units, enemies, and tile presets
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
dotnet run --project 'Assets/Tools~/SkillUsageChecks/SkillUsageChecks.csproj'
dotnet run --project 'Assets/Tools~/TerrainEffectChecks/TerrainEffectChecks.csproj'
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
