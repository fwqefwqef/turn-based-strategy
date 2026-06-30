# SRPG Project

## Next planned features ✔

# Improve AI evaluation in Unit.cs.

I want to improve the damage evaluation and add skill selection for the enemies.

Damage Evaluation
Calculate damage based on chance to hit and chance to crit, then get a weighted average damage that accounts for probability of miss, hit, or crit. 

For this evaluation, consider attack and all usable skills, including area spells, against all targets within range. It should proiperly consider the amount of hits, and the unique properties of some skills like ignoring def/res.
For area spells, consider the most targets hittable with the range and radius of the spell, and take the sum of those damages. 
Remember that area spells are not affected by hit or crit chance.
However, spells whose MP cost are higher than current MP should be excluded from evaluation.

Ultimately, the action with the highest damage evaluation should win and be selected as an action to take by that enemy.

Prioritize killing a target. Boost evaluation by 10x if the selected action is projected to kill.
Prioritize not taking damage. Boost evaluation by 1.5x if the selected action does not result in a counterattack.
Prioritize not spending MP. Boost evaluation by 1.1x if the selected action does not cost MP to use.
On highest evaluation tie, select a random action.

# Add distinct AI behavior
AI_Attack = The default AI, moves towards friendlies and attacks a friendly within range.
AI_Wait = Waits until a friendly unit enters its attack range, then behaves like AI_Attack
AI_WaitGroup = Waits until a friendly unit enters 2 units with the same WaitGroup id, then behaves like AI_Attack
id = 0,1,2,...
This should trigger even when the unit itself is not in range within a friendly, but 2 units with the same WaitGroup id are.

# Implement Enemy Range Indication

Program attack range
A / Left+Right Click on a enemy unit: Display that unit's attack range (attack command + damaging skills) in red (make it linger until you press A/Left+Right Click again on the same unit. Multiple units' attack ranges can be shown at once)
A / Left+Right Click on a blank tile: Display all enemies' collective attack ranges in pink under the red range. Do it again and it will clear the pink attack range.

# Implement passive slots

Passive slot and cost should depend on Level. Maybe gain +1 cost per 2 levels, and +1 slot per 5 levels. 
Base Level is 1 and Max Level is 20 but should be configurable.

# Out of battle menu that lets you select a level