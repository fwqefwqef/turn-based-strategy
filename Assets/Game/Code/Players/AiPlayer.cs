using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Players.AI;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Runtime.AI;

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
            StartCoroutine(ExecuteTurn(cellGrid));
        }

        private IEnumerator ExecuteTurn(CellGrid cellGrid)
        {
            IReadOnlyList<Unit> orderedUnits = SelectUnits(cellGrid);

            if (!DebugMode)
            {
                yield return AiTurnRunner.ExecuteTurn(
                    this,
                    orderedUnits,
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

                    action.InitializeDecision(this, unit, cellGrid);
                    bool shouldExecute = action.ShouldExecute(this, unit, cellGrid);

                    action.Precalculate(this, unit, cellGrid);
                    action.ShowDebugDecisionInfo(this, unit, cellGrid);
                    Debug.Log($"Current action: {action.GetType().Name}, press A to execute");
                    yield return WaitForDebugKey(KeyCode.A);

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

        private IReadOnlyList<Unit> SelectUnits(CellGrid cellGrid)
        {
            var selector = GetComponent<UnitSelection>();
            if (selector == null)
            {
                return AiTurnOrdering.OrderByMovementFreedom(
                    cellGrid.GetCurrentPlayerUnits().Where(unit => unit != null),
                    cellGrid);
            }

            List<Unit> playerUnits = cellGrid.GetCurrentPlayerUnits()
                .Where(unit => unit != null)
                .Distinct()
                .ToList();

            return selector
                .SelectNext(() => playerUnits, cellGrid)
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
