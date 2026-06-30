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
using Windy.Srpg.Game.Pathfinding;
using UnityEngine.EventSystems;
using RuntimeBuff = Windy.Srpg.Game.Buffs.Buff;


namespace Windy.Srpg.Game.Units
{
    public partial class Unit
    {
        public static IEnumerator WaitForAttackSequenceCompletion(Unit unit, float timeoutSeconds = 30f)
        {
            float elapsed = 0f;
            while (ShouldKeepWaitingForCombatCompletion(unit))
            {
                elapsed += Time.unscaledDeltaTime;
                if (elapsed >= timeoutSeconds)
                {
                    yield break;
                }

                yield return null;
            }
        }
        private static bool ShouldKeepWaitingForCombatCompletion(Unit unit)
        {
            if (unit != null && unit.IsAttackSequenceRunning)
            {
                return true;
            }

            return Unit.IsAnyCombatPresentationActive;
        }
        public virtual void EndTurnForUnit()
        {
            SetTurnStateKind(UnitTurnStateKind.Finished);
        }
        public virtual void ResetTurnState()
        {
            MovementPoints = ComputedTotalMovementPoints;
            SetTurnStateKind(UnitTurnStateKind.Normal);
        }
        private void NormalizeProgressionState(bool notifyListeners = false)
        {
            int previousLevel = level;
            int previousExperience = experience;

            level = Mathf.Clamp(level, 1, ExperienceCalculator.MaxLevel);
            experience = Mathf.Max(0, experience);

            if (level >= ExperienceCalculator.MaxLevel)
            {
                experience = 0;
            }
            else if (experience >= ExperienceCalculator.MaxGain)
            {
                int overflowLevels = experience / ExperienceCalculator.MaxGain;
                level = Mathf.Clamp(level + overflowLevels, 1, ExperienceCalculator.MaxLevel);
                experience = level >= ExperienceCalculator.MaxLevel
                    ? 0
                    : experience % ExperienceCalculator.MaxGain;
            }

            if (notifyListeners && (previousLevel != level || previousExperience != experience))
            {
                RaiseProgressionChanged();
            }
        }
        public virtual void Initialize()
        {
            if (pendingOwnedUnitSaveData != null)
            {
                InitializeFromSavedOwnedUnitData();
                return;
            }

            // Deployment changes can temporarily unregister and re-register the same
            // live scene unit. In that case, keep the existing runtime loadout/state
            // instead of rebuilding from preset defaults.
            if (Application.isPlaying && presetAppliedAtRuntime && IsRuntimeInitializedForSaveRefresh())
            {
                return;
            }

            ApplyPresetAtRuntime();
            ApplyDefaultUnitName();
            NormalizeProgressionState();
            EnsureInventory();
            EnsureSkillList();
            EnsureBuffList();
            EnsurePassiveList();
            Inventory.LoadStartingItems(GetInitialInventory());
            SkillList.LoadStartingSkills(GetInitialSkills());
            PassiveList.LoadStartingPassives(GetInitialUniquePassives(), GetInitialEquipPassives());

            SetTurnStateKind(UnitTurnStateKind.Normal, useStateTransition: false);

            ComputedTotalHitPoints = MaxHitPoints;
            ComputedTotalManaPoints = MaxManaPoints;
            ComputedTotalMovementPoints = MovementPoints;
            HitPoints = MaxHitPoints;
            CurrentManaPoints = MaxManaPoints;

            RaiseHealthChanged(HitPoints, HitPoints, null);

            foreach (var action in GetAbilities())
            {
                action.InitializeAction(this);
            }
        }
        public void ConfigureFromOwnedUnitSaveData(OwnedUnitSaveData saveData, UnitPreset visualPreset)
        {
            pendingOwnedUnitSaveData = saveData;
            pendingOwnedUnitVisualPreset = visualPreset;
            presetAppliedAtRuntime = false;
            useResolvedPresetLoadout = false;
            resolvedSecondaryStatOffsets = default;

            if (Application.isPlaying && IsRuntimeInitializedForSaveRefresh())
            {
                InitializeFromSavedOwnedUnitData();
            }
        }
        private void InitializeFromSavedOwnedUnitData()
        {
            OwnedUnitSaveData saveData = pendingOwnedUnitSaveData;
            UnitPreset visualPreset = pendingOwnedUnitVisualPreset;
            pendingOwnedUnitSaveData = null;
            pendingOwnedUnitVisualPreset = null;

            if (saveData == null)
            {
                return;
            }

            ApplySavedOwnedUnitData(saveData, visualPreset, registerAbilities: !IsRuntimeInitializedForSaveRefresh());
        }
        private bool IsRuntimeInitializedForSaveRefresh()
        {
            return hasInitializedTurnState
                && Inventory != null
                && SkillList != null
                && BuffList != null
                && PassiveList != null;
        }
        private void ApplySavedOwnedUnitData(OwnedUnitSaveData saveData, UnitPreset visualPreset, bool registerAbilities)
        {
            presetAppliedAtRuntime = true;
            useResolvedPresetLoadout = false;
            resolvedSecondaryStatOffsets = default;

            ApplySavedIdentityAndBaseStats(saveData);
            ApplySavedGrowthRates(saveData);
            ApplyPresetSprite(visualPreset);
            ApplyDefaultUnitName();
            NormalizeProgressionState();

            EnsureInventory();
            EnsureSkillList();
            EnsureBuffList();
            EnsurePassiveList();
            BuffList.Clear();

            Inventory.LoadExactItems(Unit.CreateSavedInventoryItems(saveData));
            SkillList.LoadStartingSkills(Unit.CreateSavedSkillEntries(saveData.SkillIds));
            PassiveList.LoadStartingPassives(
                Unit.CreateSavedPassiveEntries(saveData.UniquePassiveIds),
                Unit.CreateSavedPassiveEntries(saveData.EquipPassiveIds));

            SetTurnStateKind(UnitTurnStateKind.Normal, useStateTransition: false);

            ComputedTotalHitPoints = MaxHitPoints;
            ComputedTotalManaPoints = MaxManaPoints;
            ComputedTotalMovementPoints = Mathf.Max(0f, MovementPoints);
            MovementPoints = ComputedTotalMovementPoints;
            EnsureSaveIdentity(visualPreset);

            int previousHitPoints = HitPoints;
            HitPoints = Mathf.Clamp(Mathf.Max(1, saveData.CurrentHitPoints), 1, ComputedTotalHitPoints);
            CurrentManaPoints = Mathf.Clamp(saveData.CurrentManaPoints, 0, ComputedTotalManaPoints);
            RaiseHealthChanged(previousHitPoints, HitPoints, null);
            RaiseStatsChanged();
            RaiseProgressionChanged();
            RaiseBuffsChanged();

            if (!registerAbilities)
            {
                return;
            }

            foreach (var action in GetAbilities())
            {
                action.InitializeAction(this);
            }
        }
        public void OnMouseDown()
        {
            if (GameplayInputController.IsCentralizedSceneInputActive)
            {
                return;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            RaiseUnitClicked();
            UnitClicked?.Invoke(this, EventArgs.Empty);
        }
        public void OnMouseEnter()
        {
            if (GameplayInputController.IsCentralizedSceneInputActive)
            {
                return;
            }

            RaiseUnitHighlighted();
            UnitHighlighted?.Invoke(this, EventArgs.Empty);
        }
        public void OnMouseExit()
        {
            if (GameplayInputController.IsCentralizedSceneInputActive)
            {
                return;
            }

            RaiseUnitDehighlighted();
            UnitDehighlighted?.Invoke(this, EventArgs.Empty);
        }
        internal void RaiseSceneHighlightEvent()
        {
            RaiseUnitHighlighted();
            UnitHighlighted?.Invoke(this, EventArgs.Empty);
        }
        internal void RaiseSceneDehighlightEvent()
        {
            RaiseUnitDehighlighted();
            UnitDehighlighted?.Invoke(this, EventArgs.Empty);
        }
        public void OnTurnStart()
        {
            cachedPaths = null;

            PassiveList?.OnTurnStart();
            BuffList?.OnTurnStart();
            RefreshHealthState();
            SkillList?.ResetTurnUsage();
            RaiseBuffsChanged();
            SetTurnStateKind(UnitTurnStateKind.Friendly);
        }
        private void OnValidate()
        {
            ApplyPresetInEditor();
            ApplyDefaultUnitName();
            NormalizeProgressionState();
            EnsureSaveIdentity();
        }
        private void ApplyDefaultUnitName()
        {
            if (string.IsNullOrWhiteSpace(unitName) || unitName == "Ally" || unitName == "Enemy")
            {
                unitName = PlayerNumber == 0 ? "Ally" : "Enemy";
            }
        }
        private void SetTurnStateKind(UnitTurnStateKind stateKind, bool useStateTransition = true)
        {
            currentTurnStateKind = stateKind;
            hasInitializedTurnState = true;
            ApplyTurnStateVisual(stateKind);
        }
        private void ApplyTurnStateVisual(UnitTurnStateKind stateKind)
        {
            switch (stateKind)
            {
                case UnitTurnStateKind.Selected:
                    MarkAsSelected();
                    break;
                case UnitTurnStateKind.ReachableEnemy:
                    MarkAsReachableEnemy();
                    break;
                case UnitTurnStateKind.Friendly:
                    MarkAsFriendly();
                    break;
                case UnitTurnStateKind.Finished:
                    MarkAsFinished();
                    break;
                default:
                    UnMark();
                    break;
            }
        }
        public RuntimeBuff AddBuff(BuffData data)
        {
            EnsureBuffList();
            var entry = BuffList.AddBuff(data);
            if (entry != null)
            {
                RefreshHealthState();
                RaiseBuffsChanged();
            }

            return entry;
        }
        public RuntimeBuff BuffAdd(BuffData template, int? durationOverride = null, string idSuffix = null)
        {
            if (template == null)
            {
                return null;
            }

            BuffData instance = BuffRegistry.CreateRuntimeInstance(template, durationOverride, idSuffix);
            return AddBuff(instance);
        }
        public RuntimeBuff AddBuffById(string buffId)
        {
            EnsureBuffList();
            var entry = BuffList.AddBuffById(buffId);
            if (entry != null)
            {
                RefreshHealthState();
                RaiseBuffsChanged();
            }

            return entry;
        }
        public RuntimeBuff BuffAdd(string buffId, int? durationOverride = null, string idSuffix = null)
        {
            EnsureBuffList();
            if (!BuffRegistry.TryGet(buffId, out BuffData template))
            {
                Debug.LogWarning($"Unit: Buff id '{buffId}' is not registered.");
                return null;
            }

            return BuffAdd(template, durationOverride, idSuffix);
        }
        public bool RemoveBuff(RuntimeBuff entry)
        {
            EnsureBuffList();
            bool removed = BuffList.RemoveBuff(entry);
            if (removed)
            {
                RefreshHealthState();
                RaiseBuffsChanged();
            }

            return removed;
        }
        public Item AddInventoryItem(ItemData data, int? remainingChargesOverride = null)
        {
            EnsureInventory();
            return Inventory.AddItem(data, remainingChargesOverride);
        }
        public Skill AddSkill(SkillData data)
        {
            EnsureSkillList();
            return SkillList.AddSkill(data);
        }
        public Skill AddSkillById(string skillId)
        {
            EnsureSkillList();
            return SkillList.AddSkillById(skillId);
        }
        public bool RemoveSkill(Skill entry)
        {
            EnsureSkillList();
            return SkillList.RemoveSkill(entry);
        }
        public Passive AddPassive(PassiveData data)
        {
            EnsurePassiveList();
            return PassiveList.AddPassive(data);
        }
        public Passive AddEquipPassive(PassiveData data)
        {
            EnsurePassiveList();
            return PassiveList.AddEquipPassive(data);
        }
        public Passive AddPassiveById(string passiveId)
        {
            EnsurePassiveList();
            return PassiveList.AddPassiveById(passiveId);
        }
        public Passive AddEquipPassiveById(string passiveId)
        {
            EnsurePassiveList();
            return PassiveList.AddEquipPassiveById(passiveId);
        }
        public bool RemovePassive(Passive entry)
        {
            EnsurePassiveList();
            return PassiveList.RemovePassive(entry);
        }
        public List<Ability> GetAbilities()
        {
            return GetComponentsInChildren<Ability>()
                .Where(action => action != null)
                .ToList();
        }
        public bool CanUseSkill(Skill entry)
        {
            EnsureSkillList();
            return SkillList.CanUse(entry);
        }
        public bool MarkSkillUsed(Skill entry)
        {
            EnsureSkillList();
            return SkillList.MarkUsed(entry);
        }
        public void RestoreManaPoints(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            int previousManaPoints = CurrentManaPoints;
            CurrentManaPoints = Mathf.Clamp(CurrentManaPoints + amount, 0, ComputedTotalManaPoints);
            if (CurrentManaPoints != previousManaPoints)
            {
                RaiseStatsChanged();
            }
        }
        public void SetCurrentManaPoints(int value)
        {
            int clampedValue = Mathf.Clamp(value, 0, ComputedTotalManaPoints);
            if (clampedValue == CurrentManaPoints)
            {
                return;
            }

            CurrentManaPoints = clampedValue;
            RaiseStatsChanged();
        }
        public void SetLevel(int value)
        {
            int previousLevel = level;
            int previousExperience = experience;

            level = Mathf.Clamp(value, 1, ExperienceCalculator.MaxLevel);
            if (level >= ExperienceCalculator.MaxLevel)
            {
                experience = 0;
            }

            if (previousLevel != level || previousExperience != experience)
            {
                RaiseProgressionChanged();
            }
        }
        public void SetExperience(int value)
        {
            int previousLevel = level;
            int previousExperience = experience;

            experience = Mathf.Max(0, value);
            NormalizeProgressionState();

            if (previousLevel != level || previousExperience != experience)
            {
                RaiseProgressionChanged();
            }
        }
        public OwnedUnitSaveData CaptureOwnedUnitSaveData()
        {
            EnsureSaveIdentity();
            string[] weaponProficiencyIds = GetWeaponProficiencyIds().ToArray();
            SavedInventoryEntryData[] inventoryEntries = Inventory?.Entries?
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.ItemId))
                .Select(entry => new SavedInventoryEntryData
                {
                    ItemId = entry.ItemId,
                    RemainingCharges = entry.RemainingCharges
                })
                .ToArray()
                ?? Array.Empty<SavedInventoryEntryData>();

