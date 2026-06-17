using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using TbsFramework.Cells;
using Windy.Srpg.Game.Pathfinding.Algorithms;
using TbsFramework.Units;
using TbsFramework.Units.Highlighters;
using TbsFramework.Units.UnitStates;
using TbsFramework.Grid;
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
using Windy.Srpg.Runtime.Board;
using Windy.Srpg.Runtime.Units;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using FrameworkBuff = TbsFramework.Units.Buff;
using RuntimeBuff = Windy.Srpg.Game.Buffs.Buff;

namespace Windy.Srpg.Game.Units
{
    /// <summary>
    /// Base class for all units in the game.
    /// </summary>
    [ExecuteInEditMode]
    public partial class CustomUnit : Unit, IBattleUnit
    {
        // Search for "CTRL+F:" to jump between major gameplay systems in this file.
        #region CTRL+F: Events / Runtime State / Serialized Fields
        Dictionary<Cell, IList<Cell>> cachedPaths = null;
        /// <summary>
        /// UnitHealthChanged event is invoked when the unit's current hitpoints change.
        /// </summary>
        public event EventHandler<UnitHealthChangedEventArgs> UnitHealthChanged;
        public event EventHandler<AttackEventArgs> CombatDestroyed;
        public event EventHandler<CustomUnitDestroyedEventArgs> DestroyedInCombat;
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
        public new UnitState UnitState
        {
            get => base.UnitState;
            set
            {
                base.UnitState = value;
                currentTurnStateKind = ResolveTurnStateKind(value);
                hasInitializedTurnState = value != null;
            }
        }
        public UnitTurnStateKind CurrentTurnStateKind => currentTurnStateKind;
        public bool HasInitializedTurnState => hasInitializedTurnState;
        public bool IsSelectedForTurn => currentTurnStateKind == UnitTurnStateKind.Selected;
        public bool IsReachableEnemyForTurn => currentTurnStateKind == UnitTurnStateKind.ReachableEnemy;
        public bool IsFriendlyForTurn => currentTurnStateKind == UnitTurnStateKind.Friendly;
        public bool IsLegacySelectedState => IsSelectedForTurn;
        public bool IsLegacyReachableEnemyState => IsReachableEnemyForTurn;
        public bool IsLegacyFriendlyState => IsFriendlyForTurn;
        public bool IsFinishedForTurn => currentTurnStateKind == UnitTurnStateKind.Finished;
        public bool CanStartActionThisTurn => !IsFinishedForTurn;
        public new void SetState(UnitState state)
        {
            currentTurnStateKind = ResolveTurnStateKind(state);
            base.UnitState = state ?? CreateCompatibilityTurnState(currentTurnStateKind);
            hasInitializedTurnState = true;
            base.UnitState?.Apply();
            SyncMirroredRuntimeTurnState();
        }
        /// <summary>
        /// Legacy framework buff assets that directly mutate unit fields.
        /// </summary>
        private List<(FrameworkBuff buff, int timeLeft)> legacyBuffs;
        public new void AddBuff(FrameworkBuff buff)
        {
            if (buff == null)
            {
                return;
            }

            legacyBuffs ??= new List<(FrameworkBuff, int)>();
            buff.Apply(this);
            legacyBuffs.Add((buff, buff.Duration));
            RefreshHealthState();
            RaiseBuffsChanged();
        }

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

        public static IEnumerator WaitForAttackSequenceCompletion(CustomUnit unit, float timeoutSeconds = 30f)
        {
            float elapsed = 0f;
            while (unit != null && unit.IsAttackSequenceRunning)
            {
                elapsed += Time.unscaledDeltaTime;
                if (elapsed >= timeoutSeconds)
                {
                    yield break;
                }

                yield return null;
            }
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
        public new int AttackRange => MaxAttackRange;
        public virtual int NumHits => HasUsableWeapon ? Mathf.Max(1, GetActiveWeapon().NumHits) : 0;
        public virtual bool CanPursuitAttack => HasUsableWeapon && GetActiveWeapon().CanPursuitAttack;
        public virtual bool CanCounterAttack => HasUsableWeapon && GetActiveWeapon().CanCounterAttack;
        public virtual bool PreventsCounterattack => HasUsableWeapon && GetActiveWeapon().PreventsCounterattack;
        public virtual int BaseHitPoints => baseHitPoints;
        public virtual int MaxHitPoints => Mathf.Max(1, BaseHitPoints + GetPrimaryStatModifiers().MaxHitPoints + Strength);
        public new int HitPoints { get; set; }
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
        /// <summary>
        /// Determines how far on the grid the unit can move.
        /// </summary>
        public override float MovementPoints
        {
            get
            {
                return base.MovementPoints;
            }
            protected set
            {
                base.MovementPoints = value;
                SyncMirroredRuntimeMovementPoints();
            }
        }
        /// <summary>
        /// Determines speed of movement animation.
        /// </summary>
        [Obsolete("ActionPoints is deprecated. Use CanStartActionThisTurn, EndTurnForUnit, and ResetTurnState instead.")]
        public new float ActionPoints
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

        private static CustomDijkstraPathfinding _pathfinder = new CustomDijkstraPathfinding();

        /// <summary>
        /// Method called when unit was added to the game to initialize fields etc. 
        /// </summary>
        public override void Initialize()
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
            legacyBuffs = new List<(FrameworkBuff, int)>();
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

            legacyBuffs = new List<(FrameworkBuff, int)>();
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

        public override void OnMouseDown()
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            base.OnMouseDown();
        }
        public override void OnMouseEnter()
        {
            base.OnMouseEnter();
        }
        public override void OnMouseExit()
        {
            base.OnMouseExit();
        }

        /// <summary>
        /// Method is called at the start of each turn.
        /// </summary>
        public override void OnTurnStart()
        {
            cachedPaths = null;

            legacyBuffs ??= new List<(FrameworkBuff, int)>();
            legacyBuffs.FindAll(b => b.timeLeft == 0).ForEach(b => { b.buff.Undo(this); });
            legacyBuffs.RemoveAll(b => b.timeLeft == 0);
            PassiveList?.OnTurnStart();
            BuffList?.OnTurnStart();
            RefreshHealthState();
            SkillList?.ResetTurnUsage();
            RaiseBuffsChanged();
            var name = this.name;
            var state = UnitState;
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
            base.UnitState = CreateCompatibilityTurnState(stateKind);
            base.UnitState.Apply();

            if (syncRuntime)
            {
                SyncMirroredRuntimeTurnState();
            }
        }

