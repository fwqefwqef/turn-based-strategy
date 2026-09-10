# Black Fog Mechanic Notes

Design status: implemented. Play Mode verification is still recommended for chapter timing and overlay presentation.

## Core Behavior

Black Fog is a battle-only map hazard that encroaches on the map during combat.

- It is hidden during pre-battle.
- It begins on the configured arrival turn.
- Default arrival turn: 6.
- Default direction: Left.
- Default expansion distance: 2 tiles per player DoT phase.
- Once it arrives, it expands every player turn.
- On the arrival turn, it immediately covers the first configured expansion distance from its configured direction.
- It affects only player units.
- It can directly kill units.
- Fogged cells remain traversable.
- Fogged cells should show a translucent black tile highlight/overlay.

## Chapter Data

Each chapter should expose Black Fog configuration:

- `BlackFogTurn`: int, default 6.
- `BlackFogDirection`: enum, default Left.
- `BlackFogExpansionDistance`: int, default 2.

Supported directions:

- Left: fog enters from the left edge and advances right.
- Right: fog enters from the right edge and advances left.
- Up: fog enters from the top edge and advances downward.
- Down: fog enters from the bottom edge and advances upward.

## Turn Timing

Black Fog damage should happen during the same timing window as player DoT effects. Black Fog expansion should happen after that DoT damage is calculated, effectively at the start of the player's turn.

Intended flow:

```text
Player Turn
Enemy DoT Effects
Enemy Turn
Player DoT Effects, then Black Fog expansion
Player Turn
```

For the player DoT phase:

```text
1. Refresh Black Fog debuffs from the current fog state.
2. Tick player DoT effects, including Black Fog damage from already-fogged tiles.
3. Expand Black Fog by the configured expansion distance if the upcoming player turn is at or after the configured arrival turn.
4. Refresh Black Fog debuffs again so newly fogged units display the status during the player's turn.
5. Start the player's turn.
```

This means a unit standing in a newly fogged tile receives the Black Fog debuff for the upcoming player turn, but does not take Black Fog damage until the next player DoT phase if it remains in fog.

## Debuff Responsibility

Black Fog should be represented as a debuff while a player unit is standing in fog.

- Standing in Black Fog applies the Black Fog debuff.
- Leaving Black Fog removes the Black Fog debuff.
- The debuff should cleanse itself when the unit is no longer standing in Black Fog.

Recommended responsibility split:

- Black Fog system owns terrain state:
  - which cells are fogged
  - current expansion step
  - tile depth
  - tile visual overlay
  - applying/removing the Black Fog debuff based on position

- Black Fog debuff owns DoT behavior:
  - during DoT tick, ask the Black Fog system for the unit's current tile depth
  - deal damage based on that depth
  - allow the damage to kill the unit

The debuff should not own fog expansion or tile visuals.

## Damage Formula

Black Fog damage:

```text
25% Max HP * (depth + 1)
```

Depth definition:

- The current fog frontier/edge has depth 0.
- One tile deeper into the fog has depth 1.
- Two tiles deeper has depth 2.
- And so on.

Example from Left direction:

```text
F F F . . .
2 1 0
```

Damage examples:

- Depth 0: 25% Max HP
- Depth 1: 50% Max HP
- Depth 2: 75% Max HP
- Depth 3: 100% Max HP

Use a clear rounding rule when implemented. Preferred default: round up so fog is never softened by fractional HP.

## Implementation Notes

The future generic DoT phase should support more than Black Fog.

Black Fog should plug into that shared phase, rather than becoming a special one-off turn transition.

Likely structure:

```text
CellGrid turn transition
  -> ProcessDotPhase(playerId)
       -> if playerId is player:
            BlackFogSystem.RefreshFogDebuffs()
       -> each unit for playerId ticks DoT effects
       -> if playerId is player:
            BlackFogSystem.ExpandIfNeeded()
            BlackFogSystem.RefreshFogDebuffs()
```

The Black Fog debuff can then calculate damage from the live map state:

```text
BlackFogBuffEffect.OnDotTick(unit)
  -> depth = BlackFogSystem.GetDepth(unit.Cell)
  -> damage = ceil(unit.MaxHitPoints * 0.25 * (depth + 1))
  -> apply true/direct damage
```

Important constraint: newly expanded fog should be applied after the DoT tick, so new fog is visible during the upcoming player turn but does not damage until the next player DoT phase.

Current implementation:

- `BlackFogSystem` owns directional layer coverage, depth, overlays, expansion state, and positional status refresh.
- `CellGrid.BlackFog` connects the system to battle start, incoming-turn DoT phases, occupancy changes, and battle cleanup.
- `black_fog` is an infinite, non-removable `Pain` status whose built-in effect reads the unit's live fog depth.
- `CellHighlighter` renders Black Fog on an independent translucent overlay so ordinary selection and enemy-range highlights can coexist with it.
