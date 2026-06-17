using UnityEngine;
using Windy.Srpg.Runtime.Board;

namespace Windy.Srpg.Game.Grid
{
    public partial class CustomCellGrid
    {
        public void RequestFrameworkInitializeAndStart()
        {
            Initialize();
            StartBattleViaRuntimeBoard();
        }

        public void RequestFrameworkBattleStart()
        {
            StartBattleViaRuntimeBoard();
        }

        /// <summary>
        /// Runtime-led battle start: legacy GameStarted/unit hooks first, then BattleBoard kicks first turn.
        /// </summary>
        private void StartBattleViaRuntimeBoard()
        {
            ResolveRuntimeBoard();
            if (runtimeBoard == null)
            {
                StartGame();
                return;
            }

            SyncRuntimeMirrorNow();
            RoundRobinTurnPlan plan = RoundRobinBattleFlow.ResolveStart(this);
            if (plan.NextPlayer == null)
            {
                Debug.LogError("CustomCellGrid: No valid battle turn resolver or next player was found.");
                return;
            }

            ApplyLegacyStateFromRuntime(() => SyncBattleStartFromPlan(plan, kickPlayerPlay: false));
            runtimeBoard.BeginBattleFromHost(
                plan,
                kickFirstTurn: true,
                refreshSceneCollections: false);
            SyncRuntimeMirrorNow();
            SyncRuntimeSceneInputGate();
            Debug.Log("Game started via runtime board");
        }
    }
}
