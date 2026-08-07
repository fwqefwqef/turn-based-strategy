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
                .Where(unit => unit != null && !unit.ExcludedFromBattle && unit.HitPoints > 0)
                .ToList();
        }
    }

    [AddComponentMenu("TBS/Chapter/Chapter Data")]
    public sealed class ChapterData : MonoBehaviour
    {
        [SerializeField] private int averageEnemyLevel = 1;
        [SerializeField] private List<ChapterBattleCondition> battleConditions = CreateDefaultBattleConditions();

        public int AverageEnemyLevel => Mathf.Max(1, averageEnemyLevel);
        public IReadOnlyList<ChapterBattleCondition> BattleConditions => GetEffectiveBattleConditions();

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
            averageEnemyLevel = 1;
            battleConditions = CreateDefaultBattleConditions();
        }

        private void OnValidate()
        {
            averageEnemyLevel = Mathf.Max(1, averageEnemyLevel);
            if (battleConditions == null || battleConditions.Count == 0)
            {
                battleConditions = CreateDefaultBattleConditions();
            }
        }
    }
}
