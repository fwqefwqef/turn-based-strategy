# SRPG Project

## Next planned features ✔

# Add distinct AI behavior
AI_Attack = The default AI, moves towards friendlies and attacks a friendly within range.
AI_Wait = Waits until a friendly unit enters its attack range, then behaves like AI_Attack
AI_WaitGroup = Waits until a friendly unit enters 2 units with the same WaitGroup id, then behaves like AI_Attack
id = 0,1,2,...
This should trigger even when the unit itself is not in range within a friendly, but 2 units with the same WaitGroup id are.

# Implement enemy sprites showing in Attack Preview, COmbat Sequence HUD, Inspect Panel
A new property in unit preset should be added called face sprite, and this is the face that will be resized and projected onto the empty rect transforms indicating where the face goes within these UIs.
If no face sprite is set, then just use the unit sprite instead.

# Implement passive slots

Passive slot and cost should depend on Level. Maybe gain +1 cost per 2 levels, and +1 slot per 5 levels. 
Base Level is 1 and Max Level is 20 but should be configurable.

# Out of battle menu that lets you select a level to enter