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
using Windy.Srpg.Runtime.Units;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using RuntimeBuff = Windy.Srpg.Game.Buffs.Buff;

namespace Windy.Srpg.Game.Units
{
    [ExecuteInEditMode]
    public partial class Unit : MonoBehaviour, IGridUnit
    {
        // Search for "CTRL+F:" to jump between major gameplay systems in this file.
        #region CTRL+F: Events / Runtime State / Serialized Fields
        Dictionary<Cell, IList<Cell>> cachedPaths = null;
        public event EventHandler<UnitHealthChangedEventArgs> UnitHealthChanged;
        public event EventHandler<AttackEventArgs> CombatDestroyed;
        public event EventHandler<UnitDestroyedEventArgs> DestroyedInCombat;
        public event EventHandler UnitStatsChanged;
        public event EventHandler UnitBuffsChanged;
        public event EventHandler UnitProgressionChanged;
        public event EventHandler GameplaySelected;
        public event EventHandler GameplayDeselected;
        public static event EventHandler<CombatSequenceEventArgs> CombatSequenceStarted;
        public static event EventHandler<CombatSequenceEventArgs> CombatSequenceEnded;
        public static event Action<Vector3> CombatCameraFocusRequested;
        public static event Action CombatCameraFocusReleased;
        public static event Action<Vector3> PreviewMoveCameraFollowRequested;
        public static event Action PreviewMoveCameraFollowReleased;
        private bool hasInitializedTurnState;
        public UnitTurnStateKind CurrentTurnStateKind => currentTurnStateKind;
        public bool HasInitializedTurnState => hasInitializedTurnState;
        public bool IsSelectedForTurn => currentTurnStateKind == UnitTurnStateKind.Selected;
        public bool IsReachableEnemyForTurn => currentTurnStateKind == UnitTurnStateKind.ReachableEnemy;
        public bool IsFriendlyForTurn => currentTurnStateKind == UnitTurnStateKind.Friendly;
        public bool IsFinishedForTurn => currentTurnStateKind == UnitTurnStateKind.Finished;
        public bool CanStartActionThisTurn => !IsFinishedForTurn;

        private int customTotalHitPoints;
        private int customTotalManaPoints;
        private float customTotalMovementPoints;
        private UnitTurnStateKind currentTurnStateKind = UnitTurnStateKind.Normal;
        public int ComputedTotalHitPoints
        {
            get => customTotalHitPoints;
            private set => customTotalHitPoints = value;
        }
        public int ComputedTotalManaPoints
        {
            get => customTotalManaPoints;
            private set => customTotalManaPoints = value;
        }
        public float ComputedTotalMovementPoints
        {
            get => customTotalMovementPoints;
            private set => customTotalMovementPoints = value;
        }

        [SerializeField]
        private int baseHitPoints = 1;
        [SerializeField]
        private int baseManaPoints = 9;
        [SerializeField]
        private int level = 1;
        [SerializeField]
        private int experience = 0;
        public string unitName = "Ally";
        [Header("Unit Preset")]
        [SerializeField, FormerlySerializedAs("enemyPreset")] private UnitPreset preset;
        [SerializeField, FormerlySerializedAs("enemyPresetOverride")] private UnitPresetOverride presetOverride = new UnitPresetOverride();
        [Header("Save Identity")]
        [SerializeField] private string unitId = string.Empty;
        [SerializeField] private string visualId = string.Empty;
        [SerializeField]
        private List<StartingInventoryItem> startingInventory = new List<StartingInventoryItem>();
        [SerializeField]
        private List<StartingSkillEntry> startingSkills = new List<StartingSkillEntry>();
        [SerializeField]
        private List<StartingPassiveEntry> startingUniquePassives = new List<StartingPassiveEntry>();
        [SerializeField]
        private List<StartingPassiveEntry> startingEquipPassives = new List<StartingPassiveEntry>();
        [SerializeField]
        private WeaponType weaponProficiencies = WeaponType.Sword | WeaponType.Lance | WeaponType.Blunt | WeaponType.Ranged | WeaponType.Magic;
        [SerializeField]
        private int baseStrength;
        [SerializeField]
        private int baseDefense;
        [SerializeField]
        private int baseMagic;
        [SerializeField]
        private int baseResistance;
        [SerializeField]
        private int baseSpeed;
        private int PursuitAttackSpeedThreshold = 5;
        private float attackHitPauseSeconds = 0.25f;
        private float combatSequenceStartDelaySeconds = 0.25f;
        public bool IsAttackSequenceRunning { get; private set; } = false;

