using System;
using Windy.Srpg.Game.Inventory;
using UnityEngine;

namespace Windy.Srpg.Game.Buffs
{
    [Serializable]
    public class BuffData
    {
        public string Id;
        public string Name = "buff_name";
        [TextArea]
        public string Description = "buff_desc";
        // 1 = default one-turn duration, 0 = infinite duration.
        public int Duration = 1;
        public PrimaryStatModifiers PrimaryStatModifiers;
        public SecondaryStatModifiers SecondaryStatModifiers;
        public string EffectId;
    }
}



