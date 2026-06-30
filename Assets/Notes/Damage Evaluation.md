# Damage Evaluation
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