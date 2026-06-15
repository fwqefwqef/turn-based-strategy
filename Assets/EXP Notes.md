# Add Levels and EXP

Units now have a positive int number level (1-20), and an integer EXP (0-100)
When EXP reaches 100, set EXP to 0 and increase level by 1.
Residual EXP should overflow to the next level (example: gain 70 EXP at 50 EXP = You now have 20 EXP with 1 level higher.)
EXP gain is floored at 1 and capped at 100. When maximum level is reached (configurable, 20 for now), EXP does not increment beyond 0.

EXP is granted in combat, and
Variable EXP should be granted depending on your level vs enemy level
This will require a formula to be made.

[EXP Gain Formulas] - All of these should be easily tweakable

Attack/Skill targeting an enemy, not lethal: 10 * (1 + enemy level - your level)
Attacking an enemy, lethal: 30 * (1 + enemy level - your level)

Skill targeting an ally: 10 * (1 + Total average of enemy level - your level)

Area Spells
Enemy Target: 10 * (1 + average of target levels - your level)
Ally Target: 10 * (1 + Total average of enemy level - your level)
Any Target: 10 * (1 + average of target levels - your level)

There should be hooks for EXP gain so that:
You can add a passive that multiplies self EXP gain by a number amount.
Enemies can have a passive that prevents any attackers from gaining EXP off them.

A note on total average of enemy level. Later, When there are multiple maps implemented in the game, I want there to be a hardcoded displayed average level value for each map that represents the average level of the enemy. And enemies will default to this level on creation unless manually touched, for outliers who are intentionally weaker or stronger. For now, it can be derived from just manually getting the average of the enemy levels.
