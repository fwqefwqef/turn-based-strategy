# Turn Based Strategy

A Unity tactical RPG built around grid battles, a persistent party, and chapter progression. Players deploy units, move across terrain, and use weapons, skills, items, and positioning to meet each chapter's victory condition. The project includes an overworld, a shop, and Unity editor tools for building maps and managing game data.

## Open the project

Use Unity **6000.4.1f1** and open this folder as the project root (the folder containing `Assets`, `Packages`, and `ProjectSettings`).

- Open `Assets/Scenes/OverworldMenu.unity` for the campaign entry point.
- Open a scene in `Assets/Scenes/Level` to inspect a chapter or free battle directly.
- Open `Assets/Scenes/PaintedMap.unity` to work on the development map.

The campaign starts with Protagonist, Thunder, Flame, and Darkness, plus 1,000 gold. Deployment and inventory management happen before battle. Chapter completion updates progression and can add stock to the shop.

## Gameplay systems

- **Grid combat:** Units preview a move before committing an action. Combat supports basic attacks, counterattacks, pursuit attacks, combat arts, healing, area spells, and post-action movement.
- **Units and progression:** Units have Strength, Magic, Defense, Speed, and Luck, along with HP, MP, movement, equipment, weapon proficiencies, skills, passives, experience, and growth rates.
- **Statuses and terrain:** Buffs and debuffs handle stat changes, damage over time, control effects, and terrain bonuses. Maps can include Forest, Throne, Magic Tile, Burning Terrain, and chapter-configured Black Fog. Death's Door gives player units a chance to survive a lethal hit at 0 or negative HP, with movement capped until they recover.
- **Battle maps:** Rectangular multi-tile units occupy a full footprint for movement and targeting. An area effect applies once per unit even when it covers several occupied tiles. Maps can contain reinforcements; chapter data controls battle conditions, enemy order, fog, and clear rewards.
- **Enemy turns:** AI selects actions and paths according to its attack and movement behavior. Enemies can be assigned an explicit turn order by unit ID, and groups can wait for coordinated attacks.
- **Campaign:** The overworld tracks chapter unlocks and clears, roster progress, gold, inventory, and shop stock. Scene allies marked **Recruit On Chapter Clear** join the owned party if they survive a victory; owned allies who die in that battle are permanently removed on clear. Undeployed party members are unaffected. Some chapters can be replayed; this is configured per chapter.
- **Audio:** Chapter Data optionally specifies a BGM intro and loop. The intro plays once, then the loop repeats; if no intro is assigned, the loop starts immediately. A persistent sound manager also provides overlapping, cue-based SFX playback for future combat sounds.

## How the project is organized

Battle state is represented by Unity scene objects. `CellGrid` owns the turn loop and battle state, `Cell` represents each tile, and `Unit` owns character state and combat behavior. There is no separate runtime copy of the board.

| Area | Main responsibility |
| --- | --- |
| `Assets/Code/Grid` | Battle setup, deployment, turn flow, grid states, terrain, and Black Fog |
| `Assets/Code/Units` | Stats, movement, footprints, combat, and progression |
| `Assets/Code/Abilities` and `Assets/Code/Skills` | Player actions, pending action flow, targeting, and skill effects |
| `Assets/Code/Passives` and `Assets/Code/Buffs` | Passive hooks and timed status effects |
| `Assets/Code/Players` and `Assets/Code/AI` | Human and enemy turns and AI decisions |
| `Assets/Code/Campaign`, `Assets/Code/Chapters`, and `Assets/Code/Overworld` | Saves, chapter rules, progression, and shop |
| `Assets/Code/Audio` | Chapter music playback and reusable SFX cues |
| `Assets/Code/UI` and `Assets/Code/WorldUI` | Menus, previews, inspection, tile information, and world-space bars |
| `Assets/Code/Editor` | Unity tools for maps, chapters, units, and saves |

Board input flows through `GameplayInputController` into `CellGrid` states. Movement is previewed before it is committed, so attack range, skill targeting, and terrain effects can use the proposed position. `Unit` is split across partial files for combat, movement, footprints, and other behavior.

## Content and saves

Most gameplay definitions live in `Assets/Data/gdata.json`: items, skills, passives, buffs, and terrain effects. Friendly and enemy unit presets and tile presets live in `Assets/Data/Preset Data (Unit, Tile)`. Chapter scenes carry a `ChapterData` component for their name, unlock and replay rules, battle conditions, enemy order, Black Fog settings, and shop restocks. Display text is kept in `Assets/Data/game_text.csv`.

Character effects are implemented in `SkillEffects.cs`, `PassiveEffects.cs`, and `BuffEffects.cs` under their respective code folders. The catalog provides the definition and effect ID; the effect class provides behavior that needs code. Keep short effect logic inline and locally readable.

To add chapter music, assign **Bgm Intro** (optional) and **Bgm Loop** in that scene's Chapter Data. The sound manager is created automatically at runtime and stops chapter music when leaving the scene. For SFX, create a **TBS > Audio > Sound Library** asset at `Assets/Resources/SoundLibrary.asset` and assign clips to its cues; gameplay code can call `SoundManager.Instance.PlaySfx(SoundCue.Attack)` (or pass an `AudioClip` directly). No SFX calls are wired to combat yet.

Saves are JSON files under Unity's `Application.persistentDataPath`. Scenes under `Assets/Scenes/Level` use the campaign save; `PaintedMap` and other non-level scenes use a separate debug save. The Save Editor defaults to the debug slot, so select **Campaign** there when editing campaign progress.

## Unity editor tools

The **Tools > Windy SRPG** menu includes:

- **Map Painter** for placing terrain, units, and reinforcements.
- **Chapter Manager** for chapter settings and scene unit overrides.
- **Unit Preset Creator** for creating unit presets.
- **Save Editor** for changing roster, equipment, abilities, gold, shop stock, and cleared chapters.
- **Sync All Level Scenes From PaintedMap** for shared scene setup.

Selecting a unit placed in a scene opens its custom inspector for preset overrides and unit settings.

## Development and documentation

The gameplay and editor assemblies can be compiled outside Unity when the generated project files and Unity installation are available:

```powershell
dotnet build com.windy.srpg.game.csproj --no-restore
dotnet build com.windy.srpg.game.editor.csproj --no-restore
```

`Assets/Tools~` contains focused checks and the Stat Gain Lab for exploring five-stat growth rates. These tools are excluded from Unity asset import and use the .NET SDK separately.

For deeper implementation detail, see [the architecture summary](Assets/SUMMARY.md) and [mechanic notes](Assets/Notes). Those documents cover specific systems and design decisions beyond this overview.

## License

Released under [The Unlicense](LICENSE).
