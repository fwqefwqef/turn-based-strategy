using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using Windy.Srpg.Game.Pathfinding.Algorithms;
using Windy.Srpg.Game.Inventory;
using Windy.Srpg.Game.AI.Actions;
using Windy.Srpg.Game.AI.Evaluators;
using Windy.Srpg.Game.CameraControl;
using Windy.Srpg.Game.Skills;
using Windy.Srpg.Game.Buffs;
using Windy.Srpg.Game.Passives;
using Windy.Srpg.Game.UI;
using Windy.Srpg.Game.Abilities;
using Windy.Srpg.Game.Campaign;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Runtime.Actions;
using Windy.Srpg.Runtime.Grid;
using Windy.Srpg.Runtime.Pathfinding;
using Windy.Srpg.Runtime.Units;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using RuntimeBuff = Windy.Srpg.Game.Buffs.Buff;

namespace Windy.Srpg.Game.Units
{
    public partial class Unit
    {
        // Uses the legacy path convention (destination-first, origin excluded) so the values
        // returned by BuildScenePaths match what Move/PreviewMove/AnimateMovementPath expect.
        private static readonly DijkstraPathfinding ScenePathfinder = new DijkstraPathfinding();

        #region CTRL+F: Combat Entry / Attack Sequence / Defense Resolution

        public virtual bool IsUnitAttackable(Unit other, Cell sourceCell)
        {
            return IsUnitAttackable(other, other.Cell, sourceCell);
        }
        public virtual bool IsUnitAttackable(Unit other, Cell otherCell, Cell sourceCell)
        {
            if (!HasUsableWeapon || other == null || otherCell == null || sourceCell == null)
            {
                return false;
            }

            var distance = sourceCell.GetDistance(otherCell);
            return distance >= MinAttackRange
                && distance <= MaxAttackRange
                && other.PlayerNumber != PlayerNumber;
        }

        public void AttackHandler(Unit unitToAttack)
        {
            if (unitToAttack == null || IsAttackSequenceRunning || !HasUsableWeapon || !CanStartActionThisTurn)
            {
                return;
            }

            StartCoroutine(AttackSequenceRoutine(unitToAttack, BuildDefaultAttackProfile()));
        }

        public void AttackHandler(Unit unitToAttack, ResolvedAttackProfile attackProfile)
        {
            if (unitToAttack == null || IsAttackSequenceRunning || !CanStartActionThisTurn)
            {
                return;
            }

            StartCoroutine(AttackSequenceRoutine(unitToAttack, attackProfile));
        }

        public void UseSupportSkill(Unit primaryTarget, bool endsTurn, Action resolveEffect, SkillData skill = null, Windy.Srpg.Game.Grid.CellGrid cellGrid = null)
        {
            if (IsAttackSequenceRunning || !CanStartActionThisTurn)
            {
                return;
            }

            StartCoroutine(SupportSkillRoutine(primaryTarget, endsTurn, resolveEffect, skill, cellGrid));
        }

        public void UseAreaSkill(IReadOnlyList<Unit> targets, bool endsTurn, Action<Unit> resolvePerTarget, SkillData skill = null, CellGrid cellGrid = null)
        {
            if (IsAttackSequenceRunning || !CanStartActionThisTurn)
            {
                return;
            }

            StartCoroutine(AreaSkillRoutine(targets, endsTurn, resolvePerTarget, skill, cellGrid));
        }
        protected virtual AttackAction DealDamage(Unit unitToAttack)
        {
            return new AttackAction(Attack, 1f);
        }

        protected void AttackActionPerformed(float actionCost)
        {
            EndTurnForUnit();
        }

        private ResolvedAttackProfile BuildDefaultAttackProfile()
        {
            return new ResolvedAttackProfile
            {
                Damage = Attack,
                Accuracy = Accuracy,
                Crit = Crit,
                NumHits = NumHits,
                IsMagic = IsMagic,
                CanPursuitAttack = CanPursuitAttack,
                PreventsCounterattack = PreventsCounterattack,
                EndsTurn = true
            };
        }

