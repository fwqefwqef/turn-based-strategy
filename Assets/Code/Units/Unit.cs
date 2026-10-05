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
using RuntimeBuff = Windy.Srpg.Game.Buffs.Buff;


namespace Windy.Srpg.Game.Units
{
    public enum PendingUnitOperation
    {
        Movement,
        Overcharge
    }

    /// <summary>
    /// Owned unit data and gameplay behavior.
    /// This class is intentionally split across multiple files by responsibility.
    /// </summary>
    [ExecuteInEditMode]
    public partial class Unit : MonoBehaviour
    {
        public const string DeathsDoorBuffId = "death_door";
        private const bool AlliesUseDeathsDoorByDefault = true;
        private const bool EnemiesUseDeathsDoorByDefault = false;

        internal static bool AlliesUseDeathsDoor => AlliesUseDeathsDoorByDefault;
        internal static bool EnemiesUseDeathsDoor => EnemiesUseDeathsDoorByDefault;

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
        public static event Action<Vector3> EnemyMovementCameraFollowRequested;
        public static event Action EnemyMovementCameraFollowReleased;
        internal bool hasInitializedTurnState;
        public UnitTurnStateKind CurrentTurnStateKind => currentTurnStateKind;
        public bool HasInitializedTurnState => hasInitializedTurnState;
        public bool IsSelectedForTurn => currentTurnStateKind == UnitTurnStateKind.Selected;
        public bool IsReachableEnemyForTurn => currentTurnStateKind == UnitTurnStateKind.ReachableEnemy;
        public bool IsFriendlyForTurn => currentTurnStateKind == UnitTurnStateKind.Friendly;
        public bool IsFinishedForTurn => currentTurnStateKind == UnitTurnStateKind.Finished;
        public bool IsActionBlocked => BuffList != null && BuffList.GetActiveEffects().Any(effect => effect is IP_ActionBlocker);
        public bool HasRemovableDebuffs => BuffList != null && BuffList.Entries.Any(entry => entry.Removable && entry.Category != BuffCategory.Buff);
        public bool HasVantage => PassiveList != null && PassiveList.GetActiveEffects().Any(effect => effect is IP_Vantage);
        public bool PreventsCounterattackOnInitiate => PassiveList != null && PassiveList.GetActiveEffects().Any(effect => effect is IP_PreventCounterattackOnInitiate);
        public bool IsAtDeathsDoor => BuffList != null && BuffList.HasBuff(DeathsDoorBuffId);
        public bool IsAliveForBattle => HitPoints > 0 || IsAtDeathsDoor;
        public bool AttacksCannotMiss => PassiveList?.GetActiveEffects().Any(effect => effect is IP_AttackNeverMisses) == true;
        public bool CanStartActionThisTurn => !IsFinishedForTurn && !IsActionBlocked;

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
            get
            {
                float total = customTotalMovementPoints + (PassiveList?.GetMovementPointModifier() ?? 0f);
                return BuffList != null ? BuffList.ApplyMovementPointCaps(total) : total;
            }
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
        [SerializeField] internal UnitPreset preset;
        [SerializeField] private UnitPresetOverride presetOverrides = new UnitPresetOverride();
        public UnitPreset AssignedPreset => preset;
        public UnitPresetOverride PresetOverrides => presetOverrides ??= new UnitPresetOverride();
        [Header("Save Identity")]
        [SerializeField] internal string unitId = string.Empty;
        [SerializeField] internal string visualId = string.Empty;
        [SerializeField]
        internal WeaponProficiency weaponProficiencies = WeaponProficiency.Melee | WeaponProficiency.Ranged | WeaponProficiency.Magic;
        [SerializeField]
        internal UnitActionAiMode actionAiMode = UnitActionAiMode.Attack;
        [SerializeField]
        internal UnitMovementAiMode movementAiMode = UnitMovementAiMode.Move;
        [SerializeField]
        internal int waitGroupId;
        internal List<Vector2Int> aiGoalTiles = new List<Vector2Int>();
        [NonSerialized]
        internal bool aiWaitTriggered;
        [NonSerialized]
        internal bool aiGoalReached;
        [SerializeField]
        internal int baseStrength;
        [SerializeField]
        internal int baseDefense;
        [SerializeField]
        internal int baseMagic;
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
        [SerializeField] internal int growthStrength = 20;
        [SerializeField] internal int growthMagic = 20;
        [SerializeField] internal int growthDefense = 20;
        [SerializeField] internal int growthSpeed = 20;
        [SerializeField] internal int growthLuck = 20;
        public UnitInventory Inventory { get; internal set; }
        public UnitSkillList SkillList { get; internal set; }
        public UnitBuffList BuffList { get; internal set; }
        public UnitPassiveList PassiveList { get; internal set; }
        public WeaponData EquippedWeapon => GetActiveWeapon();
        public AccessoryData EquippedAccessory => Inventory?.EquippedAccessory;
        public virtual WeaponProficiency WeaponProficiencies => weaponProficiencies;
        public UnitActionAiMode ActionAiMode => actionAiMode;
        public UnitMovementAiMode MovementAiMode => movementAiMode;
        public int WaitGroupId => Mathf.Max(0, waitGroupId);
        public IReadOnlyList<Vector2Int> AiGoalTiles => aiGoalTiles;
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
        public virtual bool CanCounterAttack => !IsActionBlocked && HasUsableWeapon && GetActiveWeapon().CanCounterAttack;
        public virtual bool PreventsCounterattack => HasUsableWeapon && GetActiveWeapon().PreventsCounterattack;
        public virtual int BaseHitPoints => baseHitPoints;
        public virtual int MaxHitPoints => Mathf.Max(1, BaseHitPoints + GetPrimaryStatModifiers().MaxHitPoints);
        public int HitPoints { get; set; }
        public virtual int BaseManaPoints => baseManaPoints;
        public virtual int MaxManaPoints => Mathf.Max(0, BaseManaPoints + GetPrimaryStatModifiers().MaxManaPoints);
        public int CurrentManaPoints { get; internal set; }
        public int Level => Mathf.Clamp(level, 1, ExperienceCalculator.MaxLevel);
        public int Experience => Level >= ExperienceCalculator.MaxLevel ? 0 : Mathf.Clamp(experience, 0, ExperienceCalculator.MaxGain - 1);
        public virtual bool CanGainExperience => PlayerNumber == 0;
        public int PlayerId => PlayerNumber;
        public string UnitId => unitId;
        public string VisualId => visualId;
        public bool IsBoss => preset != null && preset.IsBoss;

