using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Windy.Srpg.Game.Skills;

namespace Windy.Srpg.Game.Units
{
    // Shared level/experience support types used across units, UI, and abilities.
    public enum ExperienceSourceKind
    {
        EnemyCombat,
        AllySkill,
        AreaEnemy,
        AreaAlly,
        AreaAny
    }

    public sealed class ExperienceGainContext
    {
        public Unit Recipient;
        public Unit PrimaryTarget;
        public IReadOnlyList<Unit> Targets;
        public Windy.Srpg.Game.Grid.CellGrid CellGrid;
        public SkillData Skill;
        public ExperienceSourceKind SourceKind;
        public bool IsLethal;
        public int Amount;
        public bool Prevented;
    }

    public interface IP_ModifyExperienceGain
    {
        void ModifyExperienceGain(ExperienceGainContext context);
    }

    public interface IP_PreventExperienceGain
    {
        void PreventExperienceGain(ExperienceGainContext context);
    }

    public static class ExperienceCalculator
    {
        public static int MaxLevel { get; set; } = 20;
        public static int MinGain { get; set; } = 1;
        public static int MaxGain { get; set; } = 100;

        public static int EnemyTargetNonLethalBase { get; set; } = 10;
        public static int EnemyTargetLethalBase { get; set; } = 30;
        public static int AllySkillBase { get; set; } = 10;
        public static int AreaEnemyBase { get; set; } = 10;
        public static int AreaAllyBase { get; set; } = 10;
        public static int AreaAnyBase { get; set; } = 10;

        public static int CalculateEnemyCombatExp(Unit user, Unit enemy, bool isLethal)
        {
            return CalculateEnemyCombatExp(user, enemy != null ? enemy.Level : 0, isLethal);
        }

        public static int CalculateEnemyCombatExp(Unit user, int enemyLevel, bool isLethal)
        {
            if (user == null || enemyLevel <= 0)
            {
                return 0;
            }

            int baseAmount = isLethal ? EnemyTargetLethalBase : EnemyTargetNonLethalBase;
            float rawAmount = baseAmount * (1f + enemyLevel - user.Level);
            return ClampExperienceGain(user, rawAmount);
        }

        public static int CalculateAllySkillExp(Unit user, Windy.Srpg.Game.Grid.CellGrid cellGrid)
        {
            if (user == null)
            {
                return 0;
            }

            float averageEnemyLevel = GetAverageEnemyLevel(user, cellGrid);
            float rawAmount = AllySkillBase * (1f + averageEnemyLevel - user.Level);
            return ClampExperienceGain(user, rawAmount);
        }

        public static int CalculateAreaEnemyExp(Unit user, int affectedEnemyCount)
        {
            if (user == null || affectedEnemyCount <= 0)
            {
                return 0;
            }

            return ClampExperienceGain(user, AreaEnemyBase * affectedEnemyCount);
        }

        public static int CalculateAreaAllyExp(Unit user, int affectedAllyCount)
        {
            if (user == null || affectedAllyCount <= 0)
            {
                return 0;
            }

            return ClampExperienceGain(user, AreaAllyBase * affectedAllyCount);
        }

        public static int CalculateAreaAnyExp(Unit user, int affectedUnitCount)
        {
            if (user == null || affectedUnitCount <= 0)
            {
                return 0;
            }

            return ClampExperienceGain(user, AreaAnyBase * affectedUnitCount);
        }

        public static int CalculateAreaSkillExp(Unit user, IReadOnlyList<Unit> targets, Windy.Srpg.Game.Grid.CellGrid cellGrid, SkillData skill, bool killedAtLeastOneTarget)
        {
            if (user == null || skill == null)
            {
                return 0;
            }

            bool affectsAllies = skill.AreaProfile.AffectsAllies;
            bool affectsEnemies = skill.AreaProfile.AffectsEnemies;

            return (affectsAllies, affectsEnemies) switch
            {
                (false, true) => CalculateAreaEnemyExp(user, targets, killedAtLeastOneTarget),
                (true, false) => CalculateAllySkillExp(user, cellGrid),
                (true, true) => CalculateAreaAnyExp(user, targets, killedAtLeastOneTarget),
                _ => 0
            };
        }

