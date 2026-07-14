# SRPG Project

## Next planned features ✔

# I want to make a new scene which is the "Overworld Menu" that lets you select a level to enter

In pre-battle menu, add a new button that lets you exit to main menu. 

In the main menu, you can choose from a scroll list of scenes to enter. Entering will start the pre-battle view of that selected level.

Currently, the available levels are Level0 and Level1 in Assets > Scenes > Level folder.


# Design Levels 1-4.

# Level4 involves a multi-tile boss

Multi-tile unit should be supported. Let's make a 3x3 boss for one.
A unique AI behavior is also necessary to support this boss.
Juggernaut
Moves forwards 2 tiles every time. 
When it reaches the end tile you automatically lose (need to add support for alternative lose condition)
Reinforcement spawn behavior needs to be added. 
Reinforcement tile: 
- Holds a list of unit objects that represents which units are spawned (mostly enemies but it should support any type of unit)
- List of int numbers represent at which turns the units are spawned 
(Unit 1 is spawned at turn[0], then unit 2 is spawned at turn[2], and so forth. If there are no more units left in the unit list, just go back to the first.)
- These tiles spawn units at the start of player turn, on the specified turns. 
- The unit is spawned at the nearest unoccupied tile if the reinforcement tile is occupied.

Black Fog mechanic needs to be implemented
Black fog encroaches on the map little by little, at the end of the player turn. Black Fog damages the player after the end of the enemy turn and before the start of the player turn, and this should apply to all DoT effects in the future.
Black fog makes the tile highlight translucent in black, still traversble, but deals damage if you stand on it. Make it deal 6 * (depth+1) damage, where depth is how far the fog tile is away from the nearest edge black fog tile. The edge fog tile deals 6 damage, and 1 deeper deals 12 damage, and so on. Make the translucent black a gradient, that gets blacker and blacker, the higher the depth.
Black fog arrives at a specified int turn, and shrinks the map by 2 tiles, either in left, up, down, or right direction. Black fog covers all row/columns starting from the specified direction, and has a dark purple warning highlight that warns the player that the black fog will arrive at those tiles after ending your turn.



# Remove all formerlyserialized in the codebase