        public void AssignSceneUnitId(string value)
        {
            unitId = value?.Trim() ?? string.Empty;
        }

        internal bool presetAppliedAtRuntime;
        internal bool useResolvedPresetLoadout;
        internal List<StartingInventoryItem> resolvedStartingInventory = new List<StartingInventoryItem>();
        internal List<StartingSkillEntry> resolvedStartingSkills = new List<StartingSkillEntry>();
        internal List<StartingPassiveEntry> resolvedStartingClassPassives = new List<StartingPassiveEntry>();
        internal SecondaryStatModifiers resolvedSecondaryStatOffsets;
        [NonSerialized] internal PrimaryStatModifiers terrainPrimaryStatModifiers;
        [NonSerialized] internal SecondaryStatModifiers terrainSecondaryStatModifiers;
        [NonSerialized] internal OwnedUnitSaveData pendingOwnedUnitSaveData;
        [NonSerialized] internal UnitPreset pendingOwnedUnitVisualPreset;
        [SerializeField, HideInInspector] internal bool spriteLayoutBaselineCaptured;
        [SerializeField, HideInInspector] internal Vector3 spriteLayoutBaselineLocalScale = Vector3.one;
        [SerializeField, HideInInspector] internal Vector3 spriteLayoutBaselineLocalPosition = new Vector3(0f, 0f, -0.1f);
        public virtual int BaseStrength => baseStrength;
        public virtual int Strength => BaseStrength + GetPrimaryStatModifiers().Strength;
        public virtual int BaseDefense => baseDefense;
        public virtual int Defense => BaseDefense + GetPrimaryStatModifiers().Defense;
        public virtual int BaseMagic => baseMagic;
        public virtual int Magic => BaseMagic + GetPrimaryStatModifiers().Magic;
        public virtual int BaseSpeed => baseSpeed;
        public virtual int Speed => BaseSpeed + GetPrimaryStatModifiers().Speed;
        public virtual int BaseLuck => baseLuck;
        public virtual int Luck => BaseLuck + GetPrimaryStatModifiers().Luck;
        public virtual int Attack
        {
            get
            {
                WeaponData weapon = GetActiveWeapon();
                return weapon != null
                    ? GetAttackForWeapon(weapon)
                    : (IsMagic ? Magic : Strength) + Might + GetPrimaryStatModifiers().Attack;
            }
        }

