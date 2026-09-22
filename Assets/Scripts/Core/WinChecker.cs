namespace TicTacFade.Core
{
    public static class WinChecker
    {
        static readonly int[][] Lines =
        {
            new[] { 0, 1, 2 }, new[] { 3, 4, 5 }, new[] { 6, 7, 8 }, // filas
            new[] { 0, 3, 6 }, new[] { 1, 4, 7 }, new[] { 2, 5, 8 }, // columnas
            new[] { 0, 4, 8 }, new[] { 2, 4, 6 }                     // diagonales
        };

        public static bool TryGetWinningLine(Occupant[] cells, Occupant player, out int[] line)
        {
            foreach (var candidate in Lines)
            {
                if (cells[candidate[0]] == player && cells[candidate[1]] == player && cells[candidate[2]] == player)
                {
                    line = candidate;
                    return true;
                }
            }

            line = null;
            return false;
        }
    }
}
