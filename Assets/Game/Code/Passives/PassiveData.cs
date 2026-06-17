using System;
using Windy.Srpg.Game.Inventory;
using UnityEngine;

namespace Windy.Srpg.Game.Passives
{
    public enum PassiveListKind
    {
        Unique,
        Equip
    }

    [Serializable]
    public class PassiveData
    {
        public string Id;
        public string Name = "passive_name";
        [TextArea]
        public string Description = "passive_desc";
        public int Cost = 0;
        public PrimaryStatModifiers PrimaryStatModifiers;
        public SecondaryStatModifiers SecondaryStatModifiers;
        public string EffectId;
    }

    [Serializable]
    public struct StartingPassiveEntry
    {
        public string PassiveId;
    }
}

