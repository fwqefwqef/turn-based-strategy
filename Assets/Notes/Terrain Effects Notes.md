# Terrain Effects

Design status: implemented. Unity Play Mode verification is still recommended for scene presentation.

## Ownership

- `Cell` owns its battle-local `TerrainEffectInstance` collection and raises `TerrainEffectsChanged`.
- `TerrainEffectSystem` initializes preset effects, ages temporary effects by round, applies live stat modifiers and occupant statuses, and clears battle state.
- `CellGrid.TerrainEffects` exposes the terrain API used by skills, Black Fog, and UI.
- `TerrainEffectRegistry` loads definitions from `Assets/Data/gdata.json`.
- `BlackFogSystem` only calculates chapter-driven coverage and depth; it publishes `black_fog` terrain effects.

Every terrain effect maintains an infinite, non-removable occupancy buff while the unit touches the terrain. Throne, Forest, and Magic Tile bonuses are sourced entirely from those buffs, not from `TerrainEffectData`. The terrain system removes the occupancy buff immediately on exit. Burning Terrain additionally applies a separate, removable `burn` Pain debuff; Black Fog uses its occupancy buff for depth-scaled Pain.

## Starting terrain

`CellTilePreset.StartingTerrainEffectIds` defines effects present when battle begins.

| Preset | Movement cost | Effect |
| --- | ---: | --- |
| Throne | 1 | Defense +5, Evade +20 |
| Forest | 2 | Evade +20 |
| Magic Tile | 1 | Magic +5 |

The preset assets are under `Assets/Data/Preset Data (Unit, Tile)/Tiles` and appear in the Map Painter tile palette.

## Temporary terrain

`SkillTerrainProfile` allows an area skill to create terrain on affected cells even when the area contains no units. `Ignite Ground` is the initial example and is included in Pip's development loadout.

Burning Terrain lasts two full round transitions. Reapplication refreshes its remaining duration. Units standing on it receive both the `burning_terrain` occupancy buff and a separate one-stack, removable Burn debuff that deals 5 Pain damage for up to two incoming-turn Pain ticks. Leaving the terrain removes `burning_terrain` immediately but does not remove Burn.

## Hover strip

`TileHoverStripUI` is a manually authored scene component referenced by `GUIController`. Its `root` panel and TMP `label` are assigned in the Inspector. `GameplayInputController.HoveredCellChanged` supplies the tile for mouse and keyboard navigation, including occupied cells. The strip displays tile name, movement cost, every terrain effect, temporary duration, and Black Fog depth.

Keep the `TileHoverStripUI` component on an always-active controller object. Assign a separate child panel as `root`; the component hides that panel when no tile is hovered. Assign the panel's TextMeshPro label as `label`, then assign the controller component to `GUIController.tileHoverStripUi`.

## Multi-tile rule

Terrain effects resolve across every occupied cell in a unit's rectangular footprint. Each distinct effect ID applies at most once even if several occupied cells contain it. Touching any affected tile is enough to receive that effect, Black Fog uses the greatest touched depth, and each movement step charges the greatest movement cost among the destination footprint's tiles.
