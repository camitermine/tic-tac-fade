using NUnit.Framework;
using TicTacFade.Core;

namespace TicTacFade.Core.Tests
{
    public class RulesEngineMoveValidationTests
    {
        [Test]
        public void IsLegal_ValidMove_ReturnsTrueWithNoneReason()
        {
            var state = GameState.CreateInitial(GameConfig.Mvp(), Occupant.X);

            bool result = RulesEngine.IsLegal(state, new Move(Occupant.X, 4), out var reason);

            Assert.IsTrue(result);
            Assert.AreEqual(MoveRejectionReason.None, reason);
        }

        [Test]
        public void Apply_MoveFromPlayerWithoutTurn_IsRejected()
        {
            var state = GameState.CreateInitial(GameConfig.Mvp(), Occupant.X);

            Assert.IsFalse(RulesEngine.IsLegal(state, new Move(Occupant.O, 0), out var reason));
            Assert.AreEqual(MoveRejectionReason.NotPlayersTurn, reason);

            var (result, events) = RulesEngine.Apply(state, new Move(Occupant.O, 0));

            Assert.AreSame(state, result);
            Assert.AreEqual(1, events.Count);
            var rejected = (MoveRejectedEvent)events[0];
            Assert.AreEqual(MoveRejectionReason.NotPlayersTurn, rejected.Reason);
        }

        [Test]
        public void Apply_CellIndexOutOfRange_IsRejected()
        {
            var state = GameState.CreateInitial(GameConfig.Mvp(), Occupant.X);

            foreach (int outOfRangeCell in new[] { -1, 9 })
            {
                Assert.IsFalse(RulesEngine.IsLegal(state, new Move(Occupant.X, outOfRangeCell), out var reason));
                Assert.AreEqual(MoveRejectionReason.CellOutOfRange, reason);
            }
        }

        [Test]
        public void Apply_CellAlreadyOccupied_IsRejected()
        {
            var state = GameState.CreateInitial(GameConfig.Mvp(), Occupant.X);
            (state, _) = RulesEngine.Apply(state, new Move(Occupant.X, 0)); // now it's O's turn

            Assert.IsFalse(RulesEngine.IsLegal(state, new Move(Occupant.O, 0), out var reason));
            Assert.AreEqual(MoveRejectionReason.CellOccupied, reason);
        }

        [Test]
        public void Apply_CellOfOwnPieceAboutToFade_IsRejectedAsOccupied()
        {
            // GDD §3.2: the new piece can't be placed on the cell of the
            // player's own oldest piece (about to fade), because that cell
            // stays occupied until the FIFO clears it AFTER the move.
            var state = GameState.CreateInitial(GameConfig.Mvp(), Occupant.X);
            state = MoveSequence.Apply(state,
                new Move(Occupant.X, 0), // Xq=[0]
                new Move(Occupant.O, 3),
                new Move(Occupant.X, 1), // Xq=[0,1]
                new Move(Occupant.O, 6),
                new Move(Occupant.X, 8), // Xq=[0,1,8] (full)
                new Move(Occupant.O, 7));

            Assert.IsFalse(state.IsOver);
            Assert.AreEqual(Occupant.X, state.CurrentPlayer);

            Assert.IsFalse(RulesEngine.IsLegal(state, new Move(Occupant.X, 0), out var reason));
            Assert.AreEqual(MoveRejectionReason.CellOccupied, reason);

            var (result, events) = RulesEngine.Apply(state, new Move(Occupant.X, 0));
            Assert.AreSame(state, result);
            Assert.AreEqual(1, events.Count);
            Assert.IsInstanceOf<MoveRejectedEvent>(events[0]);
        }

        [Test]
        public void Apply_MoveAfterGameEnded_IsRejected()
        {
            var state = GameState.CreateInitial(GameConfig.Mvp(), Occupant.X);
            state = MoveSequence.Apply(state,
                new Move(Occupant.X, 0),
                new Move(Occupant.O, 3),
                new Move(Occupant.X, 1),
                new Move(Occupant.O, 4),
                new Move(Occupant.X, 2)); // X wins row 0,1,2: the only move in the sequence allowed to end the match

            Assert.IsTrue(state.IsOver);
            Assert.AreEqual(GameEndReason.Win, state.EndReason);

            Assert.IsFalse(RulesEngine.IsLegal(state, new Move(Occupant.O, 5), out var reason));
            Assert.AreEqual(MoveRejectionReason.GameAlreadyEnded, reason);

            var (result, events) = RulesEngine.Apply(state, new Move(Occupant.O, 5));
            Assert.AreSame(state, result);
            Assert.AreEqual(1, events.Count);
            var rejected = (MoveRejectedEvent)events[0];
            Assert.AreEqual(MoveRejectionReason.GameAlreadyEnded, rejected.Reason);
        }
    }
}
