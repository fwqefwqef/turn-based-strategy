# Debuff checks

Run from the project root:

```powershell
dotnet run --project 'Assets/Tools~/DebuffChecks/DebuffChecks.csproj'
dotnet build com.windy.srpg.game.editor.csproj --no-restore
```

The console checks compile the actual buff engine, built-in buff and weapon effects,
and item definitions against small host adapters. They read the real `gdata.json`.
They cover stacking, duration refresh, three Toxic ticks, stun duration when applied
during the target's turn, stat reductions, cleansing filters, Death's Door status
separation, Toxic application at negative HP, movement capping, and battle cleanup.
Black Fog checks cover its non-removable status and rounded damage at multiple depths.
They do not exercise Unity's scene lifecycle, input, AI, or HUD.

## Play Mode checks

Equip `toxic_sword`, `stun_sword`, and `weakening_sword` through existing unit
loadout tools. `cleanse` is an adjacent-ally support spell costing 3 MP and ending
the caster's turn. The four existing owned units received it in the local save;
reload the campaign/battle to load those saved skills.

1. Hit with Toxic Sword, then advance turns. Before the afflicted side acts,
   the combat HUD should show each affected unit separately, pause for 0.3 seconds,
   subtract 5 HP per stack, and hold the result for 0.65 seconds. Verify three ticks,
   stack cap five, refresh at cap, and lethal damage without a stuck turn transition.
2. Hit with Stun Sword. The target must not counter. On its next turn, an ally is
   greyed out and cannot move or act; an enemy is skipped. Stun expires at that
   turn's end. A stun received from a counterattack must survive the current turn
   and skip the following turn. It also prevents pursuit after that counterattack.
3. Hit with Weakening Sword repeatedly. Inspect Str/Mag/Def/Spd/Lck for reductions
   from -1 to -5. They should persist through turns, disappear after battle, and
   affect later strikes in the same combat.
4. Cleanse a unit carrying all three debuffs. All three should disappear while
   beneficial buffs remain. Cleansing a stunned ally during its skipped turn
   restores its action; cleansing a unit that already acted must not grant another.
5. Misses must not apply statuses. Counterattacks and weapon combat arts can apply
   them; ordinary spells and area spells must not inherit equipped sword effects.
