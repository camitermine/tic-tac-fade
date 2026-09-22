using System.Collections.Generic;
using NUnit.Framework;
using TicTacFade.Core;

namespace TicTacFade.Core.Tests
{
    public class PositionKeyTests
    {
        static readonly GameConfig Mvp = GameConfig.Mvp();

        [Test]
        public void Compute_SameQueuesAndTurn_ReturnsSameKey()
        {
            var queueX = new List<int> { 0, 1, 2 };
            var queueO = new List<int> { 3, 4 };

            var a = PositionKey.Compute(Mvp, queueX, queueO, Occupant.O);
            var b = PositionKey.Compute(Mvp, new List<int>(queueX), new List<int>(queueO), Occupant.O);

            Assert.AreEqual(a, b);
        }

        [Test]
        public void Compute_SameCellsDifferentQueueOrder_ReturnsDifferentKey()
        {
            var queueXOldestFirst = new List<int> { 0, 1, 2 };
            var queueXReordered = new List<int> { 1, 0, 2 };
            var queueO = new List<int> { 3 };

            var a = PositionKey.Compute(Mvp, queueXOldestFirst, queueO, Occupant.O);
            var b = PositionKey.Compute(Mvp, queueXReordered, queueO, Occupant.O);

            Assert.AreNotEqual(a, b, "GDD §3.4 requires the queue order to distinguish the position.");
        }

        [Test]
        public void Compute_SameQueuesDifferentNextPlayer_ReturnsDifferentKey()
        {
            var queueX = new List<int> { 0, 1 };
            var queueO = new List<int> { 3 };

            var a = PositionKey.Compute(Mvp, queueX, queueO, Occupant.X);
            var b = PositionKey.Compute(Mvp, queueX, queueO, Occupant.O);

            Assert.AreNotEqual(a, b);
        }

        [Test]
        public void Compute_LargerBoardSize_DoesNotCollideSentinelWithValidCellIndex()
        {
            // 4x4 = 16 cells (indices 0-15). If the packing's bit width weren't
            // recomputed from BoardSize, a fixed 4-bit sentinel (15) would
            // collide with the cell at index 15.
            var config = new GameConfig(boardSize: 4, bufferSize: 2, winLength: 3, maxTotalMoves: 40, repetitionLimit: 3);

            var queueWithLastCell = new List<int> { 15 };
            var emptyQueue = new List<int>();

            var withPieceOnLastCell = PositionKey.Compute(config, queueWithLastCell, emptyQueue, Occupant.O);
            var emptyBoard = PositionKey.Compute(config, emptyQueue, emptyQueue, Occupant.O);

            Assert.AreNotEqual(withPieceOnLastCell, emptyBoard);
        }
    }
}
