using System.Collections.Generic;
using System.Linq;
using Windy.Srpg.Runtime.Players;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Runtime.Board
{
    public readonly struct RoundRobinTurnPlan
    {
        public RoundRobinTurnPlan(IBattlePlayer nextPlayer, IReadOnlyList<IBattleUnit> playableUnits)
        {
            NextPlayer = nextPlayer;
            PlayableUnits = playableUnits ?? System.Array.Empty<IBattleUnit>();
        }

        public IBattlePlayer NextPlayer { get; }
        public IReadOnlyList<IBattleUnit> PlayableUnits { get; }
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
        public static RoundRobinTurnPlan ResolveStart(IBattleBoard board)
        {
            return board == null
                ? new RoundRobinTurnPlan(null, System.Array.Empty<IBattleUnit>())
                : ResolveStart(board.Players, board.Units);
        }

        public static RoundRobinTurnPlan ResolveStart(IEnumerable<IBattlePlayer> players, IEnumerable<IBattleUnit> units)
        {
            List<IBattlePlayer> orderedPlayers = OrderPlayers(players);
            if (orderedPlayers.Count == 0)
            {
                return new RoundRobinTurnPlan(null, System.Array.Empty<IBattleUnit>());
            }

            IBattlePlayer firstActivePlayer = orderedPlayers[0];
            return new RoundRobinTurnPlan(firstActivePlayer, GetUnitsForPlayer(units, firstActivePlayer.PlayerId));
        }

        public static RoundRobinTurnPlan ResolveTurn(IBattleBoard board)
        {
            return board == null
                ? new RoundRobinTurnPlan(null, System.Array.Empty<IBattleUnit>())
                : ResolveTurn(board.Players, board.Units, board.CurrentPlayerId);
        }

        public static RoundRobinTurnPlan ResolveTurn(IEnumerable<IBattlePlayer> players, IEnumerable<IBattleUnit> units, int currentPlayerId)
        {
            List<IBattlePlayer> orderedPlayers = OrderPlayers(players);
            if (orderedPlayers.Count == 0)
            {
                return new RoundRobinTurnPlan(null, System.Array.Empty<IBattleUnit>());
            }

            int startIndex = FindNextPlayerStartIndex(orderedPlayers, currentPlayerId);
            for (int offset = 0; offset < orderedPlayers.Count; offset++)
            {
                IBattlePlayer candidate = orderedPlayers[(startIndex + offset) % orderedPlayers.Count];
                List<IBattleUnit> candidateUnits = GetUnitsForPlayer(units, candidate.PlayerId);
                if (candidateUnits.Count > 0)
                {
                    return new RoundRobinTurnPlan(candidate, candidateUnits);
                }
            }

            IBattlePlayer fallbackPlayer = orderedPlayers[startIndex % orderedPlayers.Count];
            return new RoundRobinTurnPlan(fallbackPlayer, GetUnitsForPlayer(units, fallbackPlayer.PlayerId));
        }

        public static BattleOutcome EvaluateLastSideStanding(IBattleBoard board)
        {
            return board == null
                ? new BattleOutcome(false, null, null)
                : EvaluateLastSideStanding(board.Players, board.Units);
        }

        public static BattleOutcome EvaluateLastSideStanding(IEnumerable<IBattlePlayer> players, IEnumerable<IBattleUnit> units)
        {
            List<IBattleUnit> survivingUnits = units?
                .Where(unit => unit != null)
                .ToList()
                ?? new List<IBattleUnit>();

            List<IBattlePlayer> orderedPlayers = BattleBoardQueries.OrderPlayers(players);
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
            return BattleBoardQueries.OrderPlayers(players);
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

        private static List<IBattleUnit> GetUnitsForPlayer(IEnumerable<IBattleUnit> units, int playerId)
        {
            return BattleBoardQueries.GetUnitsForPlayer(units, playerId);
        }
    }

    public static class BattleBoardQueries
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

        public static List<TUnit> GetUnitsForPlayer<TUnit>(IEnumerable<TUnit> units, int playerId)
            where TUnit : class, IBattleUnit
        {
            return units?
                .Where(unit => unit != null && unit.PlayerId == playerId)
                .ToList()
                ?? new List<TUnit>();
        }

        public static List<TUnit> GetEnemyUnits<TUnit>(IEnumerable<TUnit> units, int playerId)
            where TUnit : class, IBattleUnit
        {
            return units?
                .Where(unit => unit != null && unit.PlayerId != playerId)
                .ToList()
                ?? new List<TUnit>();
        }

        public static List<TUnit> GetCurrentPlayerUnits<TUnit>(IEnumerable<TUnit> units, int currentPlayerId)
            where TUnit : class, IBattleUnit
        {
            return GetUnitsForPlayer(units, currentPlayerId);
        }
    }
}
