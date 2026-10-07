using TicTacFade.Core;

namespace TicTacFade.Solver
{
    /// <summary>One legal move from a solved position and what it leads to.</summary>
    public readonly struct MoveResult
    {
        public int CellIndex { get; }

        /// <summary>The move wins on the spot (the game ends; there is no next position).</summary>
        public bool IsImmediateWin { get; }

        /// <summary>The position after the move (meaningless when <see cref="IsImmediateWin"/>).</summary>
        public PositionKey NextPosition { get; }

        /// <summary>Value of this move for the player who makes it.</summary>
        public PositionResult Result { get; }

        public MoveResult(int cellIndex, bool isImmediateWin, PositionKey nextPosition, PositionResult result)
        {
            CellIndex = cellIndex;
            IsImmediateWin = isImmediateWin;
            NextPosition = nextPosition;
            Result = result;
        }
    }
}
