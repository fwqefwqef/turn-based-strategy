using System.Collections;
using Windy.Srpg.Runtime.Board;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Runtime.Actions
{
    public interface IBattleAction
    {
        void InitializeAction(IBoardUnit unit);
        bool CanPerformAction(IBattleBoard board);
        IEnumerator ExecuteAction(IBattleBoard board, bool isRemoteInvocation = false);
        void DisplayAction(IBattleBoard board);
        void CleanUpAction(IBattleBoard board);
        void OnActionSelected(IBattleBoard board);
        void OnActionDeselected(IBattleBoard board);
        void OnCellClicked(IBattleCell cell, IBattleBoard board);
        void OnCellHighlighted(IBattleCell cell, IBattleBoard board);
        void OnCellDehighlighted(IBattleCell cell, IBattleBoard board);
        void OnUnitClicked(IBoardUnit unit, IBattleBoard board);
        void OnUnitHighlighted(IBoardUnit unit, IBattleBoard board);
        void OnUnitDehighlighted(IBoardUnit unit, IBattleBoard board);
        void OnTurnStarted(IBattleBoard board);
        void OnTurnEnded(IBattleBoard board);
        void OnOwnerDestroyed(IBattleBoard board);
    }
}

