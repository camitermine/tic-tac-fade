using NUnit.Framework;
using TicTacFade.Core;

namespace TicTacFade.Core.Tests
{
    public class WinCheckerTests
    {
        static Occupant[] EmptyBoard(int size) => new Occupant[size * size];

        [Test]
        public void TryGetWinningLine_HorizontalLine3x3_ReturnsTrue()
        {
            var cells = EmptyBoard(3);
            cells[3] = cells[4] = cells[5] = Occupant.X;

            bool result = WinChecker.TryGetWinningLine(cells, boardSize: 3, winLength: 3, Occupant.X, out var line);

            Assert.IsTrue(result);
            CollectionAssert.AreEquivalent(new[] { 3, 4, 5 }, line);
        }

        [Test]
        public void TryGetWinningLine_VerticalLine3x3_ReturnsTrue()
        {
            var cells = EmptyBoard(3);
            cells[1] = cells[4] = cells[7] = Occupant.O;

            bool result = WinChecker.TryGetWinningLine(cells, 3, 3, Occupant.O, out var line);

            Assert.IsTrue(result);
            CollectionAssert.AreEquivalent(new[] { 1, 4, 7 }, line);
        }

        [Test]
        public void TryGetWinningLine_BothDiagonals3x3_ReturnsTrue()
        {
            var mainDiagonal = EmptyBoard(3);
            mainDiagonal[0] = mainDiagonal[4] = mainDiagonal[8] = Occupant.X;
            Assert.IsTrue(WinChecker.TryGetWinningLine(mainDiagonal, 3, 3, Occupant.X, out _));

            var antiDiagonal = EmptyBoard(3);
            antiDiagonal[2] = antiDiagonal[4] = antiDiagonal[6] = Occupant.O;
            Assert.IsTrue(WinChecker.TryGetWinningLine(antiDiagonal, 3, 3, Occupant.O, out _));
        }

        [Test]
        public void TryGetWinningLine_MixedOccupants_ReturnsFalse()
        {
            var cells = EmptyBoard(3);
            cells[0] = Occupant.X;
            cells[1] = Occupant.O;
            cells[2] = Occupant.X;

            bool result = WinChecker.TryGetWinningLine(cells, 3, 3, Occupant.X, out var line);

            Assert.IsFalse(result);
            Assert.IsNull(line);
        }

        [Test]
        public void TryGetWinningLine_NonDefaultBoardSizeAndWinLength_DetectsWinCorrectly()
        {
            // 4x4 with WinLength 3: tests that the algorithm isn't hardcoded to 3x3/3.
            var cells = EmptyBoard(4);
            cells[5] = cells[6] = cells[7] = Occupant.X; // row 1 (cells 4-7): 5,6,7 consecutive

            bool result = WinChecker.TryGetWinningLine(cells, boardSize: 4, winLength: 3, Occupant.X, out var line);

            Assert.IsTrue(result);
            CollectionAssert.AreEquivalent(new[] { 5, 6, 7 }, line);
        }
    }
}
