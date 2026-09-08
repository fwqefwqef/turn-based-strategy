using Windy.Srpg.Game.Inventory;
using Windy.Srpg.Game.Passives;
using Windy.Srpg.Game.Skills;
using Windy.Srpg.Game.Units;

int checks = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new Exception(description);
    checks++;
}

var basis = new UnitStatBlock { HitPoints = 10, ManaPoints = 9, MovementPoints = 4, Strength = 1, Magic = 2, Defense = 3, Speed = 4, Luck = 5 };
var overrides = new UnitPresetOverride
{
    StatBonuses = new UnitStatBlock { HitPoints = 100, ManaPoints = 3, MovementPoints = 2, Strength = 5, Magic = 4, Defense = 3, Speed = 2, Luck = 1 }
};
var first = overrides.ResolveStats(basis);
Check(first.HitPoints == 110 && first.ManaPoints == 12 && first.MovementPoints == 6, "HP, MP and movement bonuses add to the preset");
Check(first.Strength == 6 && first.Magic == 6 && first.Defense == 6 && first.Speed == 6 && first.Luck == 6, "All five combat stats are additive");
Check(basis.HitPoints == 10 && basis.Strength == 1, "Resolving never mutates inherited stats");
Check(overrides.ResolveStats(basis).Equals(first), "Repeated editor refreshes never accumulate bonuses");
basis.HitPoints = 20;
Check(overrides.ResolveStats(basis).HitPoints == 120, "Preset changes remain inherited");

var inheritedInventory = new List<StartingInventoryItem> { new() { ItemId = "iron_sword", InitialCharges = -1, ChargesInitialized = true } };
overrides.AdditionalInventory.Add(new StartingInventoryItem { ItemId = "toxic_sword" });
overrides.AdditionalInventory.Add(new StartingInventoryItem { ItemId = "iron_sword", IsDroppable = true });
overrides.AdditionalInventory.Add(new StartingInventoryItem { ItemId = "potion", InitialCharges = 2, ChargesInitialized = true });
overrides.AdditionalInventory.Add(new StartingInventoryItem { ItemId = "empty_potion", InitialCharges = 0, ChargesInitialized = true });
overrides.AdditionalInventory.Add(default);
var inventory = overrides.ResolveInventory(inheritedInventory);
Check(inventory.Count == 5 && inventory[0].ItemId == "iron_sword" && inventory[1].ItemId == "toxic_sword", "Extra items append in order, skipping blank editor entries");
Check(inventory[2].ItemId == "iron_sword" && inventory[2].IsDroppable, "Duplicate item copies and drop flags survive resolution");
Check(inventory[1].InitialCharges == -1 && inventory[1].ChargesInitialized, "New inventory entries use catalog charge defaults");
Check(inventory[3].InitialCharges == 2 && inventory[4].InitialCharges == 0, "Explicit charge counts, including zero, are preserved");
Check(inheritedInventory.Count == 1 && overrides.AdditionalInventory[0].ChargesInitialized == false, "Loadout resolution does not mutate preset or override entries");
Check(overrides.ResolveInventory(inheritedInventory).Count == 5, "Repeated resolution does not multiply inventory additions");
inventory.Clear();
Check(inheritedInventory.Count == 1, "Resolved inventory owns its list");

var inheritedSkills = new[] { new StartingSkillEntry { SkillId = "heal" } };
overrides.AdditionalSkills.AddRange(new[] { new StartingSkillEntry { SkillId = " HEAL " }, new StartingSkillEntry { SkillId = "cleanse" }, default });
Check(overrides.ResolveSkills(inheritedSkills).Select(e => e.SkillId).SequenceEqual(new[] { "heal", "cleanse" }), "Skills append without case-insensitive duplicates");
var inheritedPassives = new[] { new StartingPassiveEntry { PassiveId = "guard" } };
overrides.AdditionalPassives.AddRange(new[] { new StartingPassiveEntry { PassiveId = "GUARD" }, new StartingPassiveEntry { PassiveId = "strength_up" }, default });
Check(overrides.ResolvePassives(inheritedPassives).Select(e => e.PassiveId).SequenceEqual(new[] { "guard", "strength_up" }), "Passives append without duplicates");

overrides.Enabled = false;
Check(overrides.ResolveStats(basis).Equals(basis), "Disabling overrides restores inherited stats");
Check(overrides.ResolveInventory(inheritedInventory).Count == 1, "Disabling overrides removes extra items from resolution");
Check(overrides.ResolveSkills(inheritedSkills).Count == 1 && overrides.ResolvePassives(inheritedPassives).Count == 1, "Disabling overrides removes extra skills and passives");
overrides.Enabled = true;
Check(overrides.ResolveInventory(inheritedInventory).Count == 5, "Re-enabling retains authored additions");
overrides.AdditionalInventory = null;
overrides.AdditionalSkills = null;
overrides.AdditionalPassives = null;
Check(overrides.ResolveInventory(null).Count == 0 && overrides.ResolveSkills(null).Count == 0 && overrides.ResolvePassives(null).Count == 0, "Missing lists in older serialized data are safe");
Console.WriteLine($"Passed {checks} checks against the compiled game's preset override resolver.");
