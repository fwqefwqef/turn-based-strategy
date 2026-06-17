using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Skills;
using Windy.Srpg.Game.UI;

namespace Windy.Srpg.Game.Units
{
    public partial class Unit
    {
        private ExperienceAwardResult BuildCombatExperienceAward(Unit target, bool isLethal)
        {
            return BuildCombatExperienceAward(target, target != null ? target.Level : 0, isLethal);
        }

        private ExperienceAwardResult BuildCombatExperienceAward(Unit target, int targetLevel, bool isLethal)
        {
            if (target == null && targetLevel <= 0)
            {
                return null;
            }

            int amount = ExperienceCalculator.CalculateEnemyCombatExp(this, target != null ? target.Level : targetLevel, isLethal);
            return BuildExperienceAward(new ExperienceGainContext
            {
                Recipient = this,
                PrimaryTarget = target,
                Targets = target != null ? new[] { target } : Array.Empty<Unit>(),
                CellGrid = FindSceneCellGrid(),
                SourceKind = ExperienceSourceKind.EnemyCombat,
                IsLethal = isLethal,
                Amount = amount
            });
        }

        private ExperienceAwardResult BuildSupportSkillExperienceAward(Unit primaryTarget, CellGrid cellGrid, SkillData skill)
        {
            int amount = ExperienceCalculator.CalculateAllySkillExp(this, cellGrid);
            return BuildExperienceAward(new ExperienceGainContext
            {
                Recipient = this,
                PrimaryTarget = primaryTarget,
                Targets = primaryTarget != null ? new[] { primaryTarget } : Array.Empty<Unit>(),
                CellGrid = cellGrid,
                Skill = skill,
                SourceKind = ExperienceSourceKind.AllySkill,
                Amount = amount
            });
        }

        private ExperienceAwardResult BuildAreaSkillExperienceAward(IReadOnlyList<Unit> targets, CellGrid cellGrid, SkillData skill, bool killedAtLeastOneTarget)
        {
            if (skill == null)
            {
                return null;
            }

            List<Unit> relevantTargets = targets?
                .Where(target => target != null)
                .Distinct()
                .ToList() ?? new List<Unit>();

            if (skill.AreaProfile.AffectsEnemies)
            {
                relevantTargets = relevantTargets
                    .Where(target => target.PlayerNumber == PlayerNumber || !PreventsExperienceGainFromTarget(target, skill, DetermineAreaExperienceSourceKind(skill), false, relevantTargets, cellGrid))
                    .ToList();
            }

            int amount = ExperienceCalculator.CalculateAreaSkillExp(this, relevantTargets, cellGrid, skill, killedAtLeastOneTarget);
            return BuildExperienceAward(new ExperienceGainContext
            {
                Recipient = this,
                PrimaryTarget = relevantTargets.FirstOrDefault(),
                Targets = relevantTargets,
                CellGrid = cellGrid,
                Skill = skill,
                SourceKind = DetermineAreaExperienceSourceKind(skill),
                IsLethal = killedAtLeastOneTarget,
                Amount = amount
            });
        }

        private ExperienceAwardResult BuildExperienceAward(ExperienceGainContext context)
        {
            if (context == null || context.Recipient == null || context.Amount <= 0 || !CanGainExperience)
            {
                return null;
            }

            if (Level >= ExperienceCalculator.MaxLevel)
            {
                if (experience != 0)
                {
                    experience = 0;
                    RaiseProgressionChanged();
                }

                return null;
            }

            ApplyExperiencePreventions(context);
            if (context.Prevented)
            {
                return null;
            }

            ApplyExperienceModifiers(context);
            context.Amount = ExperienceCalculator.ClampExperienceGain(this, context.Amount);
            if (context.Amount <= 0)
            {
                return null;
            }

            return BuildExperienceAwardResult(context.Amount);
        }

