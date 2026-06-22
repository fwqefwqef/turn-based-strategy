# SRPG Project

## Next planned features ✔


# Revise control scheme

In prebattle UI, in switch deployment slot / select unit UI, upon selecting a unit, the button focus should linger on the same unit, instead of defaulting to the first button in the UI. this way, you can cancel selection if you just select twice on the same unit, which is the click behavior. 

Program attack range
A / Left+Right Click on a enemy unit: Display that unit's attack range (attack command + damaging skills) in red (make it linger until you press A/Left+Right Click again on the same unit. Multiple units' attack ranges can be shown at once)
A / Left+Right Click on a blank tile: Display all enemies' collective attack ranges in pink under the red range. Do it again and it will clear the pink attack range.

# Smart action shortcut

While having selected a friendly unit, on hovering over an enemy, show an attack icon, and when the enemy is clicked and within range, preview move to attack range and open attack preview panel. 
For characters with a healing spell, on hovernig over an ally, show a heal icon, and when the ally is clicked and within range, preview move to spell range and open heal preview panel.

# Now implement passive slots

Passive slot and cost should depend on Level. Maybe gain +1 cost per 2 levels, and +1 slot per 5 levels. 
Base Level is 1 and Max Level is 20 but should be configurable.

# Improve AI evaluation DryAttack in Unit.cs.

 Currently it does not consider counterattacking, does not consider average damage outcome, just 1 roll from defend handler multiplied by numhits and pursuit attack.
Highlight tiles/units that enemies are targeting. Red for enemy -> friendly/any, blue for enemy -> enemy. One tile for single-target, all tiles for area spells.

# Make an enemy map roster manager + painter tool

# Alternate between two chapters and confirm roster changes are working

