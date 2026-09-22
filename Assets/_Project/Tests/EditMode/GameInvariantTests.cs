using NUnit.Framework;
using TicTacFade.Core;

namespace TicTacFade.Core.Tests
{
    public class GameInvariantTests
    {
        [Test]
        public void Mvp_MatchesGdd9Parameters()
        {
            var config = GameConfig.Mvp();

            Assert.AreEqual(3, config.BoardSize);
            Assert.AreEqual(3, config.BufferSize);
            Assert.AreEqual(3, config.WinLength);
            Assert.AreEqual(40, config.MaxTotalMoves);
            Assert.AreEqual(3, config.RepetitionLimit);
        }

        [Test]
        public void PlayLongMatch_AtLeastOneFreeCellWhileGameInProgress()
        {
            // GDD §3.6: with 3x3 and buffer 3, at most 2*BufferSize cells are
            // occupied at once, so at least BoardSize² - 2*BufferSize cells
            // stay free (3 in the MVP). Uses the same safe rotation as
            // RulesEngineDrawTests (never wins), truncated well before the
            // draw by repetition, to simulate a long match.
            var config = GameConfig.Mvp();
            int minFreeCellsExpected = config.CellCount - 2 * config.BufferSize;

            int[] xMoves = { 0, 1, 8, 6, 0, 1, 8, 6, 0, 1 };
            int[] oMoves = { 2, 3, 5, 7, 2, 3, 5, 7, 2, 3 };

            var state = GameState.CreateInitial(config, Occupant.X);
            for (int i = 0; i < xMoves.Length; i++)
            {
                (state, _) = RulesEngine.Apply(state, new Move(Occupant.X, xMoves[i]));
                Assert.IsFalse(state.IsOver);
                Assert.GreaterOrEqual(state.GetFreeCellIndices().Count, minFreeCellsExpected);

                (state, _) = RulesEngine.Apply(state, new Move(Occupant.O, oMoves[i]));
                Assert.IsFalse(state.IsOver);
                Assert.GreaterOrEqual(state.GetFreeCellIndices().Count, minFreeCellsExpected);
            }
        }
    }
}
