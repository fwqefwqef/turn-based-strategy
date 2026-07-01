using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Inventory;
using Windy.Srpg.Game.Players;
using Windy.Srpg.Game.Skills;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.AI
{
    internal enum AiCombatActionKind
    {
        WeaponAttack,
        Skill,
        AreaSkill
    }

    internal sealed class AiCombatPlan
    {
        public AiCombatActionKind Kind;
        public Skill Skill;
        public Item WeaponEntry;
        public Unit PrimaryTarget;
        public Cell AreaCenterCell;
        public IReadOnlyList<Unit> AreaTargets;
        public float ExpectedDamage;
        public float Score;
        public bool ProjectsKill;
        public bool AvoidsCounterattack;
        public bool CostsNoMp;
        public bool IsHealingPlan;
        public string DebugLabel;
    }

    internal static class AiCombatPlanner
    {
        private const float KillBoost = 10f;
        private const float SafeBoost = 1.5f;
        private const float NoMpBoost = 1.1f;
        private const float ScoreTieTolerance = 0.0001f;

        public static bool TryFindBestPlan(Unit actor, Player player, CellGrid grid, Cell actingCell, out AiCombatPlan plan, bool breakTiesRandomly = false)
        {
            UnitActionAiMode actionMode = actor != null ? actor.ActionAiMode : UnitActionAiMode.Attack;
            return TryFindBestPlan(actor, player, grid, actingCell, actionMode, out plan, breakTiesRandomly);
        }

        public static bool TryFindBestOffensivePlan(Unit actor, Player player, CellGrid grid, Cell actingCell, out AiCombatPlan plan)
        {
            return TryFindBestPlan(actor, player, grid, actingCell, UnitActionAiMode.Attack, out plan, breakTiesRandomly: false);
        }

        public static bool HasAnyOffensivePlan(Unit actor, Player player, CellGrid grid, Cell actingCell)
        {
            return TryFindBestOffensivePlan(actor, player, grid, actingCell, out _);
        }

        public static bool HasAnyOffensivePlanFromReachableCells(Unit actor, Player player, CellGrid grid, out Cell bestThreatCell)
        {
            bestThreatCell = actor?.Cell;
            if (actor == null || player == null || grid == null)
            {
                return false;
            }

            List<Cell> allCells = grid.GetAllCells();
            HashSet<Cell> reachableCells = actor.GetAvailableDestinations(allCells) ?? new HashSet<Cell>();
            if (actor.Cell != null)
            {
                reachableCells.Add(actor.Cell);
            }

            foreach (Cell candidateCell in reachableCells.Where(cell => cell != null))
            {
                if (!HasAnyOffensivePlan(actor, player, grid, candidateCell))
                {
                    continue;
                }

                bestThreatCell = candidateCell;
                return true;
            }

            return false;
        }

        public static bool TryFindBestPlan(Unit actor, Player player, CellGrid grid, Cell actingCell, UnitActionAiMode actionMode, out AiCombatPlan plan, bool breakTiesRandomly = false)
        {
            plan = null;
            if (actor == null || player == null || grid == null || actingCell == null || actor.HitPoints <= 0)
            {
                return false;
            }

            List<AiCombatPlan> options = BuildPlans(actor, player, grid, actingCell, actionMode);
            if (options.Count == 0)
            {
                return false;
            }

            float topScore = options.Max(option => option.Score);
            List<AiCombatPlan> topOptions = options
                .Where(option => Mathf.Abs(option.Score - topScore) <= ScoreTieTolerance)
                .ToList();

            plan = breakTiesRandomly && topOptions.Count > 1
                ? topOptions[UnityEngine.Random.Range(0, topOptions.Count)]
                : topOptions[0];
            return true;
        }

        public static float EvaluateBestPlanScore(Unit actor, Player player, CellGrid grid, Cell actingCell)
        {
            UnitActionAiMode actionMode = actor != null ? actor.ActionAiMode : UnitActionAiMode.Attack;
            return EvaluateBestPlanScore(actor, player, grid, actingCell, actionMode);
        }

        public static float EvaluateBestPlanScore(Unit actor, Player player, CellGrid grid, Cell actingCell, UnitActionAiMode actionMode)
        {
            return TryFindBestPlan(actor, player, grid, actingCell, actionMode, out AiCombatPlan plan, breakTiesRandomly: false)
                ? Mathf.Max(0f, plan.Score)
                : 0f;
        }

        public static bool HasAnyPlan(Unit actor, Player player, CellGrid grid, Cell actingCell)
        {
            return TryFindBestPlan(actor, player, grid, actingCell, out _, breakTiesRandomly: false);
        }

        private static List<AiCombatPlan> BuildPlans(Unit actor, Player player, CellGrid grid, Cell actingCell, UnitActionAiMode actionMode)
        {
            List<Unit> enemyUnits = grid.GetEnemyUnits(player)
                .Where(unit => unit != null && unit.HitPoints > 0 && unit.Cell != null && !unit.ExcludedFromBattle)
                .ToList();

            List<AiCombatPlan> options = new List<AiCombatPlan>();
            if (enemyUnits.Count == 0 && actionMode != UnitActionAiMode.Heal)
            {
                return options;
            }

            if (actionMode == UnitActionAiMode.Heal)
            {
                List<Unit> alliedUnits = grid.GetUnitsForPlayer(player)
                    .Where(unit => unit != null && unit.HitPoints > 0 && unit.Cell != null && !unit.ExcludedFromBattle)
                    .ToList();

                AddHealingPlans(actor, actingCell, grid, alliedUnits, options);
                if (options.Count > 0)
                {
                    return options;
                }
            }

            AddWeaponAttackPlans(actor, actingCell, enemyUnits, options);
            AddSkillPlans(actor, actingCell, grid, enemyUnits, options);
            return options;
        }

        private static void AddHealingPlans(Unit actor, Cell actingCell, CellGrid grid, IReadOnlyList<Unit> allies, ICollection<AiCombatPlan> options)
        {
            IReadOnlyList<Skill> skills = actor.SkillList?.Entries ?? Array.Empty<Skill>();
            foreach (Skill skill in skills)
            {
                if (skill?.Data == null || !actor.CanUseSkill(skill) || !IsHealingSkill(skill))
                {
                    continue;
                }

                if (skill.Data.AreaProfile.Enabled)
                {
                    AddAreaHealingPlans(actor, actingCell, grid, skill, allies, options);
                    continue;
                }

                foreach (Unit ally in allies)
                {
                    if (!TryBuildHealingSkillPlan(actor, actingCell, grid, skill, ally, out AiCombatPlan plan))
                    {
                        continue;
                    }

                    options.Add(plan);
                }
            }
        }

        private static void AddWeaponAttackPlans(Unit actor, Cell actingCell, IReadOnlyList<Unit> enemies, ICollection<AiCombatPlan> options)
        {
            foreach (Unit enemy in enemies)
            {
                foreach (Item weaponEntry in actor.GetWeaponsThatCanAttack(enemy, actingCell))
                {
                    if (weaponEntry?.Weapon == null)
                    {
                        continue;
                    }

                    int hitMultiplier = Mathf.Max(1, actor.GetNumHitsForWeapon(weaponEntry.Weapon));
                    if (actor.CanPursuitAttackAgainst(enemy, weaponEntry.Weapon))
                    {
                        hitMultiplier *= 2;
                    }

                    int normalDamage = CalculatePerHitDamage(actor, enemy, weaponEntry.Weapon);
                    int critDamage = CalculatePerHitCritDamage(actor, enemy, weaponEntry.Weapon);
                    int hitChance = CalculateHitChance(actor, enemy, weaponEntry.Weapon);
                    int critChance = CalculateCritChance(actor, enemy, weaponEntry.Weapon);
                    float expectedDamage = CalculateExpectedDamage(normalDamage, critDamage, hitMultiplier, hitChance, critChance);
                    bool projectsKill = normalDamage * hitMultiplier >= enemy.HitPoints;
                    bool avoidsCounter = !CanCounterattackFromPositions(enemy, actor, actingCell, weaponEntry.Weapon.PreventsCounterattack);

                    options.Add(BuildPlan(
                        AiCombatActionKind.WeaponAttack,
                        expectedDamage,
                        projectsKill,
                        avoidsCounter,
                        costsNoMp: true,
                        primaryTarget: enemy,
                        weaponEntry: weaponEntry,
                        debugLabel: $"Attack:{weaponEntry.Weapon.Name}->{enemy.unitName}"));
                }
            }
        }

        private static void AddSkillPlans(Unit actor, Cell actingCell, CellGrid grid, IReadOnlyList<Unit> enemies, ICollection<AiCombatPlan> options)
        {
            IReadOnlyList<Skill> skills = actor.SkillList?.Entries ?? Array.Empty<Skill>();
            foreach (Skill skill in skills)
            {
                if (skill?.Data == null || !actor.CanUseSkill(skill))
                {
                    continue;
                }

                if (IsAreaOffensiveSkill(skill.Data))
                {
                    AddAreaSkillPlans(actor, actingCell, grid, skill, enemies, options);
                    continue;
                }

                if (!IsSingleTargetOffensiveSkill(skill.Data))
                {
                    continue;
                }

                foreach (Unit enemy in enemies)
                {
                    if (!TryBuildSkillPlan(actor, actingCell, grid, skill, enemy, out AiCombatPlan plan))
                    {
                        continue;
                    }

                    options.Add(plan);
                }
            }
        }

        private static void AddAreaSkillPlans(Unit actor, Cell actingCell, CellGrid grid, Skill skill, IReadOnlyList<Unit> enemies, ICollection<AiCombatPlan> options)
        {
            foreach (Cell centerCell in GetAreaSkillCandidateCenters(skill, actingCell, grid))
            {
                List<Unit> affectedTargets = GetAreaSkillTargets(actor, skill, centerCell, grid);
                List<Unit> affectedEnemies = affectedTargets
                    .Where(target => target != null && target.PlayerNumber != actor.PlayerNumber)
                    .ToList();
                if (affectedEnemies.Count == 0)
                {
                    continue;
                }

                if (!TryBuildSkillAttackProfile(actor, skill, affectedEnemies[0], actingCell, grid, out ResolvedAttackProfile profile, out _, out bool ignoresDefense))
                {
                    continue;
                }

                int hitMultiplier = Mathf.Max(1, profile.NumHits);
                float expectedDamage = 0f;
                bool projectsKill = false;
                foreach (Unit enemy in affectedEnemies)
                {
                    int damage = CalculateProfilePerHitDamage(profile, enemy, ignoresDefense) * hitMultiplier;
                    expectedDamage += damage;
                    if (damage >= enemy.HitPoints)
                    {
                        projectsKill = true;
                    }
                }

                options.Add(BuildPlan(
                    AiCombatActionKind.AreaSkill,
                    expectedDamage,
                    projectsKill,
                    avoidsCounter: true,
                    costsNoMp: skill.Data.MpCost <= 0,
                    primaryTarget: affectedEnemies[0],
                    skill: skill,
                    areaCenterCell: centerCell,
                    areaTargets: affectedTargets,
                    debugLabel: $"Area:{skill.Data.Name}@{centerCell.Coordinates}->{affectedEnemies.Count}"));
            }
        }

        private static void AddAreaHealingPlans(Unit actor, Cell actingCell, CellGrid grid, Skill skill, IReadOnlyList<Unit> allies, ICollection<AiCombatPlan> options)
        {
            foreach (Cell centerCell in GetAreaSkillCandidateCenters(skill, actingCell, grid))
            {
                List<Unit> affectedTargets = GetAreaSkillTargets(actor, skill, centerCell, grid);
                List<Unit> affectedAllies = affectedTargets
                    .Where(target => target != null && target.PlayerNumber == actor.PlayerNumber)
                    .ToList();
                if (affectedAllies.Count == 0)
                {
                    continue;
                }

                if (!TryGetAreaHealingAmount(actor, skill, centerCell, affectedAllies, grid, out int healingAmount))
                {
                    continue;
                }

                bool healsAnotherAlly = affectedAllies.Any(target => target != null && target != actor && target.HitPoints < target.MaxHitPoints);
                if (!healsAnotherAlly || healingAmount <= 0)
                {
                    continue;
                }

                options.Add(BuildPlan(
                    AiCombatActionKind.AreaSkill,
                    healingAmount,
                    projectsKill: false,
                    avoidsCounter: true,
                    costsNoMp: skill.Data.MpCost <= 0,
                    primaryTarget: affectedAllies.FirstOrDefault(target => target != null && target != actor) ?? affectedAllies[0],
                    skill: skill,
                    areaCenterCell: centerCell,
                    areaTargets: affectedTargets,
                    debugLabel: $"HealArea:{skill.Data.Name}@{centerCell.Coordinates}->{affectedAllies.Count}",
                    isHealingPlan: true));
            }
        }

        private static bool TryBuildSkillPlan(Unit actor, Cell actingCell, CellGrid grid, Skill skill, Unit target, out AiCombatPlan plan)
        {
            plan = null;
            if (actor == null || skill?.Data == null || target == null || target.HitPoints <= 0)
            {
                return false;
            }

            if (!CanUseSingleTargetSkill(actor, skill, target, actingCell, grid))
            {
                return false;
            }

            if (!TryBuildSkillAttackProfile(actor, skill, target, actingCell, grid, out ResolvedAttackProfile profile, out Item weaponEntry, out bool ignoresDefense))
            {
                return false;
            }

            int hitMultiplier = Mathf.Max(1, profile.NumHits);
            int normalDamage = CalculateProfilePerHitDamage(profile, target, ignoresDefense);
            int critDamage = CalculateProfilePerHitCritDamage(profile, target, ignoresDefense);
            int hitChance = CalculateProfileHitChance(profile, target);
            int critChance = CalculateProfileCritChance(profile, target);
            float expectedDamage = CalculateExpectedDamage(normalDamage, critDamage, hitMultiplier, hitChance, critChance);
            bool projectsKill = normalDamage * hitMultiplier >= target.HitPoints;
            bool avoidsCounter = !CanCounterattackFromPositions(target, actor, actingCell, profile.PreventsCounterattack);

            plan = BuildPlan(
                AiCombatActionKind.Skill,
                expectedDamage,
                projectsKill,
                avoidsCounter,
                costsNoMp: skill.Data.MpCost <= 0,
                primaryTarget: target,
                skill: skill,
                weaponEntry: weaponEntry,
                debugLabel: $"Skill:{skill.Data.Name}->{target.unitName}");
            return true;
        }

        private static bool TryBuildHealingSkillPlan(Unit actor, Cell actingCell, CellGrid grid, Skill skill, Unit target, out AiCombatPlan plan)
        {
            plan = null;
            if (actor == null || skill?.Data == null || target == null || target == actor || target.HitPoints <= 0)
            {
                return false;
            }

            if (!CanUseSingleTargetHealingSkill(actor, skill, target, actingCell, grid))
            {
                return false;
            }

            if (!TryGetHealingAmount(actor, skill, target, grid, out int healingAmount) || healingAmount <= 0)
            {
                return false;
            }

            plan = BuildPlan(
                AiCombatActionKind.Skill,
                healingAmount,
                projectsKill: false,
                avoidsCounter: true,
                costsNoMp: skill.Data.MpCost <= 0,
                primaryTarget: target,
                skill: skill,
                debugLabel: $"Heal:{skill.Data.Name}->{target.unitName}",
                isHealingPlan: true);
            return true;
        }

        private static AiCombatPlan BuildPlan(
            AiCombatActionKind kind,
            float expectedDamage,
            bool projectsKill,
            bool avoidsCounter,
            bool costsNoMp,
            Unit primaryTarget,
            Skill skill = null,
            Item weaponEntry = null,
            Cell areaCenterCell = null,
            IReadOnlyList<Unit> areaTargets = null,
            string debugLabel = null,
            bool isHealingPlan = false)
        {
            float adjustedScore = Mathf.Max(0f, expectedDamage);
            if (projectsKill)
            {
                adjustedScore *= KillBoost;
            }

            if (avoidsCounter)
            {
                adjustedScore *= SafeBoost;
            }

            if (costsNoMp)
            {
                adjustedScore *= NoMpBoost;
            }

            return new AiCombatPlan
            {
                Kind = kind,
                Skill = skill,
                WeaponEntry = weaponEntry,
                PrimaryTarget = primaryTarget,
                AreaCenterCell = areaCenterCell,
                AreaTargets = areaTargets,
                ExpectedDamage = expectedDamage,
                Score = adjustedScore,
                ProjectsKill = projectsKill,
                AvoidsCounterattack = avoidsCounter,
                CostsNoMp = costsNoMp,
                IsHealingPlan = isHealingPlan,
                DebugLabel = debugLabel ?? kind.ToString()
            };
        }

        private static bool IsSingleTargetOffensiveSkill(SkillData data)
        {
            return data != null
                && data.AttackProfile.Enabled
                && (data.TargetingType == SkillTargetingType.EnemyUnit || data.TargetingType == SkillTargetingType.AnyUnit);
        }

        private static bool IsAreaOffensiveSkill(SkillData data)
        {
            return data != null
                && data.AreaProfile.Enabled
                && data.AttackProfile.Enabled
                && data.AreaProfile.AffectsEnemies;
        }

        private static bool IsHealingSkill(Skill skill)
        {
            return skill?.Data != null
                && !string.IsNullOrWhiteSpace(skill.Data.EffectId)
                && SkillEffectRegistry.TryCreate(skill.Data.EffectId, out ISkillEffect effect)
                && effect is IHealingSkillEffect;
        }

        private static bool CanUseSingleTargetSkill(Unit actor, Skill skill, Unit target, Cell actingCell, CellGrid grid)
        {
            if (actor == null || skill?.Data == null || target == null || actingCell == null)
            {
                return false;
            }

            if (skill.Data.TargetingType == SkillTargetingType.EnemyUnit && target.PlayerNumber == actor.PlayerNumber)
            {
                return false;
            }

            if (skill.Data.TargetingType == SkillTargetingType.AnyUnit && target == actor)
            {
                return false;
            }

            if (!TryResolveSkillRange(actor, skill, actingCell, target, grid, out int minRange, out int maxRange))
            {
                return false;
            }

            Cell targetCell = target.Cell;
            if (targetCell == null)
            {
                return false;
            }

            int distance = actingCell.GetDistance(targetCell);
            if (distance < minRange || distance > maxRange)
            {
                return false;
            }

            SkillContext context = BuildSkillContext(actor, skill, target, grid);
            return string.IsNullOrWhiteSpace(skill.Data.EffectId)
                || (SkillEffectRegistry.TryCreate(skill.Data.EffectId, out ISkillEffect effect) && effect.CanUse(actor, context));
        }

        private static bool CanUseSingleTargetHealingSkill(Unit actor, Skill skill, Unit target, Cell actingCell, CellGrid grid)
        {
            if (actor == null || skill?.Data == null || target == null || actingCell == null)
            {
                return false;
            }

            if (target == actor || target.PlayerNumber != actor.PlayerNumber)
            {
                return false;
            }

            SkillTargetingType targetingType = skill.Data.TargetingType;
            if (targetingType != SkillTargetingType.AllyUnit && targetingType != SkillTargetingType.AnyUnit)
            {
                return false;
            }

            if (!TryResolveSkillRange(actor, skill, actingCell, target, grid, out int minRange, out int maxRange))
            {
                return false;
            }

            if (target.Cell == null)
            {
                return false;
            }

            int distance = actingCell.GetDistance(target.Cell);
            if (distance < minRange || distance > maxRange)
            {
                return false;
            }

            SkillContext context = BuildSkillContext(actor, skill, target, grid);
            return SkillEffectRegistry.TryCreate(skill.Data.EffectId, out ISkillEffect effect)
                && effect is IHealingSkillEffect
                && effect.CanUse(actor, context);
        }

        private static bool TryBuildSkillAttackProfile(Unit actor, Skill skill, Unit target, Cell actingCell, CellGrid grid, out ResolvedAttackProfile profile, out Item selectedWeaponEntry, out bool ignoresDefense)
        {
            profile = default;
            selectedWeaponEntry = null;
            ignoresDefense = false;
            if (actor == null || skill?.Data == null || target == null)
            {
                return false;
            }

            SkillData data = skill.Data;
            if (!data.AttackProfile.Enabled)
            {
                return false;
            }

            if (data.Category == SkillCategory.CombatArt)
            {
                List<(ResolvedAttackProfile profile, Item weaponEntry, bool ignoresDefense, float expectedDamage)> candidates = new List<(ResolvedAttackProfile, Item, bool, float)>();
                foreach (Item weaponEntry in actor.GetWeaponInventoryEntries().Where(entry => entry?.Weapon != null && SkillMatchesWeapon(data, entry.Weapon)))
                {
                    if (!TryResolveSkillRange(actor, skill, actingCell, target, grid, out int minRange, out int maxRange, weaponEntry.Weapon))
                    {
                        continue;
                    }

                    int distance = actingCell.GetDistance(target.Cell);
                    if (distance < minRange || distance > maxRange)
                    {
                        continue;
                    }

                    ResolvedAttackProfile candidateProfile = new ResolvedAttackProfile
                    {
                        Damage = actor.GetAttackForWeapon(weaponEntry.Weapon) + data.AttackProfile.Might,
                        Accuracy = actor.GetAccuracyForWeapon(weaponEntry.Weapon) + data.AttackProfile.Accuracy,
                        Crit = actor.GetCritForWeapon(weaponEntry.Weapon) + data.AttackProfile.Crit,
                        NumHits = Mathf.Max(1, data.AttackProfile.NumHits),
                        IsMagic = data.AttackProfile.IsMagic || actor.GetIsMagicForWeapon(weaponEntry.Weapon),
                        CanPursuitAttack = false,
                        PreventsCounterattack = data.AttackProfile.PreventsCounterattack,
                        EndsTurn = data.EndsTurn
                    };

                    SkillContext context = BuildSkillContext(actor, skill, target, null);
                    if (!TryApplySkillAttackEffect(actor, skill, context, ref candidateProfile, out bool candidateIgnoresDefense))
                    {
                        continue;
                    }

                    int multiplier = Mathf.Max(1, candidateProfile.NumHits);
                    int normalDamage = CalculateProfilePerHitDamage(candidateProfile, target, candidateIgnoresDefense);
                    int critDamage = CalculateProfilePerHitCritDamage(candidateProfile, target, candidateIgnoresDefense);
                    float expectedDamage = CalculateExpectedDamage(
                        normalDamage,
                        critDamage,
                        multiplier,
                        CalculateProfileHitChance(candidateProfile, target),
                        CalculateProfileCritChance(candidateProfile, target));
                    candidates.Add((candidateProfile, weaponEntry, candidateIgnoresDefense, expectedDamage));
                }

                if (candidates.Count == 0)
                {
                    return false;
                }

                var best = candidates.OrderByDescending(candidate => candidate.expectedDamage).First();
                profile = best.profile;
                selectedWeaponEntry = best.weaponEntry;
                ignoresDefense = best.ignoresDefense;
                return true;
            }

            bool isMagic = data.AttackProfile.IsMagic;
            int offensiveStat = isMagic ? actor.Magic : actor.Strength;
            profile = new ResolvedAttackProfile
            {
                Damage = offensiveStat + data.AttackProfile.Might,
                Accuracy = actor.Speed * 5 + data.AttackProfile.Accuracy,
                Crit = actor.Luck * 5 + data.AttackProfile.Crit,
                NumHits = Mathf.Max(1, data.AttackProfile.NumHits),
                IsMagic = isMagic,
                CanPursuitAttack = false,
                PreventsCounterattack = data.AttackProfile.PreventsCounterattack,
                EndsTurn = data.EndsTurn
            };

            SkillContext genericContext = BuildSkillContext(actor, skill, target, null);
            return TryApplySkillAttackEffect(actor, skill, genericContext, ref profile, out ignoresDefense);
        }

        private static bool TryApplySkillAttackEffect(Unit actor, Skill skill, SkillContext context, ref ResolvedAttackProfile profile, out bool ignoresDefense)
        {
            ignoresDefense = string.Equals(skill?.Data?.EffectId, "ignore_def_res", StringComparison.OrdinalIgnoreCase);
            if (skill?.Data == null || string.IsNullOrWhiteSpace(skill.Data.EffectId))
            {
                return true;
            }

            if (!SkillEffectRegistry.TryCreate(skill.Data.EffectId, out ISkillEffect effect) || !effect.CanUse(actor, context))
            {
                return false;
            }

            if (effect is IAttackSkillEffect attackSkillEffect)
            {
                attackSkillEffect.ModifyAttackProfile(actor, context, ref profile);
            }

            return true;
        }

        private static SkillContext BuildSkillContext(Unit actor, Skill skill, Unit target, CellGrid grid)
        {
            return new SkillContext
            {
                User = actor,
                PrimaryTargetUnit = target,
                TargetCell = target?.Cell,
                CellGrid = grid,
                Skill = skill?.Data
            };
        }

        private static SkillContext BuildAreaSkillContext(Unit actor, Skill skill, Cell centerCell, Unit target, CellGrid grid, IReadOnlyList<Unit> areaTargets)
        {
            return new SkillContext
            {
                User = actor,
                PrimaryTargetUnit = target,
                TargetCell = centerCell,
                CellGrid = grid,
                AreaTargets = areaTargets,
                Skill = skill?.Data
            };
        }

        private static bool TryGetHealingAmount(Unit actor, Skill skill, Unit target, CellGrid grid, out int healingAmount)
        {
            healingAmount = 0;
            if (actor == null || skill?.Data == null || target == null || target.HitPoints <= 0)
            {
                return false;
            }

            if (!SkillEffectRegistry.TryCreate(skill.Data.EffectId, out ISkillEffect effect) || effect is not IHealingSkillEffect healingEffect)
            {
                return false;
            }

            SkillContext context = BuildSkillContext(actor, skill, target, grid);
            if (!effect.CanUse(actor, context))
            {
                return false;
            }

            int missingHitPoints = Mathf.Max(0, target.MaxHitPoints - target.HitPoints);
            if (missingHitPoints <= 0)
            {
                return false;
            }

            healingAmount = Mathf.Min(missingHitPoints, Mathf.Max(0, healingEffect.GetHealingAmount(actor, context)));
            return healingAmount > 0;
        }

        private static bool TryGetAreaHealingAmount(Unit actor, Skill skill, Cell centerCell, IReadOnlyList<Unit> targets, CellGrid grid, out int totalHealingAmount)
        {
            totalHealingAmount = 0;
            if (actor == null || skill?.Data == null || centerCell == null || targets == null || targets.Count == 0)
            {
                return false;
            }

            if (!SkillEffectRegistry.TryCreate(skill.Data.EffectId, out ISkillEffect effect) || effect is not IHealingSkillEffect healingEffect)
            {
                return false;
            }

            bool canUse = false;
            foreach (Unit target in targets)
            {
                if (target == null || target.HitPoints <= 0)
                {
                    continue;
                }

                SkillContext context = BuildAreaSkillContext(actor, skill, centerCell, target, grid, targets);
                if (!effect.CanUse(actor, context))
                {
                    continue;
                }

                int missingHitPoints = Mathf.Max(0, target.MaxHitPoints - target.HitPoints);
                if (missingHitPoints <= 0)
                {
                    continue;
                }

                canUse = true;
                totalHealingAmount += Mathf.Min(missingHitPoints, Mathf.Max(0, healingEffect.GetHealingAmount(actor, context)));
            }

            return canUse && totalHealingAmount > 0;
        }

        private static List<Cell> GetAreaSkillCandidateCenters(Skill skill, Cell actingCell, CellGrid grid)
        {
            List<Cell> allCells = grid?.GetAllCells() ?? new List<Cell>();
            if (skill?.Data == null || actingCell == null || allCells.Count == 0)
            {
                return new List<Cell>();
            }

            SkillData data = skill.Data;
            int minRange = Mathf.Max(0, data.AreaProfile.MinRange);
            int maxRange = ResolveAreaSkillMaxRange(data, actingCell, grid);

            if (data.AreaProfile.Shape == SkillAreaShape.Line)
            {
                List<Cell> results = new List<Cell>();
                Vector2Int source = actingCell.Coordinates;
                Vector2Int[] directions =
                {
                    Vector2Int.up,
                    Vector2Int.right,
                    Vector2Int.down,
                    Vector2Int.left
                };

                foreach (Vector2Int direction in directions)
                {
                    for (int distance = Mathf.Max(1, minRange); distance <= Mathf.Max(minRange, maxRange); distance++)
                    {
                        Vector2Int target = source + direction * distance;
                        Cell cell = allCells.FirstOrDefault(candidate => candidate != null && candidate.Coordinates == target);
                        if (cell == null)
                        {
                            break;
                        }

                        results.Add(cell);
                    }
                }

                return results;
            }

            return allCells
                .Where(cell =>
                {
                    if (cell == null)
                    {
                        return false;
                    }

                    int distance = actingCell.GetDistance(cell);
                    return distance >= minRange && distance <= Mathf.Max(minRange, maxRange);
                })
                .ToList();
        }

        private static List<Unit> GetAreaSkillTargets(Unit actor, Skill skill, Cell centerCell, CellGrid grid)
        {
            List<Unit> results = new List<Unit>();
            if (actor == null || skill?.Data == null || centerCell == null || grid == null)
            {
                return results;
            }

            HashSet<Cell> affectedCells = GetAreaSkillAffectedCells(actor, skill, centerCell, grid);
            if (affectedCells.Count == 0)
            {
                return results;
            }

            bool affectsAllies = skill.Data.AreaProfile.AffectsAllies;
            bool affectsEnemies = skill.Data.AreaProfile.AffectsEnemies;
            bool isAnyTargetArea = affectsAllies && affectsEnemies;

            foreach (Unit unit in grid.GetAllUnits())
            {
                if (unit == null || unit.HitPoints <= 0)
                {
                    continue;
                }

                if (skill.Data.SelfImmune && unit == actor)
                {
                    continue;
                }

                if (unit.Cell == null || !affectedCells.Contains(unit.Cell))
                {
                    continue;
                }

                bool isAlly = unit.PlayerNumber == actor.PlayerNumber;
                if (!isAnyTargetArea)
                {
                    if (isAlly && !affectsAllies)
                    {
                        continue;
                    }

                    if (!isAlly && !affectsEnemies)
                    {
                        continue;
                    }
                }

                results.Add(unit);
            }

            if (results.Count == 0 || string.IsNullOrWhiteSpace(skill.Data.EffectId))
            {
                return results;
            }

            return results
                .Where(unit =>
                {
                    SkillContext context = BuildAreaSkillContext(actor, skill, centerCell, unit, grid, results);
                    return SkillEffectRegistry.TryCreate(skill.Data.EffectId, out ISkillEffect effect)
                        && effect.CanUse(actor, context);
                })
                .ToList();
        }

        private static HashSet<Cell> GetAreaSkillAffectedCells(Unit actor, Skill skill, Cell centerCell, CellGrid grid)
        {
            HashSet<Cell> results = new HashSet<Cell>();
            if (actor == null || skill?.Data == null || centerCell == null || grid == null)
            {
                return results;
            }

            if (skill.Data.AreaProfile.Shape == SkillAreaShape.Line)
            {
                Vector2Int direction = centerCell.Coordinates - actor.Cell.Coordinates;
                direction = new Vector2Int(Math.Sign(direction.x), Math.Sign(direction.y));
                if (Mathf.Abs(direction.x) + Mathf.Abs(direction.y) != 1)
                {
                    return results;
                }

                int minRange = Mathf.Max(1, skill.Data.AreaProfile.MinRange);
                int maxRange = ResolveAreaSkillMaxRange(skill.Data, actor.Cell, grid);
                int halfWidth = Mathf.Max(0, skill.Data.AreaProfile.Radius);
                Vector2Int perpendicular = new Vector2Int(-direction.y, direction.x);
                Dictionary<Vector2Int, Cell> lookup = grid.GetAllCells()
                    .Where(cell => cell != null)
                    .ToDictionary(cell => cell.Coordinates, cell => cell);

                for (int distance = minRange; distance <= Mathf.Max(minRange, maxRange); distance++)
                {
                    Vector2Int centerCoord = actor.Cell.Coordinates + direction * distance;
                    for (int offset = -halfWidth; offset <= halfWidth; offset++)
                    {
                        Vector2Int targetCoord = centerCoord + perpendicular * offset;
                        if (!lookup.TryGetValue(targetCoord, out Cell cell))
                        {
                            continue;
                        }

                        if (skill.Data.SelfImmune && cell == actor.Cell)
                        {
                            continue;
                        }

                        results.Add(cell);
                    }
                }

                return results;
            }

            int radius = Mathf.Max(0, skill.Data.AreaProfile.Radius);
            foreach (Cell cell in grid.GetAllCells())
            {
                if (cell == null || centerCell.GetDistance(cell) > radius)
                {
                    continue;
                }

                if (skill.Data.SelfImmune && cell == actor.Cell)
                {
                    continue;
                }

                results.Add(cell);
            }

            return results;
        }

        private static int ResolveAreaSkillMaxRange(SkillData data, Cell actingCell, CellGrid grid)
        {
            if (data == null || actingCell == null || grid == null)
            {
                return 0;
            }

            int minRange = Mathf.Max(0, data.AreaProfile.MinRange);
            int maxRange = Mathf.Max(minRange, data.AreaProfile.MaxRange);
            if (!SkillRangeUtility.IsInfiniteRange(maxRange))
            {
                return maxRange;
            }

            return grid.GetAllCells().DefaultIfEmpty()
                .Where(cell => cell != null)
                .Select(cell => actingCell.GetDistance(cell))
                .DefaultIfEmpty(0)
                .Max();
        }

        private static bool TryResolveSkillRange(Unit actor, Skill skill, Cell actingCell, Unit target, CellGrid grid, out int minRange, out int maxRange, WeaponData explicitWeapon = null)
        {
            minRange = 0;
            maxRange = 0;
            if (actor == null || skill?.Data == null || actingCell == null)
            {
                return false;
            }

            SkillData data = skill.Data;
            if (data.Category == SkillCategory.CombatArt)
            {
                WeaponData weapon = explicitWeapon;
                if (weapon == null)
                {
                    weapon = actor.GetWeaponsThatCanAttack(target, actingCell)
                        .Select(entry => entry?.Weapon)
                        .FirstOrDefault(candidate => candidate != null && SkillMatchesWeapon(data, candidate));
                }

                if (weapon == null)
                {
                    return false;
                }

                SkillRangeUtility.ApplyCombatArtRangeModifiers(
                    actor.GetMinAttackRangeForWeapon(weapon),
                    actor.GetMaxAttackRangeForWeapon(weapon),
                    data.AttackProfile.MinRange,
                    data.AttackProfile.MaxRange,
                    out minRange,
                    out maxRange);
            }
            else if (data.AreaProfile.Enabled)
            {
                minRange = Mathf.Max(0, data.AreaProfile.MinRange);
                maxRange = Mathf.Max(minRange, ResolveAreaSkillMaxRange(data, actingCell, grid));
            }
            else
            {
                minRange = Mathf.Max(0, data.AttackProfile.MinRange);
                maxRange = Mathf.Max(minRange, data.AttackProfile.MaxRange);
            }

            if (SkillRangeUtility.IsInfiniteRange(maxRange))
            {
                maxRange = int.MaxValue;
            }

            return true;
        }

        private static bool CanCounterattackFromPositions(Unit defender, Unit aggressor, Cell aggressorCell, bool counterPrevented)
        {
            if (counterPrevented || defender == null || aggressor == null || aggressorCell == null || defender.Cell == null)
            {
                return false;
            }

            if (!defender.CanCounterAttack || defender.HitPoints <= 0 || aggressor.HitPoints <= 0)
            {
                return false;
            }

            int distance = defender.Cell.GetDistance(aggressorCell);
            return distance >= defender.MinAttackRange
                && distance <= defender.MaxAttackRange
                && defender.PlayerNumber != aggressor.PlayerNumber;
        }

        private static float CalculateExpectedDamage(int normalDamage, int critDamage, int hitMultiplier, int hitChance, int critChance)
        {
            float hitProbability = Mathf.Clamp01(hitChance / 100f);
            float critProbability = Mathf.Clamp01(critChance / 100f);
            float expectedPerHit = hitProbability * (((1f - critProbability) * normalDamage) + (critProbability * critDamage));
            return expectedPerHit * Mathf.Max(1, hitMultiplier);
        }

        private static int CalculatePerHitDamage(Unit attacker, Unit defender, WeaponData weapon)
        {
            bool isMagicAttack = attacker.GetIsMagicForWeapon(weapon);
            int attackValue = attacker.GetAttackForWeapon(weapon);
            int defenseStat = isMagicAttack ? defender.Resistance : defender.Defense;
            return Mathf.Max(1, attackValue - defenseStat);
        }

        private static int CalculatePerHitCritDamage(Unit attacker, Unit defender, WeaponData weapon)
        {
            bool isMagicAttack = attacker.GetIsMagicForWeapon(weapon);
            int attackValue = attacker.GetAttackForWeapon(weapon);
            int defenseStat = isMagicAttack ? defender.Resistance : defender.Defense;
            return Mathf.Max(1, attackValue * 2 - defenseStat);
        }

        private static int CalculateHitChance(Unit attacker, Unit defender, WeaponData weapon)
        {
            return Mathf.Clamp(attacker.GetAccuracyForWeapon(weapon) - defender.Evade, 0, 100);
        }

        private static int CalculateCritChance(Unit attacker, Unit defender, WeaponData weapon)
        {
            return Mathf.Clamp(attacker.GetCritForWeapon(weapon) - defender.CritAvoid, 0, 100);
        }

        private static int CalculateProfilePerHitDamage(ResolvedAttackProfile profile, Unit defender, bool ignoresDefense)
        {
            int defenseStat = ignoresDefense ? 0 : (profile.IsMagic ? defender.Resistance : defender.Defense);
            return Mathf.Max(1, profile.Damage - defenseStat);
        }

        private static int CalculateProfilePerHitCritDamage(ResolvedAttackProfile profile, Unit defender, bool ignoresDefense)
        {
            int defenseStat = ignoresDefense ? 0 : (profile.IsMagic ? defender.Resistance : defender.Defense);
            return Mathf.Max(1, profile.Damage * 2 - defenseStat);
        }

        private static int CalculateProfileHitChance(ResolvedAttackProfile profile, Unit defender)
        {
            return Mathf.Clamp(profile.Accuracy - defender.Evade, 0, 100);
        }

        private static int CalculateProfileCritChance(ResolvedAttackProfile profile, Unit defender)
        {
            return Mathf.Clamp(profile.Crit - defender.CritAvoid, 0, 100);
        }

        private static bool SkillMatchesWeapon(SkillData data, WeaponData weapon)
        {
            if (data == null || weapon == null)
            {
                return false;
            }

            return data.RequiredWeaponType switch
            {
                CombatArtWeaponType.Any => true,
                CombatArtWeaponType.Sword => (weapon.WeaponType & WeaponType.Sword) != 0,
                CombatArtWeaponType.Lance => (weapon.WeaponType & WeaponType.Lance) != 0,
                CombatArtWeaponType.Blunt => (weapon.WeaponType & WeaponType.Blunt) != 0,
                CombatArtWeaponType.Ranged => (weapon.WeaponType & WeaponType.Ranged) != 0,
                CombatArtWeaponType.Magic => (weapon.WeaponType & WeaponType.Magic) != 0 || weapon.DamageType == DamageType.Magic,
                _ => false
            };
        }
    }
}
