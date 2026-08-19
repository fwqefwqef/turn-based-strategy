# SRPG Project

## Next planned features ✔

# Add Level Progress Tracker

Levels should be cleared in order. Clearing one level lets you move to the next level, or replay already cleared levels.
Inaccessible levels should be greyed out and cannot be entered.

Already cleared levels show a checkmark character next to it. 

Add to chapter data: Chapter Name, Chapter id, Replayable
Chapter Name is the displayed name in the overworld.
Chapter id is the integer id of the chapter, that is used as the chapter tracker.
Replayable denotes whether the chapter is replayable by entering it in the overworld. If false, it should be displayed with a checkmark but greyed out so you cannot re-enter the level once you have cleared it. 

I'm not sure what the best structure for the progress tracking is.
I want there to be a few replayable side-levels that are unlocked after clearing a certain chatper but do not unlock any new chapters themselves.
Maybe an unlock condition in the chapter data that denotes which chapter id needs to be cleared to be able to access this level?

There is currently "Chapter 1" and "Chapter 2" scenes. Make both non-replayable, and make Chapter 2 unlock upon clearing Chapter 1.

# Add to chapter data item stock

Clearing levels should add to stock configurable items of configurable quantity.
For now, add 1 Magic sword whenever a level is cleared. 


# Add droppable items
Enemies should drop gold? Maybe make them carry Bullions, which can be sold for Gold. This requires making items droppable with a bool field, signified by a green highlight. 

# Design Levels 1-4. More sophisticated tasks:

# Black Fog mechanic
Black Fog mechanic needs to be implemented
Black fog encroaches on the map little by little, at the end of the player turn. Black Fog damages the player after the end of the enemy turn and before the start of the player turn, and this should apply to all DoT effects in the future.
Black fog makes the tile highlight translucent in black, still traversble, but deals damage if you stand on it. Make it deal 6 * (depth+1) damage, where depth is how far the fog tile is away from the nearest edge black fog tile. The edge fog tile deals 6 damage, and 1 deeper deals 12 damage, and so on. Make the translucent black a gradient, that gets blacker and blacker, the higher the depth.
Black fog arrives at a specified int turn, and shrinks the map by 2 tiles, either in left, up, down, or right direction. Black fog covers all row/columns starting from the specified direction, and has a dark purple warning highlight that warns the player that the black fog will arrive at those tiles after ending your turn.

- Black Fog Turn and Direction add to chapter data

# Passive crack behavior

Some enemies are crackable, you can set enemies to crack, and when they are killed, they drop their equip passives. You can crack up to 4 enemies per map, which are set randomly but you can change them.

# Level4 involves a multi-tile boss

Multi-tile unit should be supported. Let's make a 3x3 boss for one.
A unique AI behavior is also necessary to support this boss.
Juggernaut
Moves forwards 2 tiles every time. 
When it reaches the end tile you automatically lose (need to add support for alternative lose condition)

# Make chapters unlockable from 1-4, progress stored in save file
# Add class passives & promotion behavior
Passives will have 2 types: class passive, equip passive
equip passives are shared passives that can be equipped, unequipped, cracked.
class passives are specific to the unit, learned on level up, and cannot be cracked.

# Add more overworld features

Shop -> Shows a catalog of items and current gold, and can purchase / cancel. 

Load a catalog of available items. Iron Sword and Magic Sword. Iron Sword is available in unlimited quantity, for magic sword, only 1 is available in the shop. 

Make the current save file store 5000 gold. 

IF PURCHASED
Give item to who? menu shows up -> Shows a list of characters, including Storage at the top
If character inventory not full -> places it in an empty slot
If character inventory is full -> select an item in the inventory to replace / OR cancel
Selecting storage just sends the item directly to storage.

Add a save button. Saving applies the item & gold changes into the save file.


Seperate Overworld into Levels, Shop, Units

Units -> Shows a list of units, and can further manage their Inventory & Passives. Work on later.

Shop (catalog depends on which level you have cleared)
Units (Inventory & Passive management)