        public static float GetAverageEnemyLevel(Unit user, Windy.Srpg.Game.Grid.CellGrid cellGrid)
        {
            if (user == null)
            {
                return 1f;
            }

            List<Unit> enemyUnits = cellGrid?.GetAllUnits()
                .Where(unit => unit != null && unit.PlayerNumber != user.PlayerNumber)
                .ToList()
                ?? new List<Unit>();

            if (enemyUnits.Count == 0)
            {
                return user.Level;
            }

            return (float)enemyUnits.Average(unit => unit.Level);
        }

        public static int ClampExperienceGain(Unit user, float rawAmount)
        {
            if (user == null || !user.CanGainExperience)
            {
                return 0;
            }

            if (user.Level >= MaxLevel)
            {
                return 0;
            }

            return Mathf.Clamp(Mathf.RoundToInt(rawAmount), MinGain, MaxGain);
        }

        static int CalculateAreaEnemyExp(Unit user, IReadOnlyList<Unit> targets, bool killedAtLeastOneTarget)
        {
            float averageTargetLevel = GetAverageTargetLevel(targets);
            int baseAmount = killedAtLeastOneTarget ? AreaEnemyBase * 3 : AreaEnemyBase;
            float rawAmount = baseAmount * (1f + averageTargetLevel - user.Level);
            return ClampExperienceGain(user, rawAmount);
        }

        static int CalculateAreaAnyExp(Unit user, IReadOnlyList<Unit> targets, bool killedAtLeastOneTarget)
        {
            float averageTargetLevel = GetAverageTargetLevel(targets);
            int baseAmount = killedAtLeastOneTarget ? AreaAnyBase * 3 : AreaAnyBase;
            float rawAmount = baseAmount * (1f + averageTargetLevel - user.Level);
            return ClampExperienceGain(user, rawAmount);
        }

        static float GetAverageTargetLevel(IReadOnlyList<Unit> targets)
        {
            if (targets == null)
            {
                return 1f;
            }

            List<Unit> validTargets = targets.Where(target => target != null).ToList();
            if (validTargets.Count == 0)
            {
                return 1f;
            }

            return (float)validTargets.Average(target => target.Level);
        }
    }

    public enum LevelableStatKind
    {
        Strength = 0,
        Magic = 1,
        Defense = 2,
        Resistance = 3,
        Speed = 4,
        Luck = 5
    }

    public sealed class LevelUpGainStep
    {
        public int FromLevel { get; }
        public int ToLevel { get; }
        public IReadOnlyDictionary<LevelableStatKind, int> AutoGains { get; }

        public LevelUpGainStep(int fromLevel, int toLevel, IReadOnlyDictionary<LevelableStatKind, int> autoGains)
        {
            FromLevel = fromLevel;
            ToLevel = toLevel;
            AutoGains = autoGains ?? new Dictionary<LevelableStatKind, int>();
        }
    }

    public sealed class ExperienceBarSegment
    {
        public int Level { get; }
        public int StartExperience { get; }
        public int EndExperience { get; }

        public ExperienceBarSegment(int level, int startExperience, int endExperience)
        {
            Level = level;
            StartExperience = startExperience;
            EndExperience = endExperience;
        }
    }

    public sealed class ExperienceAwardResult
    {
        public string UnitName { get; }
        public int GrantedExperience { get; }
        public int OldLevel { get; }
        public int OldExperience { get; }
        public int FinalLevel { get; }
        public int FinalExperience { get; }
        public IReadOnlyList<ExperienceBarSegment> BarSegments { get; }
        public IReadOnlyList<LevelUpGainStep> LevelUps { get; }

        public ExperienceAwardResult(
            string unitName,
            int grantedExperience,
            int oldLevel,
            int oldExperience,
            int finalLevel,
            int finalExperience,
            IReadOnlyList<ExperienceBarSegment> barSegments,
            IReadOnlyList<LevelUpGainStep> levelUps)
        {
            UnitName = unitName ?? string.Empty;
            GrantedExperience = grantedExperience;
            OldLevel = oldLevel;
            OldExperience = oldExperience;
            FinalLevel = finalLevel;
            FinalExperience = finalExperience;
            BarSegments = barSegments ?? Array.Empty<ExperienceBarSegment>();
            LevelUps = levelUps ?? Array.Empty<LevelUpGainStep>();
        }
    }

    public sealed class LevelUpPresentation
    {
        public int OldLevel { get; }
        public int NewLevel { get; }
        public IReadOnlyDictionary<LevelableStatKind, int> BaseStatsBefore { get; }
        public IReadOnlyDictionary<LevelableStatKind, int> AutoGains { get; }

