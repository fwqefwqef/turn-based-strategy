using UnityEngine;
using Windy.Srpg.Game.Chapters;

namespace Windy.Srpg.Game.Grid
{
    public sealed class LastSideStandingCondition : MonoBehaviour, IBattleEndCondition
    {
        [SerializeField] private ChapterData chapterData;

        public BattleOutcome Evaluate(CellGrid grid)
        {
            ChapterData resolvedChapterData = ResolveChapterData(grid);
            if (resolvedChapterData != null)
            {
                return resolvedChapterData.EvaluateBattleOutcome(grid);
            }

            return RoundRobinBattleFlow.EvaluateLastSideStanding(grid);
        }

        private ChapterData ResolveChapterData(CellGrid grid)
        {
            if (chapterData != null)
            {
                return chapterData;
            }

            chapterData = ChapterData.FindForGrid(grid);
            return chapterData;
        }
    }
}
