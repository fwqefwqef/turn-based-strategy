using TbsFramework.Cells;
using UnityEngine;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Players;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.AI.Evaluators
{
    public abstract class CellEvaluator : MonoBehaviour
    {
        public float Weight = 1f;

        public virtual void Precalculate(CustomUnit evaluatingUnit, CustomPlayer currentPlayer, CustomCellGrid cellGrid)
        {
        }

        public abstract float Evaluate(Cell cellToEvaluate, CustomUnit evaluatingUnit, CustomPlayer currentPlayer, CustomCellGrid cellGrid);
    }
}
