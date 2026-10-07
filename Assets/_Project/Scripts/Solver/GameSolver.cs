using System;
using System.Collections.Generic;
using System.Diagnostics;
using TicTacFade.Core;

namespace TicTacFade.Solver
{
    /// <summary>
    /// Solves the game by retrograde analysis (GDD §8.1, ADR 0004).
    /// <list type="number">
    /// <item>Forward pass: every position reachable from the initial one,
    /// with its legal moves. A winning move ends the game, so it is an edge
    /// to "won", not a position.</item>
    /// <item>Backward pass: a position with a winning move is a Win in 1; a
    /// position with a move into a Loss is a Win (shortest such move); a
    /// position whose moves all lead to Wins is a Loss (longest such move).
    /// Positions are settled in increasing distance order.</item>
    /// <item>Whatever is left unsettled is <see cref="PositionOutcome.NoForcedWin"/>:
    /// neither side can force a win, the game cycles.</item>
    /// </list>
    /// The draw limits (<see cref="GameConfig.MaxTotalMoves"/>,
    /// <see cref="GameConfig.RepetitionLimit"/>) are not part of a position
    /// and are ignored; see ADR 0004 for why the values still hold.
    /// </summary>
    public static class GameSolver
    {
        /// <summary>
        /// Upper bound on the state space above which <see cref="Solve"/>
        /// refuses to run (3x3 with buffer 3 is far below; 4x4 with buffer 4
        /// is about 10^9).
        /// </summary>
        public const long MaxPositions = 5_000_000;

        public static SolverTable Solve(GameConfig config, Occupant startingPlayer = Occupant.X)
        {
            Validate(config, startingPlayer);

            var explore = Stopwatch.StartNew();
            var indexByKey = new Dictionary<PositionKey, int>();
            var positions = new List<SolverPosition>();
            var moves = new List<SolverMoveEdge[]>();
            long moveCount = Explore(config, SolverPosition.Initial(config, startingPlayer), indexByKey, positions, moves);
            explore.Stop();

            var retrograde = Stopwatch.StartNew();
            var results = SolveBackwards(moves);
            retrograde.Stop();

            var stats = BuildStats(results, moveCount, explore.Elapsed.TotalMilliseconds, retrograde.Elapsed.TotalMilliseconds);
            return new SolverTable(config, indexByKey, positions, moves, results, positions[0].Key, stats);
        }

        /// <summary>
        /// Upper bound on the positions of a configuration: both queues as
        /// ordered sequences of distinct cells, up to BufferSize each, times
        /// the two players to move. Used to refuse configurations that are too
        /// big before allocating anything.
        /// </summary>
        public static double EstimatePositionUpperBound(GameConfig config)
        {
            int cells = config.CellCount;
            double total = 0;
            for (int x = 0; x <= config.BufferSize; x++)
            {
                for (int o = 0; o <= config.BufferSize; o++)
                {
                    if (x + o > cells) continue;
                    double arrangements = 1;
                    for (int i = 0; i < x + o; i++)
                        arrangements *= cells - i;
                    total += arrangements;
                }
            }
            return 2 * total;
        }

        static void Validate(GameConfig config, Occupant startingPlayer)
        {
            if (startingPlayer != Occupant.X && startingPlayer != Occupant.O)
                throw new ArgumentException($"The starting player must be X or O, not {startingPlayer}.", nameof(startingPlayer));

            if (config.BoardSize < 1 || config.BufferSize < 1 || config.WinLength < 1 || config.WinLength > config.BoardSize)
            {
                throw new ArgumentException(
                    $"Invalid configuration for the solver (BoardSize={config.BoardSize}, BufferSize={config.BufferSize}, WinLength={config.WinLength}).",
                    nameof(config));
            }

            double bound = EstimatePositionUpperBound(config);
            if (bound > MaxPositions)
            {
                throw new ArgumentException(
                    $"Configuration too large to solve: BoardSize={config.BoardSize}, BufferSize={config.BufferSize} allows up to " +
                    $"{bound:N0} positions, over the limit of {MaxPositions:N0}. The solver is meant for 3x3 with buffer 3.",
                    nameof(config));
            }

            try
            {
                PositionKey.Compute(config, Array.Empty<int>(), Array.Empty<int>(), startingPlayer);
            }
            catch (InvalidOperationException e)
            {
                throw new ArgumentException($"Configuration too large to solve: {e.Message}", nameof(config), e);
            }
        }