        private IEnumerator AttackSequenceRoutine(Unit unitToAttack, ResolvedAttackProfile attackProfile)
        {
            IsAttackSequenceRunning = true;
            BeginCombatPresentation();
            bool sequenceStarted = false;
            Unit experienceTarget = unitToAttack;
            int experienceTargetLevel = experienceTarget != null ? experienceTarget.Level : 0;
            bool targetWasDefeated = false;
            EventHandler<AttackEventArgs> destroyedHandler = null;
            try
            {
                if (unitToAttack == null)
                {
                    yield break;
                }

                destroyedHandler = (_, args) =>
                {
                    if (args?.Attacker == this && args.Defender == experienceTarget)
                    {
                        targetWasDefeated = true;
                    }
                };
                unitToAttack.CombatDestroyed += destroyedHandler;

                RequestCombatCameraFocus(GetCombatFocusPosition(unitToAttack));
                CombatSequenceStarted?.Invoke(this, new CombatSequenceEventArgs(this, unitToAttack));
                sequenceStarted = true;
                yield return StartCoroutine(GameplayCameraController.WaitForFocusSettled());

                if (combatSequenceStartDelaySeconds > 0f)
                {
                    yield return new WaitForSeconds(combatSequenceStartDelaySeconds);
                }

                var preCombatContext = new CombatSequenceContext(this, unitToAttack);
                InvokeBeforeCombatSequenceAsAttacker(this, preCombatContext);
                InvokeBeforeCombatSequenceAsDefender(unitToAttack, preCombatContext);

                int baseDamage = attackProfile.Damage;
                Debug.Log($"[Combat] {name} starts a {(attackProfile.IsMagic ? "magic" : "physical")} attack on {unitToAttack.name}. (attackerId={UnitID}, defenderId={unitToAttack.UnitID}, baseDamage={baseDamage}, finishedBefore={IsFinishedForTurn})");

                int initialHits = Mathf.Max(1, attackProfile.NumHits);
                for (int i = 0; i < initialHits; i++)
                {
                    if (unitToAttack == null || HitPoints <= 0 || unitToAttack.HitPoints <= 0)
                    {
                        break;
                    }

                    MarkAsAttacking(unitToAttack);
                    yield return StartCoroutine(PlayAttackLungeAnimation(unitToAttack));
                    unitToAttack.DefendHandler(
                        this,
                        baseDamage,
                        attackProfile.Accuracy,
                        attackProfile.Crit,
                        isMagicAttack: attackProfile.IsMagic,
                        isCounterAttack: false,
                        simulateOnly: false);

                    if (attackHitPauseSeconds > 0f)
                    {
                        yield return new WaitForSeconds(attackHitPauseSeconds);
                    }
                }

                if (unitToAttack != null && HitPoints > 0 && unitToAttack.HitPoints > 0)
                {
                    yield return StartCoroutine(unitToAttack.CounterAttack(this, attackProfile.PreventsCounterattack));
                }

                ExperienceAwardResult counterExperienceAward = unitToAttack?.TakeQueuedDeferredExperienceAward();

                bool pursuitAttack = unitToAttack != null
                    && attackProfile.CanPursuitAttack
                    && Speed >= unitToAttack.Speed + PursuitAttackSpeedThreshold;

                if (pursuitAttack && HitPoints > 0 && unitToAttack != null && unitToAttack.HitPoints > 0)
                {
                    Debug.Log($"[Combat] {name} starts a pursuit {(attackProfile.IsMagic ? "magic" : "physical")} attack on {unitToAttack.name}. (attackerId={UnitID}, defenderId={unitToAttack.UnitID}, baseDamage={baseDamage}, finishedBefore={IsFinishedForTurn})");
                    int pursuitHits = Mathf.Max(1, attackProfile.NumHits);
                    for (int i = 0; i < pursuitHits; i++)
                    {
                        if (unitToAttack == null || HitPoints <= 0 || unitToAttack.HitPoints <= 0)
                        {
                            break;
                        }

                        MarkAsAttacking(unitToAttack);
                        yield return StartCoroutine(PlayAttackLungeAnimation(unitToAttack));
                        unitToAttack.DefendHandler(
                            this,
                            baseDamage,
                            attackProfile.Accuracy,
                            attackProfile.Crit,
                            isMagicAttack: attackProfile.IsMagic,
                            isCounterAttack: false,
                            simulateOnly: false);

                        if (attackHitPauseSeconds > 0f)
                        {
                            yield return new WaitForSeconds(attackHitPauseSeconds);
                        }
                    }
                }

                ExperienceAwardResult experienceAward = null;
                if (experienceTarget != null || experienceTargetLevel > 0)
                {
                    if (!targetWasDefeated && unitToAttack != null)
                    {
                        targetWasDefeated = unitToAttack.HitPoints <= 0;
                    }

                    experienceAward = BuildCombatExperienceAward(experienceTarget, experienceTargetLevel, targetWasDefeated);
                }

                if (attackProfile.EndsTurn)
                {
                    AttackActionPerformed(1f);
                }

                if (sequenceStarted)
                {
                    var combatSequenceContext = new CombatSequenceContext(this, unitToAttack);
                    InvokeAfterCombatSequenceAsAttacker(this, combatSequenceContext);
                    InvokeAfterCombatSequenceAsDefender(unitToAttack, combatSequenceContext);
                    CombatSequenceEnded?.Invoke(this, new CombatSequenceEventArgs(this, unitToAttack));
                    sequenceStarted = false;
                }

                if (experienceAward != null || counterExperienceAward != null)
                {
                    yield return StartCoroutine(PlayPostCombatExperienceAwards(
                        unitToAttack,
                        experienceAward,
                        counterExperienceAward));
                }

                Debug.Log($"[Combat] {name}'s attack sequence is complete. (attackerId={UnitID}, finishedAfter={IsFinishedForTurn})");
            }
            finally
            {
                if (experienceTarget != null && destroyedHandler != null)
                {
                    experienceTarget.CombatDestroyed -= destroyedHandler;
                }

                if (sequenceStarted)
                {
                    var combatSequenceContext = new CombatSequenceContext(this, unitToAttack);
                    InvokeAfterCombatSequenceAsAttacker(this, combatSequenceContext);
                    InvokeAfterCombatSequenceAsDefender(unitToAttack, combatSequenceContext);
                    CombatSequenceEnded?.Invoke(this, new CombatSequenceEventArgs(this, unitToAttack));
                }

                ReleaseCombatCameraFocus();
                IsAttackSequenceRunning = false;
                EndCombatPresentation();
            }
        }

        private IEnumerator PlayAttackLungeAnimation(Unit target)
        {
            if (target == null)
            {
                yield break;
            }

            Vector3 startPos = transform.localPosition;
            Vector3 targetPos = target.transform.localPosition;
            Vector3 toTarget = targetPos - startPos;
            if (toTarget.sqrMagnitude <= 0.0001f)
            {
                yield break;
            }

            // Lunge at roughly 2x movement speed, capped to 1 tile of distance.
            float lungeSpeed = MovementAnimationSpeed > 0f ? MovementAnimationSpeed * 1f : 12f;
            Vector3 lungePos = startPos + toTarget.normalized * Mathf.Min(1f, toTarget.magnitude);

            while ((transform.localPosition - lungePos).sqrMagnitude > 0.0001f)
            {
                transform.localPosition = Vector3.MoveTowards(transform.localPosition, lungePos, lungeSpeed * Time.deltaTime);
                yield return null;
            }

            while ((transform.localPosition - startPos).sqrMagnitude > 0.0001f)
            {
                transform.localPosition = Vector3.MoveTowards(transform.localPosition, startPos, lungeSpeed * Time.deltaTime);
                yield return null;
            }

            transform.localPosition = startPos;
        }

