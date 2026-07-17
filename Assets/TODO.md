# SRPG Project

## Next planned features ✔

# Remove all formerlyserialized in the codebase

# Design Levels 1-4.

# Reinforcement spawn behavior
Reinforcement spawn behavior needs to be added. 
Reinforcement tile: 
- Holds a list of unit objects that represents which units are spawned in which order (this is going to be used mostly for enemies, but it should support any type of unit)
- Integer list that represents at which turns the units are spawned 
(unit[0] is spawned at turn[0], then unit[1] is spawned at turn[1], and so forth. If there are no more units left in the unit list, just loop back to the first.)
- These tiles spawn units at the start of player turn, on the specified turns. 
- If there is a unit occupying the reinforcement tile, spawn the unit on a random nearest adjacent tile.
The reinforcement tile should have an optional spawner linked to it, that is going to be implemented as an immovable enemy unit. If that unit is killed, the reinforcement tile should stop producing units. If no spawner is assigned, the reinforcement tile produces units regardless.

# Chapter feature
Individual Scene = Chapter
Each Chapter should store
- average enemy level field
- Win/Lose condition (Default Defeat all enemies / Lose all allies)
- Black Fog Turn and Direction (Explained below)
- Many others

# Black Fog mechanic
Black Fog mechanic needs to be implemented
Black fog encroaches on the map little by little, at the end of the player turn. Black Fog damages the player after the end of the enemy turn and before the start of the player turn, and this should apply to all DoT effects in the future.
Black fog makes the tile highlight translucent in black, still traversble, but deals damage if you stand on it. Make it deal 6 * (depth+1) damage, where depth is how far the fog tile is away from the nearest edge black fog tile. The edge fog tile deals 6 damage, and 1 deeper deals 12 damage, and so on. Make the translucent black a gradient, that gets blacker and blacker, the higher the depth.
Black fog arrives at a specified int turn, and shrinks the map by 2 tiles, either in left, up, down, or right direction. Black fog covers all row/columns starting from the specified direction, and has a dark purple warning highlight that warns the player that the black fog will arrive at those tiles after ending your turn.


# Passive crack behavior

Some enemies are crackable, you can set enemies to crack, and when they are killed, they drop their equip passives. You can crack up to 4 enemies per map, which are set randomly but you can change.

# Level4 involves a multi-tile boss

Multi-tile unit should be supported. Let's make a 3x3 boss for one.
A unique AI behavior is also necessary to support this boss.
Juggernaut
Moves forwards 2 tiles every time. 
When it reaches the end tile you automatically lose (need to add support for alternative lose condition)