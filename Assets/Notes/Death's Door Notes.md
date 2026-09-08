# Death's Door Mechanic Notes

Design status: planned, not implemented yet.

## Core Behavior

Death's Door is a player-only survival mechanic.

- Player units can survive reaching 0 HP or below while they are not already at Death's Door.
- Enemy units die normally when they reach 0 HP.
- A player unit at Death's Door dies if hit again while already in that state.
- Damage-over-time can trigger Death's Door.
- Damage-over-time can kill a unit that is already at Death's Door.
- Player units remain at their literal reduced HP value, including 0 or negative HP.
- A unit must be healed above 0 HP to leave Death's Door.
- A unit can enter Death's Door again later if healed out of it first.
- Death's Door is battle-local and should not permanently alter the unit after the battle.

## Trigger

When a player unit would be reduced to 0 HP or below:

```text
If the unit has not entered Death's Door yet:
  keep the literal 0 or negative HP value
  apply Death's Door debuff
  apply Death's Door penalty
  keep the unit alive

If the unit is already in Death's Door:
  the unit dies normally
```

Important implementation constraint: do not clamp Death's Door units to 1 HP. Negative HP matters because the unit must be healed by enough HP to rise above 0.

Example:

```text
Bastion is hit from 10 HP to -8 HP.
Bastion enters Death's Door at -8 HP.
Bastion needs at least 9 healing to reach 1 HP and leave Death's Door.
```

## Penalty

Upon entering Death's Door, the unit gains a battle-local stat penalty:

```text
Str -1
Mag -1
Def -1
Spd -1
Lck -1
```

The penalty stacks up to 5 times.

Each time the unit enters Death's Door, add one stack of the stat penalty, up to 5 stacks.

Death's Door also lowers movement to 1 while the unit is in the Death's Door state.

This movement penalty belongs to the Death's Door state debuff, not the stacking stat penalty debuff.

## Suggested Implementation Shape

The unit should be allowed to have `HP <= 0` while still alive if it is at Death's Door.

This will require careful review of existing `HP <= 0` checks, because many systems may currently assume that means destroyed.

Recommended fields on `Unit`:

```text
bool IsAtDeathsDoor
int DeathsDoorPenaltyStacks
```

Recommended effect shapes:

```text
Death's Door State Buff
  category: Misc
  duration: until healed above 0 HP or battle cleanup
  max stacks: 1
  removable: false by default
  movement effect:
    movement becomes 1 while active
```

```text
Death's Door Penalty Buff
  category: Weakening
  duration: battle-only or infinite until battle cleanup
  max stacks: 5
  removable: false by default unless design changes
  stat modifiers per stack:
    Str -1
    Mag -1
    Def -1
    Spd -1
    Lck -1
```

Death's Door should be triggered in the damage/death resolution path, not as an ordinary buff trigger, because it changes whether lethal damage destroys the unit.

The Death's Door state and the stat penalty should be separate buffs.

Healing logic should check whether the unit has risen above 0 HP:

```text
If unit is at Death's Door and HP becomes greater than 0:
  remove Death's Door state buff
  keep Death's Door penalty stacks
```

The stat penalty stacks last for the rest of the battle.

## Open Questions

- Should the penalty be removable by cleansing effects?
- Should self-damage or sacrifice effects trigger Death's Door?