        public const int AccuracyPerSpeedPoint = 3;
        private const int CritPerLuck = 5;

        public virtual int Accuracy
        {
            get
            {
                return GetAttackBaseAccuracy() + GetSecondaryStatModifiers().Accuracy + Speed * AccuracyPerSpeedPoint;
            }
        }
        public virtual int Evade
        {
            get
            {
                return Speed * AccuracyPerSpeedPoint + GetSecondaryStatModifiers().Evade;
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
            BuffEffects.EnsureRegistered();
            if (BuffList == null)
            {
                BuffList = new UnitBuffList(this);
            }
        }

        private void EnsurePassiveList()
        {
            PassiveEffects.EnsureRegistered();
            if (PassiveList == null)
            {
                PassiveList = new UnitPassiveList(this);
            }
        }


        #endregion

        #region CTRL+F: Loadout Defaults / Equipment Resolution / Stat Modifiers

        private void EnsureSkillList()
        {
            SkillEffects.EnsureRegistered();
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

            return Array.Empty<StartingInventoryItem>();
        }

        private IEnumerable<StartingSkillEntry> GetInitialSkills()
        {
            if (useResolvedPresetLoadout)
            {
                return resolvedStartingSkills;
            }

            return Array.Empty<StartingSkillEntry>();
        }

        private IEnumerable<StartingPassiveEntry> GetInitialClassPassives()
        {
            if (useResolvedPresetLoadout)
            {
                return resolvedStartingClassPassives;
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

            modifiers += terrainPrimaryStatModifiers;

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

            GetPendingMoveTerrainBuffAdjustment(out PrimaryStatModifiers previewPrimary, out _);
            modifiers += previewPrimary;

            if (PassiveList != null)
            {
                modifiers += PassiveList.GetPrimaryStatModifiers();
            }

            return modifiers;
        }

        private SecondaryStatModifiers GetSecondaryStatModifiers()
        {
            SecondaryStatModifiers modifiers = resolvedSecondaryStatOffsets;
            modifiers += terrainSecondaryStatModifiers;

            if (EquippedAccessory != null)
            {
                modifiers += EquippedAccessory.SecondaryStatModifiers;
            }

            if (BuffList != null)
            {
                modifiers += BuffList.GetSecondaryStatModifiers();
            }

            GetPendingMoveTerrainBuffAdjustment(out _, out SecondaryStatModifiers previewSecondary);
            modifiers += previewSecondary;

            if (PassiveList != null)
            {
                modifiers += PassiveList.GetSecondaryStatModifiers();
            }

            return modifiers;
        }

        /// <summary>
        /// Terrain-owned buffs remain committed to the unit's real footprint until a pending
        /// move is confirmed. During preview, replace their static stat modifiers with those
        /// supplied by the preview footprint without mutating the live buff list.
        /// </summary>
        private void GetPendingMoveTerrainBuffAdjustment(
            out PrimaryStatModifiers primary,
            out SecondaryStatModifiers secondary)
        {
            primary = default;
            secondary = default;
            if (!HasPendingMove || PreviewCell == null)
            {
                return;
            }

            var terrainManagedBuffIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (TerrainEffectData terrainData in TerrainEffectRegistry.Entries)
            {
                if (terrainData == null)
                {
                    continue;
                }

                if (terrainData.RemoveOccupantBuffOnExit && !string.IsNullOrWhiteSpace(terrainData.OccupantBuffId))
                {
                    terrainManagedBuffIds.Add(terrainData.OccupantBuffId);
                }

                if (terrainData.RemoveAppliedBuffOnExit && !string.IsNullOrWhiteSpace(terrainData.AppliedBuffId))
                {
                    terrainManagedBuffIds.Add(terrainData.AppliedBuffId);
                }
            }

            // Remove the committed footprint's terrain stat buffs from the normal BuffList sum.
            foreach (string buffId in terrainManagedBuffIds)
            {
                RuntimeBuff activeBuff = BuffList?.GetBuff(buffId);
                BuffData data = activeBuff?.Data;
                if (data == null)
                {
                    continue;
                }

                int stacks = Mathf.Max(1, activeBuff.Stacks);
                primary += data.PrimaryStatModifiers * -stacks;
                secondary += data.SecondaryStatModifiers * -stacks;
            }

            CellGrid grid = FindAnyObjectByType<CellGrid>();
            if (grid == null)
            {
                return;
            }

            var desiredBuffIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<Cell> previewFootprint = GetFootprintCells(PreviewCell, grid);
            foreach (TerrainEffectData terrainData in previewFootprint
                .Where(cell => cell != null)
                .SelectMany(cell => cell.TerrainEffects ?? Array.Empty<TerrainEffectInstance>())
                .Select(effect => effect?.Data)
                .Where(data => data != null)
                .GroupBy(data => data.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First()))
            {
                bool affectsUnit = terrainData.Targeting != TerrainEffectTargeting.PlayerSide
                    || PlayerNumber == terrainData.TargetPlayerNumber;
                if (!affectsUnit)
                {
                    continue;
                }

                if (terrainData.RemoveOccupantBuffOnExit && !string.IsNullOrWhiteSpace(terrainData.OccupantBuffId))
                {
                    desiredBuffIds.Add(terrainData.OccupantBuffId);
                }

                if (terrainData.RemoveAppliedBuffOnExit && !string.IsNullOrWhiteSpace(terrainData.AppliedBuffId))
                {
                    desiredBuffIds.Add(terrainData.AppliedBuffId);
                }
            }

            foreach (string buffId in desiredBuffIds)
            {
                BuffData data = BuffRegistry.Get(buffId);
                if (data == null)
                {
                    continue;
                }

                primary += data.PrimaryStatModifiers;
                secondary += data.SecondaryStatModifiers;
            }
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
                growthSpeed,
                growthLuck
            });
        }




