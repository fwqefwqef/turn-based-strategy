using Windy.Srpg.Game.Buffs;
using Windy.Srpg.Game.Catalogs;
using Windy.Srpg.Game.Inventory;
using Windy.Srpg.Game.Grid;
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

var deathsDoor = new Unit { HitPoints = -4 };
deathsDoor.AddBuffById("death_door");
Check(deathsDoor.IsAliveForBattle, "Death's Door keeps a unit alive at negative HP");
Check(deathsDoor.Buffs.ApplyMovementPointCaps(6f) == 1f, "Death's Door caps movement at one");
Hit("toxic_sword", deathsDoor);
Check(deathsDoor.Buffs.HasBuff("toxic"), "Weapon debuffs can apply to a living Death's Door unit");
deathsDoor.AddBuffById("death_door_penalty");
deathsDoor.AddBuffById("death_door_penalty");
var deathsDoorPenalty = deathsDoor.Buffs.GetBuff("death_door_penalty");
Check(deathsDoorPenalty.Stacks == 2 && !deathsDoorPenalty.Removable
    && deathsDoor.Buffs.GetPrimaryStatModifiers().Strength == -2,
    "Death's Door penalty stacks independently from ordinary Weakening");
Check(deathsDoor.Buffs.RemoveRemovableDebuffs() == 1
    && deathsDoor.Buffs.HasBuff("death_door")
    && deathsDoor.Buffs.HasBuff("death_door_penalty"),
    "Cleanse removes Toxic but preserves both non-removable Death's Door statuses");

var fogEdge = new Unit { HitPoints = 101, ComputedTotalHitPoints = 101, BlackFogDepth = 0 };
fogEdge.AddBuffById("black_fog");
fogEdge.Buffs.OnDotTick();
Check(fogEdge.HitPoints == 75, "Black Fog depth zero deals a rounded-up 25% Max HP");
Check(fogEdge.Buffs.HasBuff("black_fog") && !fogEdge.Buffs.GetBuff("black_fog").Removable,
    "Black Fog is infinite while present and cannot be cleansed normally");

var fogDepthOne = new Unit { HitPoints = 101, ComputedTotalHitPoints = 101, BlackFogDepth = 1 };
fogDepthOne.AddBuffById("black_fog");
fogDepthOne.Buffs.OnDotTick();
Check(fogDepthOne.HitPoints == 50, "Black Fog depth one deals a rounded-up 50% Max HP");

var outsideFog = new Unit { HitPoints = 101, ComputedTotalHitPoints = 101 };
outsideFog.AddBuffById("black_fog");
outsideFog.Buffs.OnDotTick();
Check(outsideFog.HitPoints == 101, "A stale Black Fog status deals no damage outside fog coverage");

var leftOrDownDepths = BlackFogLayerCalculator.BuildDepthByLayer([0, 1, 2, 3, 4, 5], 2, false);
Check(leftOrDownDepths.Count == 2 && leftOrDownDepths[0] == 1 && leftOrDownDepths[1] == 0,
    "Left/Down fog covers ascending outer layers and measures depth from the frontier");
var rightOrUpDepths = BlackFogLayerCalculator.BuildDepthByLayer([0, 1, 2, 3, 4, 5], 2, true);
Check(rightOrUpDepths.Count == 2 && rightOrUpDepths[5] == 1 && rightOrUpDepths[4] == 0,
    "Right/Up fog covers descending outer layers and measures depth from the frontier");
var expandedDepths = BlackFogLayerCalculator.BuildDepthByLayer([0, 1, 2, 3, 4, 5], 4, false);
Check(expandedDepths.Count == 4 && expandedDepths[0] == 3 && expandedDepths[3] == 0,
    "Expanding fog increases the depth of previously covered layers");

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