        private static int activeCombatPresentationDepth;

        public static bool IsAnyCombatPresentationActive => activeCombatPresentationDepth > 0;

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

            return IsAnyCombatPresentationActive;
        }
        [SerializeField]
        private int baseLuck;
        [SerializeField] private int growthStrength = 17;
        [SerializeField] private int growthMagic = 17;
        [SerializeField] private int growthDefense = 17;
        [SerializeField] private int growthResistance = 17;
        [SerializeField] private int growthSpeed = 16;
        [SerializeField] private int growthLuck = 16;
        public UnitInventory Inventory { get; private set; }
        public UnitSkillList SkillList { get; private set; }
        public UnitBuffList BuffList { get; private set; }
        public UnitPassiveList PassiveList { get; private set; }
        public WeaponData EquippedWeapon => GetActiveWeapon();
        public AccessoryData EquippedAccessory => Inventory?.EquippedAccessory;
        public virtual WeaponType WeaponProficiencies => weaponProficiencies;
        public virtual bool HasUsableWeapon => GetActiveWeapon() != null;
        public virtual bool IsMagic => GetActiveWeapon()?.DamageType == DamageType.Magic;
        public virtual int Might => GetActiveWeapon()?.Might ?? 0;
        public virtual int MinAttackRange
        {
            get
            {
                var weapon = GetActiveWeapon();
                return weapon == null ? 0 : Mathf.Max(0, weapon.MinRange);
            }
        }
        public virtual int MaxAttackRange
        {
            get
            {
                var weapon = GetActiveWeapon();
                if (weapon == null)
                {
                    return 0;
                }

                int maxRange = weapon.MaxRange + GetSecondaryStatModifiers().AttackRange;
                return Mathf.Max(MinAttackRange, maxRange);
            }
        }
        public int AttackRange => MaxAttackRange;
        public virtual int NumHits => HasUsableWeapon ? Mathf.Max(1, GetActiveWeapon().NumHits) : 0;
        public virtual bool CanPursuitAttack => HasUsableWeapon && GetActiveWeapon().CanPursuitAttack;
        public virtual bool CanCounterAttack => HasUsableWeapon && GetActiveWeapon().CanCounterAttack;
        public virtual bool PreventsCounterattack => HasUsableWeapon && GetActiveWeapon().PreventsCounterattack;
        public virtual int BaseHitPoints => baseHitPoints;
        public virtual int MaxHitPoints => Mathf.Max(1, BaseHitPoints + GetPrimaryStatModifiers().MaxHitPoints + Strength);
        public int HitPoints { get; set; }
        public virtual int BaseManaPoints => baseManaPoints;
        public virtual int MaxManaPoints => Mathf.Max(0, BaseManaPoints + GetPrimaryStatModifiers().MaxManaPoints + ((Magic + Resistance) * 3));
        public int CurrentManaPoints { get; private set; }
        public int Level => Mathf.Clamp(level, 1, ExperienceCalculator.MaxLevel);
        public int Experience => Level >= ExperienceCalculator.MaxLevel ? 0 : Mathf.Clamp(experience, 0, ExperienceCalculator.MaxGain - 1);
        public virtual bool CanGainExperience => PlayerNumber == 0;
        public int PlayerId => PlayerNumber;
        public string UnitId => unitId;
        public string VisualId => visualId;

