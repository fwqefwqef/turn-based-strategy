Before we go further into implementing inspect UI, We haven't implmemented passives yet, have we?

I want passives to be split into 2 categories

Unique Passives:
Passives that are unique to the character and are learned on level up.

Equip Passives:
Passives that are equipped and unequipped by the unit. Units will have a maximum number of passive slots and a maximum passive cost based on their level (also not implemented yet.)
For now, just keep the passive slot maximum at 4 and passive cost maximum as 12.
Later, there will be a system to steal equip passives off defeated enemies and slot them on your own characters after battle.

Equip passives will have an integer cost, even negative cost that will offset a more expensive passive. Unique passives can be initialized as 0 cost but won't interact with passive cost and slots.

Passives should be very similar to buffs in that they have an effectId that can implement all sorts of combat effects, and also they can have a shortcut for stat gains in PrimaryStatModifier and SecondaryStatModifier

However, they are gained at the start of battle (and on level up in the future), they should not be conventionally gained or lost in battle, and they are infinite duration.

Here are 2 simple passives we should implement

1. Deep Breath
Equip Passive, 1 cost
Restore 3 HP at the start of turn.

2. Arcana Knowledge
Unique Passive
Magic +5

Make a basic passive catalogue that just gives this to everyone.
Any questions?