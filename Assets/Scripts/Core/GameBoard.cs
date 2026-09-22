using System.Collections.Generic;

namespace TicTacFade.Core
{
    public struct PlaceMoveResult
    {
        public bool Success;
        public int RemovedCell; // -1 si no se eliminó ninguna ficha
        public bool IsWin;
        public Occupant Winner;
        public int[] WinningLine;
    }

    /// <summary>
    /// Estado puro del tablero 3x3 y la lógica FIFO. Sin dependencias de Unity
    /// para poder testearlo con Unity Test Framework sin necesidad de una escena.
    /// </summary>
    public class GameBoard
    {
        public const int Size = 9;
        public const int MaxActivePieces = 3;

        public Occupant[] Cells { get; } = new Occupant[Size];

        readonly Queue<int> queueX = new Queue<int>();
        readonly Queue<int> queueO = new Queue<int>();

        public bool IsCellEmpty(int index) => Cells[index] == Occupant.None;

        public int GetActiveCount(Occupant player) => GetQueue(player).Count;

        /// <summary>
        /// Coloca la ficha, aplica la regla FIFO (elimina la más antigua si se
        /// supera el buffer de 3) y recién después evalúa la victoria sobre el
        /// tablero resultante, tal como especifica el GDD (sección 3.3).
        /// </summary>
        public PlaceMoveResult PlaceMove(Occupant player, int cellIndex)
        {
            if (player == Occupant.None || cellIndex < 0 || cellIndex >= Size || !IsCellEmpty(cellIndex))
                return new PlaceMoveResult { Success = false, RemovedCell = -1 };

            var queue = GetQueue(player);
            Cells[cellIndex] = player;
            queue.Enqueue(cellIndex);

            int removedCell = -1;
            if (queue.Count > MaxActivePieces)
            {
                removedCell = queue.Dequeue();
                Cells[removedCell] = Occupant.None;
            }

            bool isWin = WinChecker.TryGetWinningLine(Cells, player, out var line);

            return new PlaceMoveResult
            {
                Success = true,
                RemovedCell = removedCell,
                IsWin = isWin,
                Winner = isWin ? player : Occupant.None,
                WinningLine = line
            };
        }

        /// <summary>
        /// Vida visual de la ficha en esa casilla: 3 = recién colocada,
        /// 2 = estable, 1 = crítica (se elimina en la próxima jugada propia
        /// del dueño). 0 si la casilla no tiene ficha de ese jugador.
        /// </summary>
        public int GetLife(Occupant player, int cellIndex)
        {
            var queue = GetQueue(player);
            int n = queue.Count;
            int p = 0;
            foreach (var c in queue)
            {
                if (c == cellIndex)
                    return p + (4 - n);
                p++;
            }

            return 0;
        }

        public void Reset()
        {
            for (int i = 0; i < Size; i++) Cells[i] = Occupant.None;
            queueX.Clear();
            queueO.Clear();
        }

        Queue<int> GetQueue(Occupant player) => player == Occupant.X ? queueX : queueO;
    }
}