        private static Item[] CreateSavedInventoryItems(OwnedUnitSaveData saveData)
        {
            return saveData?.Inventory?
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.ItemId))
                .Select(entry => new Item(entry.ItemId, entry.RemainingCharges, entry.IsDroppable))
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

        private static WeaponProficiency GetWeaponProficienciesFromIds(IEnumerable<string> proficiencyIds)
        {
            WeaponProficiency result = WeaponProficiency.None;
            foreach (string proficiencyId in proficiencyIds ?? Array.Empty<string>())
            {
                if (Enum.TryParse(proficiencyId, true, out WeaponProficiency parsedType))
                {
                    result |= parsedType;
                }
                else if (string.Equals(proficiencyId, "Sword", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(proficiencyId, "Lance", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(proficiencyId, "Blunt", StringComparison.OrdinalIgnoreCase))
                {
                    result |= WeaponProficiency.Melee;
                }
            }

            return result;
        }

        private IEnumerable<string> GetWeaponProficiencyIds()
        {
            WeaponProficiency[] supportedTypes =
            {
                WeaponProficiency.Melee,
                WeaponProficiency.Ranged,
                WeaponProficiency.Magic
            };

            foreach (WeaponProficiency type in supportedTypes)
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

        public Sprite GetPortraitSprite()
        {
            if (preset != null)
            {
                return preset.FaceSprite != null
                    ? preset.FaceSprite
                    : preset.UnitSprite;
            }

            return ResolveUnitSpriteRenderer()?.sprite;
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
                    baseHitPoints = Mathf.Max(1, baseHitPoints + amount);
                    break;
                case LevelableStatKind.Magic:
                    baseMagic += amount;
                    baseManaPoints = Mathf.Max(0, baseManaPoints + amount * 3);
                    break;
                case LevelableStatKind.Defense:
                    baseDefense += amount;
                    baseHitPoints = Mathf.Max(1, baseHitPoints + amount);
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

            WeaponProficiency required = WeaponProficiencyUtility.ForWeapon(weapon);
            return (WeaponProficiencies & required) != 0;
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

        public int GetAttackForWeapon(WeaponData weapon, bool hybridScalingOverride = false)
        {
            if (weapon == null)
            {
                return 0;
            }

            var primaryModifiers = GetPrimaryStatModifiers(weapon);
            bool isMagic = GetIsMagicForWeapon(weapon);
            int strength = BaseStrength + primaryModifiers.Strength;
            int magic = BaseMagic + primaryModifiers.Magic;
            int offensiveStat = isMagic ? magic : strength;
            if (weapon.BonusDamageFromStrength)
                offensiveStat += strength;
            else if (hybridScalingOverride)
                offensiveStat += isMagic ? strength : magic;
            if (weapon.BonusDamageFromDefense)
                offensiveStat += BaseDefense + primaryModifiers.Defense;
            return offensiveStat + weapon.Might + primaryModifiers.Attack;
        }

        public int GetAccuracyForWeapon(WeaponData weapon)
        {
            if (weapon == null)
            {
                return 0;
            }

            return weapon.Accuracy + GetSecondaryStatModifiers().Accuracy + GetSpeedForWeapon(weapon) * AccuracyPerSpeedPoint;
        }

        public int GetCritForWeapon(WeaponData weapon)
        {
            if (weapon == null)
            {
                return 0;
            }

            return weapon.Crit + GetSecondaryStatModifiers().Crit + GetLuckForWeapon(weapon) * CritPerLuck;
        }

        public int GetCritForSkill(int skillCrit)
        {
            return skillCrit + GetSecondaryStatModifiers().Crit + Luck * CritPerLuck;
        }

        public int GetNumHitsForWeapon(WeaponData weapon)
        {
            return weapon == null ? 0 : Mathf.Max(1, weapon.NumHits);
        }

        public ResolvedAttackProfile BuildAttackProfileForWeapon(WeaponData weapon)
        {
            if (weapon == null)
            {
                return default;
            }

            return new ResolvedAttackProfile
            {
                Damage = GetAttackForWeapon(weapon),
                Accuracy = GetAccuracyForWeapon(weapon),
                Crit = GetCritForWeapon(weapon),
                NumHits = GetNumHitsForWeapon(weapon),
                PursuitSpeed = GetSpeedForWeapon(weapon),
                IsMagic = GetIsMagicForWeapon(weapon),
                CanPursuitAttack = weapon.CanPursuitAttack,
                PreventsCounterattack = weapon.PreventsCounterattack,
                EndsTurn = true,
                UsesWeaponEffects = true
            };
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

            int distance = GetFootprintDistanceTo(other, sourceCell, otherCell, FindSceneCellGrid());
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

        [SerializeField, HideInInspector]
        internal bool includeInOwnedUnitSave = true;

        [SerializeField, Tooltip("If this friendly unit survives a chapter victory, add it to the campaign's owned units.")]
        internal bool recruitOnChapterClear;

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

        public bool RecruitOnChapterClear
        {
            get => recruitOnChapterClear;
            set => recruitOnChapterClear = value;
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
            get => BuffList != null ? BuffList.ApplyMovementPointCaps(movementPointsStorage) : movementPointsStorage;
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
        private readonly List<PendingUnitOperation> pendingOperations = new List<PendingUnitOperation>();

        internal struct PendingMove
        {
            public Cell FromCell;
            public Cell ToCell;
            public IList<Cell> Path;
            public float MovementPointsBefore;
            public float MovementCost;
            public Vector3 FromLocalPos;
            public bool PreviewPositionNotified;
        }

        public bool HasPendingMove => _pendingMove.HasValue;
        public PendingUnitOperation? LatestPendingOperation => pendingOperations.Count > 0
            ? pendingOperations[pendingOperations.Count - 1]
            : null;

        internal void RecordPendingOperation(PendingUnitOperation operation)
        {
            RemovePendingOperation(operation);
            pendingOperations.Add(operation);
        }

        internal void RemovePendingOperation(PendingUnitOperation operation)
        {
            for (int i = pendingOperations.Count - 1; i >= 0; i--)
            {
                if (pendingOperations[i] != operation)
                {
                    continue;
                }

                pendingOperations.RemoveAt(i);
                return;
            }
        }

        internal void ClearPendingOperations()
        {
            pendingOperations.Clear();
        }

        public bool TryRollbackLatestPendingOperation(out PendingUnitOperation operation)
        {
            PendingUnitOperation? latest = LatestPendingOperation;
            if (!latest.HasValue)
            {
                operation = default;
                return false;
            }

            operation = latest.Value;
            return operation switch
            {
                PendingUnitOperation.Movement => CancelPendingMove(),
                PendingUnitOperation.Overcharge => CancelPendingOvercharge(),
                _ => false
            };
        }
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