        public int DefendHandler(Unit aggressor, int damage, int aggressorHit, int aggressorCrit, bool isMagicAttack = false, bool isCounterAttack = false, bool simulateOnly = false)
        {
            if (aggressor == null)
            {
                return 0;
            }

            int simulatedHitPoints = HitPoints;
            int damageTaken = 0;

            if (!simulateOnly)
            {
                Debug.Log($"[Combat] {name} is defending against {aggressor.name}'s {(isMagicAttack ? "magic" : "physical")} attack. (defenderId={UnitID}, aggressorId={aggressor.UnitID}, incomingDamage={damage})");
            }

            if (simulatedHitPoints > 0 && aggressor.HitPoints > 0)
            {
                if (!simulateOnly)
                {
                    MarkAsDefending(aggressor);
                }

                int hitChance = Mathf.Clamp(aggressorHit - Evade, 0, 100);
                int critChance = Mathf.Clamp(aggressorCrit - CritAvoid, 0, 100);
                bool isHit = UnityEngine.Random.value * 100f < hitChance;
                damageTaken = 0;
                bool isCrit = isHit && UnityEngine.Random.value * 100f < critChance;

                var damageContext = new DamageChangeContext
                {
                    Attacker = aggressor,
                    Defender = this,
                    IsHit = isHit,
                    IsCrit = isCrit,
                    IsMagicAttack = isMagicAttack,
                    IsCounterAttack = isCounterAttack,
                    IsSimulated = simulateOnly,
                    Phase = DamageChangePhase.Outcome
                };

                ApplyDamageChangeModifiers(aggressor, damageContext, 0);
                ApplyDamageTakenModifiers(this, damageContext, damageContext.Damage);
                ApplyDamageMultipliers(aggressor, damageContext, damageContext.Damage);
                ApplyTakeDamageMultipliers(this, damageContext, damageContext.Damage);

                if (!damageContext.IsHit)
                {
                    damageContext.IsCrit = false;
                    damageContext.Damage = 0;
                }
                else
                {
                    int defenseStat = isMagicAttack ? Resistance : Defense;
                    int rawDamage = damageContext.IsCrit
                        ? damage * 2 - defenseStat
                        : damage - defenseStat;

                    int mitigatedDamage = Mathf.Max(1, rawDamage);
                    damageContext.Phase = DamageChangePhase.Damage;

                    damageTaken = ApplyDamageChangeModifiers(aggressor, damageContext, mitigatedDamage);
                    damageTaken = ApplyDamageTakenModifiers(this, damageContext, damageTaken);
                    damageTaken = ApplyDamageMultipliers(aggressor, damageContext, damageTaken);
                    damageTaken = ApplyTakeDamageMultipliers(this, damageContext, damageTaken);
                    damageTaken = Mathf.Max(0, damageTaken);
                    damageContext.Damage = damageTaken;

                    if (!simulateOnly)
                    {
                        Debug.Log($"[Combat] Strike hits {name}{(damageContext.IsCrit ? " and crits" : "")}, dealing {damageTaken} damage. (defenderId={UnitID}, hitChance={hitChance}%, critChance={critChance}%, crit={damageContext.IsCrit}, mitigationStat={(isMagicAttack ? "Resistance" : "Defence")}, mitigationValue={defenseStat})");
                    }
                }

                if (!damageContext.IsHit && !simulateOnly)
                {
                    Debug.Log($"[Combat] Strike misses {name}. (defenderId={UnitID}, hitChance={hitChance}%, damageTaken=0)");
                }

                simulatedHitPoints -= damageTaken;

                if (!simulateOnly)
                {
                    int previousHitPoints = HitPoints;
                    HitPoints = simulatedHitPoints;
                    DefenceActionPerformed();
                    RaiseHealthChanged(previousHitPoints, HitPoints, aggressor);

                    if (HitPoints <= 0)
                    {
                        DestroyedInCombat?.Invoke(this, new UnitDestroyedEventArgs(aggressor, this, damageTaken));
                        CombatDestroyed?.Invoke(this, new AttackEventArgs(aggressor, this, damageTaken));
                        OnDestroyed();
                    }
                }
            }

            return damageTaken;
        }
        protected virtual int Defend(Unit aggressor, int damage)
        {
            return Mathf.Clamp(damage - Defense, 1, damage);
        }

        protected void DefenceActionPerformed() { }

        public void RefreshHealthState(Unit source = null)
        {
            int previousHitPoints = HitPoints;
            int previousMaxHitPoints = Mathf.Max(1, ComputedTotalHitPoints);
            int currentMaxHitPoints = MaxHitPoints;
            int previousManaPoints = CurrentManaPoints;
            int previousMaxManaPoints = Mathf.Max(0, ComputedTotalManaPoints);
            int currentMaxManaPoints = MaxManaPoints;

            if (currentMaxHitPoints < previousMaxHitPoints && previousHitPoints == previousMaxHitPoints)
            {
                HitPoints = currentMaxHitPoints;
            }

            HitPoints = Mathf.Min(HitPoints, currentMaxHitPoints);
            if (currentMaxManaPoints < previousMaxManaPoints && previousManaPoints == previousMaxManaPoints)
            {
                CurrentManaPoints = currentMaxManaPoints;
            }

            CurrentManaPoints = Mathf.Clamp(CurrentManaPoints, 0, currentMaxManaPoints);

            ComputedTotalHitPoints = currentMaxHitPoints;
            ComputedTotalManaPoints = currentMaxManaPoints;
            RaiseHealthChanged(previousHitPoints, HitPoints, source);
            RaiseStatsChanged();
        }
        #endregion

        #region CTRL+F: Buff Display / Event Dispatch / EXP Gain Pipeline

        public IEnumerable<string> GetActiveBuffDisplayNames()
        {
            if (BuffList != null)
            {
                foreach (var entry in BuffList.Entries)
                {
                    var data = entry?.Data;
                    if (data == null)
                    {
                        continue;
                    }

                    yield return entry.IsInfinite
                        ? data.Name
                        : $"{data.Name} ({entry.RemainingDuration})";
                }
            }
        }

        public string GetActiveBuffDisplayText()
        {
            var lines = new List<string>();

            if (BuffList != null)
            {
                foreach (var entry in BuffList.Entries)
                {
                    var data = entry?.Data;
                    if (data == null)
                    {
                        continue;
                    }

                    lines.Add(data.Name);
                    lines.Add(data.Description);
                    lines.Add(string.Empty);
                }
            }

            if (lines.Count == 0)
            {
                return "Current Buffs:\nNone";
            }

            while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
            {
                lines.RemoveAt(lines.Count - 1);
            }

            return "Current Buffs:\n\n" + string.Join("\n", lines);
        }

        private void RaiseHealthChanged(int previousHitPoints, int currentHitPoints, Unit source)
        {
            if (UnitHealthChanged != null)
            {
                UnitHealthChanged.Invoke(this, new UnitHealthChangedEventArgs(source, this, previousHitPoints, currentHitPoints));
            }
        }

