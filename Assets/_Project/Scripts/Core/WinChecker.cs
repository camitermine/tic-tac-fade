using System.Collections.Generic;

namespace TicTacFade.Core
{
    /// <summary>
    /// Generic win check (m,n,k-game style): looks for <paramref name="winLength"/>
    /// consecutive pieces of the same player on a square board of
    /// <paramref name="boardSize"/> x <paramref name="boardSize"/>, in any of
    /// the 4 directions (horizontal, vertical, diagonal-\, diagonal-/).
    /// Doesn't assume 3x3 or WinLength=3: the parameters come from <see cref="GameConfig"/>.
    /// </summary>
    public static class WinChecker
    {
        static readonly (int DeltaRow, int DeltaCol)[] Directions =
        {
            (0, 1),  // horizontal
            (1, 0),  // vertical
            (1, 1),  // diagonal \
            (1, -1), // diagonal /
        };

        public static bool TryGetWinningLine(IReadOnlyList<Occupant> cells, int boardSize, int winLength, Occupant player, out int[] line)
        {
            for (int row = 0; row < boardSize; row++)
            {
                for (int col = 0; col < boardSize; col++)
                {
                    if (cells[row * boardSize + col] != player)
                        continue;

                    foreach (var (deltaRow, deltaCol) in Directions)
                    {
                        if (TryBuildLine(cells, boardSize, winLength, player, row, col, deltaRow, deltaCol, out line))
                            return true;
                    }
                }
            }

            line = null;
            return false;
        }

        static bool TryBuildLine(IReadOnlyList<Occupant> cells, int boardSize, int winLength, Occupant player,
            int startRow, int startCol, int deltaRow, int deltaCol, out int[] line)
        {
            int endRow = startRow + deltaRow * (winLength - 1);
            int endCol = startCol + deltaCol * (winLength - 1);
            if (endRow < 0 || endRow >= boardSize || endCol < 0 || endCol >= boardSize)
            {
                line = null;
                return false;
            }

            var candidate = new int[winLength];
            for (int i = 0; i < winLength; i++)
            {
                int row = startRow + deltaRow * i;
                int col = startCol + deltaCol * i;
                int index = row * boardSize + col;
                if (cells[index] != player)
                {
                    line = null;
                    return false;
                }

                candidate[i] = index;
            }

            line = candidate;
            return true;
        }
    }
}