        // Breadth-first over reachable positions. Index 0 is the initial position.
        static long Explore(GameConfig config, SolverPosition initial, Dictionary<PositionKey, int> indexByKey,
            List<SolverPosition> positions, List<SolverMoveEdge[]> moves)
        {
            indexByKey[initial.Key] = 0;
            positions.Add(initial);

            var generated = new List<(int Cell, bool Wins, SolverPosition Next)>();
            long moveCount = 0;
            for (int index = 0; index < positions.Count; index++)
            {
                positions[index].GenerateMoves(config, generated);
                var edges = new SolverMoveEdge[generated.Count];
                for (int i = 0; i < generated.Count; i++)
                {
                    var (cell, wins, next) = generated[i];
                    int nextIndex = -1;
                    if (!wins && !indexByKey.TryGetValue(next.Key, out nextIndex))
                    {
                        nextIndex = positions.Count;
                        indexByKey[next.Key] = nextIndex;
                        positions.Add(next);
                    }
                    edges[i] = new SolverMoveEdge(cell, nextIndex);
                }
                moves.Add(edges);
                moveCount += edges.Length;
            }
            return moveCount;
        }

        static PositionResult[] SolveBackwards(List<SolverMoveEdge[]> moves)
        {
            int count = moves.Count;
            var results = new PositionResult[count];
            var settled = new bool[count];
            var unsettledMoves = new int[count]; // moves to positions not yet known to be Wins
            var predecessors = BuildPredecessors(moves);
            var queue = new Queue<int>();

            for (int index = 0; index < count; index++)
            {
                var edges = moves[index];
                bool hasImmediateWin = false;
                foreach (var edge in edges)
                    hasImmediateWin |= edge.Next < 0;

                if (hasImmediateWin)
                {
                    Settle(index, new PositionResult(PositionOutcome.Win, 1));
                    continue;
                }
                unsettledMoves[index] = edges.Length;
            }

            // FIFO order settles positions by increasing distance: every
            // position settled while processing distance d gets d + 1.
            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                var result = results[index];
                foreach (int previous in predecessors[index])
                {
                    if (settled[previous]) continue;

                    if (result.Outcome == PositionOutcome.Loss)
                    {
                        Settle(previous, new PositionResult(PositionOutcome.Win, result.Distance + 1));
                    }
                    else if (--unsettledMoves[previous] == 0)
                    {
                        // Its last move into a Win settled now: the longest one.
                        Settle(previous, new PositionResult(PositionOutcome.Loss, result.Distance + 1));
                    }
                }
            }

            for (int index = 0; index < count; index++)
            {
                if (!settled[index])
                    results[index] = PositionResult.NoForcedWin;
            }
            return results;

            void Settle(int index, PositionResult result)
            {
                settled[index] = true;
                results[index] = result;
                queue.Enqueue(index);
            }
        }

        // One entry per move, so a position reached by two moves would count twice
        // (it can't happen here: different cells give different queues).
        static List<int>[] BuildPredecessors(List<SolverMoveEdge[]> moves)
        {
            var predecessors = new List<int>[moves.Count];
            for (int index = 0; index < predecessors.Length; index++)
                predecessors[index] = new List<int>();

            for (int index = 0; index < moves.Count; index++)
            {
                foreach (var edge in moves[index])
                {
                    if (edge.Next >= 0)
                        predecessors[edge.Next].Add(index);
                }
            }
            return predecessors;
        }

        static SolverStats BuildStats(PositionResult[] results, long moveCount, double exploreMs, double retrogradeMs)
        {
            int wins = 0, losses = 0, none = 0, maxWin = 0, maxLoss = 0;
            var byDistance = new List<int>();
            foreach (var result in results)
            {
                switch (result.Outcome)
                {
                    case PositionOutcome.Win:
                        wins++;
                        maxWin = Math.Max(maxWin, result.Distance);
                        break;
                    case PositionOutcome.Loss:
                        losses++;
                        maxLoss = Math.Max(maxLoss, result.Distance);
                        break;
                    default:
                        none++;
                        continue;
                }

                while (byDistance.Count <= result.Distance)
                    byDistance.Add(0);
                byDistance[result.Distance]++;
            }

            return new SolverStats(results.Length, moveCount, wins, losses, none, maxWin, maxLoss, byDistance, exploreMs, retrogradeMs);
        }
    }
}
