using NUnit.Framework;
using TicTacFade.Core;

namespace TicTacFade.Core.Tests
{
    public class RulesEngineImmutabilityAndEventsTests
    {
        [Test]
        public void Apply_DoesNotMutateInputState()
        {
            var original = GameState.CreateInitial(GameConfig.Mvp(), Occupant.X);
            var originalCellsSnapshot = new Occupant[original.Cells.Count];
            for (int i = 0; i < originalCellsSnapshot.Length; i++)
                originalCellsSnapshot[i] = original.Cells[i];
            int originalTotalMoves = original.TotalMoves;
            bool originalIsOver = original.IsOver;
            Occupant originalCurrentPlayer = original.CurrentPlayer;
            int originalQueueXCount = original.QueueX.Count;

            RulesEngine.Apply(original, new Move(Occupant.X, 4));

            for (int i = 0; i < originalCellsSnapshot.Length; i++)
                Assert.AreEqual(originalCellsSnapshot[i], original.Cells[i], $"Cell {i} of the input state changed.");
            Assert.AreEqual(originalTotalMoves, original.TotalMoves);
            Assert.AreEqual(originalIsOver, original.IsOver);
            Assert.AreEqual(originalCurrentPlayer, original.CurrentPlayer);
            Assert.AreEqual(originalQueueXCount, original.QueueX.Count);
        }

        [Test]
        public void Apply_EventOrder_PiecePlacedThenPieceFadedThenGameEnded()
        {
            // X fills its buffer with [0,1,5]; the 4th piece (at 3) makes the
            // FIFO remove cell 0 (which isn't part of the line) AND at the
            // same time completes row 3,4,5: FIFO and win on the same move.
            var state = GameState.CreateInitial(GameConfig.Mvp(), Occupant.X);

            (state, _) = RulesEngine.Apply(state, new Move(Occupant.X, 0));
            Assert.IsFalse(state.IsOver);
            (state, _) = RulesEngine.Apply(state, new Move(Occupant.O, 6));
            Assert.IsFalse(state.IsOver);
            (state, _) = RulesEngine.Apply(state, new Move(Occupant.X, 4));
            Assert.IsFalse(state.IsOver);
            (state, _) = RulesEngine.Apply(state, new Move(Occupant.O, 7));
            Assert.IsFalse(state.IsOver);
            (state, _) = RulesEngine.Apply(state, new Move(Occupant.X, 5));
            Assert.IsFalse(state.IsOver);
            (state, _) = RulesEngine.Apply(state, new Move(Occupant.O, 2));
            Assert.IsFalse(state.IsOver);

            var (finalState, events) = RulesEngine.Apply(state, new Move(Occupant.X, 3));

            Assert.AreEqual(3, events.Count, "Expected exactly 3 events: place, fade and end.");
            Assert.IsInstanceOf<PiecePlacedEvent>(events[0]);
            Assert.IsInstanceOf<PieceFadedEvent>(events[1]);
            Assert.IsInstanceOf<GameEndedEvent>(events[2]);

            var placed = (PiecePlacedEvent)events[0];
            Assert.AreEqual(Occupant.X, placed.Player);
            Assert.AreEqual(3, placed.CellIndex);

            var faded = (PieceFadedEvent)events[1];
            Assert.AreEqual(Occupant.X, faded.Player);
            Assert.AreEqual(0, faded.CellIndex);

            var ended = (GameEndedEvent)events[2];
            Assert.AreEqual(GameEndReason.Win, ended.Reason);
            Assert.AreEqual(Occupant.X, ended.Winner);
            CollectionAssert.AreEquivalent(new[] { 3, 4, 5 }, ended.WinningLine);

            Assert.IsTrue(finalState.IsOver);
        }
    }
}
