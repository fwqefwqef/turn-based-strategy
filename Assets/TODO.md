# SRPG Project
## Next planned features ✔

# Revise control scheme for controller / keyboard only play

Inspect should be switched to left+right click, and also 

Left Click / Keyboard X / Controller A = select
Right Click / Keyboard Z / Controller Z = cancel
Left+Right Click / Keyboard S / Controller X = Inspect unit
Middle Mouse Click / Keyboard A / Controller Y = Display range of hovered enemy, if none hovered then display range of all enemies (not implemented yet)

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