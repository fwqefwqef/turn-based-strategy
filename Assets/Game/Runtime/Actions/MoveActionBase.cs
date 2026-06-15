using Windy.Srpg.Runtime.Board;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Runtime.Actions
{
    public abstract class MoveActionBase : BattleAction
    {
        public virtual bool CanTraverse(BoardCell cell)
        {
            var unit = GetUnit<BattleUnit>();
            return unit != null && unit.CanTraverse(cell);
        }

        public virtual bool CanStopOn(BoardCell cell)
        {
            var unit = GetUnit<BattleUnit>();
            return unit != null && unit.CanStopOn(cell);
        }
    }
}
