using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using Windy.Srpg.Game.Inventory;
using Windy.Srpg.Game.Skills;
using Windy.Srpg.Game.Buffs;
using Windy.Srpg.Game.Passives;
using Windy.Srpg.Game.Abilities;
using Windy.Srpg.Game.AI.Actions;
using Windy.Srpg.Game.AI.Evaluators;
using Windy.Srpg.Game.Campaign;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Pathfinding.Algorithms;
using UnityEngine.Serialization;
using RuntimeBuff = Windy.Srpg.Game.Buffs.Buff;


namespace Windy.Srpg.Game.Units
{
    /// <summary>
    /// Owned unit data and gameplay behavior.
    /// This class is intentionally split across multiple files by responsibility.
    /// </summary>
    [ExecuteInEditMode]
    public partial class Unit : MonoBehaviour
    {
// Search for "CTRL+F:" to jump between major gameplay systems in this file.
        #region CTRL+F: Events / Runtime State / Serialized Fields
        internal Dictionary<Cell, IList<Cell>> cachedPaths = null;
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
        internal bool hasInitializedTurnState;
        public UnitTurnStateKind CurrentTurnStateKind => currentTurnStateKind;
        public bool HasInitializedTurnState => hasInitializedTurnState;
        public bool IsSelectedForTurn => currentTurnStateKind == UnitTurnStateKind.Selected;
        public bool IsReachableEnemyForTurn => currentTurnStateKind == UnitTurnStateKind.ReachableEnemy;
        public bool IsFriendlyForTurn => currentTurnStateKind == UnitTurnStateKind.Friendly;
        public bool IsFinishedForTurn => currentTurnStateKind == UnitTurnStateKind.Finished;
        public bool CanStartActionThisTurn => !IsFinishedForTurn;

        internal int customTotalHitPoints;
        internal int customTotalManaPoints;
        internal float customTotalMovementPoints;
        internal UnitTurnStateKind currentTurnStateKind = UnitTurnStateKind.Normal;
        public int ComputedTotalHitPoints
        {
            get => customTotalHitPoints;
            internal set => customTotalHitPoints = value;
        }
        public int ComputedTotalManaPoints
        {
            get => customTotalManaPoints;
            internal set => customTotalManaPoints = value;
        }
        public float ComputedTotalMovementPoints
        {
            get => customTotalMovementPoints;
            internal set => customTotalMovementPoints = value;
        }

        [SerializeField]
        internal int baseHitPoints = 1;
        [SerializeField]
        internal int baseManaPoints = 9;
        [SerializeField]
        internal int level = 1;
        [SerializeField]
        internal int experience = 0;
        public string unitName = "Ally";
        [Header("Unit Preset")]
        [SerializeField, FormerlySerializedAs("enemyPreset")] internal UnitPreset preset;
        [SerializeField, FormerlySerializedAs("enemyPresetOverride")] internal UnitPresetOverride presetOverride = new UnitPresetOverride();
        [Header("Save Identity")]
        [SerializeField] internal string unitId = string.Empty;
        [SerializeField] internal string visualId = string.Empty;
        [SerializeField]
        internal List<StartingInventoryItem> startingInventory = new List<StartingInventoryItem>();
        [SerializeField]
        internal List<StartingSkillEntry> startingSkills = new List<StartingSkillEntry>();
        [SerializeField]
        internal List<StartingPassiveEntry> startingUniquePassives = new List<StartingPassiveEntry>();
        [SerializeField]
        internal List<StartingPassiveEntry> startingEquipPassives = new List<StartingPassiveEntry>();
        [SerializeField]
        internal WeaponType weaponProficiencies = WeaponType.Sword | WeaponType.Lance | WeaponType.Blunt | WeaponType.Ranged | WeaponType.Magic;
        [SerializeField]
        internal UnitActionAiMode actionAiMode = UnitActionAiMode.Attack;
        [SerializeField]
        internal UnitMovementAiMode movementAiMode = UnitMovementAiMode.Move;
        [SerializeField]
        internal int waitGroupId;
        [NonSerialized]
        internal bool aiWaitTriggered;
        [SerializeField]
        internal int baseStrength;
        [SerializeField]
        internal int baseDefense;
        [SerializeField]
        internal int baseMagic;
        [SerializeField]
        internal int baseResistance;
        [SerializeField]
        internal int baseSpeed;
        internal int PursuitAttackSpeedThreshold = 5;
        internal float attackHitPauseSeconds = 0.25f;
        internal float combatSequenceStartDelaySeconds = 0.25f;
        public bool IsAttackSequenceRunning { get; internal set; } = false;

        internal static int activeCombatPresentationDepth;

        public static bool IsAnyCombatPresentationActive => activeCombatPresentationDepth > 0;


