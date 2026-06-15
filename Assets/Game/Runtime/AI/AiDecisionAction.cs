using System.Collections;
using UnityEngine;
using Windy.Srpg.Runtime.Board;
using Windy.Srpg.Runtime.Players;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Runtime.AI
{
    public abstract class AiDecisionAction : MonoBehaviour
    {
        public abstract void InitializeDecision(IBattlePlayer player, IBattleUnit unit, IBattleBoard board);
        public abstract bool ShouldExecute(IBattlePlayer player, IBattleUnit unit, IBattleBoard board);
        public abstract void Precalculate(IBattlePlayer player, IBattleUnit unit, IBattleBoard board);
        public abstract IEnumerator ExecuteDecision(IBattlePlayer player, IBattleUnit unit, IBattleBoard board);
        public abstract void CleanUpDecision(IBattlePlayer player, IBattleUnit unit, IBattleBoard board);
        public abstract void ShowDebugDecisionInfo(IBattlePlayer player, IBattleUnit unit, IBattleBoard board);
    }
}