        private ExperienceAwardResult BuildExperienceAwardResult(int amount)
        {
            if (amount <= 0 || !CanGainExperience || Level >= ExperienceCalculator.MaxLevel)
            {
                return null;
            }

            int grantedAmount = Mathf.Clamp(amount, ExperienceCalculator.MinGain, ExperienceCalculator.MaxGain);
            int currentLevel = Level;
            int currentExperience = Experience;
            int remainingAmount = grantedAmount;
            List<ExperienceBarSegment> barSegments = new List<ExperienceBarSegment>();
            List<LevelUpGainStep> levelUpSteps = new List<LevelUpGainStep>();
            IReadOnlyList<int> normalizedGrowthRates = GetNormalizedGrowthRates();

            while (remainingAmount > 0 && currentLevel < ExperienceCalculator.MaxLevel)
            {
                int requiredForLevel = ExperienceCalculator.MaxGain - currentExperience;
                int segmentGain = Mathf.Min(remainingAmount, requiredForLevel);
                int targetExperience = currentExperience + segmentGain;
                bool overflowsLevel = targetExperience >= ExperienceCalculator.MaxGain && currentLevel < ExperienceCalculator.MaxLevel;

                barSegments.Add(new ExperienceBarSegment(
                    currentLevel,
                    currentExperience,
                    overflowsLevel ? ExperienceCalculator.MaxGain : targetExperience));

                remainingAmount -= segmentGain;

                if (overflowsLevel)
                {
                    levelUpSteps.Add(LevelUpGainCalculator.BuildStep(normalizedGrowthRates, currentLevel));
                    currentLevel = Mathf.Min(currentLevel + 1, ExperienceCalculator.MaxLevel);
                    currentExperience = 0;
                }
                else
                {
                    currentExperience = targetExperience;
                }
            }

            if (currentLevel >= ExperienceCalculator.MaxLevel)
            {
                currentExperience = 0;
            }

            return new ExperienceAwardResult(
                unitName,
                grantedAmount,
                Level,
                Experience,
                currentLevel,
                currentExperience,
                barSegments,
                levelUpSteps);
        }

        private ExperienceAwardResult _queuedDeferredExperienceAward;

        internal void QueueDeferredExperienceAward(ExperienceAwardResult award)
        {
            if (award == null)
            {
                return;
            }

            _queuedDeferredExperienceAward = award;
        }

        internal ExperienceAwardResult TakeQueuedDeferredExperienceAward()
        {
            ExperienceAwardResult award = _queuedDeferredExperienceAward;
            _queuedDeferredExperienceAward = null;
            return award;
        }

        private IEnumerator PlayPostCombatExperienceAwards(
            Unit defender,
            ExperienceAwardResult primaryAward,
            ExperienceAwardResult counterAward)
        {
            if (primaryAward == null && counterAward == null)
            {
                yield break;
            }

            yield return WaitForCombatHudToClose();

            if (primaryAward != null)
            {
                yield return PlayExperienceAwardSequence(this, primaryAward);
            }

            if (counterAward != null && defender != null)
            {
                yield return PlayExperienceAwardSequence(defender, counterAward);
            }
        }

        private IEnumerator PlayExperienceAwardSequence(Unit recipient, ExperienceAwardResult award)
        {
            if (recipient == null || award == null)
            {
                yield break;
            }

            ExperienceGainHUD experienceHud = FindSceneExperienceGainHud();
            if (experienceHud != null)
            {
                yield return experienceHud.ShowAndWait(recipient, award);
            }

            if (award.LevelUps.Count > 0)
            {
                float levelUpDelaySeconds = experienceHud != null ? experienceHud.LevelUpDelaySeconds : 0f;
                if (levelUpDelaySeconds > 0f)
                {
                    yield return new WaitForSeconds(levelUpDelaySeconds);
                }

                LevelUpUI levelUpUi = FindSceneLevelUpUi();
                foreach (LevelUpGainStep step in award.LevelUps)
                {
                    LevelableStatKind selectedStat = recipient.GetDefaultManualLevelUpStat();
                    if (levelUpUi != null)
                    {
                        bool resolved = false;
                        yield return levelUpUi.ShowAndWait(recipient, recipient.BuildLevelUpPresentation(step), stat =>
                        {
                            selectedStat = stat;
                            resolved = true;
                        });

                        if (!resolved)
                        {
                            selectedStat = recipient.GetDefaultManualLevelUpStat();
                        }
                    }

                    recipient.ApplyLevelUpStep(step, selectedStat);
                }
            }

            recipient.ApplyProgressionState(award.FinalLevel, award.FinalExperience);
        }

