using UnityEngine;
using Windy.Srpg.Runtime.Board;

namespace Windy.Srpg.Game.Grid
{
    public partial class CustomCellGrid
    {
        public void RequestFrameworkInitializeAndStart()
        {
            InitializeBattleScene();
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
                StartLegacyBattle();
                return;
            }

            SyncRuntimeMirrorNow();
            RoundRobinTurnPlan plan = RoundRobinBattleFlow.ResolveStart(this);
            if (plan.NextPlayer == null)
            {
                Debug.LogError("CustomCellGrid: No valid battle turn resolver or next player was found.");
                return;
            }

            ApplyLegacyStateFromRuntime(() => SyncBattleStartFromPlan(plan, kickPlayerPlay: false, syncUnitTurnHooks: false));
            PrepareRuntimeTurnStartForPlan(plan);
            runtimeBoard.BeginBattleFromHost(
                plan,
                kickFirstTurn: false,
                refreshSceneCollections: false);
            ApplyRuntimeTurnStartToLegacyPlayableUnits();
            SyncRuntimeMirrorNow();
            SyncRuntimeSceneInputGate();
            runtimeBoard.KickCurrentTurnPlay();
            Debug.Log("Game started via runtime board");
        }
    }
}
