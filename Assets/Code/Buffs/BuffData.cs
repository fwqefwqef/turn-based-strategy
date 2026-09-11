using System;
using Windy.Srpg.Game.Inventory;
using UnityEngine;

namespace Windy.Srpg.Game.Buffs
{
    public enum BuffCategory
    {
        Buff,
        Weakening,
        CC,
        Pain,
        Misc
    }

    [Serializable]
    public class BuffData
    {
        public string Id;
        public string Name = "buff_name";
        [TextArea]
        public string Description = "buff_desc";
        // 1 = default one-turn duration, 0 = infinite duration.
        public int Duration = 1;
        public BuffCategory Category = BuffCategory.Buff;
        public int MaxStacks = 1;
        public bool Removable = true;
        public string StackKey;
        public PrimaryStatModifiers PrimaryStatModifiers;
        public SecondaryStatModifiers SecondaryStatModifiers;
        public string EffectId;

        public string StackingId => string.IsNullOrWhiteSpace(StackKey) ? Id : StackKey;
        public int ResolvedMaxStacks => Mathf.Max(1, MaxStacks);
    }
}



