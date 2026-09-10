namespace UnityEngine
{
    public sealed class SerializeField : Attribute { }
    public sealed class TextAreaAttribute : Attribute { }

    public static class Mathf
    {
        public static int Max(int left, int right) => Math.Max(left, right);
    }

    public static class Debug
    {
        public static void LogWarning(string message) { }
    }
}

namespace Windy.Srpg.Game.Inventory
{
    public sealed class Item
    {
        public List<string> GrantedSkillIds = new();
    }

    public sealed class UnitInventory
    {
        public Item EquippedWeapon;
        public Item EquippedAccessory;
    }
}

namespace Windy.Srpg.Game.Units
{
    using Windy.Srpg.Game.Inventory;

    public sealed class Unit
    {
        public bool CanStartActionThisTurn = true;
        public int CurrentManaPoints = 20;
        public UnitInventory Inventory = new();

        public bool TrySpendManaPoints(int amount)
        {
            if (amount < 0 || CurrentManaPoints < amount)
            {
                return false;
            }

            CurrentManaPoints -= amount;
            return true;
        }
    }
}

namespace Windy.Srpg.Game.Skills
{
    public static class SkillRegistry
    {
        private static readonly Dictionary<string, SkillData> Entries = new(StringComparer.OrdinalIgnoreCase);

        public static void Register(SkillData data)
        {
            Entries[data.Id] = data;
        }

        public static SkillData Get(string id) => Entries.TryGetValue(id, out SkillData data) ? data : null;

        public static bool TryGet(string id, out SkillData data) => Entries.TryGetValue(id, out data);
    }
}
