using UnityEngine;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Players;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.AI.Evaluators
{
    public abstract class UnitEvaluator : MonoBehaviour
    {
        public float Weight = 1f;

        public virtual void Precalculate(Unit evaluatingUnit, Player currentPlayer, CellGrid cellGrid)
        {
        }

        public abstract float Evaluate(Unit unitToEvaluate, Unit evaluatingUnit, Player currentPlayer, CellGrid cellGrid);
    }
}

