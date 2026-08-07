using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Windy.Srpg.Game.CameraControl;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.Grid
{
    [Serializable]
    public sealed class ReinforcementUnitEntry
    {
        [Min(0)] public int PlayerNumber = 1;
        public UnitPreset Preset;
        public UnitPresetOverride PresetOverride = new UnitPresetOverride();
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class ReinforcementTile : MonoBehaviour
    {
        [SerializeField] private Cell boardCell;
        [SerializeField] private List<ReinforcementUnitEntry> units = new List<ReinforcementUnitEntry>();
        [SerializeField] private List<int> spawnTurns = new List<int>();
        [SerializeField] private Unit spawner;

        private CellGrid cellGrid;
        private int nextSpawnIndex;
        private bool requiresLivingSpawner;

        public Cell Cell => boardCell;
        public IReadOnlyList<ReinforcementUnitEntry> Units => units;
        public IReadOnlyList<int> SpawnTurns => spawnTurns;
        public Unit Spawner => spawner;
        public int NextSpawnIndex => nextSpawnIndex;
        public bool IsOperational => !requiresLivingSpawner || IsSpawnerAlive();
        public bool HasPendingSpawns => IsOperational && units.Count > 0 && nextSpawnIndex < spawnTurns.Count;

        private void Awake()
        {
            EnsureRegistryCellBinding();
            SyncToCell();
            requiresLivingSpawner = spawner != null;
            nextSpawnIndex = 0;
            BindToGrid();
        }

        private void OnEnable()
        {
            EnsureRegistryCellBinding();
            SyncToCell();
            BindToGrid();
        }

        private void Start()
        {
            BindToGrid();
        }

        private void OnDisable()
        {
            UnbindFromGrid();
        }

        private void OnValidate()
        {
            units ??= new List<ReinforcementUnitEntry>();
            foreach (ReinforcementUnitEntry entry in units.Where(entry => entry != null))
            {
                entry.PlayerNumber = Mathf.Max(0, entry.PlayerNumber);
                entry.PresetOverride ??= new UnitPresetOverride();
            }

            spawnTurns ??= new List<int>();
            for (int i = 0; i < spawnTurns.Count; i++)
            {
                spawnTurns[i] = Mathf.Max(1, spawnTurns[i]);
            }

            SyncToCell();
        }

        public void EnsureRegistryCellBinding(Cell[] candidateTiles = null)
        {
            if (boardCell != null)
            {
                return;
            }

            if (candidateTiles == null || candidateTiles.Length == 0)
            {
                candidateTiles = FindObjectsByType<Cell>(FindObjectsInactive.Include);
            }

            const float maxBindingDistanceSqr = 1f;
            float closestDistance = float.MaxValue;
            Vector3 markerPosition = transform.position;
            foreach (Cell candidate in candidateTiles)
            {
                if (candidate == null)
                {
                    continue;
                }

                float distance = (candidate.transform.position - markerPosition).sqrMagnitude;
                if (distance > maxBindingDistanceSqr || distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = distance;
                boardCell = candidate;
            }
        }

        public void Configure(
            Cell linkedCell,
            IEnumerable<ReinforcementUnitEntry> configuredUnits,
            IEnumerable<int> turns,
            Unit linkedSpawner)
        {
            BindToCell(linkedCell);
            units = configuredUnits?
                .Where(entry => entry != null && entry.Preset != null)
                .ToList()
                ?? new List<ReinforcementUnitEntry>();
            spawnTurns = turns?.Select(turn => Mathf.Max(1, turn)).ToList() ?? new List<int>();
            spawner = linkedSpawner;
            requiresLivingSpawner = spawner != null;
            nextSpawnIndex = 0;
            BindToGrid();
        }

        public void BindToCell(Cell cell)
        {
            boardCell = cell;
            SyncToCell();
        }

        public void SyncToCell()
        {
            if (boardCell != null)
            {
                transform.position = boardCell.transform.position;
            }
        }

        private void BindToGrid()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            CellGrid resolvedGrid = boardCell != null
                ? boardCell.GetComponentInParent<CellGrid>()
                : null;
            resolvedGrid ??= GetComponentInParent<CellGrid>() ?? FindAnyObjectByType<CellGrid>();
            if (resolvedGrid == null || resolvedGrid == cellGrid)
            {
                return;
            }

            UnbindFromGrid();
            cellGrid = resolvedGrid;
            cellGrid.TurnStarted += OnTurnStarted;
        }

        private void UnbindFromGrid()
        {
            if (cellGrid != null)
            {
                cellGrid.TurnStarted -= OnTurnStarted;
            }

            cellGrid = null;
        }

        private void OnTurnStarted(object sender, EventArgs e)
        {
            if (cellGrid == null || !cellGrid.IsBattleStarted || !HasPendingSpawns)
            {
                return;
            }

            int currentRound = Mathf.Max(1, cellGrid.RoundCount);
            int currentPlayerId = cellGrid.CurrentPlayerId;
            if (currentPlayerId != 0)
            {
                return;
            }

            if (nextSpawnIndex < spawnTurns.Count
                && Mathf.Max(1, spawnTurns[nextSpawnIndex]) <= currentRound)
            {
                cellGrid.QueueTurnStartPresentation(SpawnDueReinforcements);
            }
        }

        private IEnumerator SpawnDueReinforcements()
        {
            if (cellGrid == null || !cellGrid.IsBattleStarted || !HasPendingSpawns)
            {
                yield break;
            }

            int currentRound = Mathf.Max(1, cellGrid.RoundCount);
            while (nextSpawnIndex < spawnTurns.Count)
            {
                int scheduledRound = Mathf.Max(1, spawnTurns[nextSpawnIndex]);
                if (scheduledRound > currentRound)
                {
                    yield break;
                }

                ReinforcementUnitEntry unitEntry = ResolveUnitEntry(nextSpawnIndex);
                if (unitEntry?.Preset == null)
                {
                    nextSpawnIndex++;
                    continue;
                }

                Cell spawnCell = ResolveSpawnCell();
                if (spawnCell == null)
                {
                    Debug.LogWarning($"ReinforcementTile: No free traversable cell is available near {boardCell?.Coordinates}.", this);
                    yield break;
                }

                Unit spawnedUnit = null;
                GameplayCameraController.BeginPresentationFocus(spawnCell);
                try
                {
                    yield return GameplayCameraController.WaitForFocusSettled(timeoutSeconds: 0f);
                    spawnedUnit = cellGrid.SpawnReinforcementUnit(
                        unitEntry.Preset,
                        unitEntry.PresetOverride,
                        unitEntry.PlayerNumber,
                        spawnCell);
                }
                finally
                {
                    GameplayCameraController.EndPresentationFocus();
                }

                if (spawnedUnit == null)
                {
                    yield break;
                }

                BattleLog.Log(
                    "Reinforcement",
                    $"{spawnedUnit.name} arrived for player {spawnedUnit.PlayerNumber} at {spawnCell.Coordinates}.");
                nextSpawnIndex++;

                if (!IsOperational)
                {
                    yield break;
                }
            }
        }

        private ReinforcementUnitEntry ResolveUnitEntry(int scheduleIndex)
        {
            if (units == null || units.Count == 0 || scheduleIndex < 0)
            {
                return null;
            }

            return units[scheduleIndex % units.Count];
        }

        private bool IsSpawnerAlive()
        {
            return spawner != null && !spawner.ExcludedFromBattle && spawner.HitPoints > 0;
        }

        private Cell ResolveSpawnCell()
        {
            if (boardCell == null || cellGrid == null)
            {
                return null;
            }

            if (IsCellAvailable(boardCell))
            {
                return boardCell;
            }

            IReadOnlyCollection<Cell> allCells = cellGrid.GetAllCells();
            HashSet<Cell> visited = new HashSet<Cell> { boardCell };
            List<Cell> frontier = new List<Cell> { boardCell };

            while (frontier.Count > 0)
            {
                List<Cell> nextFrontier = new List<Cell>();
                List<Cell> availableAtDistance = new List<Cell>();
                foreach (Cell frontierCell in frontier)
                {
                    foreach (Cell neighbour in frontierCell.GetNeighbours(allCells))
                    {
                        if (neighbour == null || !visited.Add(neighbour) || !neighbour.IsTraversable)
                        {
                            continue;
                        }

                        nextFrontier.Add(neighbour);
                        if (IsCellAvailable(neighbour))
                        {
                            availableAtDistance.Add(neighbour);
                        }
                    }
                }

                if (availableAtDistance.Count > 0)
                {
                    return availableAtDistance[UnityEngine.Random.Range(0, availableAtDistance.Count)];
                }

                frontier = nextFrontier;
            }

            return null;
        }

        private static bool IsCellAvailable(Cell candidate)
        {
            return candidate != null
                && candidate.IsTraversable
                && (candidate.CurrentUnits == null
                    || candidate.CurrentUnits.All(unit => unit == null || unit.ExcludedFromBattle));
        }

        private void OnDrawGizmos()
        {
            Vector3 markerPosition = boardCell != null ? boardCell.transform.position : transform.position;
            Gizmos.color = new Color(0.95f, 0.2f, 0.65f, 0.9f);
            Gizmos.DrawWireCube(markerPosition, new Vector3(0.72f, 0.72f, 0.05f));
            Gizmos.DrawLine(markerPosition + Vector3.left * 0.24f, markerPosition + Vector3.right * 0.24f);
            Gizmos.DrawLine(markerPosition + Vector3.down * 0.24f, markerPosition + Vector3.up * 0.24f);
        }
    }
}