        private UnitState CreateCompatibilityTurnState(UnitTurnStateKind stateKind)
        {
            return new CompatibilityUnitState(this, stateKind);
        }

        private static UnitTurnStateKind ResolveTurnStateKind(UnitState state)
        {
            if (state is CompatibilityUnitState compatibilityState)
            {
                return compatibilityState.Kind;
            }

            return state?.GetType().Name switch
            {
                "UnitStateMarkedAsSelected" => UnitTurnStateKind.Selected,
                "UnitStateMarkedAsReachableEnemy" => UnitTurnStateKind.ReachableEnemy,
                "UnitStateMarkedAsFriendly" => UnitTurnStateKind.Friendly,
                "UnitStateMarkedAsFinished" => UnitTurnStateKind.Finished,
                _ => UnitTurnStateKind.Normal
            };
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

        private sealed class CompatibilityUnitState : UnitState
        {
            public CompatibilityUnitState(CustomUnit unit, UnitTurnStateKind kind) : base(unit)
            {
                Kind = kind;
            }

            public UnitTurnStateKind Kind { get; }

            public override void Apply()
            {
                if (_unit is CustomUnit customUnit)
                {
                    customUnit.ApplyTurnStateVisual(Kind);
                }
            }

            public override void MakeTransition(UnitState state)
            {
                _unit.UnitState = state;
                state?.Apply();
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
                Debug.LogWarning($"CustomUnit: Buff id '{buffId}' is not registered.");
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
            SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>();
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

        public bool UseConsumable(Item entry, CustomUnit target = null)
        {
            EnsureInventory();
            return Inventory.UseConsumable(entry, target);
        }

        public bool CanUseConsumable(Item entry, CustomUnit target = null)
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

        public IEnumerable<Item> GetWeaponsThatCanAttack(CustomUnit target, Cell sourceCell)
        {
            if (target == null || sourceCell == null)
            {
                return Enumerable.Empty<Item>();
            }

            return GetWeaponInventoryEntries()
                .Where(entry => entry?.Weapon != null && CanWeaponAttackTarget(entry.Weapon, target, target.Cell, sourceCell));
        }

        public bool CanAttackTargetWithAnyWeapon(CustomUnit target, Cell sourceCell)
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

        public bool CanPursuitAttackAgainst(CustomUnit other, WeaponData weapon)
        {
            return weapon != null
                && other != null
                && weapon.CanPursuitAttack
                && HitPoints > 0
                && other.HitPoints > 0
                && GetSpeedForWeapon(weapon) >= other.Speed + PursuitAttackSpeedThreshold;
        }

        public bool HasAnyWeaponThatCanAttack(IEnumerable<CustomUnit> potentialTargets, Cell sourceCell)
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

        public bool TryEquipWeaponThatCanAttack(IEnumerable<CustomUnit> potentialTargets, Cell sourceCell)
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

        public void RestoreHitPoints(int amount, CustomUnit source = null)
        {
            if (amount <= 0 || HitPoints <= 0)
            {
                return;
            }

            int previousHitPoints = HitPoints;
            HitPoints = Mathf.Clamp(HitPoints + amount, 0, ComputedTotalHitPoints);
            RaiseHealthChanged(previousHitPoints, HitPoints, source);
        }

        public void SetCurrentHitPoints(int value, CustomUnit source = null)
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

        public bool CanDisplaceTarget(CustomUnit target, Windy.Srpg.Game.Grid.CustomCellGrid cellGrid, int distance = 1, bool push = true, bool moveUserWithTarget = false)
        {
            Cell actingCell = HasPendingMove ? PreviewCell : Cell;
            Cell targetCell = target != null && target.HasPendingMove ? target.PreviewCell : target?.Cell;
            return UnitDisplacementUtility.CanDisplaceRelative(this, actingCell, target, targetCell, cellGrid, distance, push, moveUserWithTarget);
        }

        public bool CanDisplaceTarget(CustomUnit target, CellGrid cellGrid, int distance = 1, bool push = true, bool moveUserWithTarget = false)
        {
            return CanDisplaceTarget(target, cellGrid as Windy.Srpg.Game.Grid.CustomCellGrid, distance, push, moveUserWithTarget);
        }

        public DisplacementResult DisplaceTarget(CustomUnit target, Windy.Srpg.Game.Grid.CustomCellGrid cellGrid, int distance = 1, bool push = true, bool moveUserWithTarget = false)
        {
            return UnitDisplacementUtility.TryDisplaceRelative(this, target, cellGrid, distance, push, moveUserWithTarget);
        }

        public DisplacementResult DisplaceTarget(CustomUnit target, CellGrid cellGrid, int distance = 1, bool push = true, bool moveUserWithTarget = false)
        {
            return DisplaceTarget(target, cellGrid as Windy.Srpg.Game.Grid.CustomCellGrid, distance, push, moveUserWithTarget);
        }
        /// <summary>
        /// Method is called at the end of each turn.
        /// </summary>
        public override void OnTurnEnd()
        {
            if (HasPendingMove && !ConfirmPendingMove())
            {
                CancelPendingMove();
            }

            legacyBuffs ??= new List<(FrameworkBuff, int)>();
            for (int i = 0; i < legacyBuffs.Count; i++)
            {
                (FrameworkBuff buff, int timeLeft) = legacyBuffs[i];
                legacyBuffs[i] = (buff, timeLeft - 1);
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

        private bool CanWeaponAttackTarget(WeaponData weapon, CustomUnit other, Cell otherCell, Cell sourceCell)
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
        /// <summary>
        /// Method is called when units HP drops below 1.
        /// </summary>
        protected override void OnDestroyed()
        {
            bool wasRunningAttackSequence = IsAttackSequenceRunning;
            IsAttackSequenceRunning = false;

            Cell currentCell = Cell;
            currentCell?.CurrentUnits.Remove(this);
            RefreshLegacyCellOccupancy(currentCell);
            ClearMirroredRuntimeCell();
            MarkAsDestroyed();

            if (wasRunningAttackSequence)
            {
                CombatSequenceEnded?.Invoke(this, new CombatSequenceEventArgs(this, this));
                ReleaseCombatCameraFocus();

                var cellGrid = FindSceneCellGrid();
                if (cellGrid != null)
                {
                    if (cellGrid.GameFinished)
                    {
                        cellGrid.SyncCustomStateToGameOver();
                    }
                    else if (cellGrid.IsHumanTurn
                        && cellGrid.CurrentCustomState is Windy.Srpg.Game.Grid.States.CustomCellGridStateBlockInput)
                    {
                        cellGrid.EnterPostCombatGridState();
                    }
                }
            }

            Destroy(gameObject);
        }

        /// <summary>
        /// Method is called when unit is selected.
        /// </summary>
        public override void OnUnitSelected()
        {
            if (BelongsToCurrentGameplayPlayer())
            {
                SetTurnStateKind(UnitTurnStateKind.Selected);
            }

            GameplaySelected?.Invoke(this, EventArgs.Empty);
        }
        /// <summary>
        /// Method is called when unit is deselected.
        /// </summary>
        public override void OnUnitDeselected()
        {
            if (BelongsToCurrentGameplayPlayer())
            {
                SetTurnStateKind(IsFinishedForTurn ? UnitTurnStateKind.Finished : UnitTurnStateKind.Friendly);
            }

            GameplayDeselected?.Invoke(this, EventArgs.Empty);
        }

        private bool BelongsToCurrentGameplayPlayer()
        {
            CustomCellGrid cellGrid = FindAnyObjectByType<CustomCellGrid>();
            return cellGrid != null && cellGrid.GetCurrentPlayerCustomUnits().Contains(this);
        }
        #endregion

        #region CTRL+F: Combat Entry / Attack Sequence / Defense Resolution

        /// <summary>
        /// Method indicates if it is possible to attack a unit from given cell.
        /// </summary>
        /// <param name="other">CustomUnit to attack</param>
        /// <param name="sourceCell">Cell to perform an attack from</param>
        /// <returns>Boolean value whether unit can be attacked or not</returns>
        public virtual bool IsUnitAttackable(CustomUnit other, Cell sourceCell)
        {
            return IsUnitAttackable(other, other.Cell, sourceCell);
        }
        public virtual bool IsUnitAttackable(CustomUnit other, Cell otherCell, Cell sourceCell)
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


        /// <summary>
        /// Method performs an attack on given unit.
        /// </summary>
        public void AttackHandler(CustomUnit unitToAttack)
        {
            if (unitToAttack == null || IsAttackSequenceRunning || !HasUsableWeapon || !CanStartActionThisTurn)
            {
                return;
            }

            StartCoroutine(AttackSequenceRoutine(unitToAttack, BuildDefaultAttackProfile()));
        }

        public void AttackHandler(CustomUnit unitToAttack, ResolvedAttackProfile attackProfile)
        {
            if (unitToAttack == null || IsAttackSequenceRunning || !CanStartActionThisTurn)
            {
                return;
            }

            StartCoroutine(AttackSequenceRoutine(unitToAttack, attackProfile));
        }

        public void UseSupportSkill(CustomUnit primaryTarget, bool endsTurn, Action resolveEffect, SkillData skill = null, Windy.Srpg.Game.Grid.CustomCellGrid cellGrid = null)
        {
            if (IsAttackSequenceRunning || !CanStartActionThisTurn)
            {
                return;
            }

            StartCoroutine(SupportSkillRoutine(primaryTarget, endsTurn, resolveEffect, skill, cellGrid));
        }

        public void UseSupportSkill(CustomUnit primaryTarget, bool endsTurn, Action resolveEffect, SkillData skill = null, CellGrid cellGrid = null)
        {
            UseSupportSkill(primaryTarget, endsTurn, resolveEffect, skill, cellGrid as Windy.Srpg.Game.Grid.CustomCellGrid);
        }

        public void UseAreaSkill(IReadOnlyList<CustomUnit> targets, bool endsTurn, Action<CustomUnit> resolvePerTarget, SkillData skill = null, Windy.Srpg.Game.Grid.CustomCellGrid cellGrid = null)
        {
            if (IsAttackSequenceRunning || !CanStartActionThisTurn)
            {
                return;
            }

            StartCoroutine(AreaSkillRoutine(targets, endsTurn, resolvePerTarget, skill, cellGrid));
        }

        public void UseAreaSkill(IReadOnlyList<CustomUnit> targets, bool endsTurn, Action<CustomUnit> resolvePerTarget, SkillData skill = null, CellGrid cellGrid = null)
        {
            UseAreaSkill(targets, endsTurn, resolvePerTarget, skill, cellGrid as Windy.Srpg.Game.Grid.CustomCellGrid);
        }
        /// <summary>
        /// Method for calculating damage and action points cost of attacking given unit
        /// </summary>
        /// <returns></returns>
        protected virtual AttackAction DealDamage(CustomUnit unitToAttack)
        {
            return new AttackAction(Attack, 1f);
        }
        /// <summary>
        /// Method called after unit performed an attack.
        /// </summary>
        /// <param name="actionCost">Action point cost of performed attack</param>
        protected override void AttackActionPerformed(float actionCost)
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

        private IEnumerator AttackSequenceRoutine(CustomUnit unitToAttack, ResolvedAttackProfile attackProfile)
        {
            IsAttackSequenceRunning = true;
            bool sequenceStarted = false;
            CustomUnit experienceTarget = unitToAttack;
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

                if (experienceAward != null)
                {
                    yield return StartCoroutine(WaitForCombatHudToClose());
                    yield return StartCoroutine(PlayExperienceAwardSequence(experienceAward));
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
            }
        }

        private IEnumerator PlayAttackLungeAnimation(CustomUnit target)
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

        /// <summary>
        /// Handler method for defending against an attack.
        /// </summary>
        /// <param name="aggressor">CustomUnit that performed the attack</param>
        /// <param name="damage">Amount of damge that the attack caused</param>
        public int DefendHandler(CustomUnit aggressor, int damage, int aggressorHit, int aggressorCrit, bool isMagicAttack = false, bool isCounterAttack = false, bool simulateOnly = false)
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
                        DestroyedInCombat?.Invoke(this, new CustomUnitDestroyedEventArgs(aggressor, this, damageTaken));
                        CombatDestroyed?.Invoke(this, new AttackEventArgs(aggressor, this, damageTaken));
                        OnDestroyed();
                    }
                }
            }

            return damageTaken;
        }
        /// <summary>
        /// Method for calculating actual damage taken by the unit.
        /// </summary>
        /// <param name="aggressor">CustomUnit that performed the attack</param>
        /// <param name="damage">Amount of damge that the attack caused</param>
        /// <returns>Amount of damage that the unit has taken</returns>        
        protected virtual int Defend(CustomUnit aggressor, int damage)
        {
            return Mathf.Clamp(damage - Defense, 1, damage);
        }
        /// <summary>
        /// Method callef after unit performed defence.
        /// </summary>
        protected override void DefenceActionPerformed() { }

        public void RefreshHealthState(CustomUnit source = null)
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

            if (legacyBuffs == null)
            {
                yield break;
            }

            foreach (var (buff, timeLeft) in legacyBuffs)
            {
                if (buff == null)
                {
                    continue;
                }

                yield return buff.Duration < 0 ? buff.name : $"{buff.name} ({Mathf.Max(0, timeLeft)})";
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

            if (legacyBuffs != null)
            {
                foreach (var (buff, _) in legacyBuffs)
                {
                    if (buff == null)
                    {
                        continue;
                    }

                    lines.Add(buff.name);
                    lines.Add("Legacy buff");
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

        private void RaiseHealthChanged(int previousHitPoints, int currentHitPoints, CustomUnit source)
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

        private IEnumerator CounterAttack(CustomUnit aggressor, bool counterPrevented = false)
        {
            if (ShouldTriggerCounterAttack(aggressor, counterPrevented))
            {
                Debug.Log($"[Combat] {name} counterattacks {aggressor.name} after attack resolution. (defenderId={UnitID}, aggressorId={aggressor.UnitID})");
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
            }
            else
            {
                Debug.Log($"[Combat] {name} does not counterattack after attack resolution. (defenderId={UnitID}, canCounter={CanCounterAttack}, defenderDead={HitPoints <= 0}, aggressorDead={(aggressor == null || aggressor.HitPoints <= 0)}, aggressorInRange={IsAggressorInCounterRange(aggressor)}, counterPrevented={counterPrevented})");
            }
            yield break;
        }

        private bool ShouldTriggerCounterAttack(CustomUnit aggressor, bool counterPrevented = false)
        {
            return !counterPrevented
                && CanCounterAttack
                && HitPoints > 0
                && aggressor != null
                && aggressor.HitPoints > 0
                && IsAggressorInCounterRange(aggressor);
        }

        public bool CanCounterAttackAgainst(CustomUnit aggressor, bool counterPrevented = false)
        {
            return ShouldTriggerCounterAttack(aggressor, counterPrevented);
        }

        public bool CanPursuitAttackAgainst(CustomUnit other)
        {
            return other != null
                && CanPursuitAttack
                && HitPoints > 0
                && other.HitPoints > 0
                && Speed >= other.Speed + PursuitAttackSpeedThreshold;
        }

        private bool IsAggressorInCounterRange(CustomUnit aggressor)
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

        public int DryAttack(CustomUnit other)
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

        private static int ApplyDamageChangeModifiers(CustomUnit attacker, DamageChangeContext context, int currentDamage)
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

        private static int ApplyDamageTakenModifiers(CustomUnit defender, DamageChangeContext context, int currentDamage)
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

        private static int ApplyDamageMultipliers(CustomUnit attacker, DamageChangeContext context, int currentDamage)
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

        private static int ApplyTakeDamageMultipliers(CustomUnit defender, DamageChangeContext context, int currentDamage)
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

        private static void InvokeAfterCombatSequenceAsAttacker(CustomUnit attacker, CombatSequenceContext context)
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

        private IEnumerator SupportSkillRoutine(CustomUnit primaryTarget, bool endsTurn, Action resolveEffect, SkillData skill, Windy.Srpg.Game.Grid.CustomCellGrid cellGrid)
        {
            IsAttackSequenceRunning = true;
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
                    cellGrid as Windy.Srpg.Game.Grid.CustomCellGrid ?? FindSceneCellGrid(),
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
                    yield return StartCoroutine(PlayExperienceAwardSequence(experienceAward));
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
            }
        }

        private IEnumerator AreaSkillRoutine(IReadOnlyList<CustomUnit> targets, bool endsTurn, Action<CustomUnit> resolvePerTarget, SkillData skill, Windy.Srpg.Game.Grid.CustomCellGrid cellGrid)
        {
            IsAttackSequenceRunning = true;

            const float groupedStartDelaySeconds = 0.12f;
            const float groupedStepDelaySeconds = 0.18f;

            try
            {
                bool killedAtLeastOneTarget = false;
                var orderedTargets = targets?
                    .Where(target => target != null)
                    .Distinct()
                    .ToList() ?? new List<CustomUnit>();

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
                    CustomUnit target = orderedTargets[i];
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
                    cellGrid as Windy.Srpg.Game.Grid.CustomCellGrid ?? FindSceneCellGrid(),
                    skill,
                    killedAtLeastOneTarget);

                if (experienceAward != null)
                {
                    yield return StartCoroutine(WaitForCombatHudToClose());
                    yield return StartCoroutine(PlayExperienceAwardSequence(experienceAward));
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

        private Vector3 GetCombatFocusPosition(CustomUnit target)
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

        private static Vector3 GetAreaCombatFocusPosition(IReadOnlyList<CustomUnit> targets)
        {
            if (targets == null || targets.Count == 0)
            {
                return Vector3.zero;
            }

            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (CustomUnit target in targets)
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

        private static void InvokeBeforeCombatSequenceAsAttacker(CustomUnit attacker, CombatSequenceContext context)
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

        private static void InvokeBeforeCombatSequenceAsDefender(CustomUnit defender, CombatSequenceContext context)
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

        private static void InvokeAfterCombatSequenceAsDefender(CustomUnit defender, CombatSequenceContext context)
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

        /// <summary>
        /// Handler method for moving the unit.
        /// </summary>
        /// <param name="destinationCell">Cell to move the unit to</param>
        /// <param name="path">A list of cells, path from source to destination cell</param>
        public override IEnumerator Move(Cell destinationCell, IList<Cell> path)
        {
            if (destinationCell == null || path == null || path.Count == 0)
            {
                yield break;
            }

            Windy.Srpg.Game.Grid.CustomCellGrid cellGrid = FindSceneCellGrid();
            Cell fromCell = Cell;
            Cell resolvedStartCell = ResolveTransformStartCell(cellGrid, fromCell);
            if (resolvedStartCell != null && resolvedStartCell != fromCell)
            {
                fromCell?.CurrentUnits.Remove(this);
                RefreshLegacyCellOccupancy(fromCell);

                Cell = resolvedStartCell;
                if (!resolvedStartCell.CurrentUnits.Contains(this))
                {
                    resolvedStartCell.CurrentUnits.Add(this);
                }

                RefreshLegacyCellOccupancy(resolvedStartCell);
                SyncMirroredRuntimeCell(resolvedStartCell);
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

            BattleUnit runtimeMovementUnit = TryUseRuntimePathAuthority(out _, out BattleUnit runtimePathUnit)
                ? runtimePathUnit
                : null;

            if (runtimeMovementUnit != null
                && TryBuildRuntimeMovementPath(path, out var runtimeOrderedPath))
            {
                SyncMirroredRuntimeNow();

                BoardCell runtimeDestination = ResolveLinkedRuntimeCell(destinationCell);
                bool startedPendingMove = runtimeDestination != null
                    && runtimeMovementUnit.BeginPendingMove(runtimeDestination, runtimeOrderedPath);

                if (startedPendingMove)
                {
                    if (MovementAnimationSpeed > 0)
                    {
                        yield return StartCoroutine(
                            runtimeMovementUnit.AnimateAlongPathVisual(runtimeOrderedPath, MovementAnimationSpeed, IsSceneGrid2D()));
                    }
                    else
                    {
                        SnapToCellLocalPosition(destinationCell);
                    }

                    if (runtimeMovementUnit.ConfirmPendingMove(consumeAllRemainingMovement: false, syncTransform: false))
                    {
                        fromCell?.CurrentUnits.Remove(this);
                        RefreshLegacyCellOccupancy(fromCell);

                        Cell resolvedDestinationCell = ResolveLinkedLegacyCell(runtimeMovementUnit.CurrentCell) ?? destinationCell;
                        Cell = resolvedDestinationCell;
                        if (resolvedDestinationCell != null && !resolvedDestinationCell.CurrentUnits.Contains(this))
                        {
                            resolvedDestinationCell.CurrentUnits.Add(this);
                        }

                        MovementPoints = runtimeMovementUnit.MovementPointsRemaining;
                        cachedPaths = null;
                        RefreshLegacyCellOccupancy(resolvedDestinationCell);
                        SyncMirroredRuntimeCell(resolvedDestinationCell);
                        RefreshSceneOccupancyFromLiveUnits();
                        cellGrid?.RequestBattleOutcomeEvaluation();
                        yield break;
                    }

                    runtimeMovementUnit.CancelPendingMove();
                }
            }

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

            fromCell?.CurrentUnits.Remove(this);
            RefreshLegacyCellOccupancy(fromCell);

            Cell = destinationCell;
            if (destinationCell != null && !destinationCell.CurrentUnits.Contains(this))
            {
                destinationCell.CurrentUnits.Add(this);
            }

            cachedPaths = null;
            RefreshLegacyCellOccupancy(destinationCell);
            SyncMirroredRuntimeCell(destinationCell);
            RefreshSceneOccupancyFromLiveUnits();
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
            if (TryUseRuntimeMovementAuthority(out CustomCellGrid cellGrid, out BattleUnit runtimeUnit)
                && TryCommitPendingMoveViaRuntime(p, runtimeUnit, cellGrid, consumeAllRemainingMovement))
            {
                _pendingMove = null;
                return true;
            }

            bool isStayingInPlace = p.ToCell == p.FromCell;
            if (!isStayingInPlace && !CanOccupyLegacyCell(p.ToCell))
            {
                CancelPendingMove();
                return false;
            }

            MovementPoints = consumeAllRemainingMovement
                ? 0f
                : Mathf.Max(0f, p.MovementPointsBefore - p.MovementCost);

            p.FromCell?.CurrentUnits.Remove(this);
            RefreshLegacyCellOccupancy(p.FromCell);

            Cell = p.ToCell;
            if (p.ToCell != null && !p.ToCell.CurrentUnits.Contains(this))
            {
                p.ToCell.CurrentUnits.Add(this);
            }

            RefreshLegacyCellOccupancy(p.ToCell);
            SyncMirroredRuntimeCell(p.ToCell);
            cachedPaths = null;
            RefreshSceneOccupancyFromLiveUnits();

            OnMoveFinished();
            FindSceneCellGrid()?.RequestBattleOutcomeEvaluation();

            _pendingMove = null;
            return true;
        }

        private bool TryCommitPendingMoveViaRuntime(
            PendingMove pendingMove,
            BattleUnit runtimeUnit,
            CustomCellGrid cellGrid,
            bool consumeAllRemainingMovement)
        {
            if (runtimeUnit == null)
            {
                return false;
            }

            BoardCell runtimeDestination = ResolveLinkedRuntimeCell(pendingMove.ToCell);
            if (runtimeDestination == null)
            {
                return false;
            }

            if (pendingMove.FromCell != null)
            {
                SyncMirroredRuntimeCell(pendingMove.FromCell);
            }
            else
            {
                ClearMirroredRuntimeCell();
            }

            bool startedPendingMove;
            if (pendingMove.ToCell == pendingMove.FromCell)
            {
                startedPendingMove = runtimeUnit.BeginPendingMoveInPlace();
            }
            else if (TryBuildRuntimeMovementPath(pendingMove.Path, out var runtimePath))
            {
                startedPendingMove = runtimeUnit.BeginPendingMove(runtimeDestination, runtimePath);
            }
            else
            {
                return false;
            }

            if (!startedPendingMove || !runtimeUnit.ConfirmPendingMove(consumeAllRemainingMovement, syncTransform: false))
            {
                return false;
            }

            Cell fromCell = pendingMove.FromCell;
            fromCell?.CurrentUnits.Remove(this);
            RefreshLegacyCellOccupancy(fromCell);

            Cell resolvedDestinationCell = ResolveLinkedLegacyCell(runtimeUnit.CurrentCell) ?? pendingMove.ToCell;
            Cell = resolvedDestinationCell;
            if (resolvedDestinationCell != null && !resolvedDestinationCell.CurrentUnits.Contains(this))
            {
                resolvedDestinationCell.CurrentUnits.Add(this);
            }

            MovementPoints = runtimeUnit.MovementPointsRemaining;
            cachedPaths = null;
            RefreshLegacyCellOccupancy(resolvedDestinationCell);
            RefreshSceneOccupancyFromLiveUnits();
            OnMoveFinished();
            cellGrid?.RequestBattleOutcomeEvaluation();
            return true;
        }

        public virtual bool CancelPendingMove()
        {
            if (!_pendingMove.HasValue)
                return false;

            ResolveRuntimeUnit()?.CancelPendingMove();

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

            if (TryUseRuntimeMovementAuthority(out _, out BattleUnit runtimeUnit))
            {
                runtimeUnit.BeginPendingMoveInPlace();
            }

            return true;
        }

        protected virtual IEnumerator PreviewMovementAnimation(IList<Cell> path, int previewMoveVersion)
        {
            Windy.Srpg.Game.Grid.CustomCellGrid runtimeMovementGrid = FindSceneCellGrid();
            BattleUnit runtimeMovementUnit = (runtimeMovementGrid != null
                && runtimeMovementGrid.UseRuntimeMovementExecution
                && runtimeMovementGrid.IsHumanTurn)
                ? GetComponent<BattleUnit>()
                : null;

            if (runtimeMovementUnit != null && MovementAnimationSpeed > 0
                && TryBuildRuntimeMovementPath(path, out var runtimeOrderedPath))
            {
                yield return StartCoroutine(runtimeMovementUnit.AnimateAlongPathVisual(
                    runtimeOrderedPath,
                    MovementAnimationSpeed,
                    IsSceneGrid2D(),
                    () => previewMoveVersion != _previewMoveVersion || !_pendingMove.HasValue,
                    worldPosition => PreviewMoveCameraFollowRequested?.Invoke(worldPosition)));
                yield break;
            }

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
            Windy.Srpg.Game.Grid.CustomCellGrid cellGrid = FindSceneCellGrid();
            bool isMap2D = cellGrid != null && cellGrid.Is2D;
            Vector3 destinationPos = isMap2D
                ? new Vector3(destinationCell.transform.localPosition.x, destinationCell.transform.localPosition.y, transform.localPosition.z)
                : destinationCell.transform.localPosition;
            transform.localPosition = destinationPos;
        }

        private Cell ResolveTransformStartCell(Windy.Srpg.Game.Grid.CustomCellGrid cellGrid, Cell fallbackCell)
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

        protected override IEnumerator MovementAnimation(IList<Cell> path)
        {
            yield return AnimateMovementPath(path);
            OnMoveFinished();
        }
        /// <summary>
        /// Method called after movement animation has finished.
        /// </summary>
        protected override void OnMoveFinished() {
        
        }

        ///<summary>
        /// Method indicates if unit is capable of moving to cell given as parameter.
        /// </summary>
        public override bool IsCellMovableTo(Cell cell)
        {
            return CanOccupyLegacyCell(cell);
        }
        /// <summary>
        /// Method indicates if unit is capable of moving through cell given as parameter.
        /// </summary>
        public override bool IsCellTraversable(Cell cell)
        {
            return CanTraverseLegacyCell(cell);
        }
        /// <summary>
        /// Method returns all cells that the unit is capable of moving to.
        /// </summary>
        public new HashSet<Cell> GetAvailableDestinations(List<Cell> cells)
        {
            if (TryUseRuntimePathAuthority(out _, out BattleUnit runtimeUnit))
            {
                SyncMirroredRuntimeNow();
                List<BoardCell> runtimeCells = cells?
                    .Select(ResolveLinkedRuntimeCell)
                    .Where(cell => cell != null)
                    .ToList() ?? new List<BoardCell>();
                HashSet<BoardCell> runtimeReachable = runtimeUnit.GetAvailableDestinations(runtimeCells);
                var runtimeDestinations = new HashSet<Cell>();
                foreach (BoardCell runtimeCell in runtimeReachable)
                {
                    Cell legacyCell = ResolveLinkedLegacyCell(runtimeCell);
                    if (legacyCell != null)
                    {
                        runtimeDestinations.Add(legacyCell);
                    }
                }

                return runtimeDestinations;
            }

            CachePaths(cells);

            var availableDestinations = new HashSet<Cell>();
            foreach (var cell in cells.Where(c => IsCellMovableTo(c)))
            {
                if(cachedPaths.TryGetValue(cell, out var path))
                {
                    var pathCost = path.Sum(c => c.MovementCost);
                    if (pathCost <= MovementPoints)
                    {
                        availableDestinations.Add(cell);
                    }
                }
            }

            return availableDestinations;
        }

        public new void CachePaths(List<Cell> cells)
        {
            if (TryUseRuntimePathAuthority(out _, out BattleUnit runtimeUnit))
            {
                SyncMirroredRuntimeNow();
                List<BoardCell> runtimeCells = cells?
                    .Select(ResolveLinkedRuntimeCell)
                    .Where(cell => cell != null)
                    .ToList() ?? new List<BoardCell>();
                runtimeUnit.CachePaths(runtimeCells);
                cachedPaths = null;
                return;
            }

            cachedPaths = BuildCurrentPaths(cells);
        }

        public new IList<Cell> FindPath(List<Cell> cells, Cell destination)
        {
            if (TryUseRuntimePathAuthority(out _, out BattleUnit runtimeUnit))
            {
                SyncMirroredRuntimeNow();
                List<BoardCell> runtimeCells = cells?
                    .Select(ResolveLinkedRuntimeCell)
                    .Where(cell => cell != null)
                    .ToList() ?? new List<BoardCell>();
                BoardCell runtimeDestination = ResolveLinkedRuntimeCell(destination);
                if (runtimeDestination != null
                    && TryBuildLegacyMovementPath(runtimeUnit.FindPath(runtimeCells, runtimeDestination), out List<Cell> runtimePath))
                {
                    return runtimePath;
                }

                return new List<Cell>();
            }

            if (cachedPaths == null)
            {
                CachePaths(cells);
            }

            if (cachedPaths.TryGetValue(destination, out var path))
            {
                return path;
            }
            return new List<Cell>();
        }

        private Dictionary<Cell, IList<Cell>> BuildCurrentPaths(List<Cell> cells)
        {
            if (cells == null || Cell == null)
            {
                return new Dictionary<Cell, IList<Cell>>();
            }

            var edges = GetGraphEdges(cells);
            return _pathfinder.FindAllPaths(edges, Cell);
        }

        private bool CanOccupyLegacyCell(Cell cell)
        {
            if (cell == null || cell == Cell)
            {
                return false;
            }

            if (!IsLinkedBoardCellTraversable(cell))
            {
                return false;
            }

            return !cell.IsTaken && !HasBlockingOccupant(cell) && !HasBlockingRuntimeOccupant(cell);
        }

        private bool CanTraverseLegacyCell(Cell cell)
        {
            if (cell == null)
            {
                return false;
            }

            if (!IsLinkedBoardCellTraversable(cell))
            {
                return false;
            }

            if (cell == Cell)
            {
                return true;
            }

            return !cell.IsTaken && !HasBlockingOccupant(cell) && !HasBlockingRuntimeOccupant(cell);
        }

        private bool HasBlockingOccupant(Cell cell)
        {
            if (cell?.CurrentUnits == null)
            {
                return HasBlockingSceneOccupant(cell, this);
            }

            foreach (Unit occupant in cell.CurrentUnits)
            {
                if (occupant == null || occupant == this || !occupant.Obstructable || occupant.ExcludedFromBattle)
                {
                    continue;
                }

                return true;
            }

            return HasBlockingSceneOccupant(cell, this);
        }

        /// <summary>
        /// Method returns graph representation of cell grid for pathfinding.
        /// </summary>
        protected override Dictionary<Cell, Dictionary<Cell, float>> GetGraphEdges(List<Cell> cells)
        {
            Dictionary<Cell, Dictionary<Cell, float>> ret = new Dictionary<Cell, Dictionary<Cell, float>>();
            foreach (var cell in cells)
            {
                if (IsCellTraversable(cell) || cell == Cell)
                {
                    ret[cell] = new Dictionary<Cell, float>();
                    foreach (var neighbour in cell.GetNeighbours(cells))
                    {
                        if(IsCellTraversable(neighbour) || IsCellMovableTo(neighbour))
                        {
                            ret[cell][neighbour] = neighbour.MovementCost;
                        }
                    }
                }
            }
            return ret;
        }

        #endregion

        #region CTRL+F: Visual Marking / Editor Helpers / Auto-Setup

        /// <summary>
        /// Gives visual indication that the unit is under attack.
        /// </summary>
        /// <param name="aggressor">
        /// CustomUnit that is attacking.
        /// </param>
        public virtual void MarkAsDefending(CustomUnit aggressor)
        {
            if (UnitHighlighterAggregator != null)
            {
                UnitHighlighterAggregator.MarkAsDefendingFn?.ForEach(o => o.Apply(this, aggressor));
            }
        }
        /// <summary>
        /// Gives visual indication that the unit is attacking.
        /// </summary>
        /// <param name="target">
        /// CustomUnit that is under attack.
        /// </param>
        public virtual void MarkAsAttacking(CustomUnit target)
        {
            if (UnitHighlighterAggregator != null)
            {
                UnitHighlighterAggregator.MarkAsAttackingFn?.ForEach(o => o.Apply(this, target));
            }
        }
        /// <summary>
        /// Gives visual indication that the unit is destroyed. It gets called right before the unit game object is
        /// destroyed.
        /// </summary>
        public override void MarkAsDestroyed()
        {
            if (UnitHighlighterAggregator != null)
            {
                UnitHighlighterAggregator.MarkAsDestroyedFn?.ForEach(o => o.Apply(this, null));
            }
        }

        /// <summary>
        /// Method marks unit as current players unit.
        /// </summary>
        public override void MarkAsFriendly()
        {
            if (UnitHighlighterAggregator != null)
            {
                UnitHighlighterAggregator.MarkAsFriendlyFn?.ForEach(o => o.Apply(this, null));
            }
        }
        /// <summary>
        /// Method mark units to indicate user that the unit is in range and can be attacked.
        /// </summary>
        public override void MarkAsReachableEnemy()
        {
            if (UnitHighlighterAggregator != null)
            {
                UnitHighlighterAggregator.MarkAsReachableEnemyFn?.ForEach(o => o.Apply(this, null));
            }
        }
        /// <summary>
        /// Method marks unit as currently selected, to distinguish it from other units.
        /// </summary>
        public override void MarkAsSelected()
        {
            if (UnitHighlighterAggregator != null)
            {
                UnitHighlighterAggregator.MarkAsSelectedFn?.ForEach(o => o.Apply(this, null));
            }
        }
        /// <summary>
        /// Method marks unit to indicate user that he can't do anything more with it this turn.
        /// </summary>
        public override void MarkAsFinished()
        {
            if (UnitHighlighterAggregator != null)
            {
                UnitHighlighterAggregator.MarkAsFinishedFn?.ForEach(o => o.Apply(this, null));
            }
        }
        /// <summary>
        /// Method returns the unit to its base appearance
        /// </summary>
        public override void UnMark()
        {
            if (UnitHighlighterAggregator != null)
            {
                UnitHighlighterAggregator.UnMarkFn?.ForEach(o => o.Apply(this, null));
            }
        }
        public override void SetColor(Color color) { }

        [ExecuteInEditMode]
        public new void OnDestroy()
        {
            #if UNITY_EDITOR
            if (Cell != null && !Application.isPlaying)
            {
                Cell.IsTaken = false;
                UnityEditor.EditorUtility.SetDirty(Cell);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            }
            #endif
        }

        private void Reset()
        {
            if (GetComponent<CustomAttackAbility>() == null)
            {
                gameObject.AddComponent<CustomAttackAbility>();
            }
            if (GetComponent<CustomMoveAbility>() == null)
            {
                gameObject.AddComponent<CustomMoveAbility>();
            }
            if (GetComponent<CustomAttackRangeHighlightAbility>() == null)
            {
                gameObject.AddComponent<CustomAttackRangeHighlightAbility>();
            }

            GameObject brain = new GameObject("Brain");
            brain.transform.parent = transform;

            brain.AddComponent<CustomMoveToPositionAIAction>();
            brain.AddComponent<CustomAttackAIAction>();

            brain.AddComponent<DamageCellEvaluator>();
            brain.AddComponent<DamageUnitEvaluator>();
        }

        #endregion
    }

    #region CTRL+F: Combat Data / Hook Interfaces
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
        public CustomUnit Attacker;
        public CustomUnit Defender;
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
        public CustomUnit Attacker;
        public CustomUnit Defender;

        public CombatSequenceContext(CustomUnit attacker, CustomUnit defender)
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

    #endregion

    #region CTRL+F: Event Args

    public class UnitHealthChangedEventArgs : EventArgs
    {
        public CustomUnit Source;
        public CustomUnit CustomUnit;
        public int PreviousHitPoints;
        public int CurrentHitPoints;
        public int Delta;

        public UnitHealthChangedEventArgs(CustomUnit source, CustomUnit unit, int previousHitPoints, int currentHitPoints)
        {
            Source = source;
            CustomUnit = unit;
            PreviousHitPoints = previousHitPoints;
            CurrentHitPoints = currentHitPoints;
            Delta = currentHitPoints - previousHitPoints;
        }
    }

    public class CombatSequenceEventArgs : EventArgs
    {
        public CustomUnit Attacker;
        public CustomUnit Defender;

        public CombatSequenceEventArgs(CustomUnit attacker, CustomUnit defender)
        {
            Attacker = attacker;
            Defender = defender;
        }
    }

    public class CustomUnitDestroyedEventArgs : EventArgs
    {
        public CustomUnit Attacker;
        public CustomUnit Defender;
        public int Damage;

        public CustomUnitDestroyedEventArgs(CustomUnit attacker, CustomUnit defender, int damage)
        {
            Attacker = attacker;
            Defender = defender;
            Damage = damage;
        }
    }

    #endregion

    #region CTRL+F: Experience Data / EXP Calculator / EXP Hooks

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
        public CustomUnit Recipient;
        public CustomUnit PrimaryTarget;
        public IReadOnlyList<CustomUnit> Targets;
        public Windy.Srpg.Game.Grid.CustomCellGrid CellGrid;
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

        public static int CalculateEnemyCombatExp(CustomUnit user, CustomUnit enemy, bool isLethal)
        {
            return CalculateEnemyCombatExp(user, enemy != null ? enemy.Level : 0, isLethal);
        }

        public static int CalculateEnemyCombatExp(CustomUnit user, int enemyLevel, bool isLethal)
        {
            if (user == null || enemyLevel <= 0)
            {
                return 0;
            }

            int baseAmount = isLethal ? EnemyTargetLethalBase : EnemyTargetNonLethalBase;
            float rawAmount = baseAmount * (1f + enemyLevel - user.Level);
            return ClampExperienceGain(user, rawAmount);
        }

        public static int CalculateAllySkillExp(CustomUnit user, Windy.Srpg.Game.Grid.CustomCellGrid cellGrid)
        {
            if (user == null)
            {
                return 0;
            }

            float averageEnemyLevel = GetAverageEnemyLevel(user, cellGrid);
            float rawAmount = AllySkillBase * (1f + averageEnemyLevel - user.Level);
            return ClampExperienceGain(user, rawAmount);
        }

        public static int CalculateAreaSkillExp(CustomUnit user, IReadOnlyList<CustomUnit> targets, Windy.Srpg.Game.Grid.CustomCellGrid cellGrid, SkillData skill, bool killedAtLeastOneTarget)
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

        public static float GetAverageEnemyLevel(CustomUnit user, Windy.Srpg.Game.Grid.CustomCellGrid cellGrid)
        {
            if (user == null)
            {
                return user != null ? user.Level : 1f;
            }

            List<CustomUnit> enemyUnits = cellGrid?.GetAllCustomUnits()
                .Where(unit => unit != null && unit.PlayerNumber != user.PlayerNumber)
                .ToList()
                ?? new List<CustomUnit>();

            if (enemyUnits.Count == 0)
            {
                return user.Level;
            }

            return (float)enemyUnits.Average(unit => unit.Level);
        }

        public static float GetAverageTargetLevel(IReadOnlyList<CustomUnit> targets)
        {
            if (targets == null)
            {
                return 1f;
            }

            List<CustomUnit> validTargets = targets
                .Where(target => target != null)
                .Distinct()
                .ToList();

            if (validTargets.Count == 0)
            {
                return 1f;
            }

            return (float)validTargets.Average(target => target.Level);
        }

        public static int ClampExperienceGain(CustomUnit recipient, float rawAmount)
        {
            if (recipient == null || recipient.Level >= MaxLevel)
            {
                return 0;
            }

            int flooredAmount = Mathf.FloorToInt(rawAmount);
            return Mathf.Clamp(flooredAmount, MinGain, MaxGain);
        }

        private static int CalculateAreaEnemyExp(CustomUnit user, IReadOnlyList<CustomUnit> targets, bool killedAtLeastOneTarget)
        {
            float averageTargetLevel = GetAverageTargetLevel(targets);
            int baseAmount = killedAtLeastOneTarget ? AreaEnemyBase * 3 : AreaEnemyBase;
            float rawAmount = baseAmount * (1f + averageTargetLevel - user.Level);
            return ClampExperienceGain(user, rawAmount);
        }

        private static int CalculateAreaAnyExp(CustomUnit user, IReadOnlyList<CustomUnit> targets, bool killedAtLeastOneTarget)
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

    #endregion

}
