using System;
using System.Collections.Generic;
using TbsFramework.Cells;
using TbsFramework.Grid;
using TbsFramework.Players;
using TbsFramework.Units;
using UnityEngine;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Runtime.Board;
using Windy.Srpg.Runtime.Players;

namespace Windy.Srpg.Game.Grid
{
    public partial class CustomCellGrid
    {
        [HideInInspector] public bool Is2D;
        public Transform PlayersParent;
        public bool ShouldStartGameImmediately = true;

        private FrameworkCellGridAnchor cachedLegacyGridAnchor;

        internal FrameworkCellGridAnchor EnsureFrameworkCellGridAnchor()
        {
            if (cachedLegacyGridAnchor == null)
            {
                cachedLegacyGridAnchor = GetComponent<FrameworkCellGridAnchor>();
            }

            if (cachedLegacyGridAnchor == null)
            {
                cachedLegacyGridAnchor = gameObject.AddComponent<FrameworkCellGridAnchor>();
            }

            return cachedLegacyGridAnchor;
        }

        public FrameworkCellGridAnchor LegacyGrid => EnsureFrameworkCellGridAnchor();

        private List<Cell> Cells => LegacyGrid.Cells;

        private List<Unit> Units => LegacyGrid.Units;

        private List<Player> Players => LegacyGrid.Players;

        private List<IBattleTurnPlayer> RuntimePlayers => LegacyGrid.RuntimePlayers;

        private int CurrentPlayerNumber => LegacyGrid.CurrentPlayerNumber;

        public bool GameFinished => LegacyGrid.GameFinished;

        public event EventHandler LevelLoading
        {
            add => LegacyGrid.LevelLoading += value;
            remove => LegacyGrid.LevelLoading -= value;
        }

        private CellGrid.CellGridState cellGridState
        {
            get => LegacyGrid.cellGridState;
            set => LegacyGrid.cellGridState = value;
        }

        private void EnsureLegacyGridHost()
        {
            FrameworkCellGridAnchor anchor = EnsureFrameworkCellGridAnchor();
            anchor.SyncHostSettings(Is2D, PlayersParent, ShouldStartGameImmediately);
        }

        private void WireLegacyGridEvents()
        {
            FrameworkCellGridAnchor anchor = LegacyGrid;
            anchor.GameStarted += OnGameStarted;
            anchor.GameEnded += OnLegacyGameEnded;
            anchor.LevelLoadingDone += OnLevelLoadingDoneInternal;
            anchor.TurnEnded += OnTurnEnded;
            anchor.UnitAdded += OnUnitAdded;
        }

        private void UnwireLegacyGridEvents()
        {
            if (cachedLegacyGridAnchor == null)
            {
                return;
            }

            cachedLegacyGridAnchor.GameStarted -= OnGameStarted;
            cachedLegacyGridAnchor.GameEnded -= OnLegacyGameEnded;
            cachedLegacyGridAnchor.LevelLoadingDone -= OnLevelLoadingDoneInternal;
            cachedLegacyGridAnchor.TurnEnded -= OnTurnEnded;
            cachedLegacyGridAnchor.UnitAdded -= OnUnitAdded;
        }

        public void InitializeBattleScene()
        {
            EnsureLegacyGridHost();
            LegacyGrid.Initialize();
        }

        public void StartLegacyBattle()
        {
            LegacyGrid.StartGame();
        }

        internal int AllocateNextUnitId() => LegacyGrid.AllocateNextUnitIdInternal();

        internal void NotifyUnitAdded(Transform unitTransform) => LegacyGrid.NotifyUnitAddedInternal(unitTransform);

        internal void CommitTurnTransition(
            RoundRobinTurnPlan plan,
            bool isNetworkInvoked = false,
            bool kickPlayerPlay = true,
            bool syncUnitTurnHooks = true)
        {
            LegacyGrid.CommitTurnTransitionInternal(plan, isNetworkInvoked, kickPlayerPlay, syncUnitTurnHooks);
        }

        internal void SyncBattleStartFromPlan(
            RoundRobinTurnPlan plan,
            bool kickPlayerPlay = true,
            bool syncUnitTurnHooks = true)
        {
            LegacyGrid.SyncBattleStartFromPlanInternal(plan, kickPlayerPlay, syncUnitTurnHooks);
        }

        internal void EndUnitsForCurrentPlayerTurn() => LegacyGrid.EndUnitsForCurrentPlayerTurnInternal();

        internal bool CheckGameFinished() => LegacyGrid.CheckGameFinishedInternal();

        internal bool TryApplyBattleOutcome(BattleOutcome outcome) => LegacyGrid.TryApplyBattleOutcomeInternal(outcome);

        public void EndTurn(bool isNetworkInvoked = false) => LegacyGrid.EndTurn(isNetworkInvoked);

        internal static void SyncRegistryAnchorFromCustomUnit(CustomUnit customUnit, FrameworkUnitAnchor anchor = null)
        {
            if (customUnit == null)
            {
                return;
            }

            anchor ??= customUnit.EnsureFrameworkUnitAnchor();
            anchor.UnitID = customUnit.UnitID;
            anchor.PlayerNumber = customUnit.PlayerNumber;
            anchor.Obstructable = customUnit.Obstructable;
            anchor.Cell = customUnit.Cell;
        }
    }
}
