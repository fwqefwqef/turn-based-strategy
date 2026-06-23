using UnityEngine;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Players;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Runtime.Grid;

namespace Windy.Srpg.Game.AI.Evaluators
{
    public abstract class CellEvaluator : MonoBehaviour
    {
        public float Weight = 1f;

        public virtual void Precalculate(Unit evaluatingUnit, Player currentPlayer, CellGrid cellGrid)
        {
        }

        public abstract float Evaluate(Cell cellToEvaluate, Unit evaluatingUnit, Player currentPlayer, CellGrid cellGrid);
    }
}
