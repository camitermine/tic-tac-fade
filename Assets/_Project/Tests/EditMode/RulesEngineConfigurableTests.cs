using System.Linq;
using NUnit.Framework;
using TicTacFade.Core;

namespace TicTacFade.Core.Tests
{
    /// <summary>
    /// Nothing about FIFO/win can be hardcoded to the MVP values: these tests
    /// use a different <see cref="GameConfig"/> (4x4, buffer 4, WinLength 3)
    /// to check that.
    /// </summary>
    public class RulesEngineConfigurableTests
    {
        static GameConfig NonDefaultConfig() => new GameConfig(boardSize: 4, bufferSize: 4, winLength: 3, maxTotalMoves: 40, repetitionLimit: 3);

        [Test]
        public void Apply_NonDefaultConfig_FifoStillWorks()
        {
            var config = NonDefaultConfig();
            var state = GameState.CreateInitial(config, Occupant.X);

            // O's cells (2,4,11,13) are a non-attacking-4-queens pattern (one
            // per row and per column, sharing no diagonal), so no three of
            // them can ever be collinear: with WinLength 3 on a 4-column
            // board, that guarantees zero accidental O alignments. This is
            // needed on purpose: a naive sequence like 12,13,14 (full row 3)
            // hands O the match before X reaches its 4th piece, and
            // MoveSequence.Apply would have caught that.
            state = MoveSequence.Apply(state,
                new Move(Occupant.X, 0),
                new Move(Occupant.O, 2),
                new Move(Occupant.X, 1),
                new Move(Occupant.O, 4),
                new Move(Occupant.X, 3),
                new Move(Occupant.O, 11),
                new Move(Occupant.X, 7), // Xq=[0,1,3,7], full (buffer 4)
                new Move(Occupant.O, 13));

            Assert.AreEqual(4, state.GetActiveCount(Occupant.X));
            Assert.IsFalse(state.IsOver);

            var (result, events) = RulesEngine.Apply(state, new Move(Occupant.X, 8)); // X's 5th piece

            var faded = events.OfType<PieceFadedEvent>().Single();
            Assert.AreEqual(Occupant.X, faded.Player);
            Assert.AreEqual(0, faded.CellIndex);
            Assert.AreEqual(4, result.GetActiveCount(Occupant.X));
            Assert.IsFalse(result.IsOver);
        }

        [Test]
        public void Apply_NonDefaultConfig_WinStillWorks()
        {
            var config = NonDefaultConfig();
            var state = GameState.CreateInitial(config, Occupant.X);

            state = MoveSequence.Apply(state,
                new Move(Occupant.X, 4),
                new Move(Occupant.O, 12),
                new Move(Occupant.X, 5),
                new Move(Occupant.O, 13));

            var (result, events) = RulesEngine.Apply(state, new Move(Occupant.X, 6)); // row 4,5,6 (WinLength 3 on a 4x4 board)

            Assert.IsTrue(result.IsOver);
            Assert.AreEqual(GameEndReason.Win, result.EndReason);
            Assert.AreEqual(Occupant.X, result.Winner);
            CollectionAssert.AreEquivalent(new[] { 4, 5, 6 }, result.WinningLine);
            Assert.IsTrue(events.Any(e => e is GameEndedEvent));
        }
    }
}
