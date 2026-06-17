using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Grid.States;
using Windy.Srpg.Game.Players.AI;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Runtime.AI;
using Windy.Srpg.Runtime.Board;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Game.Players
{
    public sealed class AiPlayer : Player
    {
        [SerializeField] private bool debugMode;

        public bool DebugMode => debugMode;
        public override bool IsHumanControlled => false;

        protected override void OnInitialize(CellGrid cellGrid)
        {
            cellGrid.BattleEnded += OnGameEnded;
        }

        private void OnDisable()
        {
            StopAllCoroutines();
        }

        public override void Play(CellGrid cellGrid)
        {
            cellGrid.EnterAiTurnState(this);
            StartCoroutine(ExecuteRuntimeRoutedTurn(cellGrid));
        }

        private IEnumerator ExecuteRuntimeRoutedTurn(CellGrid cellGrid)
        {
            BattleBoard board = cellGrid.GetComponent<BattleBoard>();
            if (board == null)
            {
                yield return ExecuteTurn(cellGrid);
                yield break;
            }

            IReadOnlyList<BoardUnit> runtimeOrder = SelectRuntimeUnits(board, cellGrid);
            cellGrid.PrepareRuntimeRoutedAiTurn();

            if (!DebugMode)
            {
                yield return AiTurnRunner.ExecuteTurn(
                    this,
                    runtimeOrder.Cast<IBoardUnit>(),
                    board,
                    () => cellGrid.RequestEndTurn());
                yield break;
            }

            foreach (BoardUnit runtimeUnit in runtimeOrder)
            {
                Unit unit = runtimeUnit?.GetComponent<Unit>();
                if (unit == null)
                {
                    continue;
                }

                unit.MarkAsSelected();
                Debug.Log($"Current unit: {unit.name}, press N to continue");
                yield return WaitForDebugKey(KeyCode.N);

                AiDecisionAction[] actions = unit.GetComponentsInChildren<AiDecisionAction>();
                foreach (AiDecisionAction action in actions)
                {
                    if (action == null || unit == null)
                    {
                        break;
                    }

                    yield return null;

                    action.InitializeDecision(this, unit, board);
                    bool shouldExecute = action.ShouldExecute(this, unit, board);

                    action.Precalculate(this, unit, board);
                    action.ShowDebugDecisionInfo(this, unit, board);
                    Debug.Log($"Current action: {action.GetType().Name}, press A to execute");
                    yield return WaitForDebugKey(KeyCode.A);

                    if (shouldExecute)
                    {
                        yield return null;
                        yield return action.ExecuteDecision(this, unit, board);
                    }

                    if (action == null || unit == null)
                    {
                        break;
                    }

                    action.CleanUpDecision(this, unit, board);
                }

                if (unit == null)
                {
                    continue;
                }

                unit.MarkAsFriendly();
            }

            cellGrid.RequestEndTurn();
            yield return null;
        }

        private IEnumerator ExecuteTurn(CellGrid cellGrid)
        {
            IReadOnlyList<Unit> orderedUnits = SelectUnits(cellGrid);

            if (!DebugMode)
            {
                yield return AiTurnRunner.ExecuteTurn(
                    this,
                    orderedUnits.Cast<Windy.Srpg.Runtime.Units.IBoardUnit>(),
                    cellGrid,
                    () => cellGrid.RequestEndTurn());
                yield break;
            }

            foreach (Unit unit in orderedUnits)
            {
                if (unit == null)
                {
                    continue;
                }

                if (DebugMode)
                {
                    unit.MarkAsSelected();
                    Debug.Log($"Current unit: {unit.name}, press N to continue");
                    yield return WaitForDebugKey(KeyCode.N);
                }

                AiDecisionAction[] actions = unit.GetComponentsInChildren<AiDecisionAction>();
                foreach (AiDecisionAction action in actions)
                {
                    if (action == null || unit == null)
                    {
                        break;
                    }

                    yield return null;

                    action.InitializeDecision(this, unit, cellGrid);
                    bool shouldExecute = action.ShouldExecute(this, unit, cellGrid);

                    if (DebugMode)
                    {
                        action.Precalculate(this, unit, cellGrid);
                        action.ShowDebugDecisionInfo(this, unit, cellGrid);
                        Debug.Log($"Current action: {action.GetType().Name}, press A to execute");
                        yield return WaitForDebugKey(KeyCode.A);
                    }
                    else if (shouldExecute)
                    {
                        yield return null;
                        action.Precalculate(this, unit, cellGrid);
                    }

                    if (shouldExecute)
                    {
                        yield return null;
                        yield return action.ExecuteDecision(this, unit, cellGrid);
                    }

                    if (action == null || unit == null)
                    {
                        break;
                    }

                    action.CleanUpDecision(this, unit, cellGrid);
                }

                if (unit == null)
                {
                    continue;
                }

                unit.MarkAsFriendly();
            }

            cellGrid.RequestEndTurn();
            yield return null;
        }

        private IReadOnlyList<BoardUnit> SelectRuntimeUnits(BattleBoard board, CellGrid cellGrid)
        {
            var selector = GetComponent<UnitSelection>();
            if (selector == null)
            {
                return AiTurnOrdering.OrderByMovementFreedom(board.GetCurrentPlayerUnits());
            }

            List<Unit> playerUnits = cellGrid.GetUnitsForPlayer(board.CurrentPlayerId);
            return selector
                .SelectNext(() => playerUnits, cellGrid)
                .Where(unit => unit != null)
                .Select(unit => unit.GetComponent<BoardUnit>())
                .Where(unit => unit != null)
                .ToList();
        }

        private IReadOnlyList<Unit> SelectUnits(CellGrid cellGrid)
        {
            var selector = GetComponent<UnitSelection>();
            if (selector == null)
            {
                List<BoardUnit> runtimeUnits = cellGrid.GetCurrentPlayerUnits()
                    .Where(unit => unit != null)
                    .Select(unit => unit.GetComponent<BoardUnit>())
                    .Where(unit => unit != null)
                    .ToList();

                IReadOnlyList<Unit> orderedUnits = AiTurnOrdering.OrderByMovementFreedom(runtimeUnits)
                    .Select(unit => unit != null ? unit.GetComponent<Unit>() : null)
                    .Where(unit => unit != null)
                    .ToList();

                if (orderedUnits.Count > 0)
                {
                    return orderedUnits;
                }

                return cellGrid.GetCurrentPlayerUnits().Where(unit => unit != null).ToList();
            }

            return selector
                .SelectNext(() => cellGrid.GetCurrentPlayerUnits().ToList(), cellGrid)
                .Where(unit => unit != null)
                .ToList();
        }

        private static IEnumerator WaitForDebugKey(KeyCode keyCode)
        {
            while (!Input.GetKeyDown(keyCode))
            {
                yield return null;
            }

            yield return null;
        }

        private void OnGameEnded(object sender, System.EventArgs e)
        {
            StopAllCoroutines();
            if (sender is CellGrid cellGrid)
            {
                cellGrid.SyncStateToGameOver();
            }
        }

        private void Reset()
        {
            if (GetComponent<UnitSelection>() == null)
            {
                gameObject.AddComponent<MovementFreedomUnitSelection>();
            }
        }
    }
}