        private LevelUpPresentation BuildLevelUpPresentation(LevelUpGainStep step)
        {
            IReadOnlyDictionary<LevelableStatKind, int> baseStats = GetBaseStatSnapshot();
            return new LevelUpPresentation(step.FromLevel, step.ToLevel, baseStats, step.AutoGains);
        }

        private LevelableStatKind GetDefaultManualLevelUpStat()
        {
            return Enum.GetValues(typeof(LevelableStatKind))
                .Cast<LevelableStatKind>()
                .OrderBy(GetBaseStatValue)
                .ThenBy(stat => (int)stat)
                .First();
        }

        private void ApplyLevelUpStep(LevelUpGainStep step, LevelableStatKind manualSelection)
        {
            if (step == null)
            {
                return;
            }

            foreach (var pair in step.AutoGains)
            {
                ApplyBaseStatIncreaseInternal(pair.Key, pair.Value);
            }

            ApplyBaseStatIncreaseInternal(manualSelection, 1);
            level = Mathf.Clamp(step.ToLevel, 1, ExperienceCalculator.MaxLevel);
            experience = 0;
            RefreshHealthState();
            RaiseProgressionChanged();
        }

        private void ApplyProgressionState(int newLevel, int newExperience)
        {
            int previousLevel = level;
            int previousExperience = experience;

            level = Mathf.Clamp(newLevel, 1, ExperienceCalculator.MaxLevel);
            experience = level >= ExperienceCalculator.MaxLevel
                ? 0
                : Mathf.Clamp(newExperience, 0, ExperienceCalculator.MaxGain - 1);

            if (previousLevel != level || previousExperience != experience)
            {
                RaiseProgressionChanged();
            }
        }

        private void ApplyExperienceModifiers(ExperienceGainContext context)
        {
            foreach (var modifier in GetComponentsInChildren<MonoBehaviour>(true).OfType<IP_ModifyExperienceGain>())
            {
                modifier.ModifyExperienceGain(context);
            }

            if (BuffList != null)
            {
                foreach (var effect in BuffList.GetActiveEffects())
                {
                    if (effect is IP_ModifyExperienceGain modifier)
                    {
                        modifier.ModifyExperienceGain(context);
                    }
                }
            }

            if (PassiveList != null)
            {
                foreach (var effect in PassiveList.GetActiveEffects())
                {
                    if (effect is IP_ModifyExperienceGain modifier)
                    {
                        modifier.ModifyExperienceGain(context);
                    }
                }
            }
        }

        private IEnumerator WaitForCombatHudToClose()
        {
            const float maxWaitSeconds = 2f;
            float elapsedSeconds = 0f;
            while (CombatSequenceUI.IsVisible)
            {
                elapsedSeconds += Time.unscaledDeltaTime;
                if (elapsedSeconds >= maxWaitSeconds)
                {
                    FindSceneCombatSequenceUi()?.Hide();
                    yield break;
                }

                yield return null;
            }
        }

        private void ApplyExperiencePreventions(ExperienceGainContext context)
        {
            if (context?.Targets == null)
            {
                return;
            }

            foreach (Unit target in context.Targets.Where(target => target != null && target.PlayerNumber != PlayerNumber).Distinct())
            {
                if (PreventsExperienceGainFromTarget(target, context.Skill, context.SourceKind, context.IsLethal, context.Targets, context.CellGrid))
                {
                    context.Prevented = true;
                    context.Amount = 0;
                    return;
                }
            }
        }