            string[] skillIds = SkillList?.LearnedEntries?
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.SkillId))
                .Select(entry => entry.SkillId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
                ?? Array.Empty<string>();

            string[] uniquePassiveIds = PassiveList?.UniqueEntries?
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.PassiveId))
                .Select(entry => entry.PassiveId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
                ?? Array.Empty<string>();

            string[] equipPassiveIds = PassiveList?.EquippedEntries?
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.PassiveId))
                .Select(entry => entry.PassiveId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
                ?? Array.Empty<string>();

            return new OwnedUnitSaveData
            {
                UnitId = unitId ?? string.Empty,
                VisualId = visualId ?? string.Empty,
                UnitName = unitName ?? string.Empty,
                Level = Level,
                Experience = Experience,
                WeaponProficiencyIds = weaponProficiencyIds,
                BaseStats = new UnitStatBlock
                {
                    HitPoints = BaseHitPoints,
                    ManaPoints = BaseManaPoints,
                    MovementPoints = ComputedTotalMovementPoints > 0f ? ComputedTotalMovementPoints : MovementPoints,
                    Strength = BaseStrength,
                    Defense = BaseDefense,
                    Magic = BaseMagic,
                    Resistance = BaseResistance,
                    Speed = BaseSpeed,
                    Luck = BaseLuck
                },
                GrowthRates = new UnitGrowthRates
                {
                    Strength = growthStrength,
                    Magic = growthMagic,
                    Defense = growthDefense,
                    Resistance = growthResistance,
                    Speed = growthSpeed,
                    Luck = growthLuck
                },
                CurrentHitPoints = Mathf.Max(0, HitPoints),
                CurrentManaPoints = Mathf.Max(0, CurrentManaPoints),
                Inventory = inventoryEntries,
                SkillIds = skillIds,
                UniquePassiveIds = uniquePassiveIds,
                EquipPassiveIds = equipPassiveIds
            };
        }
        private void ApplySavedIdentityAndBaseStats(OwnedUnitSaveData saveData)
        {
            unitId = saveData.UnitId ?? string.Empty;
            visualId = saveData.VisualId ?? string.Empty;
            unitName = saveData.UnitName ?? string.Empty;
            level = Mathf.Max(1, saveData.Level);
            experience = Mathf.Max(0, saveData.Experience);
            weaponProficiencies = GetWeaponProficienciesFromIds(saveData.WeaponProficiencyIds);
            MovementPoints = Mathf.Max(0f, saveData.BaseStats.MovementPoints);
            baseHitPoints = Mathf.Max(1, saveData.BaseStats.HitPoints);
            baseManaPoints = Mathf.Max(0, saveData.BaseStats.ManaPoints);
            baseStrength = saveData.BaseStats.Strength;
            baseDefense = saveData.BaseStats.Defense;
            baseMagic = saveData.BaseStats.Magic;
            baseResistance = saveData.BaseStats.Resistance;
            baseSpeed = saveData.BaseStats.Speed;
            baseLuck = saveData.BaseStats.Luck;
        }
        private void ApplySavedGrowthRates(OwnedUnitSaveData saveData)
        {
            growthStrength = Mathf.Max(0, saveData.GrowthRates.Strength);
            growthMagic = Mathf.Max(0, saveData.GrowthRates.Magic);
            growthDefense = Mathf.Max(0, saveData.GrowthRates.Defense);
            growthResistance = Mathf.Max(0, saveData.GrowthRates.Resistance);
            growthSpeed = Mathf.Max(0, saveData.GrowthRates.Speed);
            growthLuck = Mathf.Max(0, saveData.GrowthRates.Luck);
        }
        private void EnsureSaveIdentity(UnitPreset sourcePreset = null)
        {
            if (string.IsNullOrWhiteSpace(visualId))
            {
                visualId = sourcePreset?.PresetId ?? preset?.PresetId ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(unitId))
            {
                unitId = !string.IsNullOrWhiteSpace(visualId)
                    ? visualId
                    : BuildFallbackUnitId();
            }
        }
        private void ApplyPresetAtRuntime()
        {
            if (presetAppliedAtRuntime || preset == null)
            {
                return;
            }

            presetAppliedAtRuntime = true;
            useResolvedPresetLoadout = true;
            resolvedSecondaryStatOffsets = presetOverride?.SecondaryStatOffsets ?? default;

            ApplyPresetIdentityAndBaseStats(preset);
            ApplyPresetGrowthRates(preset);
            ApplyPresetSprite(preset);
            ApplyPresetLevel(preset, presetOverride);
            ApplyPresetPrimaryStatOffsets(presetOverride?.StatOffsets ?? default);
            ResolvePresetLoadout(preset, presetOverride);
            EnsureSaveIdentity(preset);
        }
        private void ApplyPresetInEditor()
        {
            if (Application.isPlaying || preset == null)
            {
                return;
            }

            presetAppliedAtRuntime = false;
            useResolvedPresetLoadout = false;
            resolvedSecondaryStatOffsets = presetOverride?.SecondaryStatOffsets ?? default;

            ApplyPresetIdentityAndBaseStats(preset);
            ApplyPresetGrowthRates(preset);
            ApplyPresetSprite(preset);
            ApplyPresetLevel(preset, presetOverride);
            ApplyPresetPrimaryStatOffsets(presetOverride?.StatOffsets ?? default);
            ResolvePresetLoadout(preset, presetOverride);
            EnsureSaveIdentity(preset);
        }
        internal void RefreshPresetFromAssetInEditor(UnitPreset preset)
        {
            if (preset == null || preset != this.preset)
            {
                return;
            }

            ApplyPresetInEditor();
        }
        private void ApplyPresetIdentityAndBaseStats(UnitPreset preset)
        {
            if (preset == null)
            {
                return;
            }

            string resolvedUnitName = preset.UnitName;
            if (presetOverride != null
                && presetOverride.OverrideUnitName
                && !string.IsNullOrWhiteSpace(presetOverride.UnitNameOverride))
            {
                resolvedUnitName = presetOverride.UnitNameOverride;
            }

            if (!string.IsNullOrWhiteSpace(resolvedUnitName))
            {
                unitName = resolvedUnitName;
            }

            weaponProficiencies = preset.WeaponProficiencies;
            float presetMovementPoints = preset.BaseStats.MovementPoints > 0f
                ? preset.BaseStats.MovementPoints
                : preset.LegacyBaseMovementPoints;
            MovementPoints = Mathf.Max(0f, presetMovementPoints);

            if (presetOverride != null && presetOverride.OverrideMovementPoints)
            {
                MovementPoints = Mathf.Max(0f, presetOverride.FinalMovementPoints);
            }

            baseHitPoints = Mathf.Max(1, preset.BaseStats.HitPoints);
            baseManaPoints = Mathf.Max(0, preset.BaseStats.ManaPoints);
            baseStrength = preset.BaseStats.Strength;
            baseDefense = preset.BaseStats.Defense;
            baseMagic = preset.BaseStats.Magic;
            baseResistance = preset.BaseStats.Resistance;
            baseSpeed = preset.BaseStats.Speed;
            baseLuck = preset.BaseStats.Luck;
        }
        private void ApplyPresetGrowthRates(UnitPreset preset)
        {
            if (preset == null)
            {
                return;
            }

            growthStrength = Mathf.Max(0, preset.GrowthRates.Strength);
            growthMagic = Mathf.Max(0, preset.GrowthRates.Magic);
            growthDefense = Mathf.Max(0, preset.GrowthRates.Defense);
            growthResistance = Mathf.Max(0, preset.GrowthRates.Resistance);
            growthSpeed = Mathf.Max(0, preset.GrowthRates.Speed);
            growthLuck = Mathf.Max(0, preset.GrowthRates.Luck);
        }
        private void ApplyPresetSprite(UnitPreset preset)
        {
            SpriteRenderer spriteRenderer = ResolveUnitSpriteRenderer();
            if (spriteRenderer == null)
            {
                return;
            }

            CaptureSpriteLayoutBaseline(spriteRenderer);
            spriteRenderer.sprite = preset != null ? preset.UnitSprite : null;

            if (preset?.UnitSprite != null)
            {
                ApplyPresetSpriteLayout(preset, spriteRenderer);
                return;
            }

            RestoreSpriteLayoutBaseline(spriteRenderer);
        }
        private void CaptureSpriteLayoutBaseline(SpriteRenderer spriteRenderer)
        {
            if (spriteRenderer == null || spriteLayoutBaselineCaptured)
            {
                return;
            }

            spriteLayoutBaselineLocalScale = spriteRenderer.transform.localScale;
            spriteLayoutBaselineLocalPosition = spriteRenderer.transform.localPosition;
            spriteLayoutBaselineCaptured = true;
        }
        private void ApplyPresetSpriteLayout(UnitPreset preset, SpriteRenderer spriteRenderer)
        {
            if (preset == null || spriteRenderer?.sprite == null)
            {
                return;
            }

            CaptureSpriteLayoutBaseline(spriteRenderer);

            UnitSpriteLayoutSettings layout = preset.SpriteLayout;
            Vector3 baseScale = spriteLayoutBaselineLocalScale == Vector3.zero ? Vector3.one : spriteLayoutBaselineLocalScale;
            Vector3 basePosition = spriteLayoutBaselineLocalPosition;
            float scaleFactor = ResolvePresetSpriteScaleFactor(layout.ResolvedTargetSize, spriteRenderer.sprite, baseScale);
            Vector3 resolvedScale = new Vector3(baseScale.x * scaleFactor, baseScale.y * scaleFactor, baseScale.z);

            spriteRenderer.transform.localScale = resolvedScale;
            spriteRenderer.transform.localPosition = basePosition + new Vector3(layout.OffsetX, layout.OffsetY, 0f);
        }
        private void RestoreSpriteLayoutBaseline(SpriteRenderer spriteRenderer)
        {
            if (spriteRenderer == null)
            {
                return;
            }

            CaptureSpriteLayoutBaseline(spriteRenderer);
            spriteRenderer.transform.localScale = spriteLayoutBaselineLocalScale == Vector3.zero
                ? Vector3.one
                : spriteLayoutBaselineLocalScale;
            spriteRenderer.transform.localPosition = spriteLayoutBaselineLocalPosition;
        }
        private void ResetSpriteLayoutBaseline()
        {
            spriteLayoutBaselineCaptured = false;
            spriteLayoutBaselineLocalScale = Vector3.one;
            spriteLayoutBaselineLocalPosition = new Vector3(0f, 0f, -0.1f);
        }
        private void OnTransformChildrenChanged()
        {
            if (!Application.isPlaying)
            {
                ResetSpriteLayoutBaseline();
            }
        }
        private void ApplyPresetLevel(UnitPreset preset, UnitPresetOverride presetOverride)
        {
            if (preset == null)
            {
                return;
            }

            int presetLevel = Mathf.Clamp(preset.BaseLevel, 1, ExperienceCalculator.MaxLevel);
            int finalLevel = presetLevel;

            if (presetOverride != null && presetOverride.OverrideLevel)
            {
                finalLevel = Mathf.Clamp(presetOverride.FinalLevel, presetLevel, ExperienceCalculator.MaxLevel);
            }

            level = presetLevel;
            experience = 0;

            if (finalLevel <= presetLevel)
            {
                return;
            }

            IReadOnlyList<int> normalizedGrowthRates = LevelUpGainCalculator.NormalizeGrowthRates(new[]
            {
                growthStrength,
                growthMagic,
                growthDefense,
                growthResistance,
                growthSpeed,
                growthLuck
            });

            for (int currentLevel = presetLevel; currentLevel < finalLevel; currentLevel++)
            {
                LevelUpGainStep step = LevelUpGainCalculator.BuildStep(normalizedGrowthRates, currentLevel);
                foreach (var pair in step.AutoGains)
                {
                    ApplyBaseStatIncreaseInternal(pair.Key, pair.Value);
                }

                level = step.ToLevel;
            }
        }
        private void ApplyPresetPrimaryStatOffsets(UnitStatBlock offsets)
        {
            baseHitPoints = Mathf.Max(1, baseHitPoints + offsets.HitPoints);
            baseManaPoints = Mathf.Max(0, baseManaPoints + offsets.ManaPoints);
            baseStrength += offsets.Strength;
            baseDefense += offsets.Defense;
            baseMagic += offsets.Magic;
            baseResistance += offsets.Resistance;
            baseSpeed += offsets.Speed;
            baseLuck += offsets.Luck;
        }
        private void ResolvePresetLoadout(UnitPreset preset, UnitPresetOverride presetOverride)
        {
            resolvedStartingInventory = new List<StartingInventoryItem>(
                preset != null ? preset.StartingInventory : Enumerable.Empty<StartingInventoryItem>());
            resolvedStartingSkills = new List<StartingSkillEntry>(
                preset != null ? preset.StartingSkills : Enumerable.Empty<StartingSkillEntry>());
            resolvedStartingUniquePassives = new List<StartingPassiveEntry>(
                preset != null ? preset.StartingUniquePassives : Enumerable.Empty<StartingPassiveEntry>());
            resolvedStartingEquipPassives = new List<StartingPassiveEntry>(
                preset != null ? preset.StartingEquipPassives : Enumerable.Empty<StartingPassiveEntry>());

            if (presetOverride == null)
            {
                return;
            }

            if (presetOverride.ExtraInventory != null)
            {
                resolvedStartingInventory.AddRange(presetOverride.ExtraInventory);
            }

            if (presetOverride.ExtraSkills != null)
            {
                resolvedStartingSkills.AddRange(presetOverride.ExtraSkills);
            }

            if (presetOverride.ExtraUniquePassives != null)
            {
                resolvedStartingUniquePassives.AddRange(presetOverride.ExtraUniquePassives);
            }

            if (presetOverride.ExtraEquipPassives != null)
            {
                resolvedStartingEquipPassives.AddRange(presetOverride.ExtraEquipPassives);
            }
        }
        public void ApplyBaseStatIncrease(LevelableStatKind stat, int amount)
        {
            ApplyBaseStatIncreaseInternal(stat, amount);
            RefreshHealthState();
        }
        public int GrantExperience(int amount)
        {
            if (amount <= 0)
            {
                return 0;
            }

            if (Level >= ExperienceCalculator.MaxLevel)
            {
                if (experience != 0)
                {
                    experience = 0;
                    RaiseProgressionChanged();
                }

                return 0;
            }

            int grantedAmount = Mathf.Clamp(amount, ExperienceCalculator.MinGain, ExperienceCalculator.MaxGain);
            int previousLevel = level;
            int previousExperience = experience;
            int totalExperience = experience + grantedAmount;

            int gainedLevels = totalExperience / ExperienceCalculator.MaxGain;
            level = Mathf.Clamp(level + gainedLevels, 1, ExperienceCalculator.MaxLevel);
            experience = level >= ExperienceCalculator.MaxLevel
                ? 0
                : totalExperience % ExperienceCalculator.MaxGain;

            if (previousLevel != level || previousExperience != experience)
            {
                RaiseProgressionChanged();
            }

            return grantedAmount;
        }
        public bool TrySpendManaPoints(int amount)
        {
            int spendAmount = Mathf.Max(0, amount);
            if (CurrentManaPoints < spendAmount)
            {
                return false;
            }

            if (spendAmount == 0)
            {
                return true;
            }

            CurrentManaPoints -= spendAmount;
            RaiseStatsChanged();
            return true;
        }
        public Item AddInventoryItemById(string itemId, int? remainingChargesOverride = null)
        {
            EnsureInventory();
            return Inventory.AddItemById(itemId, remainingChargesOverride);
        }
        public bool RemoveInventoryItem(Item entry)
        {
            EnsureInventory();
            return Inventory.RemoveItem(entry);
        }
        public bool EquipWeapon(Item entry)
        {
            EnsureInventory();
            return Inventory.EquipWeapon(entry);
        }
        public bool EquipAccessory(Item entry)
        {
            EnsureInventory();
            return Inventory.EquipAccessory(entry);
        }
        public bool UseConsumable(Item entry, Unit target = null)
        {
            EnsureInventory();
            return Inventory.UseConsumable(entry, target);
        }
        public bool CanUseConsumable(Item entry, Unit target = null)
        {
            EnsureInventory();
            return Inventory.CanUseConsumable(entry, target);
        }
        public IEnumerable<IUnitPassive> CreateActiveEquipmentEffects()
        {
            EnsureInventory();
            return Inventory.CreateActiveEquipmentEffects();
        }
        public IEnumerable<Item> GetWeaponInventoryEntries()
        {
            return Inventory?.Entries.Where(entry => entry?.Weapon != null && CanEquipWeapon(entry.Weapon)) ?? Enumerable.Empty<Item>();
        }
        public IEnumerable<Item> GetWeaponsThatCanAttack(Unit target, Cell sourceCell)
        {
            if (target == null || sourceCell == null)
            {
                return Enumerable.Empty<Item>();
            }

            return GetWeaponInventoryEntries()
                .Where(entry => entry?.Weapon != null && CanWeaponAttackTarget(entry.Weapon, target, target.Cell, sourceCell));
        }
        public bool CanAttackTargetWithAnyWeapon(Unit target, Cell sourceCell)
        {
            return GetWeaponsThatCanAttack(target, sourceCell).Any();
        }
        public bool CanPursuitAttackAgainst(Unit other, WeaponData weapon)
        {
            return weapon != null
                && other != null
                && weapon.CanPursuitAttack
                && HitPoints > 0
                && other.HitPoints > 0
                && GetSpeedForWeapon(weapon) >= other.Speed + PursuitAttackSpeedThreshold;
        }
        public bool HasAnyWeaponThatCanAttack(IEnumerable<Unit> potentialTargets, Cell sourceCell)
        {
            if (potentialTargets == null || sourceCell == null)
            {
                return false;
            }

            var targets = potentialTargets.Where(target => target != null).ToList();
            if (targets.Count == 0)
            {
                return false;
            }

            if (HasUsableWeapon && targets.Any(target => IsUnitAttackable(target, target.Cell, sourceCell)))
            {
                return true;
            }

            foreach (var entry in GetWeaponInventoryEntries())
            {
                if (entry?.Weapon == null)
                {
                    continue;
                }

                if (targets.Any(target => CanWeaponAttackTarget(entry.Weapon, target, target.Cell, sourceCell)))
                {
                    return true;
                }
            }

            return false;
        }
        public bool TryEquipWeaponThatCanAttack(IEnumerable<Unit> potentialTargets, Cell sourceCell)
        {
            if (potentialTargets == null || sourceCell == null)
            {
                return false;
            }

            var targets = potentialTargets.Where(target => target != null).ToList();
            if (targets.Count == 0)
            {
                return false;
            }

            if (HasUsableWeapon && targets.Any(target => IsUnitAttackable(target, target.Cell, sourceCell)))
            {
                return true;
            }

            foreach (var entry in GetWeaponInventoryEntries())
            {
                if (entry?.Weapon == null)
                {
                    continue;
                }

                if (!targets.Any(target => CanWeaponAttackTarget(entry.Weapon, target, target.Cell, sourceCell)))
                {
                    continue;
                }

                return EquipWeapon(entry);
            }

            return false;
        }
        public void OnInventoryChanged()
        {
            SkillList?.RefreshEquipmentGrantedSkills();
            RefreshHealthState();
            RaiseStatsChanged();
        }
        public void OnPassivesChanged()
        {
            RefreshHealthState();
            RaiseStatsChanged();
        }
        public void RestoreHitPoints(int amount, Unit source = null)
        {
            if (amount <= 0 || HitPoints <= 0)
            {
                return;
            }

            int previousHitPoints = HitPoints;
            HitPoints = Mathf.Clamp(HitPoints + amount, 0, ComputedTotalHitPoints);
            RaiseHealthChanged(previousHitPoints, HitPoints, source);
        }
        public void SetCurrentHitPoints(int value, Unit source = null)
        {
            int clampedValue = Mathf.Clamp(value, 0, ComputedTotalHitPoints);
            if (clampedValue == HitPoints)
            {
                return;
            }

            int previousHitPoints = HitPoints;
            HitPoints = clampedValue;
            RaiseHealthChanged(previousHitPoints, HitPoints, source);
        }
        public bool CanDisplaceTarget(Unit target, Windy.Srpg.Game.Grid.CellGrid cellGrid, int distance = 1, bool push = true, bool moveUserWithTarget = false)
        {
            Cell actingCell = HasPendingMove ? PreviewCell : Cell;
            Cell targetCell = target != null && target.HasPendingMove ? target.PreviewCell : target?.Cell;
            return UnitDisplacementUtility.CanDisplaceRelative(this, actingCell, target, targetCell, cellGrid, distance, push, moveUserWithTarget);
        }
        public DisplacementResult DisplaceTarget(Unit target, CellGrid cellGrid, int distance = 1, bool push = true, bool moveUserWithTarget = false)
        {
            return UnitDisplacementUtility.TryDisplaceRelative(this, target, cellGrid, distance, push, moveUserWithTarget);
        }
        public void OnTurnEnd()
        {
            if (HasPendingMove && !ConfirmPendingMove())
            {
                CancelPendingMove();
            }

            BuffList?.OnTurnEnd();
            PassiveList?.OnTurnEnd();
            RaiseBuffsChanged();
            ResetTurnState();
        }
        protected void OnDestroyed()
        {
            bool wasRunningAttackSequence = IsAttackSequenceRunning;

            Cell currentCell = Cell;
            UnregisterCellOccupancyList(currentCell);
            RefreshCellOccupancy(currentCell);
            MarkAsDestroyed();
            SuppressDefeatedInteraction();

            if (wasRunningAttackSequence)
            {
                CombatSequenceEnded?.Invoke(this, new CombatSequenceEventArgs(this, this));
                ReleaseCombatCameraFocus();

                var cellGrid = FindSceneCellGrid();
                if (cellGrid != null)
                {
                    if (cellGrid.GameFinished)
                    {
                        cellGrid.SyncStateToGameOver();
                    }
                    else if (cellGrid.IsHumanTurn
                        && cellGrid.CurrentState is Windy.Srpg.Game.Grid.States.CellGridStateBlockInput)
                    {
                        cellGrid.EnterPostCombatGridState();
                    }
                }
            }

            RequestDeferredDestroy();
        }
        private void RequestDeferredDestroy()
        {
            if (pendingDeferredDestroy)
            {
                return;
            }

            pendingDeferredDestroy = true;
            CellGrid cellGrid = FindSceneCellGrid();
            cellGrid?.EnqueueDeferredDestroy(this);
            cellGrid?.TryFlushDeferredDestroyQueue();
        }
        internal void CompleteDeferredDestroy()
        {
            if (!pendingDeferredDestroy)
            {
                return;
            }

            pendingDeferredDestroy = false;
            Destroy(gameObject);
        }
        private void SuppressDefeatedInteraction()
        {
            foreach (var collider in GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }
        }
        private static void BeginCombatPresentation()
        {
            Unit.activeCombatPresentationDepth++;
            if (Unit.activeCombatPresentationDepth == 1)
            {
                FindAnyObjectByType<CellGrid>()?.NotifyCombatPresentationBegan();
            }
        }
        private static void EndCombatPresentation()
        {
            Unit.activeCombatPresentationDepth = Mathf.Max(0, Unit.activeCombatPresentationDepth - 1);
            CellGrid cellGrid = FindAnyObjectByType<CellGrid>();
            cellGrid?.TryFlushDeferredDestroyQueue();
            if (Unit.activeCombatPresentationDepth == 0)
            {
                cellGrid?.NotifyCombatPresentationEnded();
            }
        }
        public void OnUnitSelected()
        {
            if (BelongsToCurrentGameplayPlayer())
            {
                SetTurnStateKind(UnitTurnStateKind.Selected);
            }

            GameplaySelected?.Invoke(this, EventArgs.Empty);
            UnitSelected?.Invoke(this, EventArgs.Empty);
        }
        public void OnUnitDeselected()
        {
            if (BelongsToCurrentGameplayPlayer())
            {
                SetTurnStateKind(IsFinishedForTurn ? UnitTurnStateKind.Finished : UnitTurnStateKind.Friendly);
            }

            GameplayDeselected?.Invoke(this, EventArgs.Empty);
            UnitDeselected?.Invoke(this, EventArgs.Empty);
        }
        private bool BelongsToCurrentGameplayPlayer()
        {
            CellGrid cellGrid = FindAnyObjectByType<CellGrid>();
            return cellGrid != null && cellGrid.GetCurrentPlayerUnits().Contains(this);
        }
        internal void RaiseUnitClicked() => UnitClicked?.Invoke(this, EventArgs.Empty);
        internal void RaiseUnitHighlighted() => UnitHighlighted?.Invoke(this, EventArgs.Empty);
        internal void RaiseUnitDehighlighted() => UnitDehighlighted?.Invoke(this, EventArgs.Empty);
        internal void RaiseUnitDestroyed(AttackEventArgs args) => UnitDestroyed?.Invoke(this, args);
        internal Cell ResolveOccupancyCell(Cell targetCell = null)
        {
            Cell sourceCell = targetCell ?? Cell;
            if (sourceCell == null)
            {
                return null;
            }

            CellGrid cellGrid = FindSceneCellGrid();
            Cell canonicalCell = cellGrid?.ResolveCanonicalCell(sourceCell) ?? sourceCell;
            if (targetCell == null || ReferenceEquals(targetCell, Cell))
            {
                cell = canonicalCell;
            }

            return canonicalCell;
        }
        internal void EnsureSceneCellBinding(bool notifyGrid = true)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (Cell == null)
            {
                CellGrid cellGrid = FindSceneCellGrid();
                Cell resolved = ResolveTransformStartCell(cellGrid, null);
                if (resolved == null)
                {
                    return;
                }

                Cell = resolved;
            }

            RegisterCellOccupancyList(Cell, notifyGrid);
        }
        internal void RegisterCellOccupancyList(Cell targetCell = null, bool notifyGrid = true)
        {
            Cell resolvedCell = ResolveOccupancyCell(targetCell);
            if (resolvedCell == null)
            {
                return;
            }

            if (!resolvedCell.CurrentUnits.Contains(this))
            {
                resolvedCell.CurrentUnits.Add(this);
            }

            RefreshCellOccupancy(resolvedCell);
            if (notifyGrid)
            {
                FindSceneCellGrid()?.NotifyOccupancyChanged();
            }
        }
        internal void UnregisterCellOccupancyList(Cell targetCell = null, bool notifyGrid = true)
        {
            Cell sourceCell = targetCell ?? Cell;
            Cell resolvedCell = FindSceneCellGrid()?.ResolveCanonicalCell(sourceCell) ?? sourceCell;
            if (resolvedCell == null)
            {
                return;
            }

            resolvedCell.CurrentUnits.Remove(this);
            RefreshCellOccupancy(resolvedCell);
            if (notifyGrid)
            {
                FindSceneCellGrid()?.NotifyOccupancyChanged();
            }
        }
        internal static void RefreshCellOccupancy(Cell cell)
        {
            if (cell == null)
            {
                return;
            }

            CellGrid cellGrid = FindAnyObjectByType<CellGrid>();
            Cell canonicalCell = cellGrid?.ResolveCanonicalCell(cell) ?? cell;

            bool hasBlockingUnit = canonicalCell.CurrentUnits != null
                && canonicalCell.CurrentUnits.Any(occupant =>
                    occupant != null && occupant.Obstructable && !occupant.ExcludedFromBattle);

            canonicalCell.IsTaken = !canonicalCell.IsTraversable || hasBlockingUnit;
        }
        internal void InvalidateCachedPaths()
        {
            cachedPaths = null;
        }
        private static CellGrid FindSceneCellGrid()
        {
            return FindAnyObjectByType<CellGrid>();
        }
        private static ExperienceGainHUD FindSceneExperienceGainHud()
        {
            return FindAnyObjectByType<ExperienceGainHUD>();
        }
        private static LevelUpUI FindSceneLevelUpUi()
        {
            return FindAnyObjectByType<LevelUpUI>();
        }
        private static CombatSequenceUI FindSceneCombatSequenceUi()
        {
            return FindAnyObjectByType<CombatSequenceUI>();
        }
        private static bool IsSceneGrid2D()
        {
            return FindSceneCellGrid()?.Is2D ?? true;
        }
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

            LogBattleAction($"attacks {DescribeUnit(unitToAttack)} with {GetEquippedWeaponDisplayName()}.");
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

            string skillName = skill != null && !string.IsNullOrWhiteSpace(skill.Name) ? skill.Name : "Support Skill";
            LogBattleAction($"uses {skillName} on {DescribeUnit(primaryTarget)}.");
            StartCoroutine(SupportSkillRoutine(primaryTarget, endsTurn, resolveEffect, skill, cellGrid));
        }
        public void UseAreaSkill(IReadOnlyList<Unit> targets, bool endsTurn, Action<Unit> resolvePerTarget, SkillData skill = null, CellGrid cellGrid = null)
        {
            if (IsAttackSequenceRunning || !CanStartActionThisTurn)
            {
                return;
            }

            string skillName = skill != null && !string.IsNullOrWhiteSpace(skill.Name) ? skill.Name : "Area Skill";
            string targetSummary = DescribeUnitList(targets);
            LogBattleAction($"uses {skillName} on area targets: {targetSummary}.");
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

                var preCombatContext = new CombatSequenceContext(this, unitToAttack, attackProfile.PreventsCounterattack);
                InvokeBeforeCombatSequenceAsAttacker(this, preCombatContext);
                InvokeBeforeCombatSequenceAsDefender(unitToAttack, preCombatContext);

                int baseDamage = attackProfile.Damage;
                BattleLog.Log("Combat", $"{name} starts a {(attackProfile.IsMagic ? "magic" : "physical")} attack on {unitToAttack.name}. (attackerId={UnitID}, defenderId={unitToAttack.UnitID}, baseDamage={baseDamage}, finishedBefore={IsFinishedForTurn})");

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
                    BattleLog.Log("Combat", $"{name} starts a pursuit {(attackProfile.IsMagic ? "magic" : "physical")} attack on {unitToAttack.name}. (attackerId={UnitID}, defenderId={unitToAttack.UnitID}, baseDamage={baseDamage}, finishedBefore={IsFinishedForTurn})");
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
                    var combatSequenceContext = new CombatSequenceContext(this, unitToAttack, attackProfile.PreventsCounterattack);
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

            }
            finally
            {
                if (experienceTarget != null && destroyedHandler != null)
                {
                    experienceTarget.CombatDestroyed -= destroyedHandler;
                }

                if (sequenceStarted)
                {
                    var combatSequenceContext = new CombatSequenceContext(this, unitToAttack, attackProfile.PreventsCounterattack);
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
                        BattleLog.Log("Combat", $"Strike hits {name}{(damageContext.IsCrit ? " and crits" : "")}, dealing {damageTaken} damage. (defenderId={UnitID}, hitChance={hitChance}%, critChance={critChance}%, crit={damageContext.IsCrit}, mitigationStat={(isMagicAttack ? "Resistance" : "Defence")}, mitigationValue={defenseStat})");
                    }
                }

                if (!damageContext.IsHit && !simulateOnly)
                {
                    BattleLog.Log("Combat", $"Strike misses {name}. (defenderId={UnitID}, hitChance={hitChance}%, damageTaken=0)");
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

        private void LogBattleAction(string actionDescription)
        {
            BattleLog.Log("Action", $"{DescribeUnit(this)} {actionDescription}");
        }

        private static string DescribeUnit(Unit unit)
        {
            if (unit == null)
            {
                return "None";
            }

            string side = unit.PlayerNumber switch
            {
                0 => "Friendly",
                1 => "Enemy",
                _ => $"Player {unit.PlayerNumber}"
            };

            string nameLabel = string.IsNullOrWhiteSpace(unit.unitName) ? unit.name : unit.unitName;
            return $"{side} {nameLabel}";
        }

        private string GetEquippedWeaponDisplayName()
        {
            return !string.IsNullOrWhiteSpace(EquippedWeapon?.Name) ? EquippedWeapon.Name : "basic attack";
        }

        private static string DescribeUnitList(IReadOnlyList<Unit> units)
        {
            if (units == null || units.Count == 0)
            {
                return "none";
            }

            return string.Join(", ", units
                .Where(unit => unit != null)
                .Select(DescribeUnit)
                .Distinct());
        }
        private IEnumerator CounterAttack(Unit aggressor, bool counterPrevented = false)
        {
            if (!ShouldTriggerCounterAttack(aggressor, counterPrevented))
            {
                yield break;
            }

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

                BattleLog.Log("Combat", $"{name} counterattacks {aggressor.name} after attack resolution. (defenderId={UnitID}, aggressorId={aggressor.UnitID})");
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
        public IEnumerator Move(Cell destinationCell, IList<Cell> path)
        {
            if (destinationCell == null || path == null || path.Count == 0)
            {
                yield break;
            }

            Windy.Srpg.Game.Grid.CellGrid cellGrid = FindSceneCellGrid();
            Cell canonicalDestination = ResolveOccupancyCell(destinationCell);
            Cell fromCell = ResolveOccupancyCell(Cell);
            Cell resolvedStartCell = ResolveTransformStartCell(cellGrid, fromCell);
            resolvedStartCell = ResolveOccupancyCell(resolvedStartCell);
            if (resolvedStartCell != null && resolvedStartCell != fromCell)
            {
                UnregisterCellOccupancyList(fromCell, notifyGrid: false);
                RegisterCellOccupancyList(resolvedStartCell, notifyGrid: false);
                Cell = resolvedStartCell;
                cellGrid?.NotifyOccupancyChanged();
                cachedPaths = null;
                InvalidateCachedPaths();
                fromCell = resolvedStartCell;

                if (cellGrid != null)
                {
                    List<Cell> allCells = cellGrid.GetAllCells() ?? new List<Cell>();
                    CachePaths(allCells);
                    path = FindPath(allCells, canonicalDestination);
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

            if (!CanOccupyCell(canonicalDestination))
            {
                yield break;
            }

            UnregisterCellOccupancyList(fromCell, notifyGrid: false);
            RegisterCellOccupancyList(canonicalDestination, notifyGrid: false);
            Cell = canonicalDestination;
            cellGrid?.NotifyOccupancyChanged();
            cachedPaths = null;
            InvalidateCachedPaths();

            var totalMovementCost = path.Sum(h => h.MovementCost);
            MovementPoints -= totalMovementCost;

            if (MovementAnimationSpeed > 0)
            {
                yield return StartCoroutine(AnimateMovementPath(path));
            }
            else
            {
                SnapToCellLocalPosition(canonicalDestination);
                OnMoveFinished();
            }

            cellGrid?.RequestBattleOutcomeEvaluation();
        }
        internal float GetPendingMovementPointsBefore() =>
            _pendingMove.HasValue ? _pendingMove.Value.MovementPointsBefore : MovementPoints;
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

            UnregisterCellOccupancyList(p.FromCell, notifyGrid: false);
            RegisterCellOccupancyList(p.ToCell, notifyGrid: false);
            Cell = ResolveOccupancyCell(p.ToCell);
            cachedPaths = null;
            InvalidateCachedPaths();
            FindSceneCellGrid()?.NotifyOccupancyChanged();

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
            Cell canonicalCell = FindSceneCellGrid()?.ResolveCanonicalCell(cell) ?? cell;
            if (canonicalCell?.CurrentUnits == null)
            {
                return false;
            }

            foreach (Unit occupant in canonicalCell.CurrentUnits)
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
}

