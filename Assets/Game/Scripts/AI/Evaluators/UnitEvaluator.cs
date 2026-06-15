using UnityEngine;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Players;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.AI.Evaluators
{
    public abstract class UnitEvaluator : MonoBehaviour
    {
        public float Weight = 1f;

        public virtual void Precalculate(CustomUnit evaluatingUnit, CustomPlayer currentPlayer, CustomCellGrid cellGrid)
        {
        }

        public abstract float Evaluate(CustomUnit unitToEvaluate, CustomUnit evaluatingUnit, CustomPlayer currentPlayer, CustomCellGrid cellGrid);
    }
}
