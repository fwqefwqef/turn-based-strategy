# SRPG Project

## Next planned features ✔

# Implement pre-battle Inventory Management

Scene wiring mostly done, check for logic

# Implement pre-battle passive management

In Pre-Battle UI, add a Equip Passive Button, where you can equip and unequip passives from units. 

Each unit should have a passive slot and passive cost limit, and equipping passives should respect both limits. 

Passive slot and max passive cost should depend on Level. Gain +1 cost per 2 levels (Lv2, 4, 6, ...), and +1 slot per 5 levels (Lv5, 10, 15, 20). 
Lv1 4 Cost, 2 Slots
Lv20  14 Cost, 6 Slots

In the Equip Passive Menu, you can select a unit, and upon selecting, it shows a list of passives the unit is currently equipping, and a list of passives equipped by every unit, with your own passives greyed out and unselectable. Equipping/Unequipping passives should work very similarly to Inventory Management, but limited by Passive Slot and Passive Cost limits instead of Inventory Size. There should also be an equip passive storage where you can store passives and take them to equip on units. This should also be in the save file. 
Upon saving, the equip passive changes should be applied on the next load.

# Out of battle menu that lets you select a level to enter