        private bool PreventsExperienceGainFromTarget(
            Unit target,
            SkillData skill,
            ExperienceSourceKind sourceKind,
            bool isLethal,
            IReadOnlyList<Unit> targets,
            CellGrid cellGrid)
        {
            if (target == null)
            {
                return false;
            }

            ExperienceGainContext context = new ExperienceGainContext
            {
                Recipient = this,
                PrimaryTarget = target,
                Targets = targets,
                CellGrid = cellGrid,
                Skill = skill,
                SourceKind = sourceKind,
                IsLethal = isLethal
            };

            foreach (var blocker in target.GetComponentsInChildren<MonoBehaviour>(true).OfType<IP_PreventExperienceGain>())
            {
                blocker.PreventExperienceGain(context);
                if (context.Prevented)
                {
                    return true;
                }
            }

            if (target.BuffList != null)
            {
                foreach (var effect in target.BuffList.GetActiveEffects())
                {
                    if (effect is IP_PreventExperienceGain blocker)
                    {
                        blocker.PreventExperienceGain(context);
                        if (context.Prevented)
                        {
                            return true;
                        }
                    }
                }
            }

            if (target.PassiveList != null)
            {
                foreach (var effect in target.PassiveList.GetActiveEffects())
                {
                    if (effect is IP_PreventExperienceGain blocker)
                    {
                        blocker.PreventExperienceGain(context);
                        if (context.Prevented)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static ExperienceSourceKind DetermineAreaExperienceSourceKind(SkillData skill)
        {
            if (skill == null)
            {
                return ExperienceSourceKind.AreaAny;
            }

            bool affectsAllies = skill.AreaProfile.AffectsAllies;
            bool affectsEnemies = skill.AreaProfile.AffectsEnemies;

            return (affectsAllies, affectsEnemies) switch
            {
                (false, true) => ExperienceSourceKind.AreaEnemy,
                (true, false) => ExperienceSourceKind.AreaAlly,
                _ => ExperienceSourceKind.AreaAny
            };
        }
    }

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
                return user != null ? user.Level : 1f;
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

        public static float GetAverageTargetLevel(IReadOnlyList<Unit> targets)
        {
            if (targets == null)
            {
                return 1f;
            }

            List<Unit> validTargets = targets
                .Where(target => target != null)
                .Distinct()
                .ToList();

            if (validTargets.Count == 0)
            {
                return 1f;
            }

            return (float)validTargets.Average(target => target.Level);
        }

        public static int ClampExperienceGain(Unit recipient, float rawAmount)
        {
            if (recipient == null || recipient.Level >= MaxLevel)
            {
                return 0;
            }

            int flooredAmount = Mathf.FloorToInt(rawAmount);
            return Mathf.Clamp(flooredAmount, MinGain, MaxGain);
        }

        private static int CalculateAreaEnemyExp(Unit user, IReadOnlyList<Unit> targets, bool killedAtLeastOneTarget)
        {
            float averageTargetLevel = GetAverageTargetLevel(targets);
            int baseAmount = killedAtLeastOneTarget ? AreaEnemyBase * 3 : AreaEnemyBase;
            float rawAmount = baseAmount * (1f + averageTargetLevel - user.Level);
            return ClampExperienceGain(user, rawAmount);
        }

        private static int CalculateAreaAnyExp(Unit user, IReadOnlyList<Unit> targets, bool killedAtLeastOneTarget)
        {
            float averageTargetLevel = GetAverageTargetLevel(targets);
            int baseAmount = killedAtLeastOneTarget ? AreaAnyBase * 3 : AreaAnyBase;
            float rawAmount = baseAmount * (1f + averageTargetLevel - user.Level);
            return ClampExperienceGain(user, rawAmount);
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
            StartExperience = Mathf.Clamp(startExperience, 0, ExperienceCalculator.MaxGain);
            EndExperience = Mathf.Clamp(endExperience, 0, ExperienceCalculator.MaxGain);
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
            int gain = GetAutoGain(stat);
            if (manualSelection.HasValue && manualSelection.Value == stat)
            {
                gain += 1;
            }

            return gain;
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

            int remaining = ExpectedGrowthTotal - normalized.Sum();
            foreach ((int index, _) in remainders
                .OrderByDescending(entry => entry.Fraction)
                .ThenBy(entry => entry.Index))
            {
                if (remaining <= 0)
                {
                    break;
                }

                normalized[index] += 1;
                remaining--;
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

        private static int SelectStatIndex(IReadOnlyList<int> progress, IReadOnlyCollection<int> excludedIndices)
        {
            int selectedIndex = -1;
            int selectedProgress = int.MinValue;

            for (int i = 0; i < progress.Count; i++)
            {
                if (excludedIndices.Contains(i))
                {
                    continue;
                }

                if (progress[i] > selectedProgress)
                {
                    selectedProgress = progress[i];
                    selectedIndex = i;
                }
            }

            if (selectedIndex < 0)
            {
                throw new InvalidOperationException("Failed to choose a stat for level-up.");
            }

            return selectedIndex;
        }
    }

}
