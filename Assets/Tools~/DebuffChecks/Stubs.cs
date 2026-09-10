// Host adapters for testing the real buff engine without launching Unity.
// These do not test Unit's Unity lifecycle, input, AI or HUD integration.
using System.Text.Json;
using System.Text.Json.Serialization;
using Windy.Srpg.Game.Buffs;
using Windy.Srpg.Game.Inventory;

namespace UnityEngine
{
    public class SerializeField : Attribute { }
    public class TextAreaAttribute : Attribute { }
    public class HideInInspector : Attribute { }
    public static class Mathf
    {
        public static int Max(int a, int b) => Math.Max(a, b);
        public static int Clamp(int value, int min, int max) => Math.Clamp(value, min, max);
        public static int CeilToInt(float value) => (int)Math.Ceiling(value);
    }
    public static class Debug
    {
        public static void LogWarning(string message) => throw new Exception(message);
    }
}

namespace Windy.Srpg.Game.Units
{
    public class Unit
    {
        public int HitPoints = 200;
        public int ComputedTotalHitPoints = 200;
        public int Magic;
        public int Defense;
        public int? BlackFogDepth;
        public UnitBuffList Buffs;
        public Unit() { Buffs = new UnitBuffList(this); }
        public bool IsAtDeathsDoor => Buffs.HasBuff("death_door");
        public bool IsAliveForBattle => HitPoints > 0 || IsAtDeathsDoor;
        internal bool TryGetBlackFogDepth(out int depth)
        {
            depth = BlackFogDepth ?? 0;
            return BlackFogDepth.HasValue;
        }
        public Buff AddBuffById(string id) => Buffs.AddBuffById(id);
        public bool RemoveBuff(Buff entry) => Buffs.RemoveBuff(entry);
        public void ApplyPainDamage(int damage) => HitPoints = Math.Max(0, HitPoints - damage);
        public void RestoreHitPoints(int amount, Unit source) => HitPoints += amount;
    }
    public enum DamageChangePhase { Outcome, Damage }
    public class DamageChangeContext
    {
        public DamageChangePhase Phase;
        public int Damage;
        public bool IsHit;
        public bool IsMagicAttack;
        public Unit Defender;
    }
    public class CombatSequenceContext { }
    public interface IP_TakeDamageChange { void TakeDamageChange(DamageChangeContext context); }
    public interface IP_DamageChange { void DamageChange(DamageChangeContext context); }
    public interface IP_AfterCombat_Attacker { void AfterCombatSequenceAsAttacker(CombatSequenceContext context); }
}

namespace Windy.Srpg.Game.Catalogs
{
    public static class CatalogResourceLoader
    {
        public static readonly JsonSerializerOptions Options = new()
        {
            IncludeFields = true,
            Converters = { new JsonStringEnumConverter() }
        };
        public static JsonElement Root => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "gdata.json"))).RootElement;
        public static BuffCatalogResource LoadBuffCatalog() => new();
        public static ItemCatalogResource LoadItemCatalog() => new();
    }
    public class BuffCatalogResource
    {
        public IEnumerable<BuffData> ToRuntimeDefinitions() => CatalogResourceLoader.Root.GetProperty("Buffs").GetProperty("Buffs")
            .EnumerateArray().Select(e => e.Deserialize<BuffData>(CatalogResourceLoader.Options));
    }
    public class ItemCatalogResource
    {
        public IEnumerable<ItemData> ToRuntimeDefinitions() => CatalogResourceLoader.Root.GetProperty("Items").GetProperty("Weapons")
            .EnumerateArray().Select(e => e.Deserialize<WeaponData>(CatalogResourceLoader.Options));
    }
}