        private bool presetAppliedAtRuntime;
        private bool useResolvedPresetLoadout;
        private List<StartingInventoryItem> resolvedStartingInventory = new List<StartingInventoryItem>();
        private List<StartingSkillEntry> resolvedStartingSkills = new List<StartingSkillEntry>();
        private List<StartingPassiveEntry> resolvedStartingUniquePassives = new List<StartingPassiveEntry>();
        private List<StartingPassiveEntry> resolvedStartingEquipPassives = new List<StartingPassiveEntry>();
        private SecondaryStatModifiers resolvedSecondaryStatOffsets;
        [NonSerialized] private OwnedUnitSaveData pendingOwnedUnitSaveData;
        [NonSerialized] private UnitPreset pendingOwnedUnitVisualPreset;
        [SerializeField, HideInInspector] private bool spriteLayoutBaselineCaptured;
        [SerializeField, HideInInspector] private Vector3 spriteLayoutBaselineLocalScale = Vector3.one;
        [SerializeField, HideInInspector] private Vector3 spriteLayoutBaselineLocalPosition = new Vector3(0f, 0f, -0.1f);
        public virtual int BaseStrength => baseStrength;
        public virtual int Strength => BaseStrength;
        public virtual int BaseDefense => baseDefense;
        public virtual int Defense => BaseDefense + GetPrimaryStatModifiers().Defense;
        public virtual int BaseMagic => baseMagic;
        public virtual int Magic => BaseMagic + GetPrimaryStatModifiers().Magic;
        public virtual int BaseResistance => baseResistance;
        public virtual int Resistance => BaseResistance + GetPrimaryStatModifiers().Resistance;
        public virtual int BaseSpeed => baseSpeed;
        public virtual int Speed => BaseSpeed + GetPrimaryStatModifiers().Speed;
        public virtual int BaseLuck => baseLuck;
        public virtual int Luck => BaseLuck + GetPrimaryStatModifiers().Luck;
        public virtual int Attack => (IsMagic ? Magic : Strength) + Might + GetPrimaryStatModifiers().Attack;

        private const int AccuracyPerSpeed = 5;
        private const int CritPerLuck = 5;

        public virtual int Accuracy
        {
            get
            {
                return GetAttackBaseAccuracy() + GetSecondaryStatModifiers().Accuracy + Speed * AccuracyPerSpeed;
            }
        }
        public virtual int Evade
        {
            get
            {
                return Speed * AccuracyPerSpeed;
            }
        }
        public virtual int Crit
        {
            get
            {
                return GetAttackBaseCrit() + GetSecondaryStatModifiers().Crit + Luck * CritPerLuck;
            }
        }
        public virtual int CritAvoid
        {
            get
            {
                return GetSecondaryStatModifiers().CritAvoid + Luck * CritPerLuck;
            }
        }
        [Obsolete("ActionPoints is deprecated. Use CanStartActionThisTurn, EndTurnForUnit, and ResetTurnState instead.")]
        public float ActionPoints
        {
            get
            {
                return CanStartActionThisTurn ? 1f : 0f;
            }
            set
            {
                if (value <= 0f)
                {
                    SetTurnStateKind(UnitTurnStateKind.Finished);
                }
                else if (IsFinishedForTurn)
                {
                    SetTurnStateKind(UnitTurnStateKind.Normal);
                }
            }
        }
        #endregion

        #region CTRL+F: Turn State / Initialization / Validation

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

        private static DijkstraPathfinding _pathfinder = new DijkstraPathfinding();

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
                SyncMirroredRuntimeNow();
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

            SetTurnStateKind(UnitTurnStateKind.Normal, useStateTransition: false, syncRuntime: false);

            ComputedTotalHitPoints = MaxHitPoints;
            ComputedTotalManaPoints = MaxManaPoints;
            ComputedTotalMovementPoints = MovementPoints;
            HitPoints = MaxHitPoints;
            CurrentManaPoints = MaxManaPoints;

