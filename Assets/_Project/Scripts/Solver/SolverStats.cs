using System.Collections.Generic;

namespace TicTacFade.Solver
{
    /// <summary>Size and timing of a solve, plus how outcomes are distributed.</summary>
    public sealed class SolverStats
    {
        /// <summary>Positions reachable from the initial one (the game hasn't ended in them).</summary>
        public int PositionCount { get; }

        /// <summary>Legal moves from all positions, immediate wins included.</summary>
        public long MoveCount { get; }

        public int WinCount { get; }
        public int LossCount { get; }
        public int NoForcedWinCount { get; }

        /// <summary>Longest forced win / longest forced loss over all reachable positions, in moves.</summary>
        public int MaxWinDistance { get; }
        public int MaxLossDistance { get; }

        /// <summary>Positions per distance (index = distance), Win and Loss together; index 0 unused.</summary>
        public IReadOnlyList<int> PositionsByDistance { get; }

        public double ExploreMilliseconds { get; }
        public double RetrogradeMilliseconds { get; }
        public double TotalMilliseconds => ExploreMilliseconds + RetrogradeMilliseconds;

        public SolverStats(int positionCount, long moveCount, int winCount, int lossCount, int noForcedWinCount,
            int maxWinDistance, int maxLossDistance, IReadOnlyList<int> positionsByDistance,
            double exploreMilliseconds, double retrogradeMilliseconds)
        {
            PositionCount = positionCount;
            MoveCount = moveCount;
            WinCount = winCount;
            LossCount = lossCount;
            NoForcedWinCount = noForcedWinCount;
            MaxWinDistance = maxWinDistance;
            MaxLossDistance = maxLossDistance;
            PositionsByDistance = positionsByDistance;
            ExploreMilliseconds = exploreMilliseconds;
            RetrogradeMilliseconds = retrogradeMilliseconds;
        }
    }
}
