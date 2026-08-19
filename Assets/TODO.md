# SRPG Project

## Next planned features ✔

# Add droppable items
Enemies should drop gold? Maybe make them carry Bullions, which can be sold for Gold. This requires making items droppable with a bool field, signified by a green highlight.

# Design Levels 1-4.
# Character Design
https://docs.google.com/document/d/1Y6QMzXYegTebZVENsxgHyMaUxY_MVO_SdaqCbWU9lSk/edit?usp=sharing

# Black Fog mechanic
Black Fog mechanic needs to be implemented
Black fog encroaches on the map little by little, at the end of the player turn. Black Fog damages the player after the end of the enemy turn and before the start of the player turn, and this should apply to all DoT effects in the future.
Black fog makes the tile highlight translucent in black, still traversble, but deals damage if you stand on it. Make it deal 6 * (depth+1) damage, where depth is how far the fog tile is away from the nearest edge black fog tile. The edge fog tile deals 6 damage, and 1 deeper deals 12 damage, and so on. Make the translucent black a gradient, that gets blacker and blacker, the higher the depth.
Black fog arrives at a specified int turn, and shrinks the map by 2 tiles, either in left, up, down, or right direction. Black fog covers all row/columns starting from the specified direction, and has a dark purple warning highlight that warns the player that the black fog will arrive at those tiles after ending your turn.

- Black Fog Turn and Direction add to chapter data

# Level4 involves a multi-tile boss

Multi-tile unit should be supported. Let's make a 3x3 boss for one.
A unique AI behavior is also necessary to support this boss.
Juggernaut
Moves forwards 2 tiles every time. 
When it reaches the end tile you automatically lose (need to add support for alternative lose condition)