        private void RaiseBuffsChanged()
        {
            UnitBuffsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void RaiseStatsChanged()
        {
            UnitStatsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void RaiseProgressionChanged()
        {
            UnitProgressionChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region CTRL+F: Counterattacks / Damage Hooks / Skill Resolution / Camera

        private IEnumerator CounterAttack(Unit aggressor, bool counterPrevented = false)
        {
            if (!ShouldTriggerCounterAttack(aggressor, counterPrevented))
            {
                Debug.Log($"[Combat] {name} does not counterattack after attack resolution. (defenderId={UnitID}, canCounter={CanCounterAttack}, defenderDead={HitPoints <= 0}, aggressorDead={(aggressor == null || aggressor.HitPoints <= 0)}, aggressorInRange={IsAggressorInCounterRange(aggressor)}, counterPrevented={counterPrevented})");
                yield break;
            }

            Debug.Log($"[Combat] {name} counterattacks {aggressor.name} after attack resolution. (defenderId={UnitID}, aggressorId={aggressor.UnitID})");

            Unit experienceTarget = aggressor;
            int experienceTargetLevel = experienceTarget != null ? experienceTarget.Level : 0;
            bool targetWasDefeated = false;
            EventHandler<AttackEventArgs> destroyedHandler = null;

            try
            {
                if (experienceTarget != null)
                {
                    destroyedHandler = (_, args) =>
                    {
                        if (args?.Attacker == this && args.Defender == experienceTarget)
                        {
                            targetWasDefeated = true;
                        }
                    };
                    experienceTarget.CombatDestroyed += destroyedHandler;
                }

                MarkAsAttacking(aggressor);
                yield return StartCoroutine(PlayAttackLungeAnimation(aggressor));
                var counterDamage = Attack;
                aggressor.DefendHandler(
                    this,
                    counterDamage,
                    Accuracy,
                    Crit,
                    isMagicAttack: IsMagic,
                    isCounterAttack: true,
                    simulateOnly: false);

                if (attackHitPauseSeconds > 0f)
                {
                    yield return new WaitForSeconds(attackHitPauseSeconds);
                }

                if (!targetWasDefeated && experienceTarget != null)
                {
                    targetWasDefeated = experienceTarget.HitPoints <= 0;
                }

                ExperienceAwardResult experienceAward = BuildCombatExperienceAward(
                    experienceTarget,
                    experienceTargetLevel,
                    targetWasDefeated);
                if (experienceAward != null)
                {
                    QueueDeferredExperienceAward(experienceAward);
                }
            }
            finally
            {
                if (experienceTarget != null && destroyedHandler != null)
                {
                    experienceTarget.CombatDestroyed -= destroyedHandler;
                }
            }
        }

        private bool ShouldTriggerCounterAttack(Unit aggressor, bool counterPrevented = false)
        {
            return !counterPrevented
                && CanCounterAttack
                && HitPoints > 0
                && aggressor != null
                && aggressor.HitPoints > 0
                && IsAggressorInCounterRange(aggressor);
        }

        public bool CanCounterAttackAgainst(Unit aggressor, bool counterPrevented = false)
        {
            return ShouldTriggerCounterAttack(aggressor, counterPrevented);
        }

        public bool CanPursuitAttackAgainst(Unit other)
        {
            return other != null
                && CanPursuitAttack
                && HitPoints > 0
                && other.HitPoints > 0
                && Speed >= other.Speed + PursuitAttackSpeedThreshold;
        }

        private bool IsAggressorInCounterRange(Unit aggressor)
        {
            if (aggressor == null)
            {
                return false;
            }

            var defenderCell = HasPendingMove ? PreviewCell : Cell;
            var aggressorCell = aggressor.HasPendingMove ? aggressor.PreviewCell : aggressor.Cell;
            if (defenderCell == null || aggressorCell == null)
            {
                return false;
            }

            var distance = defenderCell.GetDistance(aggressorCell);
            return distance >= MinAttackRange
                && distance <= MaxAttackRange
                && aggressor.PlayerNumber != PlayerNumber;
        }

        public int DryAttack(Unit other)
        {
            if (other == null || !HasUsableWeapon)
            {
                return 0;
            }

            int damage = Attack;
            int singleHitDamage = other.DefendHandler(
                this,
                damage,
                Accuracy,
                Crit,
                isMagicAttack: IsMagic,
                isCounterAttack: false,
                simulateOnly: true);

            int estimatedDamage = singleHitDamage * Mathf.Max(1, NumHits);
            bool pursuitAttack = CanPursuitAttack && Speed >= other.Speed + PursuitAttackSpeedThreshold;
            if (pursuitAttack)
            {
                estimatedDamage *= 2;
            }

            return estimatedDamage;
        }

        private static int ApplyDamageChangeModifiers(Unit attacker, DamageChangeContext context, int currentDamage)
        {
            if (attacker == null)
            {
                return currentDamage;
            }

            context.Damage = currentDamage;

            var modifiers = attacker.GetComponentsInChildren<MonoBehaviour>(true).OfType<IP_DamageChange>();
            foreach (var modifier in modifiers)
            {
                modifier.DamageChange(context);
            }

            if (attacker.BuffList != null)
            {
                foreach (var effect in attacker.BuffList.GetActiveEffects())
                {
                    if (effect is IP_DamageChange damageChange)
                    {
                        damageChange.DamageChange(context);
                    }
                }
            }

            if (attacker.PassiveList != null)
            {
                foreach (var effect in attacker.PassiveList.GetActiveEffects())
                {
                    if (effect is IP_DamageChange damageChange)
                    {
                        damageChange.DamageChange(context);
                    }
                }
            }

            return context.Damage;
        }

        private static int ApplyDamageTakenModifiers(Unit defender, DamageChangeContext context, int currentDamage)
        {
            if (defender == null)
            {
                return currentDamage;
            }

            context.Damage = currentDamage;

            var modifiers = defender.GetComponentsInChildren<MonoBehaviour>(true).OfType<IP_TakeDamageChange>();
            foreach (var modifier in modifiers)
            {
                modifier.TakeDamageChange(context);
            }

            if (defender.BuffList != null)
            {
                foreach (var effect in defender.BuffList.GetActiveEffects())
                {
                    if (effect is IP_TakeDamageChange takeDamageChange)
                    {
                        takeDamageChange.TakeDamageChange(context);
                    }
                }
            }

            if (defender.PassiveList != null)
            {
                foreach (var effect in defender.PassiveList.GetActiveEffects())
                {
                    if (effect is IP_TakeDamageChange takeDamageChange)
                    {
                        takeDamageChange.TakeDamageChange(context);
                    }
                }
            }

            return context.Damage;
        }

        private static int ApplyDamageMultipliers(Unit attacker, DamageChangeContext context, int currentDamage)
        {
            if (attacker == null)
            {
                return currentDamage;
            }

            context.Damage = currentDamage;

            var modifiers = attacker.GetComponentsInChildren<MonoBehaviour>(true).OfType<IP_DamageMultiplier>();
            foreach (var modifier in modifiers)
            {
                modifier.DamageMultiplier(context);
            }

            if (attacker.BuffList != null)
            {
                foreach (var effect in attacker.BuffList.GetActiveEffects())
                {
                    if (effect is IP_DamageMultiplier damageMultiplier)
                    {
                        damageMultiplier.DamageMultiplier(context);
                    }
                }
            }

            if (attacker.PassiveList != null)
            {
                foreach (var effect in attacker.PassiveList.GetActiveEffects())
                {
                    if (effect is IP_DamageMultiplier damageMultiplier)
                    {
                        damageMultiplier.DamageMultiplier(context);
                    }
                }
            }

            return context.Damage;
        }

        private static int ApplyTakeDamageMultipliers(Unit defender, DamageChangeContext context, int currentDamage)
        {
            if (defender == null)
            {
                return currentDamage;
            }

            context.Damage = currentDamage;

            var modifiers = defender.GetComponentsInChildren<MonoBehaviour>(true).OfType<IP_TakeDamageMultiplier>();
            foreach (var modifier in modifiers)
            {
                modifier.TakeDamageMultiplier(context);
            }

            if (defender.BuffList != null)
            {
                foreach (var effect in defender.BuffList.GetActiveEffects())
                {
                    if (effect is IP_TakeDamageMultiplier takeDamageMultiplier)
                    {
                        takeDamageMultiplier.TakeDamageMultiplier(context);
                    }
                }
            }

            if (defender.PassiveList != null)
            {
                foreach (var effect in defender.PassiveList.GetActiveEffects())
                {
                    if (effect is IP_TakeDamageMultiplier takeDamageMultiplier)
                    {
                        takeDamageMultiplier.TakeDamageMultiplier(context);
                    }
                }
            }

            return context.Damage;
        }

        private static void InvokeAfterCombatSequenceAsAttacker(Unit attacker, CombatSequenceContext context)
        {
            if (attacker == null)
            {
                return;
            }

            foreach (var listener in attacker.GetComponentsInChildren<MonoBehaviour>(true).OfType<IP_AfterCombat_Attacker>())
            {
                listener.AfterCombatSequenceAsAttacker(context);
            }

            if (attacker.BuffList != null)
            {
                foreach (var effect in attacker.BuffList.GetActiveEffects())
                {
                    if (effect is IP_AfterCombat_Attacker listener)
                    {
                        listener.AfterCombatSequenceAsAttacker(context);
                    }
                }
            }

            if (attacker.PassiveList != null)
            {
                foreach (var effect in attacker.PassiveList.GetActiveEffects())
                {
                    if (effect is IP_AfterCombat_Attacker listener)
                    {
                        listener.AfterCombatSequenceAsAttacker(context);
                    }
                }
            }
        }

        private IEnumerator SupportSkillRoutine(Unit primaryTarget, bool endsTurn, Action resolveEffect, SkillData skill, Windy.Srpg.Game.Grid.CellGrid cellGrid)
        {
            IsAttackSequenceRunning = true;
            BeginCombatPresentation();
            bool sequenceStarted = false;
            try
            {
                RequestCombatCameraFocus(GetCombatFocusPosition(primaryTarget ?? this));
                CombatSequenceStarted?.Invoke(this, new CombatSequenceEventArgs(this, primaryTarget ?? this));
                sequenceStarted = true;
                yield return StartCoroutine(GameplayCameraController.WaitForFocusSettled());

                if (combatSequenceStartDelaySeconds > 0f)
                {
                    yield return new WaitForSeconds(combatSequenceStartDelaySeconds);
                }

                resolveEffect?.Invoke();

                ExperienceAwardResult experienceAward = BuildSupportSkillExperienceAward(
                    primaryTarget,
                    cellGrid as Windy.Srpg.Game.Grid.CellGrid ?? FindSceneCellGrid(),
                    skill);

                if (attackHitPauseSeconds > 0f)
                {
                    yield return new WaitForSeconds(attackHitPauseSeconds);
                }

                if (endsTurn)
                {
                    EndTurnForUnit();
                }

                if (sequenceStarted)
                {
                    CombatSequenceEnded?.Invoke(this, new CombatSequenceEventArgs(this, primaryTarget ?? this));
                    sequenceStarted = false;
                }

                if (experienceAward != null)
                {
                    yield return StartCoroutine(WaitForCombatHudToClose());
                    yield return StartCoroutine(PlayExperienceAwardSequence(this, experienceAward));
                }
            }
            finally
            {
                if (sequenceStarted)
                {
                    CombatSequenceEnded?.Invoke(this, new CombatSequenceEventArgs(this, primaryTarget ?? this));
                }

                ReleaseCombatCameraFocus();
                IsAttackSequenceRunning = false;
                EndCombatPresentation();
            }
        }

        private IEnumerator AreaSkillRoutine(IReadOnlyList<Unit> targets, bool endsTurn, Action<Unit> resolvePerTarget, SkillData skill, Windy.Srpg.Game.Grid.CellGrid cellGrid)
        {
            IsAttackSequenceRunning = true;
            BeginCombatPresentation();

            const float groupedStartDelaySeconds = 0.12f;
            const float groupedStepDelaySeconds = 0.18f;

            try
            {
                bool killedAtLeastOneTarget = false;
                var orderedTargets = targets?
                    .Where(target => target != null)
                    .Distinct()
                    .ToList() ?? new List<Unit>();

                if (orderedTargets.Count == 0)
                {
                    if (endsTurn)
                    {
                        EndTurnForUnit();
                    }

                    yield break;
                }

                RequestCombatCameraFocus(GetAreaCombatFocusPosition(orderedTargets));
                yield return StartCoroutine(GameplayCameraController.WaitForFocusSettled());

                if (groupedStartDelaySeconds > 0f)
                {
                    yield return new WaitForSeconds(groupedStartDelaySeconds);
                }

                for (int i = 0; i < orderedTargets.Count; i++)
                {
                    Unit target = orderedTargets[i];
                    if (target == null)
                    {
                        continue;
                    }

                    bool targetWasAliveBefore = target.HitPoints > 0;
                    CombatSequenceStarted?.Invoke(this, new CombatSequenceEventArgs(this, target));
                    resolvePerTarget?.Invoke(target);
                    CombatSequenceEnded?.Invoke(this, new CombatSequenceEventArgs(this, target));

                    if (targetWasAliveBefore && target.HitPoints <= 0)
                    {
                        killedAtLeastOneTarget = true;
                    }

                    if (i < orderedTargets.Count - 1 && groupedStepDelaySeconds > 0f)
                    {
                        yield return new WaitForSeconds(groupedStepDelaySeconds);
                    }
                }

                ExperienceAwardResult experienceAward = BuildAreaSkillExperienceAward(
                    orderedTargets,
                    cellGrid as Windy.Srpg.Game.Grid.CellGrid ?? FindSceneCellGrid(),
                    skill,
                    killedAtLeastOneTarget);

                if (experienceAward != null)
                {
                    yield return StartCoroutine(WaitForCombatHudToClose());
                    yield return StartCoroutine(PlayExperienceAwardSequence(this, experienceAward));
                }

                if (endsTurn)
                {
                    EndTurnForUnit();
                }
            }
            finally
            {
                ReleaseCombatCameraFocus();
                IsAttackSequenceRunning = false;
                EndCombatPresentation();
            }
        }

        private static void RequestCombatCameraFocus(Vector3 worldPosition)
        {
            CombatCameraFocusRequested?.Invoke(worldPosition);
        }

        private static void ReleaseCombatCameraFocus()
        {
            CombatCameraFocusReleased?.Invoke();
        }

        private Vector3 GetCombatFocusPosition(Unit target)
        {
            if (target == null)
            {
                return transform.position;
            }

            Cell targetCell = target.HasPendingMove ? target.PreviewCell : target.Cell;
            if (targetCell != null)
            {
                return targetCell.transform.position;
            }

            return target.transform.position;
        }

        private static Vector3 GetAreaCombatFocusPosition(IReadOnlyList<Unit> targets)
        {
            if (targets == null || targets.Count == 0)
            {
                return Vector3.zero;
            }

            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (Unit target in targets)
            {
                if (target == null)
                {
                    continue;
                }

                Cell focusCell = target.HasPendingMove ? target.PreviewCell : target.Cell;
                sum += focusCell != null ? focusCell.transform.position : target.transform.position;
                count++;
            }

            return count > 0 ? sum / count : Vector3.zero;
        }

        private static void InvokeBeforeCombatSequenceAsAttacker(Unit attacker, CombatSequenceContext context)
        {
            if (attacker == null)
            {
                return;
            }

            foreach (var listener in attacker.GetComponentsInChildren<MonoBehaviour>(true).OfType<IP_BeforeCombat_Attacker>())
            {
                listener.BeforeCombatSequenceAsAttacker(context);
            }

            if (attacker.BuffList != null)
            {
                foreach (var effect in attacker.BuffList.GetActiveEffects())
                {
                    if (effect is IP_BeforeCombat_Attacker listener)
                    {
                        listener.BeforeCombatSequenceAsAttacker(context);
                    }
                }
            }

            if (attacker.PassiveList != null)
            {
                foreach (var effect in attacker.PassiveList.GetActiveEffects())
                {
                    if (effect is IP_BeforeCombat_Attacker listener)
                    {
                        listener.BeforeCombatSequenceAsAttacker(context);
                    }
                }
            }
        }

        private static void InvokeBeforeCombatSequenceAsDefender(Unit defender, CombatSequenceContext context)
        {
            if (defender == null)
            {
                return;
            }

            foreach (var listener in defender.GetComponentsInChildren<MonoBehaviour>(true).OfType<IP_BeforeCombat_Defender>())
            {
                listener.BeforeCombatSequenceAsDefender(context);
            }

            if (defender.BuffList != null)
            {
                foreach (var effect in defender.BuffList.GetActiveEffects())
                {
                    if (effect is IP_BeforeCombat_Defender listener)
                    {
                        listener.BeforeCombatSequenceAsDefender(context);
                    }
                }
            }

            if (defender.PassiveList != null)
            {
                foreach (var effect in defender.PassiveList.GetActiveEffects())
                {
                    if (effect is IP_BeforeCombat_Defender listener)
                    {
                        listener.BeforeCombatSequenceAsDefender(context);
                    }
                }
            }
        }

        private static void InvokeAfterCombatSequenceAsDefender(Unit defender, CombatSequenceContext context)
        {
            if (defender == null)
            {
                return;
            }

            foreach (var listener in defender.GetComponentsInChildren<MonoBehaviour>(true).OfType<IP_AfterCombat_Defender>())
            {
                listener.AfterCombatSequenceAsDefender(context);
            }

            if (defender.BuffList != null)
            {
                foreach (var effect in defender.BuffList.GetActiveEffects())
                {
                    if (effect is IP_AfterCombat_Defender listener)
                    {
                        listener.AfterCombatSequenceAsDefender(context);
                    }
                }
            }

            if (defender.PassiveList != null)
            {
                foreach (var effect in defender.PassiveList.GetActiveEffects())
                {
                    if (effect is IP_AfterCombat_Defender listener)
                    {
                        listener.AfterCombatSequenceAsDefender(context);
                    }
                }
            }
        }

        #endregion

        #region CTRL+F: Movement / Pending Move Preview / Pathfinding

        public IEnumerator Move(Cell destinationCell, IList<Cell> path)
        {
            if (destinationCell == null || path == null || path.Count == 0)
            {
                yield break;
            }

            Windy.Srpg.Game.Grid.CellGrid cellGrid = FindSceneCellGrid();
            Cell fromCell = Cell;
            Cell resolvedStartCell = ResolveTransformStartCell(cellGrid, fromCell);
            if (resolvedStartCell != null && resolvedStartCell != fromCell)
            {
                UnregisterCellOccupancyList(fromCell);
                RefreshCellOccupancy(fromCell);

                Cell = resolvedStartCell;
                RegisterCellOccupancyList(resolvedStartCell);

                RefreshCellOccupancy(resolvedStartCell);
                cachedPaths = null;
                fromCell = resolvedStartCell;

                if (cellGrid != null)
                {
                    List<Cell> allCells = cellGrid?.GetAllCells() ?? new List<Cell>();
                    CachePaths(allCells);
                    path = FindPath(allCells, destinationCell);
                    if (path == null || path.Count == 0)
                    {
                        yield break;
                    }
                }
            }

            if (fromCell != null)
            {
                SnapToCellLocalPosition(fromCell);
            }

            // Phase 5: the scene Unit executes the move directly. The runtime mirror is
            // Scene Unit owns movement; occupancy is updated on commit.
            var totalMovementCost = path.Sum(h => h.MovementCost);
            MovementPoints -= totalMovementCost;

            if (MovementAnimationSpeed > 0)
            {
                yield return StartCoroutine(AnimateMovementPath(path));
            }
            else
            {
                SnapToCellLocalPosition(destinationCell);
                OnMoveFinished();
            }

            UnregisterCellOccupancyList(fromCell);
            RefreshCellOccupancy(fromCell);

            Cell = destinationCell;
            RegisterCellOccupancyList(destinationCell);

            cachedPaths = null;
            RefreshCellOccupancy(destinationCell);
            FindSceneCellGrid()?.RefreshSceneCellOccupancyNow();
            cellGrid?.RequestBattleOutcomeEvaluation();
        }

        // CTRL+F: PENDING MOVE
        private PendingMove? _pendingMove;
        private int _previewMoveVersion;

        private struct PendingMove
        {
            public Cell FromCell;
            public Cell ToCell;
            public IList<Cell> Path;
            public float MovementPointsBefore;
            public float MovementCost;
            public Vector3 FromLocalPos;
        }

        public bool HasPendingMove => _pendingMove.HasValue;
        internal float GetPendingMovementPointsBefore() =>
            _pendingMove.HasValue ? _pendingMove.Value.MovementPointsBefore : MovementPoints;
        public Cell PreviewCell
        {
            get
            {
                return _pendingMove.HasValue ? _pendingMove.Value.ToCell : Cell;
            }
        }

        public virtual IEnumerator PreviewMove(Cell destinationCell, IList<Cell> path)
        {
            // If already previewing, revert first (safe)
            CancelPendingMove();

            if (destinationCell == null || path == null || path.Count == 0)
                yield break;

            int previewMoveVersion = ++_previewMoveVersion;

            _pendingMove = new PendingMove
            {
                FromCell = Cell,
                ToCell = destinationCell,
                Path = path,
                MovementPointsBefore = MovementPoints,
                MovementCost = path.Sum(h => h.MovementCost),
                FromLocalPos = transform.localPosition
            };

            // Do NOT touch Cell/occupancy or MovementPoints here.
            if (MovementAnimationSpeed > 0)
            {
                PreviewMoveCameraFollowRequested?.Invoke(transform.position);
                yield return PreviewMovementAnimation(path, previewMoveVersion);
                PreviewMoveCameraFollowReleased?.Invoke();
            }
            else
            {
                var isMap2D = IsSceneGrid2D();
                var destLocal = destinationCell.transform.localPosition;
                if (isMap2D)
                    destLocal = new Vector3(destLocal.x, destLocal.y, transform.localPosition.z);
                transform.localPosition = destLocal;
            }
        }

        public virtual bool ConfirmPendingMove(bool consumeAllRemainingMovement = true)
        {
            if (!_pendingMove.HasValue)
                return false;

            var p = _pendingMove.Value;
            bool isStayingInPlace = p.ToCell == p.FromCell;
            if (!isStayingInPlace && !CanOccupyCell(p.ToCell))
            {
                CancelPendingMove();
                return false;
            }

            MovementPoints = consumeAllRemainingMovement
                ? 0f
                : Mathf.Max(0f, p.MovementPointsBefore - p.MovementCost);

            UnregisterCellOccupancyList(p.FromCell);
            RefreshCellOccupancy(p.FromCell);

            Cell = p.ToCell;
            RegisterCellOccupancyList(p.ToCell);

            RefreshCellOccupancy(p.ToCell);
            cachedPaths = null;
            FindSceneCellGrid()?.RefreshSceneCellOccupancyNow();

            OnMoveFinished();
            FindSceneCellGrid()?.RequestBattleOutcomeEvaluation();

            _pendingMove = null;
            return true;
        }

        public virtual bool CancelPendingMove()
        {
            if (!_pendingMove.HasValue)
                return false;

            var p = _pendingMove.Value;
            _previewMoveVersion++;

            MovementPoints = p.MovementPointsBefore;

            // Snap back visually (local space)
            transform.localPosition = p.FromLocalPos;

            _pendingMove = null;
            PreviewMoveCameraFollowReleased?.Invoke();
            return true;
        }

        public virtual bool BeginPendingMoveInPlace()
        {
            CancelPendingMove();

            if (Cell == null)
            {
                return false;
            }

            _previewMoveVersion++;
            _pendingMove = new PendingMove
            {
                FromCell = Cell,
                ToCell = Cell,
                Path = new List<Cell>() { Cell },
                MovementPointsBefore = MovementPoints,
                MovementCost = 0f,
                FromLocalPos = transform.localPosition
            };

            return true;
        }

        protected virtual IEnumerator PreviewMovementAnimation(IList<Cell> path, int previewMoveVersion)
        {
            // Phase 5: the scene Unit owns its preview animation.
            var isMap2D = IsSceneGrid2D();
            for (int i = path.Count - 1; i >= 0; i--)
            {
                if (previewMoveVersion != _previewMoveVersion || !_pendingMove.HasValue)
                {
                    yield break;
                }

                var currentCell = path[i];
                Vector3 destination_pos = isMap2D
                    ? new Vector3(currentCell.transform.localPosition.x, currentCell.transform.localPosition.y, transform.localPosition.z)
                    : new Vector3(currentCell.transform.localPosition.x, currentCell.transform.localPosition.y, currentCell.transform.localPosition.z);

                while (transform.localPosition != destination_pos)
                {
                    if (previewMoveVersion != _previewMoveVersion || !_pendingMove.HasValue)
                    {
                        PreviewMoveCameraFollowReleased?.Invoke();
                        yield break;
                    }

                    transform.localPosition = Vector3.MoveTowards(transform.localPosition, destination_pos, Time.deltaTime * MovementAnimationSpeed);
                    PreviewMoveCameraFollowRequested?.Invoke(transform.position);
                    yield return null;
                }
            }
        }

        private IEnumerator AnimateMovementPath(IList<Cell> path)
        {
            var isMap2D = IsSceneGrid2D();
            for (int i = path.Count - 1; i >= 0; i--)
            {
                var currentCell = path[i];
                Vector3 destinationPos = isMap2D
                    ? new Vector3(currentCell.transform.localPosition.x, currentCell.transform.localPosition.y, transform.localPosition.z)
                    : currentCell.transform.localPosition;

                while ((transform.localPosition - destinationPos).sqrMagnitude > 0.0001f)
                {
                    transform.localPosition = Vector3.MoveTowards(transform.localPosition, destinationPos, Time.deltaTime * MovementAnimationSpeed);
                    yield return null;
                }

                transform.localPosition = destinationPos;
            }
        }

        private void SnapToCellLocalPosition(Cell destinationCell)
        {
            Windy.Srpg.Game.Grid.CellGrid cellGrid = FindSceneCellGrid();
            bool isMap2D = cellGrid != null && cellGrid.Is2D;
            Vector3 destinationPos = isMap2D
                ? new Vector3(destinationCell.transform.localPosition.x, destinationCell.transform.localPosition.y, transform.localPosition.z)
                : destinationCell.transform.localPosition;
            transform.localPosition = destinationPos;
        }

        private Cell ResolveTransformStartCell(Windy.Srpg.Game.Grid.CellGrid cellGrid, Cell fallbackCell)
        {
            List<Cell> allCells = cellGrid?.GetAllCells()
                ?? new List<Cell>();
            if (allCells.Count == 0)
            {
                return fallbackCell;
            }

            Vector3 currentPosition = transform.position;
            Cell closestCell = fallbackCell;
            float closestDistanceSqr = fallbackCell != null
                ? (fallbackCell.transform.position - currentPosition).sqrMagnitude
                : float.MaxValue;

            foreach (Cell candidate in allCells)
            {
                if (candidate == null)
                {
                    continue;
                }

                float distanceSqr = (candidate.transform.position - currentPosition).sqrMagnitude;
                if (distanceSqr < closestDistanceSqr)
                {
                    closestDistanceSqr = distanceSqr;
                    closestCell = candidate;
                }
            }

            const float maxSnapDistanceSqr = 0.16f;
            return closestDistanceSqr <= maxSnapDistanceSqr ? closestCell : fallbackCell;
        }

        protected IEnumerator MovementAnimation(IList<Cell> path)
        {
            yield return AnimateMovementPath(path);
            OnMoveFinished();
        }
        protected void OnMoveFinished() {
        
        }

        public bool IsCellMovableTo(Cell cell)
        {
            return CanOccupyCell(cell);
        }

        public bool IsCellTraversable(Cell cell)
        {
            return CanTraverseCell(cell);
        }

        public HashSet<Cell> GetAvailableDestinations(List<Cell> cells)
        {
            // Phase 5: scene Unit owns pathfinding. The runtime mirror is push-only.
            return ComputeAvailableDestinationsSceneOnly(cells);
        }

        internal HashSet<Cell> ComputeAvailableDestinationsSceneOnly(List<Cell> cells)
        {
            CachePathsSceneOnly(cells);

            Cell originCell = ResolvePathfindingCell(cells ?? new List<Cell>(), Cell);
            HashSet<Cell> reachableCells = new HashSet<Cell>();
            foreach (Cell candidate in cells ?? new List<Cell>())
            {
                if (candidate == null
                    || candidate == originCell
                    || (originCell != null && candidate.Coordinates == originCell.Coordinates))
                {
                    continue;
                }

                if (!IsCellMovableTo(candidate))
                {
                    continue;
                }

                if (!TryGetCachedPath(candidate, out IList<Cell> route) || route == null)
                {
                    continue;
                }

                if (route.Count < 1)
                {
                    continue;
                }

                float totalMovementCost = SumPathMovementCost(route);
                if (totalMovementCost > MovementPoints)
                {
                    continue;
                }

                reachableCells.Add(candidate);
            }

            return reachableCells;
        }

        public void CachePaths(List<Cell> cells)
        {
            CachePathsSceneOnly(cells);
        }

        internal void CachePathsSceneOnly(List<Cell> cells)
        {
            cachedPaths = BuildScenePaths(cells);
        }

        public IList<Cell> FindPath(List<Cell> cells, Cell destination)
        {
            return ComputeFindPathSceneOnly(cells, destination);
        }

        internal IList<Cell> ComputeFindPathSceneOnly(List<Cell> cells, Cell destination)
        {
            if (destination == null)
            {
                return new List<Cell>();
            }

            if (cachedPaths == null)
            {
                CachePathsSceneOnly(cells);
            }

            if (TryGetCachedPath(destination, out IList<Cell> path))
            {
                return path;
            }

            return new List<Cell>();
        }

        private bool TryGetCachedPath(Cell destination, out IList<Cell> path)
        {
            path = null;
            if (cachedPaths == null || destination == null)
            {
                return false;
            }

            if (cachedPaths.TryGetValue(destination, out path))
            {
                return path != null;
            }

            foreach (KeyValuePair<Cell, IList<Cell>> entry in cachedPaths)
            {
                if (entry.Key != null && entry.Key.Coordinates == destination.Coordinates)
                {
                    path = entry.Value;
                    return path != null;
                }
            }

            return false;
        }

        private Dictionary<Cell, IList<Cell>> BuildScenePaths(List<Cell> cells)
        {
            Cell originCell = ResolvePathfindingCell(cells, Cell);
            if (cells == null || originCell == null)
            {
                return new Dictionary<Cell, IList<Cell>>();
            }

            Dictionary<Cell, Dictionary<Cell, float>> edges = GetSceneGraphEdges(cells);
            if (!edges.ContainsKey(originCell))
            {
                return new Dictionary<Cell, IList<Cell>>();
            }

            return ScenePathfinder.FindAllPaths(edges, originCell);
        }

        private static Cell ResolvePathfindingCell(List<Cell> cells, Cell preferredCell)
        {
            if (preferredCell == null)
            {
                return null;
            }

            if (cells != null)
            {
                foreach (Cell candidate in cells)
                {
                    if (candidate != null && candidate.Coordinates == preferredCell.Coordinates)
                    {
                        return candidate;
                    }
                }
            }

            return preferredCell;
        }

        private static float SumPathMovementCost(IList<Cell> path)
        {
            if (path == null || path.Count == 0)
            {
                return 0f;
            }

            // Legacy path convention excludes the origin cell, so every entry is a movement step.
            float total = 0f;
            for (int i = 0; i < path.Count; i++)
            {
                Cell step = path[i];
                if (step != null)
                {
                    total += step.MovementCost;
                }
            }

            return total;
        }

        private bool CanOccupyCell(Cell cell)
        {
            if (cell == null || cell == Cell)
            {
                return false;
            }

            return cell.IsTraversable && !HasBlockingOccupant(cell);
        }

        private bool CanTraverseCell(Cell cell)
        {
            if (cell == null)
            {
                return false;
            }

            if (cell == Cell)
            {
                return true;
            }

            return cell.IsTraversable && !HasBlockingOccupant(cell);
        }

        private bool HasBlockingOccupant(Cell cell)
        {
            if (cell?.CurrentUnits == null)
            {
                return false;
            }

            foreach (Unit occupant in cell.CurrentUnits)
            {
                if (occupant == null || occupant == this)
                {
                    continue;
                }

                if (!occupant.Obstructable || occupant.ExcludedFromBattle)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private Dictionary<Cell, Dictionary<Cell, float>> GetSceneGraphEdges(List<Cell> cells)
        {
            Cell originCell = ResolvePathfindingCell(cells, Cell);
            Dictionary<Cell, Dictionary<Cell, float>> edgeLookup = new Dictionary<Cell, Dictionary<Cell, float>>();
            if (cells == null)
            {
                return edgeLookup;
            }

            foreach (Cell cell in cells)
            {
                if (cell == null)
                {
                    continue;
                }

                if (!IsCellTraversable(cell) && cell != originCell)
                {
                    continue;
                }

                Dictionary<Cell, float> neighbours = new Dictionary<Cell, float>();
                foreach (Cell adjacentCell in cell.GetNeighbours(cells))
                {
                    if (adjacentCell == null)
                    {
                        continue;
                    }

                    if (IsCellTraversable(adjacentCell) || IsCellMovableTo(adjacentCell))
                    {
                        neighbours[adjacentCell] = Mathf.Max(0f, adjacentCell.TraversalCost);
                    }
                }

                edgeLookup[cell] = neighbours;
            }

            return edgeLookup;
        }

        protected Dictionary<Cell, Dictionary<Cell, float>> GetGraphEdges(List<Cell> cells)
        {
            return GetSceneGraphEdges(cells);
        }

        #endregion
    }

    public struct ResolvedAttackProfile
    {
        public int Damage;
        public int Accuracy;
        public int Crit;
        public int NumHits;
        public bool IsMagic;
        public bool CanPursuitAttack;
        public bool PreventsCounterattack;
        public bool EndsTurn;
    }

    public enum DamageChangePhase
    {
        Outcome,
        Damage
    }

    public class DamageChangeContext
    {
        public Unit Attacker;
        public Unit Defender;
        public int Damage;
        public bool IsHit;
        public bool IsMagicAttack;
        public bool IsCrit;
        public bool IsCounterAttack;
        public bool IsSimulated;
        public DamageChangePhase Phase;
    }

    // Canonical combat hook interfaces. These are intended to be searched and implemented directly.
    public interface IP_DamageChange
    {
        void DamageChange(DamageChangeContext context);
    }

    public interface IP_TakeDamageChange
    {
        void TakeDamageChange(DamageChangeContext context);
    }

    public interface IP_DamageMultiplier
    {
        void DamageMultiplier(DamageChangeContext context);
    }

    public interface IP_TakeDamageMultiplier
    {
        void TakeDamageMultiplier(DamageChangeContext context);
    }

    public class CombatSequenceContext
    {
        public Unit Attacker;
        public Unit Defender;

        public CombatSequenceContext(Unit attacker, Unit defender)
        {
            Attacker = attacker;
            Defender = defender;
        }
    }

    public interface IP_AfterCombat_Attacker
    {
        void AfterCombatSequenceAsAttacker(CombatSequenceContext context);
    }

    public interface IP_BeforeCombat_Attacker
    {
        void BeforeCombatSequenceAsAttacker(CombatSequenceContext context);
    }

    public interface IP_AfterCombat_Defender
    {
        void AfterCombatSequenceAsDefender(CombatSequenceContext context);
    }

    public interface IP_BeforeCombat_Defender
    {
        void BeforeCombatSequenceAsDefender(CombatSequenceContext context);
    }
}
