using System;
using UnityEngine;

namespace Windy.Srpg.Game.Skills
{
    public enum SkillCategory
    {
        CombatArt,
        Spell,
        AreaSpell,
        Misc
    }

    public enum SkillTargetingType
    {
        None,
        Self,
        EnemyUnit,
        AllyUnit,
        AnyUnit,
        Cell,
        AreaCell
    }

    public enum CombatArtWeaponType
    {
        Any,
        Sword,
        Lance,
        Blunt,
        Ranged,
        Magic
    }

    public enum SkillAreaShape
    {
        Centered,
        Line
    }

    [Serializable]
    public struct SkillAttackProfile
    {
        public bool Enabled;
        public bool IsMagic;
        public int Might;
        public int Accuracy;
        public int Crit;
        public int MinRange;
        public int MaxRange;
        public int NumHits;
        public bool PreventsCounterattack;
    }

    [Serializable]
    public struct SkillAreaProfile
    {
        public bool Enabled;
        public SkillAreaShape Shape;
        public int MinRange;
        public int MaxRange;
        public int Radius;
        public int Might;
        public bool IsMagic;
        public bool AffectsAllies;
        public bool AffectsEnemies;
    }

    [Serializable]
    public class SkillData
    {
        public string Id;
        public string Name = "skill_name";
        [TextArea]
        public string Description = "skill_desc";

        public SkillCategory Category = SkillCategory.Misc;
        public SkillTargetingType TargetingType = SkillTargetingType.None;
        public CombatArtWeaponType RequiredWeaponType = CombatArtWeaponType.Any;

        public bool EndsTurn = true;
        public bool OncePerTurn = true;
        public bool SelfImmune = false;
        public int MpCost = 3;

        public SkillAttackProfile AttackProfile;
        public SkillAreaProfile AreaProfile;

        public string EffectId;
    }

    [Serializable]
    public struct StartingSkillEntry
    {
        public string SkillId;
    }

    public static class SkillRangeUtility
    {
        public const int InfiniteRangeThreshold = 11;

        public static void ApplyCombatArtRangeModifiers(
            int weaponMinRange,
            int weaponMaxRange,
            int minRangeModifier,
            int maxRangeModifier,
            out int resolvedMinRange,
            out int resolvedMaxRange)
        {
            resolvedMinRange = Mathf.Max(1, weaponMinRange + minRangeModifier);
            resolvedMaxRange = Mathf.Max(1, weaponMaxRange + maxRangeModifier);

            if (resolvedMaxRange < resolvedMinRange)
            {
                resolvedMaxRange = resolvedMinRange;
            }
        }

        public static bool IsInfiniteRange(int maxRange)
        {
            return maxRange >= InfiniteRangeThreshold;
        }
    }
}



