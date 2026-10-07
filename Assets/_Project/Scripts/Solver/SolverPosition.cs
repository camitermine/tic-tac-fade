using System;
using System.Collections.Generic;
using TicTacFade.Core;

namespace TicTacFade.Solver
{
    /// <summary>
    /// A position as the solver sees it: both queues in order (oldest first)
    /// and the player to move. Nothing else: no move count, no repetition
    /// history (ADR 0004). The same data <see cref="PositionKey"/> identifies.
    /// </summary>
    internal sealed class SolverPosition
    {
        public int[] QueueX { get; }
        public int[] QueueO { get; }
        public Occupant ToMove { get; }
        public PositionKey Key { get; }

        public SolverPosition(GameConfig config, int[] queueX, int[] queueO, Occupant toMove)
        {
            QueueX = queueX;
            QueueO = queueO;
            ToMove = toMove;
            Key = PositionKey.Compute(config, queueX, queueO, toMove);
        }

        public static SolverPosition Initial(GameConfig config, Occupant startingPlayer) =>
            new SolverPosition(config, Array.Empty<int>(), Array.Empty<int>(), startingPlayer);

        /// <summary>
        /// Every legal move, mirroring <see cref="RulesEngine"/> (GDD §3.2,
        /// §3.3): any empty cell (the mover's oldest piece still occupies its
        /// cell during the move, which is the replacement restriction), then
        /// FIFO, then the win check on the post-FIFO board. Only the mover can
        /// win: a move adds a piece of its own and can only remove one of its
        /// own. Verified against the Core in the EditMode tests.
        /// </summary>
        public void GenerateMoves(GameConfig config, List<(int Cell, bool Wins, SolverPosition Next)> moves)
        {
            moves.Clear();
            var cells = BuildCells(config);
            int[] own = ToMove == Occupant.X ? QueueX : QueueO;
            Occupant opponent = ToMove == Occupant.X ? Occupant.O : Occupant.X;

            for (int cell = 0; cell < cells.Length; cell++)
            {
                if (cells[cell] != Occupant.None)
                    continue;

                int[] newOwn = Push(own, cell, config.BufferSize, out int fadedCell);

                cells[cell] = ToMove;
                if (fadedCell >= 0)
                    cells[fadedCell] = Occupant.None;

                bool wins = WinChecker.TryGetWinningLine(cells, config.BoardSize, config.WinLength, ToMove, out _);
                SolverPosition next = null;
                if (!wins)
                {
                    next = ToMove == Occupant.X
                        ? new SolverPosition(config, newOwn, QueueO, opponent)
                        : new SolverPosition(config, QueueX, newOwn, opponent);
                }
                moves.Add((cell, wins, next));

                // Back to the position before this move.
                cells[cell] = Occupant.None;
                if (fadedCell >= 0)
                    cells[fadedCell] = ToMove;
            }
        }

        Occupant[] BuildCells(GameConfig config)
        {
            var cells = new Occupant[config.CellCount];
            foreach (int cell in QueueX)
                cells[cell] = Occupant.X;
            foreach (int cell in QueueO)
                cells[cell] = Occupant.O;
            return cells;
        }

        // Appends the new piece; drops the oldest when the buffer overflows.
        static int[] Push(int[] queue, int cell, int bufferSize, out int fadedCell)
        {
            if (queue.Length < bufferSize)
            {
                fadedCell = -1;
                var grown = new int[queue.Length + 1];
                Array.Copy(queue, grown, queue.Length);
                grown[queue.Length] = cell;
                return grown;
            }

            fadedCell = queue[0];
            var shifted = new int[queue.Length];
            Array.Copy(queue, 1, shifted, 0, queue.Length - 1);
            shifted[queue.Length - 1] = cell;
            return shifted;
        }
    }
}
