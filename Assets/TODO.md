# SRPG Project

## Next planned features ✔

# Add death's door mechanic
Player units can enter <= 0HP once. If hit again at this state, die. Enemies die normally when they hit 0HP. Upon reaching death's door. Gain Str/Mag/Def/Spd/Lck -1 for this battle, stacking up to 5 times. 
When at death's door, movement is lowered to 1.

# Black Fog mechanic
Black fog encroaches on the map little by little, at the start of each player turn. Black Fog damages the player after the enemy's turn ends and before the player's next turn starts, and this should apply to all DoT effects in the future. 
Implement it like this: While the unit is standing in Black Fog, gain a Black Fog debuff. It cleanses itself when you exit Black Fog.

Player Turn
Process DoT on enemies
Enemy Turn
Process DoT on players
Player Turn 

Black fog makes the tile highlight translucent in black, still traversble, but deals damage if you stand on it. Make it deal 25% Max HP * (depth+1) damage, where depth is how far the fog tile is away from the nearest edge black fog tile. The edge fog tile deals 25% Max HP as damage, and 1 deeper deals 50%, and so on. 

Black fog arrives at the start of a specified int turn, either in left, up, down, or right direction. Black fog covers all row/columns starting from the specified direction.

- Add Black Fog Turn and Direction to chapter data. Black fog arrives at default Turn 6 and Left direction.

# Level4 involves a multi-tile boss

Multi-tile unit should be supported. Let's make a 3x3 boss for one.
A unique AI behavior is also necessary to support this boss.
Juggernaut
Moves forwards 2 tiles every time. 
When it reaches the end tile you automatically lose (need to add support for alternative lose condition)

==============================
This is the split where SRPG direction may diverge. Make sure to make a visible backup / fork here.

# Design Levels 1-4. Design 4-6 Characters.
