using Windy.Srpg.Game.Buffs;
using Windy.Srpg.Game.Catalogs;
using Windy.Srpg.Game.Inventory;
using Windy.Srpg.Game.Units;

BuiltInBuffCatalog.EnsureRegistered();
BuiltInItemCatalog.EnsureRegistered();
int checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    checks++;
}
void Hit(string sword, Unit target)
{
    Check(UnitPassiveRegistry.TryCreate(((WeaponData)ItemRegistry.Get(sword)).EffectId, out var effect), "Weapon effect is registered");
    ((IWeaponHitEffect)effect).OnWeaponHit(new Unit(), target);
}

var toxic = new Unit();
for (int i = 0; i < 7; i++) Hit("toxic_sword", toxic);
var poison = toxic.Buffs.Entries.Single();
Check(poison.Stacks == 5 && poison.RemainingDuration == 3, "Toxic caps at five with a shared three-tick duration");
toxic.Buffs.OnTurnEnd();
Check(poison.RemainingDuration == 3, "Counterattack-applied Toxic does not age at turn end");
toxic.Buffs.OnDotTick();
Check(toxic.HitPoints == 175 && poison.RemainingDuration == 2, "Five stacks deal 25 damage");
Hit("toxic_sword", toxic);
Check(poison.Stacks == 5 && poison.RemainingDuration == 3, "Reapplication at cap refreshes all stacks");
for (int i = 0; i < 3; i++)
{
    toxic.Buffs.OnDotTick();
    toxic.Buffs.OnTurnStart();
    toxic.Buffs.OnTurnEnd();
}
Check(toxic.HitPoints == 100 && toxic.Buffs.Entries.Count == 0, "Exactly three refreshed ticks then expiration");
toxic.Buffs.OnDotTick();
Check(toxic.HitPoints == 100, "Expired Toxic cannot tick again");

var stunned = new Unit();
stunned.Buffs.OnTurnStart();
Hit("stun_sword", stunned);
Check(stunned.Buffs.GetActiveEffects().Any(e => e is IP_ActionBlocker), "Stun exposes action/counterattack blocking immediately");
stunned.Buffs.OnTurnEnd();
Check(stunned.Buffs.HasBuff("stun"), "Stun received during own turn survives that turn end");
stunned.Buffs.OnTurnStart();
stunned.Buffs.OnTurnEnd();
Check(!stunned.Buffs.HasBuff("stun"), "Stun expires after the next full turn");
Hit("stun_sword", stunned);
Hit("stun_sword", stunned);
Check(stunned.Buffs.Entries.Single().Stacks == 1, "Repeated stun never queues stacks");

var weakened = new Unit();
for (int i = 0; i < 7; i++) Hit("weakening_sword", weakened);
for (int i = 0; i < 8; i++) { weakened.Buffs.OnTurnStart(); weakened.Buffs.OnTurnEnd(); }
var stats = weakened.Buffs.GetPrimaryStatModifiers();
Check(stats.Strength == -5 && stats.Magic == -5 && stats.Defense == -5 && stats.Speed == -5 && stats.Luck == -5,
    "Weakening applies all five stat reductions and lasts indefinitely within battle");
Hit("toxic_sword", weakened);
Hit("stun_sword", weakened);
weakened.AddBuffById("attack_up");
weakened.Buffs.AddBuff(new BuffData { Id = "protected_debuff", Category = BuffCategory.Misc, Duration = 0, Removable = false });
Check(weakened.Buffs.RemoveRemovableDebuffs() == 3, "Cleanse removes all three removable debuffs");
Check(weakened.Buffs.HasBuff("attack_up") && weakened.Buffs.HasBuff("protected_debuff"), "Cleanse respects beneficial buffs and removability");
Check(weakened.Buffs.GetPrimaryStatModifiers().Strength == 0, "Cleansing restores weakened stats");
weakened.Buffs.Clear();
Check(weakened.Buffs.Entries.Count == 0, "Battle cleanup removes every status");

var cloneUnit = new Unit();
cloneUnit.AddBuffById("weakening");
cloneUnit.Buffs.AddBuff(BuffRegistry.CreateRuntimeInstance(BuffRegistry.Get("weakening")));
Check(cloneUnit.Buffs.Entries.Count == 1 && cloneUnit.Buffs.Entries.Single().Stacks == 2, "Runtime instances share the stacking key");

var cleanse = CatalogResourceLoader.Root.GetProperty("Skills").GetProperty("Skills").EnumerateArray()
    .Single(e => e.GetProperty("Id").GetString() == "cleanse");
Check(cleanse.GetProperty("EffectId").GetString() == "cleanse" && cleanse.GetProperty("TargetingType").GetString() == "AllyUnit"
    && cleanse.GetProperty("MpCost").GetInt32() == 3 && !cleanse.GetProperty("AttackProfile").GetProperty("Enabled").GetBoolean(),
    "Cleanse is an ally support spell costing three MP");
Console.WriteLine($"Passed {checks} debuff/catalog checks (Unity presentation and input require Play Mode).");
