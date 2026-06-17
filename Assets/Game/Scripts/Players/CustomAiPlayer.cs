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
    public sealed class CustomAiPlayer : CustomPlayer
    {
        [SerializeField] private bool debugMode;

        public bool DebugMode => debugMode;
        public override bool IsHumanControlled => false;

        protected override void OnInitialize(CustomCellGrid cellGrid)
        {
            cellGrid.BattleEnded += OnGameEnded;
        }

        private void OnDisable()
        {
            StopAllCoroutines();
        }

        public override void Play(CustomCellGrid cellGrid)
        {
            cellGrid.EnterAiTurnState(this);
            StartCoroutine(ExecuteRuntimeRoutedTurn(cellGrid));
        }

        private IEnumerator ExecuteRuntimeRoutedTurn(CustomCellGrid cellGrid)
        {
            BattleBoard board = cellGrid.GetComponent<BattleBoard>();
            if (board == null)
            {
                yield return ExecuteTurn(cellGrid);
                yield break;
            }

            IReadOnlyList<BattleUnit> runtimeOrder = SelectRuntimeUnits(board, cellGrid);
            cellGrid.PrepareRuntimeRoutedAiTurn();

            if (!DebugMode)
            {
                yield return AiTurnRunner.ExecuteTurn(
                    this,
                    runtimeOrder.Cast<IBattleUnit>(),
                    board,
                    () => cellGrid.RequestEndTurn());
                yield break;
            }

            foreach (BattleUnit runtimeUnit in runtimeOrder)
            {
                CustomUnit unit = runtimeUnit?.GetComponent<CustomUnit>();
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

        private IEnumerator ExecuteTurn(CustomCellGrid cellGrid)
        {
            IReadOnlyList<CustomUnit> orderedUnits = SelectUnits(cellGrid);

            if (!DebugMode)
            {
                yield return AiTurnRunner.ExecuteTurn(
                    this,
                    orderedUnits.Cast<Windy.Srpg.Runtime.Units.IBattleUnit>(),
                    cellGrid,
                    () => cellGrid.RequestEndTurn());
                yield break;
            }

            foreach (CustomUnit unit in orderedUnits)
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

        private IReadOnlyList<BattleUnit> SelectRuntimeUnits(BattleBoard board, CustomCellGrid cellGrid)
        {
            var selector = GetComponent<CustomUnitSelection>();
            if (selector == null)
            {
                return AiTurnOrdering.OrderByMovementFreedom(board.GetCurrentPlayerUnits());
            }

            List<CustomUnit> playerUnits = cellGrid.GetUnitsForPlayer(board.CurrentPlayerId);
            return selector
                .SelectNext(() => playerUnits, cellGrid)
                .Where(unit => unit != null)
                .Select(unit => unit.GetComponent<BattleUnit>())
                .Where(unit => unit != null)
                .ToList();
        }

        private IReadOnlyList<CustomUnit> SelectUnits(CustomCellGrid cellGrid)
        {
            var selector = GetComponent<CustomUnitSelection>();
            if (selector == null)
            {
                List<BattleUnit> runtimeUnits = cellGrid.GetCurrentPlayerCustomUnits()
                    .Where(unit => unit != null)
                    .Select(unit => unit.GetComponent<BattleUnit>())
                    .Where(unit => unit != null)
                    .ToList();

                IReadOnlyList<CustomUnit> orderedUnits = AiTurnOrdering.OrderByMovementFreedom(runtimeUnits)
                    .Select(unit => unit != null ? unit.GetComponent<CustomUnit>() : null)
                    .Where(unit => unit != null)
                    .ToList();

                if (orderedUnits.Count > 0)
                {
                    return orderedUnits;
                }

                return cellGrid.GetCurrentPlayerCustomUnits().Where(unit => unit != null).ToList();
            }

            return selector
                .SelectNext(() => cellGrid.GetCurrentPlayerCustomUnits().ToList(), cellGrid)
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
            if (sender is CustomCellGrid cellGrid)
            {
                cellGrid.SyncCustomStateToGameOver();
            }
        }

        private void Reset()
        {
            if (GetComponent<CustomUnitSelection>() == null)
            {
                gameObject.AddComponent<CustomMovementFreedomUnitSelection>();
            }
        }
    }
}