        public LevelUpPresentation(
            int oldLevel,
            int newLevel,
            IReadOnlyDictionary<LevelableStatKind, int> baseStatsBefore,
            IReadOnlyDictionary<LevelableStatKind, int> autoGains)
        {
            OldLevel = oldLevel;
            NewLevel = newLevel;
            BaseStatsBefore = baseStatsBefore ?? new Dictionary<LevelableStatKind, int>();
            AutoGains = autoGains ?? new Dictionary<LevelableStatKind, int>();
        }

        public int GetBaseStat(LevelableStatKind stat)
        {
            return BaseStatsBefore.TryGetValue(stat, out int value) ? value : 0;
        }

        public int GetAutoGain(LevelableStatKind stat)
        {
            return AutoGains.TryGetValue(stat, out int value) ? value : 0;
        }

        public int GetDisplayedGain(LevelableStatKind stat, LevelableStatKind? manualSelection)
        {
            int autoGain = GetAutoGain(stat);
            if (manualSelection.HasValue && manualSelection.Value == stat)
            {
                autoGain += 1;
            }

            return autoGain;
        }
    }

    public static class LevelUpGainCalculator
    {
        public const int GainableStatCount = 6;
        public const int ExpectedGrowthTotal = 100;

        public static IReadOnlyList<int> NormalizeGrowthRates(IReadOnlyList<int> rawGrowthRates)
        {
            int[] clampedRates = new int[GainableStatCount];
            for (int i = 0; i < GainableStatCount; i++)
            {
                clampedRates[i] = rawGrowthRates != null && i < rawGrowthRates.Count
                    ? Mathf.Max(0, rawGrowthRates[i])
                    : 0;
            }

            int rawTotal = clampedRates.Sum();
            if (rawTotal <= 0)
            {
                return new[] { 17, 17, 17, 17, 16, 16 };
            }

            double scale = ExpectedGrowthTotal / (double)rawTotal;
            int[] normalized = new int[GainableStatCount];
            List<(int Index, double Fraction)> remainders = new List<(int, double)>(GainableStatCount);

            for (int i = 0; i < GainableStatCount; i++)
            {
                double scaled = clampedRates[i] * scale;
                int floored = Mathf.FloorToInt((float)scaled);
                normalized[i] = floored;
                remainders.Add((i, scaled - floored));
            }

            int missing = ExpectedGrowthTotal - normalized.Sum();
            foreach ((int index, _) in remainders.OrderByDescending(entry => entry.Fraction).ThenBy(entry => entry.Index).Take(missing))
            {
                normalized[index] += 1;
            }

            return normalized;
        }

        public static LevelUpGainStep BuildStep(IReadOnlyList<int> growthRates, int fromLevel)
        {
            IReadOnlyList<int> normalizedGrowthRates = NormalizeGrowthRates(growthRates);
            int[] progress = new int[GainableStatCount];

            for (int currentLevel = 1; currentLevel <= fromLevel; currentLevel++)
            {
                for (int i = 0; i < GainableStatCount; i++)
                {
                    progress[i] += normalizedGrowthRates[i] * 2;
                }

                int firstIndex = SelectStatIndex(progress, Array.Empty<int>());
                progress[firstIndex] -= ExpectedGrowthTotal;

                int secondIndex = SelectStatIndex(progress, new[] { firstIndex });
                progress[secondIndex] -= ExpectedGrowthTotal;

                if (currentLevel == fromLevel)
                {
                    Dictionary<LevelableStatKind, int> gains = new Dictionary<LevelableStatKind, int>
                    {
                        [(LevelableStatKind)firstIndex] = 1
                    };

                    LevelableStatKind secondStat = (LevelableStatKind)secondIndex;
                    gains[secondStat] = gains.TryGetValue(secondStat, out int existing) ? existing + 1 : 1;
                    return new LevelUpGainStep(fromLevel, fromLevel + 1, gains);
                }
            }

            return new LevelUpGainStep(fromLevel, fromLevel + 1, new Dictionary<LevelableStatKind, int>());
        }

        static int SelectStatIndex(IReadOnlyList<int> progress, IReadOnlyCollection<int> excludedIndices)
        {
            int bestIndex = 0;
            int bestValue = int.MinValue;

            for (int i = 0; i < progress.Count; i++)
            {
                if (excludedIndices != null && excludedIndices.Contains(i))
                {
                    continue;
                }

                if (progress[i] > bestValue)
                {
                    bestValue = progress[i];
                    bestIndex = i;
                }
            }

            return bestIndex;
        }
    }
}
