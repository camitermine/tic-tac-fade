using System;
using System.Collections.Generic;

namespace TicTacFade.Core
{
    /// <summary>
    /// Game state, immutable: no instance changes after it's created.
    /// <see cref="RulesEngine.Apply"/> is the only way to advance from one
    /// <see cref="GameState"/> to the next.
    /// </summary>
    public sealed class GameState
    {
        public GameConfig Config { get; }
        public IReadOnlyList<Occupant> Cells { get; }
        public IReadOnlyList<int> QueueX { get; }
        public IReadOnlyList<int> QueueO { get; }
        public Occupant CurrentPlayer { get; }
        public int TotalMoves { get; }
        public bool IsOver { get; }
        public Occupant Winner { get; }
        public GameEndReason? EndReason { get; }
        public IReadOnlyList<int> WinningLine { get; }

        /// <summary>
        /// Occurrence count per position, for the draw-by-repetition rule
        /// (GDD §3.4). This is an implementation detail of RulesEngine: it's
        /// not exposed publicly, only to this project's tests via
        /// <c>InternalsVisibleTo</c> (see AssemblyInfo.cs).
        /// </summary>
        internal IReadOnlyDictionary<PositionKey, int> PositionCounts { get; }

        internal GameState(
            GameConfig config,
            IReadOnlyList<Occupant> cells,
            IReadOnlyList<int> queueX,
            IReadOnlyList<int> queueO,
            Occupant currentPlayer,
            int totalMoves,
            bool isOver,
            Occupant winner,
            GameEndReason? endReason,
            IReadOnlyList<int> winningLine,
            IReadOnlyDictionary<PositionKey, int> positionCounts)
        {
            Config = config;
            Cells = cells;
            QueueX = queueX;
            QueueO = queueO;
            CurrentPlayer = currentPlayer;
            TotalMoves = totalMoves;
            IsOver = isOver;
            Winner = winner;
            EndReason = endReason;
            WinningLine = winningLine ?? Array.Empty<int>();
            PositionCounts = positionCounts;
        }

        public static GameState CreateInitial(GameConfig config, Occupant startingPlayer)
        {
            var cells = new Occupant[config.CellCount];
            var queueX = Array.Empty<int>();
            var queueO = Array.Empty<int>();

            // The initial position (empty board, `startingPlayer` starts) counts
            // as its own 1st occurrence. With the MVP parameters this exact
            // position can never be reached again (once the queues fill up, they
            // never empty back out), but recording it keeps the semantics of
            // "position" consistent if the parameters change down the line.
            var initialKey = PositionKey.Compute(config, queueX, queueO, startingPlayer);
            var positionCounts = new Dictionary<PositionKey, int> { [initialKey] = 1 };

            return new GameState(
                config,
                cells,
                queueX,
                queueO,
                startingPlayer,
                totalMoves: 0,
                isOver: false,
                winner: Occupant.None,
                endReason: null,
                winningLine: Array.Empty<int>(),
                positionCounts: positionCounts);
        }

        public int GetActiveCount(Occupant player) => GetQueue(player).Count;

        /// <summary>
        /// Visual life of the piece on that cell: <c>Config.BufferSize</c> =
        /// just placed, counting down to 1 = critical (removed on the owner's
        /// next move). 0 if that cell doesn't hold a piece of that player.
        /// </summary>
        public int GetLife(Occupant player, int cellIndex)
        {
            var queue = GetQueue(player);
            int n = queue.Count;
            for (int p = 0; p < n; p++)
            {
                if (queue[p] == cellIndex)
                    return p + (Config.BufferSize + 1 - n);
            }

            return 0;
        }

        public IReadOnlyList<int> GetFreeCellIndices()
        {
            var free = new List<int>();
            for (int i = 0; i < Cells.Count; i++)
            {
                if (Cells[i] == Occupant.None)
                    free.Add(i);
            }

            return free;
        }

        internal IReadOnlyList<int> GetQueue(Occupant player) => player == Occupant.X ? QueueX : QueueO;
    }
}
