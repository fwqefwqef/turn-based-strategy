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
    public partial class CustomUnit
    {
        private ExperienceAwardResult BuildCombatExperienceAward(CustomUnit target, bool isLethal)
        {
            return BuildCombatExperienceAward(target, target != null ? target.Level : 0, isLethal);
        }

        private ExperienceAwardResult BuildCombatExperienceAward(CustomUnit target, int targetLevel, bool isLethal)
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
                Targets = target != null ? new[] { target } : Array.Empty<CustomUnit>(),
                CellGrid = FindSceneCellGrid(),
                SourceKind = ExperienceSourceKind.EnemyCombat,
                IsLethal = isLethal,
                Amount = amount
            });
        }

        private ExperienceAwardResult BuildSupportSkillExperienceAward(CustomUnit primaryTarget, CustomCellGrid cellGrid, SkillData skill)
        {
            int amount = ExperienceCalculator.CalculateAllySkillExp(this, cellGrid);
            return BuildExperienceAward(new ExperienceGainContext
            {
                Recipient = this,
                PrimaryTarget = primaryTarget,
                Targets = primaryTarget != null ? new[] { primaryTarget } : Array.Empty<CustomUnit>(),
                CellGrid = cellGrid,
                Skill = skill,
                SourceKind = ExperienceSourceKind.AllySkill,
                Amount = amount
            });
        }

        private ExperienceAwardResult BuildAreaSkillExperienceAward(IReadOnlyList<CustomUnit> targets, CustomCellGrid cellGrid, SkillData skill, bool killedAtLeastOneTarget)
        {
            if (skill == null)
            {
                return null;
            }

            List<CustomUnit> relevantTargets = targets?
                .Where(target => target != null)
                .Distinct()
                .ToList() ?? new List<CustomUnit>();

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

        private IEnumerator PlayDeferredExperienceAward(ExperienceAwardResult award)
        {
            if (award == null)
            {
                yield break;
            }

            BeginCombatPresentation();
            try
            {
                yield return WaitForCombatHudToClose();
                yield return PlayExperienceAwardSequence(this, award);
            }
            finally
            {
                EndCombatPresentation();
            }
        }

        private IEnumerator PlayExperienceAwardSequence(CustomUnit recipient, ExperienceAwardResult award)
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

            foreach (CustomUnit target in context.Targets.Where(target => target != null && target.PlayerNumber != PlayerNumber).Distinct())
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
            CustomUnit target,
            SkillData skill,
            ExperienceSourceKind sourceKind,
            bool isLethal,
            IReadOnlyList<CustomUnit> targets,
            CustomCellGrid cellGrid)
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
}
