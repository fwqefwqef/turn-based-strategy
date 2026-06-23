using System.Collections.Generic;
using System.Linq;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Runtime.Players;

namespace Windy.Srpg.Runtime.Grid
{
    public readonly struct RoundRobinTurnPlan
    {
        public RoundRobinTurnPlan(IBattlePlayer nextPlayer, IReadOnlyList<Unit> playableUnits)
        {
            NextPlayer = nextPlayer;
            PlayableUnits = playableUnits ?? System.Array.Empty<Unit>();
        }

        public IBattlePlayer NextPlayer { get; }
        public IReadOnlyList<Unit> PlayableUnits { get; }
    }

    public readonly struct BattleOutcome
    {
        public BattleOutcome(bool isFinished, IReadOnlyList<int> winningPlayerIds, IReadOnlyList<int> defeatedPlayerIds)
        {
            IsFinished = isFinished;
            WinningPlayerIds = winningPlayerIds ?? System.Array.Empty<int>();
            DefeatedPlayerIds = defeatedPlayerIds ?? System.Array.Empty<int>();
        }

        public bool IsFinished { get; }
        public IReadOnlyList<int> WinningPlayerIds { get; }
        public IReadOnlyList<int> DefeatedPlayerIds { get; }
    }

    public static class RoundRobinBattleFlow
    {
        public static RoundRobinTurnPlan ResolveStart(Game.Grid.CellGrid grid)
        {
            return grid == null
                ? new RoundRobinTurnPlan(null, System.Array.Empty<Unit>())
                : ResolveStart(
                    grid.GetOrderedPlayers().Cast<IBattlePlayer>(),
                    grid.GetAllUnits());
        }

        public static RoundRobinTurnPlan ResolveStart(IEnumerable<IBattlePlayer> players, IEnumerable<Unit> units)
        {
            List<IBattlePlayer> orderedPlayers = OrderPlayers(players);
            if (orderedPlayers.Count == 0)
            {
                return new RoundRobinTurnPlan(null, System.Array.Empty<Unit>());
            }

            IBattlePlayer firstActivePlayer = orderedPlayers[0];
            return new RoundRobinTurnPlan(firstActivePlayer, GetUnitsForPlayer(units, firstActivePlayer.PlayerId));
        }

        public static RoundRobinTurnPlan ResolveTurn(Game.Grid.CellGrid grid)
        {
            return grid == null
                ? new RoundRobinTurnPlan(null, System.Array.Empty<Unit>())
                : ResolveTurn(
                    grid.GetOrderedPlayers().Cast<IBattlePlayer>(),
                    grid.GetAllUnits(),
                    grid.CurrentPlayerId);
        }

        public static RoundRobinTurnPlan ResolveTurn(IEnumerable<IBattlePlayer> players, IEnumerable<Unit> units, int currentPlayerId)
        {
            List<IBattlePlayer> orderedPlayers = OrderPlayers(players);
            if (orderedPlayers.Count == 0)
            {
                return new RoundRobinTurnPlan(null, System.Array.Empty<Unit>());
            }

            int startIndex = FindNextPlayerStartIndex(orderedPlayers, currentPlayerId);
            for (int offset = 0; offset < orderedPlayers.Count; offset++)
            {
                IBattlePlayer candidate = orderedPlayers[(startIndex + offset) % orderedPlayers.Count];
                List<Unit> candidateUnits = GetUnitsForPlayer(units, candidate.PlayerId);
                if (candidateUnits.Count > 0)
                {
                    return new RoundRobinTurnPlan(candidate, candidateUnits);
                }
            }

            IBattlePlayer fallbackPlayer = orderedPlayers[startIndex % orderedPlayers.Count];
            return new RoundRobinTurnPlan(fallbackPlayer, GetUnitsForPlayer(units, fallbackPlayer.PlayerId));
        }

        public static BattleOutcome EvaluateLastSideStanding(Game.Grid.CellGrid grid)
        {
            return grid == null
                ? new BattleOutcome(false, null, null)
                : EvaluateLastSideStanding(
                    grid.GetOrderedPlayers().Cast<IBattlePlayer>(),
                    grid.GetAllUnits());
        }

        public static BattleOutcome EvaluateLastSideStanding(IEnumerable<IBattlePlayer> players, IEnumerable<Unit> units)
        {
            List<Unit> survivingUnits = units?
                .Where(unit => unit != null)
                .ToList()
                ?? new List<Unit>();

            List<IBattlePlayer> orderedPlayers = GridQueries.OrderPlayers(players);
            if (survivingUnits.Count == 0 || orderedPlayers.Count == 0)
            {
                return new BattleOutcome(false, null, null);
            }

            List<int> survivingPlayerIds = survivingUnits
                .Select(unit => unit.PlayerId)
                .Distinct()
                .OrderBy(playerId => playerId)
                .ToList();

            if (survivingPlayerIds.Count != 1)
            {
                return new BattleOutcome(false, null, null);
            }

            int winningPlayerId = survivingPlayerIds[0];
            List<int> defeatedPlayerIds = orderedPlayers
                .Where(player => player.PlayerId != winningPlayerId)
                .Select(player => player.PlayerId)
                .OrderBy(playerId => playerId)
                .ToList();

            return new BattleOutcome(true, survivingPlayerIds, defeatedPlayerIds);
        }

        private static List<IBattlePlayer> OrderPlayers(IEnumerable<IBattlePlayer> players)
        {
            return GridQueries.OrderPlayers(players);
        }

        private static int FindNextPlayerStartIndex(IReadOnlyList<IBattlePlayer> orderedPlayers, int currentPlayerId)
        {
            for (int i = 0; i < orderedPlayers.Count; i++)
            {
                if (orderedPlayers[i].PlayerId == currentPlayerId)
                {
                    return (i + 1) % orderedPlayers.Count;
                }
            }

            return 0;
        }

        private static List<Unit> GetUnitsForPlayer(IEnumerable<Unit> units, int playerId)
        {
            return GridQueries.GetUnitsForPlayer(units, playerId);
        }
    }

    public static class GridQueries
    {
        public static List<TPlayer> OrderPlayers<TPlayer>(IEnumerable<TPlayer> players)
            where TPlayer : class, IBattlePlayer
        {
            return players?
                .Where(player => player != null)
                .OrderBy(player => player.PlayerId)
                .ToList()
                ?? new List<TPlayer>();
        }

        public static TPlayer GetPlayerById<TPlayer>(IEnumerable<TPlayer> players, int playerId)
            where TPlayer : class, IBattlePlayer
        {
            return OrderPlayers(players)
                .FirstOrDefault(player => player.PlayerId == playerId);
        }

        public static List<Unit> GetUnitsForPlayer(IEnumerable<Unit> units, int playerId)
        {
            return units?
                .Where(unit => unit != null && unit.PlayerId == playerId)
                .ToList()
                ?? new List<Unit>();
        }

        public static List<Unit> GetEnemyUnits(IEnumerable<Unit> units, int playerId)
        {
            return units?
                .Where(unit => unit != null && unit.PlayerId != playerId)
                .ToList()
                ?? new List<Unit>();
        }

        public static List<Unit> GetCurrentPlayerUnits(IEnumerable<Unit> units, int currentPlayerId)
        {
            return GetUnitsForPlayer(units, currentPlayerId);
        }
    }
}
