# Terrain Effects

Design status: implemented. Unity Play Mode verification is still recommended for scene presentation.

## Ownership

- `Cell` owns its battle-local `TerrainEffectInstance` collection and raises `TerrainEffectsChanged`.
- `TerrainEffectSystem` initializes preset effects, ages temporary effects by round, applies live stat modifiers and occupant statuses, and clears battle state.
- `CellGrid.TerrainEffects` exposes the terrain API used by skills, Black Fog, and UI.
- `TerrainEffectRegistry` loads definitions from `Assets/Game/Data/gdata.json`.
- `BlackFogSystem` only calculates chapter-driven coverage and depth; it publishes `black_fog` terrain effects.

Terrain stat bonuses are not ordinary buffs. They cannot be cleansed and disappear immediately when occupancy changes. Burning Terrain applies the removable `burn` Pain status. Black Fog applies its non-removable exposure status only while a player unit occupies fog.

## Starting terrain

`CellTilePreset.StartingTerrainEffectIds` defines effects present when battle begins.

| Preset | Movement cost | Effect |
| --- | ---: | --- |
| Throne | 1 | Defense +5, Evade +20 |
| Forest | 2 | Evade +20 |
| Magic Tile | 1 | Magic +5 |

The preset assets are under `Assets/Game/Data/Preset Data (Unit, Tile)/Tiles` and appear in the Map Painter tile palette.

## Temporary terrain

`SkillTerrainProfile` allows an area skill to create terrain on affected cells even when the area contains no units. `Ignite Ground` is the initial example and is included in Pip's development loadout.

Burning Terrain lasts two full round transitions. Reapplication refreshes its remaining duration. Units standing on it receive a one-stack, removable Burn status that deals 5 Pain damage for up to two incoming-turn Pain ticks. Leaving the terrain does not remove an already-applied Burn.

## Hover strip

`TileHoverStripUI` is a manually authored scene component referenced by `GUIController`. Its `root` panel and TMP `label` are assigned in the Inspector. `GameplayInputController.HoveredCellChanged` supplies the tile for mouse and keyboard navigation, including occupied cells. The strip displays tile name, movement cost, every terrain effect, temporary duration, and Black Fog depth.

Keep the `TileHoverStripUI` component on an always-active controller object. Assign a separate child panel as `root`; the component hides that panel when no tile is hovered. Assign the panel's TextMeshPro label as `label`, then assign the controller component to `GUIController.tileHoverStripUi`.

## Multi-tile rule

The terrain system currently resolves the unit's canonical occupied cell. When multi-tile footprints are introduced, the resolver should union distinct effect IDs across the footprint so identical effects never multiply. Black Fog should use the greatest covered depth and terrain movement should use the greatest destination-footprint cost.