        [SerializeField]
        internal int baseLuck;
        [SerializeField] internal int growthStrength = 17;
        [SerializeField] internal int growthMagic = 17;
        [SerializeField] internal int growthDefense = 17;
        [SerializeField] internal int growthResistance = 17;
        [SerializeField] internal int growthSpeed = 16;
        [SerializeField] internal int growthLuck = 16;
        public UnitInventory Inventory { get; internal set; }
        public UnitSkillList SkillList { get; internal set; }
        public UnitBuffList BuffList { get; internal set; }
        public UnitPassiveList PassiveList { get; internal set; }
        public WeaponData EquippedWeapon => GetActiveWeapon();
        public AccessoryData EquippedAccessory => Inventory?.EquippedAccessory;
        public virtual WeaponType WeaponProficiencies => weaponProficiencies;
        public UnitActionAiMode ActionAiMode => actionAiMode;
        public UnitMovementAiMode MovementAiMode => movementAiMode;
        public int WaitGroupId => Mathf.Max(0, waitGroupId);
        public bool IsAiWaitTriggered => aiWaitTriggered;
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
        public int CurrentManaPoints { get; internal set; }
        public int Level => Mathf.Clamp(level, 1, ExperienceCalculator.MaxLevel);
        public int Experience => Level >= ExperienceCalculator.MaxLevel ? 0 : Mathf.Clamp(experience, 0, ExperienceCalculator.MaxGain - 1);
        public virtual bool CanGainExperience => PlayerNumber == 0;
        public int PlayerId => PlayerNumber;
        public string UnitId => unitId;
        public string VisualId => visualId;

        internal bool presetAppliedAtRuntime;
        internal bool useResolvedPresetLoadout;
        internal List<StartingInventoryItem> resolvedStartingInventory = new List<StartingInventoryItem>();
        internal List<StartingSkillEntry> resolvedStartingSkills = new List<StartingSkillEntry>();
        internal List<StartingPassiveEntry> resolvedStartingUniquePassives = new List<StartingPassiveEntry>();
        internal List<StartingPassiveEntry> resolvedStartingEquipPassives = new List<StartingPassiveEntry>();
        internal SecondaryStatModifiers resolvedSecondaryStatOffsets;
        [NonSerialized] internal OwnedUnitSaveData pendingOwnedUnitSaveData;
        [NonSerialized] internal UnitPreset pendingOwnedUnitVisualPreset;
        [SerializeField, HideInInspector] internal bool spriteLayoutBaselineCaptured;
        [SerializeField, HideInInspector] internal Vector3 spriteLayoutBaselineLocalScale = Vector3.one;
        [SerializeField, HideInInspector] internal Vector3 spriteLayoutBaselineLocalPosition = new Vector3(0f, 0f, -0.1f);
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




        private static readonly DijkstraPathfinding Pathfinder = new DijkstraPathfinding();













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















        #endregion

        #region CTRL+F: Mana / Progression / Equipment Utility







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











        public bool CanEquipWeapon(WeaponData weapon)
        {
            if (weapon == null)
            {
                return false;
            }

            WeaponType requiredType = weapon.WeaponType == WeaponType.None ? WeaponType.Sword : weapon.WeaponType;
            return (WeaponProficiencies & requiredType) != 0;
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



        #endregion

        #region CTRL+F: Health / Displacement / Turn End








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

        internal bool pendingDeferredDestroy;







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

// --- Scene binding layer ---
        public event EventHandler UnitClicked;
        public event EventHandler UnitHighlighted;
        public event EventHandler UnitDehighlighted;
        public event EventHandler UnitSelected;
        public event EventHandler UnitDeselected;
        public event EventHandler<AttackEventArgs> UnitDestroyed;

        public int UnitID { get; set; }
        public bool Obstructable = true;

        [SerializeField, HideInInspector]
        internal bool excludedFromBattle;

        [SerializeField]
        internal bool participatesInDeploymentRoster = true;

        [SerializeField]
        internal bool includeInOwnedUnitSave = true;

        public bool ExcludedFromBattle
        {
            get => excludedFromBattle;
            set => excludedFromBattle = value;
        }

        public bool ParticipatesInDeploymentRoster
        {
            get => participatesInDeploymentRoster;
            set => participatesInDeploymentRoster = value;
        }

        public bool IncludeInOwnedUnitSave
        {
            get => includeInOwnedUnitSave;
            set => includeInOwnedUnitSave = value;
        }

        [SerializeField, HideInInspector]
        internal Cell cell;

        public Cell Cell
        {
            get => cell;
            set => cell = value;
        }

        [SerializeField]
        internal float movementPointsStorage;

        public int PlayerNumber;
        public float MovementAnimationSpeed;

        public virtual float MovementPoints
        {
            get => movementPointsStorage;
            set => movementPointsStorage = value;
        }
















// Uses the legacy path convention (destination-first, origin excluded) so the values
        // returned by BuildScenePaths match what Move/PreviewMove/AnimateMovementPath expect.
        private static readonly DijkstraPathfinding ScenePathfinder = new DijkstraPathfinding();

        #region CTRL+F: Combat Entry / Attack Sequence / Defense Resolution












        #endregion

        #region CTRL+F: Buff Display / Event Dispatch / EXP Gain Pipeline







        #endregion

        #region CTRL+F: Counterattacks / Damage Hooks / Skill Resolution / Camera





















        #endregion

        #region CTRL+F: Movement / Pending Move Preview / Pathfinding


        // CTRL+F: PENDING MOVE
        internal PendingMove? _pendingMove;
        internal int _previewMoveVersion;

        internal struct PendingMove
        {
            public Cell FromCell;
            public Cell ToCell;
            public IList<Cell> Path;
            public float MovementPointsBefore;
            public float MovementCost;
            public Vector3 FromLocalPos;
        }

        public bool HasPendingMove => _pendingMove.HasValue;
        public Cell PreviewCell
        {
            get
            {
                return _pendingMove.HasValue ? _pendingMove.Value.ToCell : Cell;
            }
        }



























        #endregion







        internal ExperienceAwardResult _queuedDeferredExperienceAward;






    }

}