            RaiseHealthChanged(HitPoints, HitPoints, null);

            foreach (var action in GetBattleActions())
            {
                action.InitializeAction(this);
            }

            SyncMirroredRuntimeNow();
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

            Inventory.LoadExactItems(CreateSavedInventoryItems(saveData));
            SkillList.LoadStartingSkills(CreateSavedSkillEntries(saveData.SkillIds));
            PassiveList.LoadStartingPassives(
                CreateSavedPassiveEntries(saveData.UniquePassiveIds),
                CreateSavedPassiveEntries(saveData.EquipPassiveIds));

            SetTurnStateKind(UnitTurnStateKind.Normal, useStateTransition: false, syncRuntime: false);

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

            foreach (var action in GetBattleActions())
            {
                action.InitializeAction(this);
            }

            SyncMirroredRuntimeNow();
        }

        public void OnMouseDown()
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            CellGrid grid = FindSceneCellGrid();
            if (grid != null && grid.ShouldSuppressFrameworkSceneInput)
            {
                return;
            }

            RaiseUnitClicked();
            UnitClicked?.Invoke(this, EventArgs.Empty);
        }
        public void OnMouseEnter()
        {
            CellGrid grid = FindSceneCellGrid();
            if (grid != null && grid.ShouldSuppressFrameworkSceneInput)
            {
                return;
            }

            RaiseUnitHighlighted();
            UnitHighlighted?.Invoke(this, EventArgs.Empty);
        }

        public void OnMouseExit()
        {
            CellGrid grid = FindSceneCellGrid();
            if (grid != null && grid.ShouldSuppressFrameworkSceneInput)
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

        private void EnsureInventory()
        {
            BuiltInItemCatalog.EnsureRegistered();
            if (Inventory == null)
            {
                Inventory = new UnitInventory(this);
            }
        }

        private void EnsureBuffList()
        {
            BuiltInBuffCatalog.EnsureRegistered();
            if (BuffList == null)
            {
                BuffList = new UnitBuffList(this);
            }
        }

        private void EnsurePassiveList()
        {
            BuiltInPassiveCatalog.EnsureRegistered();
            if (PassiveList == null)
            {
                PassiveList = new UnitPassiveList(this);
            }
        }

        private void SetTurnStateKind(UnitTurnStateKind stateKind, bool useStateTransition = true, bool syncRuntime = true)
        {
            currentTurnStateKind = stateKind;
            hasInitializedTurnState = true;
            ApplyTurnStateVisual(stateKind);

            if (syncRuntime)
            {
                SyncMirroredRuntimeTurnState();
            }
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
        #endregion

        #region CTRL+F: Loadout Defaults / Equipment Resolution / Stat Modifiers

        private void EnsureSkillList()
        {
            BuiltInSkillCatalog.EnsureRegistered();
            if (SkillList == null)
            {
                SkillList = new UnitSkillList(this);
            }
        }

        private IEnumerable<StartingInventoryItem> GetInitialInventory()
        {
            if (useResolvedPresetLoadout)
            {
                return resolvedStartingInventory;
            }

            if (startingInventory != null && startingInventory.Count > 0)
            {
                return startingInventory;
            }

            return Array.Empty<StartingInventoryItem>();
        }

        private IEnumerable<StartingSkillEntry> GetInitialSkills()
        {
            if (useResolvedPresetLoadout)
            {
                return resolvedStartingSkills;
            }

            if (startingSkills != null && startingSkills.Count > 0)
            {
                return startingSkills;
            }

            return Array.Empty<StartingSkillEntry>();
        }

        private IEnumerable<StartingPassiveEntry> GetInitialUniquePassives()
        {
            if (useResolvedPresetLoadout)
            {
                return resolvedStartingUniquePassives;
            }

            if (startingUniquePassives != null && startingUniquePassives.Count > 0)
            {
                return startingUniquePassives;
            }

            return Array.Empty<StartingPassiveEntry>();
        }

        private IEnumerable<StartingPassiveEntry> GetInitialEquipPassives()
        {
            if (useResolvedPresetLoadout)
            {
                return resolvedStartingEquipPassives;
            }

            if (startingEquipPassives != null && startingEquipPassives.Count > 0)
            {
                return startingEquipPassives;
            }

            return Array.Empty<StartingPassiveEntry>();
        }

        private WeaponData GetActiveWeapon()
        {
            EnsureInventory();
            return Inventory?.EquippedWeapon;
        }

        private PrimaryStatModifiers GetPrimaryStatModifiers()
        {
            return GetPrimaryStatModifiers(GetActiveWeapon());
        }

        private PrimaryStatModifiers GetPrimaryStatModifiers(WeaponData weapon)
        {
            PrimaryStatModifiers modifiers = default;

            if (weapon != null)
            {
                modifiers += weapon.StatModifiers;
            }

            if (EquippedAccessory != null)
            {
                modifiers += EquippedAccessory.StatModifiers;
            }

            if (BuffList != null)
            {
                modifiers += BuffList.GetPrimaryStatModifiers();
            }

            if (PassiveList != null)
            {
                modifiers += PassiveList.GetPrimaryStatModifiers();
            }

            return modifiers;
        }

        private SecondaryStatModifiers GetSecondaryStatModifiers()
        {
            SecondaryStatModifiers modifiers = resolvedSecondaryStatOffsets;

            if (EquippedAccessory != null)
            {
                modifiers += EquippedAccessory.SecondaryStatModifiers;
            }

            if (BuffList != null)
            {
                modifiers += BuffList.GetSecondaryStatModifiers();
            }

            if (PassiveList != null)
            {
                modifiers += PassiveList.GetSecondaryStatModifiers();
            }

            return modifiers;
        }
        #endregion

        #region CTRL+F: Buff / Inventory / Skill / Passive Runtime APIs

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

        public List<IBattleAction> GetBattleActions()
        {
            return GetComponentsInChildren<MonoBehaviour>()
                .OfType<IBattleAction>()
                .Where(action => action != null)
                .ToList();
        }
        #endregion

        #region CTRL+F: Mana / Progression / Equipment Utility

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

        public int GetBaseStatValue(LevelableStatKind stat)
        {
            return stat switch
            {
                LevelableStatKind.Strength => BaseStrength,
                LevelableStatKind.Magic => BaseMagic,
                LevelableStatKind.Defense => BaseDefense,
                LevelableStatKind.Resistance => BaseResistance,
                LevelableStatKind.Speed => BaseSpeed,
                LevelableStatKind.Luck => BaseLuck,
                _ => 0
            };
        }

        public IReadOnlyDictionary<LevelableStatKind, int> GetBaseStatSnapshot()
        {
            return new Dictionary<LevelableStatKind, int>
            {
                [LevelableStatKind.Strength] = BaseStrength,
                [LevelableStatKind.Magic] = BaseMagic,
                [LevelableStatKind.Defense] = BaseDefense,
                [LevelableStatKind.Resistance] = BaseResistance,
                [LevelableStatKind.Speed] = BaseSpeed,
                [LevelableStatKind.Luck] = BaseLuck
            };
        }

        public IReadOnlyList<int> GetNormalizedGrowthRates()
        {
            return LevelUpGainCalculator.NormalizeGrowthRates(new[]
            {
                growthStrength,
                growthMagic,
                growthDefense,
                growthResistance,
                growthSpeed,
                growthLuck
            });
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

        private static Item[] CreateSavedInventoryItems(OwnedUnitSaveData saveData)
        {
            return saveData?.Inventory?
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.ItemId))
                .Select(entry => new Item(entry.ItemId, entry.RemainingCharges))
                .ToArray()
                ?? Array.Empty<Item>();
        }

        private static StartingSkillEntry[] CreateSavedSkillEntries(IEnumerable<string> skillIds)
        {
            return skillIds?
                .Where(skillId => !string.IsNullOrWhiteSpace(skillId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(skillId => new StartingSkillEntry { SkillId = skillId })
                .ToArray()
                ?? Array.Empty<StartingSkillEntry>();
        }

        private static StartingPassiveEntry[] CreateSavedPassiveEntries(IEnumerable<string> passiveIds)
        {
            return passiveIds?
                .Where(passiveId => !string.IsNullOrWhiteSpace(passiveId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(passiveId => new StartingPassiveEntry { PassiveId = passiveId })
                .ToArray()
                ?? Array.Empty<StartingPassiveEntry>();
        }

        private static WeaponType GetWeaponProficienciesFromIds(IEnumerable<string> proficiencyIds)
        {
            WeaponType result = WeaponType.None;
            foreach (string proficiencyId in proficiencyIds ?? Array.Empty<string>())
            {
                if (Enum.TryParse(proficiencyId, true, out WeaponType parsedType))
                {
                    result |= parsedType;
                }
            }

            return result;
        }

        private IEnumerable<string> GetWeaponProficiencyIds()
        {
            WeaponType[] supportedTypes =
            {
                WeaponType.Sword,
                WeaponType.Lance,
                WeaponType.Ranged,
                WeaponType.Blunt,
                WeaponType.Magic
            };

            foreach (WeaponType type in supportedTypes)
            {
                if ((WeaponProficiencies & type) != 0)
                {
                    yield return type.ToString();
                }
            }
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

        private string BuildFallbackUnitId()
        {
            string source = !string.IsNullOrWhiteSpace(unitName) ? unitName : gameObject?.name;
            if (string.IsNullOrWhiteSpace(source))
            {
                return string.Empty;
            }

            List<char> buffer = new List<char>(source.Length);
            bool previousWasSeparator = false;
            foreach (char character in source)
            {
                if (char.IsLetterOrDigit(character))
                {
                    buffer.Add(char.ToLowerInvariant(character));
                    previousWasSeparator = false;
                    continue;
                }

                if (previousWasSeparator || buffer.Count == 0)
                {
                    continue;
                }

                buffer.Add('_');
                previousWasSeparator = true;
            }

            while (buffer.Count > 0 && buffer[buffer.Count - 1] == '_')
            {
                buffer.RemoveAt(buffer.Count - 1);
            }

            return new string(buffer.ToArray());
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
            if (preset == null || this.preset != preset)
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

        private static SpriteRenderer ResolveUnitSpriteRenderer(Unit unit)
        {
            if (unit == null)
            {
                return null;
            }

            Transform spriteTransform = unit.transform.Find("Sprite");
            if (spriteTransform != null && spriteTransform.TryGetComponent(out SpriteRenderer dedicatedRenderer))
            {
                return dedicatedRenderer;
            }

            foreach (SpriteRenderer renderer in unit.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer != null && renderer.transform.name == "Sprite")
                {
                    return renderer;
                }
            }

            return unit.GetComponentInChildren<SpriteRenderer>(true);
        }

        private SpriteRenderer ResolveUnitSpriteRenderer()
        {
            return ResolveUnitSpriteRenderer(this);
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

        private float ResolvePresetSpriteScaleFactor(Vector2 targetSize, Sprite sprite, Vector3 baseScale)
        {
            if (sprite == null)
            {
                return 1f;
            }

            Vector2 spriteSize = sprite.bounds.size;
            Vector2 baseSize = new Vector2(Mathf.Abs(baseScale.x) * spriteSize.x, Mathf.Abs(baseScale.y) * spriteSize.y);
            if (baseSize.x <= 0f || baseSize.y <= 0f)
            {
                return 1f;
            }

            Vector2 targetWorldSize = ResolvePresetSpriteTargetWorldSize(targetSize);
            if (targetWorldSize.x <= 0f || targetWorldSize.y <= 0f)
            {
                return 1f;
            }

            float widthFactor = targetWorldSize.x / baseSize.x;
            float heightFactor = targetWorldSize.y / baseSize.y;
            return Mathf.Min(widthFactor, heightFactor);
        }

        private Vector2 ResolvePresetSpriteTargetWorldSize(Vector2 targetSize)
        {
            Vector2 referenceSize = GetPresetSpriteReferenceWorldSize();
            return new Vector2(referenceSize.x * targetSize.x, referenceSize.y * targetSize.y);
        }

        private Vector2 GetPresetSpriteReferenceWorldSize()
        {
            if (Cell != null)
            {
                Vector3 rawCellSize = Cell.GetCellDimensions();
                Vector2 cellSize = new Vector2(Mathf.Abs(rawCellSize.x), Mathf.Abs(rawCellSize.y));
                if (cellSize.x > 0f && cellSize.y > 0f)
                {
                    return cellSize;
                }
            }

            return Vector2.one;
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

        private void ApplyBaseStatIncreaseInternal(LevelableStatKind stat, int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            switch (stat)
            {
                case LevelableStatKind.Strength:
                    baseStrength += amount;
                    break;
                case LevelableStatKind.Magic:
                    baseMagic += amount;
                    break;
                case LevelableStatKind.Defense:
                    baseDefense += amount;
                    break;
                case LevelableStatKind.Resistance:
                    baseResistance += amount;
                    break;
                case LevelableStatKind.Speed:
                    baseSpeed += amount;
                    break;
                case LevelableStatKind.Luck:
                    baseLuck += amount;
                    break;
            }
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

        public bool CanEquipWeapon(WeaponData weapon)
        {
            if (weapon == null)
            {
                return false;
            }

            WeaponType requiredType = weapon.WeaponType == WeaponType.None ? WeaponType.Sword : weapon.WeaponType;
            return (WeaponProficiencies & requiredType) != 0;
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

        public int GetMinAttackRangeForWeapon(WeaponData weapon)
        {
            return weapon == null ? 0 : Mathf.Max(0, weapon.MinRange);
        }

        public int GetMaxAttackRangeForWeapon(WeaponData weapon)
        {
            if (weapon == null)
            {
                return 0;
            }

            int minRange = GetMinAttackRangeForWeapon(weapon);
            int maxRange = weapon.MaxRange + GetSecondaryStatModifiers().AttackRange;
            return Mathf.Max(minRange, maxRange);
        }

        public bool GetIsMagicForWeapon(WeaponData weapon)
        {
            return weapon?.DamageType == DamageType.Magic;
        }

        public int GetMagicForWeapon(WeaponData weapon)
        {
            return BaseMagic + GetPrimaryStatModifiers(weapon).Magic;
        }

        public int GetSpeedForWeapon(WeaponData weapon)
        {
            return BaseSpeed + GetPrimaryStatModifiers(weapon).Speed;
        }

        public int GetLuckForWeapon(WeaponData weapon)
        {
            return BaseLuck + GetPrimaryStatModifiers(weapon).Luck;
        }

        public int GetAttackForWeapon(WeaponData weapon)
        {
            if (weapon == null)
            {
                return 0;
            }

            var primaryModifiers = GetPrimaryStatModifiers(weapon);
            bool isMagic = GetIsMagicForWeapon(weapon);
            int offensiveStat = isMagic ? BaseMagic + primaryModifiers.Magic : BaseStrength;
            return offensiveStat + weapon.Might + primaryModifiers.Attack;
        }

        public int GetAccuracyForWeapon(WeaponData weapon)
        {
            if (weapon == null)
            {
                return 0;
            }

            return weapon.Accuracy + GetSecondaryStatModifiers().Accuracy + GetSpeedForWeapon(weapon) * AccuracyPerSpeed;
        }

        public int GetCritForWeapon(WeaponData weapon)
        {
            if (weapon == null)
            {
                return 0;
            }

            return weapon.Crit + GetSecondaryStatModifiers().Crit + GetLuckForWeapon(weapon) * CritPerLuck;
        }

        public int GetNumHitsForWeapon(WeaponData weapon)
        {
            return weapon == null ? 0 : Mathf.Max(1, weapon.NumHits);
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
        #endregion

        #region CTRL+F: Health / Displacement / Turn End

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

        protected virtual int GetAttackBaseAccuracy()
        {
            return GetActiveWeapon()?.Accuracy ?? 0;
        }

        protected virtual int GetAttackBaseCrit()
        {
            return GetActiveWeapon()?.Crit ?? 0;
        }

        private bool CanWeaponAttackTarget(WeaponData weapon, Unit other, Cell otherCell, Cell sourceCell)
        {
            if (weapon == null || other == null || otherCell == null || sourceCell == null)
            {
                return false;
            }

            int distance = sourceCell.GetDistance(otherCell);
            int minRange = Mathf.Max(0, weapon.MinRange);
            int maxRange = Mathf.Max(minRange, weapon.MaxRange + GetSecondaryStatModifiers().AttackRange);
            return distance >= minRange
                && distance <= maxRange
                && other.PlayerNumber != PlayerNumber;
        }
        protected void OnDestroyed()
        {
            bool wasRunningAttackSequence = IsAttackSequenceRunning;

            Cell currentCell = Cell;
            UnregisterCellOccupancyList(currentCell);
            RefreshCellOccupancy(currentCell);
            ClearMirroredRuntimeCell();
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

        private bool pendingDeferredDestroy;

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
            activeCombatPresentationDepth++;
            if (activeCombatPresentationDepth == 1)
            {
                FindAnyObjectByType<CellGrid>()?.NotifyCombatPresentationBegan();
            }
        }

        private static void EndCombatPresentation()
        {
            activeCombatPresentationDepth = Mathf.Max(0, activeCombatPresentationDepth - 1);
            CellGrid cellGrid = FindAnyObjectByType<CellGrid>();
            cellGrid?.TryFlushDeferredDestroyQueue();
            if (activeCombatPresentationDepth == 0)
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
        #endregion

        #region CTRL+F: Visual Marking / Editor Helpers / Auto-Setup

        public virtual void MarkAsDefending(Unit aggressor)
        {
        }

        public virtual void MarkAsAttacking(Unit target)
        {
        }

        public void MarkAsDestroyed()
        {
        }

        public virtual void MarkAsFriendly()
        {
        }

        public virtual void MarkAsReachableEnemy()
        {
        }

        public virtual void MarkAsSelected()
        {
        }

        public virtual void MarkAsFinished()
        {
        }

        public virtual void UnMark()
        {
        }
        public virtual void SetColor(Color color) { }

        [ExecuteInEditMode]
        public void OnDestroy()
        {
            #if UNITY_EDITOR
            if (this.Cell != null && !Application.isPlaying)
            {
                this.Cell.IsTaken = false;
                UnityEditor.EditorUtility.SetDirty(this.Cell);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            }
            #endif
        }

        private void Reset()
        {
            if (GetComponent<AttackAbility>() == null)
            {
                gameObject.AddComponent<AttackAbility>();
            }
            if (GetComponent<MoveAbility>() == null)
            {
                gameObject.AddComponent<MoveAbility>();
            }
            if (GetComponent<AttackRangeHighlightAbility>() == null)
            {
                gameObject.AddComponent<AttackRangeHighlightAbility>();
            }

            GameObject brain = new GameObject("Brain");
            brain.transform.parent = transform;

            brain.AddComponent<MoveToPositionAIAction>();
            brain.AddComponent<AttackAIAction>();

            brain.AddComponent<DamageCellEvaluator>();
            brain.AddComponent<DamageUnitEvaluator>();
        }

        #endregion
    }
}
