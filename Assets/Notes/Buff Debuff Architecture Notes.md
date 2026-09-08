# Buff / Debuff Architecture Notes

Design status: planned, partially implemented.

## Naming

Buffs and debuffs are both represented as `Buffs` in code.

The code-level term can stay `Buff` for all temporary status effects, while design/UI categories identify whether the effect is beneficial, harmful, controlling, or damage-over-time.

## Categories

Each buff should have a category.

Planned categories:

- `Buff`: beneficial effect.
- `Weakening`: debuff that lowers stats.
- `CC`: debuff that prevents actions, restricts movement, or otherwise controls the unit.
- `Pain`: debuff that deals damage over time.
- `Misc`: debuffs that do not cleanly fit in one category.

Examples:

- Strength Up: `Buff`
- Defense Down: `Weakening`
- Stun or Root: `CC`
- Poison or Black Fog: `Pain`
- Death's Door state: `Misc`

## Duration

Remaining turns are already implemented.

Keep using remaining duration for temporary statuses.

Duration behavior should remain explicit:

- finite duration: decrements through the turn timing system
- infinite duration: does not decrement
- battle-local duration: lasts until battle cleanup even if represented as infinite in the runtime buff list

## Stacking

Each buff should define a maximum stack count.

Default max stacks: 1.

Desired behavior:

```text
If applying a buff that is not already present:
  add it with 1 stack

If applying a buff that is already present:
  increase stacks up to max stacks
  refresh duration
```

Stacking needs to be handled at the buff-instance level, not by adding duplicate entries with the same buff id.

Reapplying an existing buff always refreshes remaining duration. This applies even when the buff is already at maximum stacks, including single-stack buffs with `MaxStacks = 1`. There is no per-buff toggle for this behavior.

Likely runtime data:

```text
BuffData:
  BuffCategory category
  int MaxStacks = 1
  bool Removable = true/false

Buff:
  int RemainingDuration
  int Stacks
```

Stat modifiers should multiply by stack count unless a specific effect overrides that behavior.

## Removable

Buffs should expose whether they can be removed by cleanse effects.

Planned field:

```text
bool Removable
```

Design rules:

- Beneficial buffs may be removable or not, depending on effect.
- Debuffs may be removable through cleansing effects if `Removable = true`.
- Some debuffs should not be removable.
- Black Fog debuff likely should cleanse itself when the unit leaves fog, but should not necessarily be removable by ordinary cleanse unless explicitly allowed.
- Death's Door penalty likely should be non-removable unless design changes.

## DoT Timing

Damage-over-time effects should tick in a shared DoT phase, not scattered inside unrelated turn-start hooks.

Planned flow:

```text
Player Turn
Enemy DoT Effects
Enemy Turn
Player DoT Effects
Player Turn
```

Black Fog should participate in the player DoT phase, with the timing details recorded in `Black Fog Notes.md`.

## Implementation Implications

Existing buff effect callbacks include turn-start and turn-end hooks.

Future cleanup should consider adding a dedicated DoT callback, for example:

```text
IP_DotTick
  OnDotTick(Unit unit, Buff entry)
```

This would prevent `Pain` effects from being hidden inside generic `OnTurnStart` or `OnTurnEnd` logic.

Cleanse effects should operate by category and removability:

```text
Remove all removable Weakening / CC / Pain debuffs
or
Remove all removable debuffs matching selected categories
```

The category system should support UI display, filtering, and cleanse targeting.
