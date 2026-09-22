using System.Linq;
using NUnit.Framework;
using TicTacFade.Core;

namespace TicTacFade.Core.Tests
{
    public class RulesEngineFifoTests
    {
        [Test]
        public void Apply_LessThanBufferSizePieces_DoesNotRemoveOrRaisePieceFaded()
        {
            var state = GameState.CreateInitial(GameConfig.Mvp(), Occupant.X);

            var (result, events) = RulesEngine.Apply(state, new Move(Occupant.X, 0));

            Assert.IsFalse(events.Any(e => e is PieceFadedEvent));
            Assert.AreEqual(Occupant.X, result.Cells[0]);
            Assert.AreEqual(1, result.GetActiveCount(Occupant.X));
        }

        [Test]
        public void Apply_FourthPieceSamePlayer_RemovesOldestAndRaisesPieceFaded()
        {
            var state = GameState.CreateInitial(GameConfig.Mvp(), Occupant.X);
            state = MoveSequence.Apply(state,
                new Move(Occupant.X, 0), // Xq=[0]
                new Move(Occupant.O, 3),
                new Move(Occupant.X, 1), // Xq=[0,1]
                new Move(Occupant.O, 5),
                new Move(Occupant.X, 8), // Xq=[0,1,8], full
                new Move(Occupant.O, 7));

            var (result, events) = RulesEngine.Apply(state, new Move(Occupant.X, 6)); // X's 4th piece

            var faded = events.OfType<PieceFadedEvent>().Single();
            Assert.AreEqual(Occupant.X, faded.Player);
            Assert.AreEqual(0, faded.CellIndex);

            Assert.AreEqual(Occupant.None, result.Cells[0]);
            Assert.AreEqual(3, result.GetActiveCount(Occupant.X));
            Assert.IsFalse(result.IsOver);
        }

        [Test]
        public void Apply_QueuesArePerPlayer_PlacingOpponentDoesNotAffectOwnBuffer()
        {
            var state = GameState.CreateInitial(GameConfig.Mvp(), Occupant.X);
            state = MoveSequence.Apply(state,
                new Move(Occupant.X, 0),
                new Move(Occupant.O, 3),
                new Move(Occupant.X, 1),
                new Move(Occupant.O, 5),
                new Move(Occupant.X, 8)); // X buffer full (3)

            var (result, _) = RulesEngine.Apply(state, new Move(Occupant.O, 7));

            Assert.AreEqual(3, result.GetActiveCount(Occupant.X));
            Assert.AreEqual(Occupant.X, result.Cells[0]);
            Assert.AreEqual(Occupant.X, result.Cells[1]);
            Assert.AreEqual(Occupant.X, result.Cells[8]);
        }

        [Test]
        public void GetLife_AsPiecesAreAdded_TransitionsThreeTwoOne()
        {
            var state = GameState.CreateInitial(GameConfig.Mvp(), Occupant.X);

            (state, _) = RulesEngine.Apply(state, new Move(Occupant.X, 0));
            Assert.AreEqual(3, state.GetLife(Occupant.X, 0));

            (state, _) = RulesEngine.Apply(state, new Move(Occupant.O, 3));
            (state, _) = RulesEngine.Apply(state, new Move(Occupant.X, 1));
            Assert.AreEqual(2, state.GetLife(Occupant.X, 0));
            Assert.AreEqual(3, state.GetLife(Occupant.X, 1));

            (state, _) = RulesEngine.Apply(state, new Move(Occupant.O, 5));
            (state, _) = RulesEngine.Apply(state, new Move(Occupant.X, 8));
            Assert.AreEqual(1, state.GetLife(Occupant.X, 0));
            Assert.AreEqual(2, state.GetLife(Occupant.X, 1));
            Assert.AreEqual(3, state.GetLife(Occupant.X, 8));
        }
    }
}
