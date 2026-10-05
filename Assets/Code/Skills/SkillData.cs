using System;
using UnityEngine;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Units;

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
        Melee,
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
        public bool HybridScaling;
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
        public bool CenterOnCasterFootprint;
        public int MinRange;
        public int MaxRange;
        public int Radius;
        public int Might;
        public bool IsMagic;
        public bool HybridScaling;
        public bool AffectsAllies;
        public bool AffectsEnemies;
    }

    [Serializable]
    public struct SkillTerrainProfile
    {
        public bool Enabled;
        public string TerrainEffectId;
        public int DurationRounds;
    }

    [Serializable]
    public struct SkillHealProfile
    {
        public bool Enabled;
        public int Might;
        public bool ScalesWithMagic;
        public bool DoubleAtDeathsDoor;
        public int MinRange;
        public int MaxRange;
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
        [Tooltip("Optional exact weapon id required by this skill. Empty allows any weapon in RequiredWeaponType.")]
        public string RequiredWeaponId;

        public bool EndsTurn = true;
        public bool OncePerTurn;
        [Tooltip("Limits this skill to one committed use by the unit for the entire battle.")]
        public bool OncePerBattle = false;
        public bool SelfImmune = false;
        public int MpCost = 3;

        public bool HasOncePerTurnUsageLimit => OncePerTurn;

        public SkillAttackProfile AttackProfile;
        public SkillAreaProfile AreaProfile;
        public SkillTerrainProfile TerrainProfile;
        public SkillHealProfile HealProfile;

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

        public static Cell GetCasterFootprintCenter(Unit user, Cell anchor, CellGrid grid)
        {
            if (user == null || anchor == null || grid == null) return null;
            Vector2Int offset = new Vector2Int((user.FootprintWidth - 1) / 2, (user.FootprintHeight - 1) / 2);
            return grid.FindCellByCoordinates(anchor.Coordinates + offset);
        }

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



