using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.Chapters
{
    public enum ChapterBattleConditionResult
    {
        Victory,
        Defeat
    }

    public enum ChapterBattleConditionKind
    {
        DefeatAllEnemies,
        LoseAllAllies
    }

    public enum BlackFogDirection
    {
        Left,
        Right,
        Up,
        Down
    }

    [Serializable]
    public sealed class ChapterBattleCondition
    {
        private const int PlayerSideId = 0;

        public ChapterBattleConditionResult Result;
        public ChapterBattleConditionKind Kind;

        public ChapterBattleCondition()
        {
        }

        public ChapterBattleCondition(ChapterBattleConditionResult result, ChapterBattleConditionKind kind)
        {
            Result = result;
            Kind = kind;
        }

        public bool IsMet(CellGrid grid)
        {
            if (grid == null)
            {
                return false;
            }

            List<Unit> aliveUnits = GetAliveBattleUnits(grid);
            int aliveAllies = aliveUnits.Count(unit => unit.PlayerId == PlayerSideId);
            int aliveEnemies = aliveUnits.Count(unit => unit.PlayerId != PlayerSideId);

            return Kind switch
            {
                ChapterBattleConditionKind.DefeatAllEnemies => aliveAllies > 0 && aliveEnemies == 0,
                ChapterBattleConditionKind.LoseAllAllies => aliveAllies == 0,
                _ => false
            };
        }

        public BattleOutcome BuildOutcome(CellGrid grid)
        {
            if (grid == null)
            {
                return new BattleOutcome(false, null, null);
            }

            List<int> orderedPlayerIds = grid.GetOrderedPlayers()
                .Where(player => player != null)
                .Select(player => player.PlayerId)
                .Distinct()
                .OrderBy(playerId => playerId)
                .ToList();

            List<int> aliveEnemyPlayerIds = GetAliveBattleUnits(grid)
                .Where(unit => unit.PlayerId != PlayerSideId)
                .Select(unit => unit.PlayerId)
                .Distinct()
                .OrderBy(playerId => playerId)
                .ToList();

            if (Result == ChapterBattleConditionResult.Victory)
            {
                return new BattleOutcome(
                    true,
                    new[] { PlayerSideId },
                    orderedPlayerIds.Where(playerId => playerId != PlayerSideId).ToArray());
            }

            IReadOnlyList<int> winningPlayerIds = aliveEnemyPlayerIds.Count > 0
                ? aliveEnemyPlayerIds
                : orderedPlayerIds.Where(playerId => playerId != PlayerSideId).ToArray();

            return new BattleOutcome(
                true,
                winningPlayerIds,
                new[] { PlayerSideId });
        }

        private static List<Unit> GetAliveBattleUnits(CellGrid grid)
        {
            return grid.GetAllUnits()
                .Where(unit => unit != null && !unit.ExcludedFromBattle && unit.IsAliveForBattle)
                .ToList();
        }
    }

    [AddComponentMenu("TBS/Chapter/Chapter Data")]
    public sealed class ChapterData : MonoBehaviour
    {
        [SerializeField] private string chapterName = "Chapter";
        [SerializeField] private float chapterId = 1f;
        [SerializeField] private bool replayable = true;
        [SerializeField] private float unlockRequiredChapterId;
        [SerializeField] private int averageEnemyLevel = 1;
        [Header("Black Fog")]
        [SerializeField] private int blackFogTurn = 6;
        [SerializeField] private BlackFogDirection blackFogDirection = BlackFogDirection.Left;
        [SerializeField] private int blackFogExpansionDistance = 2;
        [SerializeField] private List<UnitPreset> enemyPaintPresets = new List<UnitPreset>();
        [Header("Enemy Turn Order")]
        [Tooltip("Stable Unit IDs in action order. Drag entries to rearrange them; newly added enemies append to the end.")]
        [SerializeField] private List<string> enemyTurnOrderUnitIds = new List<string>();
        [SerializeField] private List<ChapterBattleCondition> battleConditions = CreateDefaultBattleConditions();

        public string ChapterName => string.IsNullOrWhiteSpace(chapterName) ? gameObject.scene.name : chapterName;
        public float ChapterId => Mathf.Max(0f, chapterId);
        public bool Replayable => replayable;
        public float UnlockRequiredChapterId => Mathf.Max(0f, unlockRequiredChapterId);
        public int AverageEnemyLevel => Mathf.Max(1, averageEnemyLevel);
        public int BlackFogTurn => Mathf.Max(1, blackFogTurn);
        public BlackFogDirection BlackFogDirection => blackFogDirection;
        public int BlackFogExpansionDistance => Mathf.Max(1, blackFogExpansionDistance);
        public IReadOnlyList<UnitPreset> EnemyPaintPresets => enemyPaintPresets ??= new List<UnitPreset>();
        public IReadOnlyList<string> EnemyTurnOrderUnitIds => enemyTurnOrderUnitIds ??= new List<string>();
        public IReadOnlyList<ChapterBattleCondition> BattleConditions => GetEffectiveBattleConditions();

        public IReadOnlyList<Unit> OrderEnemyUnits(IEnumerable<Unit> units)
        {
            List<Unit> candidates = units?
                .Where(unit => unit != null && unit.PlayerId != 0)
                .Distinct()
                .ToList()
                ?? new List<Unit>();
            AppendMissingEnemyUnits(candidates);

            Dictionary<string, int> orderById = enemyTurnOrderUnitIds
                .Select((unitId, index) => (unitId, index))
                .Where(entry => !string.IsNullOrWhiteSpace(entry.unitId))
                .GroupBy(entry => entry.unitId.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().index, StringComparer.OrdinalIgnoreCase);

            return candidates
                .Select((unit, fallbackIndex) => (unit, fallbackIndex))
                .OrderBy(entry => orderById.TryGetValue(entry.unit.UnitId ?? string.Empty, out int index) ? index : int.MaxValue)
                .ThenBy(entry => entry.fallbackIndex)
                .Select(entry => entry.unit)
                .ToList();
        }

        public bool SynchronizeEnemyTurnOrder(IEnumerable<Unit> units)
        {
            List<Unit> enemies = units?
                .Where(unit => unit != null && unit.PlayerId != 0 && !string.IsNullOrWhiteSpace(unit.UnitId))
                .Distinct()
                .ToList()
                ?? new List<Unit>();
            HashSet<string> validIds = new HashSet<string>(enemies.Select(unit => unit.UnitId.Trim()), StringComparer.OrdinalIgnoreCase);
            List<string> synchronized = (enemyTurnOrderUnitIds ?? new List<string>())
                .Where(unitId => !string.IsNullOrWhiteSpace(unitId) && validIds.Contains(unitId.Trim()))
                .Select(unitId => unitId.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            HashSet<string> includedIds = new HashSet<string>(synchronized, StringComparer.OrdinalIgnoreCase);
            synchronized.AddRange(enemies.Select(unit => unit.UnitId.Trim()).Where(includedIds.Add));

            bool changed = enemyTurnOrderUnitIds == null || !enemyTurnOrderUnitIds.SequenceEqual(synchronized, StringComparer.OrdinalIgnoreCase);
            if (changed)
            {
                enemyTurnOrderUnitIds = synchronized;
            }

            return changed;
        }

        public bool AppendEnemyToTurnOrder(Unit unit)
        {
            if (unit == null || unit.PlayerId == 0 || string.IsNullOrWhiteSpace(unit.UnitId))
            {
                return false;
            }

            enemyTurnOrderUnitIds ??= new List<string>();
            string unitId = unit.UnitId.Trim();
            if (enemyTurnOrderUnitIds.Any(existing => string.Equals(existing?.Trim(), unitId, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            enemyTurnOrderUnitIds.Add(unitId);
            return true;
        }

        private void AppendMissingEnemyUnits(IEnumerable<Unit> units)
        {
            foreach (Unit unit in units ?? Enumerable.Empty<Unit>())
            {
                AppendEnemyToTurnOrder(unit);
            }
        }

        public BattleOutcome EvaluateBattleOutcome(CellGrid grid)
        {
            foreach (ChapterBattleCondition condition in GetEffectiveBattleConditions())
            {
                if (condition != null && condition.IsMet(grid))
                {
                    return condition.BuildOutcome(grid);
                }
            }

            return new BattleOutcome(false, null, null);
        }

        public static ChapterData FindForGrid(CellGrid grid)
        {
            Scene scene = grid != null && grid.gameObject.scene.IsValid()
                ? grid.gameObject.scene
                : SceneManager.GetActiveScene();

            ChapterData sceneChapterData = FindInScene(scene, grid);
            if (sceneChapterData != null)
            {
                return sceneChapterData;
            }

            ChapterData fallbackChapterData = UnityEngine.Object.FindAnyObjectByType<ChapterData>();
            return fallbackChapterData != null && fallbackChapterData.gameObject.scene.IsValid()
                ? fallbackChapterData
                : null;
        }

        private static ChapterData FindInScene(Scene scene, CellGrid grid)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return null;
            }

            List<ChapterData> chapterDataComponents = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<ChapterData>(includeInactive: true))
                .Where(chapterData => chapterData != null)
                .ToList();

            return chapterDataComponents.FirstOrDefault(chapterData => grid == null || chapterData.gameObject != grid.gameObject)
                ?? chapterDataComponents.FirstOrDefault();
        }

        private IReadOnlyList<ChapterBattleCondition> GetEffectiveBattleConditions()
        {
            return battleConditions != null && battleConditions.Count > 0
                ? battleConditions
                : CreateDefaultBattleConditions();
        }

        private static List<ChapterBattleCondition> CreateDefaultBattleConditions()
        {
            return new List<ChapterBattleCondition>
            {
                new ChapterBattleCondition(ChapterBattleConditionResult.Victory, ChapterBattleConditionKind.DefeatAllEnemies),
                new ChapterBattleCondition(ChapterBattleConditionResult.Defeat, ChapterBattleConditionKind.LoseAllAllies)
            };
        }

        private void Reset()
        {
            chapterName = gameObject.scene.IsValid() && !string.IsNullOrWhiteSpace(gameObject.scene.name)
                ? gameObject.scene.name
                : "Chapter";
            chapterId = 1f;
            replayable = true;
            unlockRequiredChapterId = 0f;
            averageEnemyLevel = 1;
            blackFogTurn = 6;
            blackFogDirection = BlackFogDirection.Left;
            blackFogExpansionDistance = 2;
            enemyPaintPresets = new List<UnitPreset>();
            enemyTurnOrderUnitIds = new List<string>();
            battleConditions = CreateDefaultBattleConditions();
        }

        private void OnValidate()
        {
            chapterId = Mathf.Max(0f, chapterId);
            unlockRequiredChapterId = Mathf.Max(0f, unlockRequiredChapterId);
            averageEnemyLevel = Mathf.Max(1, averageEnemyLevel);
            blackFogTurn = Mathf.Max(1, blackFogTurn);
            blackFogExpansionDistance = Mathf.Max(1, blackFogExpansionDistance);
            if (battleConditions == null || battleConditions.Count == 0)
            {
                battleConditions = CreateDefaultBattleConditions();
            }

            enemyPaintPresets ??= new List<UnitPreset>();
            enemyTurnOrderUnitIds ??= new List<string>();

            if (gameObject.scene.IsValid() && gameObject.scene.isLoaded)
            {
                IEnumerable<Unit> sceneUnits = gameObject.scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Unit>(includeInactive: true));
                SynchronizeEnemyTurnOrder(sceneUnits);
            }
        }
    }
}
