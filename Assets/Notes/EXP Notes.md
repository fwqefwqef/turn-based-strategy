# Levels And EXP

Units have a level from 1 to 20 and an EXP value from 0 to 99.

When EXP reaches 100, the unit levels up and residual EXP overflows into the next level.

Example:

```text
Current EXP: 50
EXP gained: 70
Result: level +1, EXP 20
```

EXP gain is rounded to the nearest integer, floored at 1, and capped at 100. If the unit is already at max level, EXP gain is 0 and displayed EXP stays 0.

Only units that can gain EXP receive EXP. In current code, friendly player units can gain EXP.

## Core Level Scaling

EXP uses a ratio-based level scaling formula.

Let:

```text
base = the base EXP value for the action
difference = target level - user level
```

If levels are equal:

```text
EXP = base
```

If the target/enemy level is higher:

```text
EXP = base * ((difference + 2) / 2)
```

Examples with base 10:

```text
Equal level: 10
Enemy +1:    10 * 3/2 = 15
Enemy +2:    10 * 4/2 = 20
Enemy +3:    10 * 5/2 = 25
Enemy +4:    10 * 6/2 = 30
```

If the user level is higher:

```text
EXP = base * (2 / (abs(difference) + 2))
```

Examples with base 10:

```text
User +1: 10 * 2/3 = 6.66 -> 7
User +2: 10 * 2/4 = 5
User +3: 10 * 2/5 = 4
User +4: 10 * 2/6 = 3.33 -> 3
```

## Combat EXP

Attack or skill targeting an enemy, non-lethal:

```text
base = 10
EXP = LevelScaled(base, user level, enemy level)
```

Attack or skill targeting an enemy, lethal:

```text
base = 30
EXP = LevelScaled(base, user level, enemy level)
```

## Ally Skill EXP

Skill targeting an ally:

```text
base = 10
EXP = LevelScaled(base, user level, average enemy level)
```

For now, average enemy level is derived from the enemies currently in the scene.

Later, when multiple maps are implemented, this should probably use a hardcoded displayed average level value for each map. Enemies can default to that level on creation unless manually adjusted for intentional outliers.

## Area Skill EXP

Area skill affecting enemies only:

```text
base = 10
if at least one target was killed, base = 30
EXP = LevelScaled(base, user level, average target level)
```

Area skill affecting allies only:

```text
base = 10
EXP = LevelScaled(base, user level, average enemy level)
```

Area skill affecting both allies and enemies:

```text
base = 10
if at least one target was killed, base = 30
EXP = LevelScaled(base, user level, average target level)
```

There are also simple helper formulas in code for direct area counts:

```text
AreaEnemyBase * affected enemy count
AreaAllyBase * affected ally count
AreaAnyBase * affected unit count
```

The main skill path uses the level-scaled area formulas above.

## Hooks

EXP gain has hooks so other systems can modify or prevent EXP.

Current hook types:

```text
IP_ModifyExperienceGain
IP_PreventExperienceGain
```

Use cases:

- A passive can multiply self EXP gain.
- A passive can set EXP gain to a fixed amount.
- An enemy passive can prevent attackers from gaining EXP from that enemy.

Current built-in examples:

```text
exp_x2
exp_set_100
prevent_exp_to_attackers
```
