using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TicTacFade.Core;

namespace TicTacFade.Core.Tests
{
    public class RulesEngineWinTests
    {
        [Test]
        public void Apply_CompletingLine_ResultsInWinWithWinningLine()
        {
            var state = GameState.CreateInitial(GameConfig.Mvp(), Occupant.X);
            state = MoveSequence.Apply(state,
                new Move(Occupant.X, 0),
                new Move(Occupant.O, 3),
                new Move(Occupant.X, 1),
                new Move(Occupant.O, 4));

            var (result, events) = RulesEngine.Apply(state, new Move(Occupant.X, 2));

            Assert.IsTrue(result.IsOver);
            Assert.AreEqual(Occupant.X, result.Winner);
            Assert.AreEqual(GameEndReason.Win, result.EndReason);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, result.WinningLine);

            var ended = events.OfType<GameEndedEvent>().Single();
            Assert.AreEqual(Occupant.X, ended.Winner);
            Assert.AreEqual(GameEndReason.Win, ended.Reason);
        }

        [Test]
        public void Apply_WinDependingOnJustRemovedPiece_IsNotAWin()
        {
            // X builds row 0,1,2 but the 4th piece (at 2) makes the FIFO
            // remove exactly cell 0: the line is no longer complete after
            // the FIFO, so it must not count as a win (GDD §3.3).
            var state = GameState.CreateInitial(GameConfig.Mvp(), Occupant.X);
            state = MoveSequence.Apply(state,
                new Move(Occupant.X, 0), // Xq=[0]
                new Move(Occupant.O, 3),
                new Move(Occupant.X, 1), // Xq=[0,1]
                new Move(Occupant.O, 4),
                new Move(Occupant.X, 5), // Xq=[0,1,5], full
                new Move(Occupant.O, 6));

            Assert.IsFalse(state.IsOver);

            var (result, events) = RulesEngine.Apply(state, new Move(Occupant.X, 2)); // would complete 0,1,2 if not for the FIFO

            var faded = events.OfType<PieceFadedEvent>().Single();
            Assert.AreEqual(0, faded.CellIndex);

            Assert.IsFalse(events.Any(e => e is GameEndedEvent), "The match should not have ended.");
            Assert.IsFalse(result.IsOver);
            Assert.AreEqual(Occupant.None, result.Winner);
            Assert.AreEqual(Occupant.None, result.Cells[0]);
        }

        [Test]
        public void Apply_WinAndMoveLimitOnSameMove_WinTakesPriority()
        {
            var config = GameConfig.Mvp();
            var cells = new Occupant[config.CellCount];
            cells[0] = Occupant.X;
            cells[1] = Occupant.X;

            var state = new GameState(
                config, cells,
                queueX: new[] { 0, 1 }, queueO: Array.Empty<int>(),
                currentPlayer: Occupant.X,
                totalMoves: config.MaxTotalMoves - 1,
                isOver: false,
                winner: Occupant.None,
                endReason: null,
                winningLine: Array.Empty<int>(),
                positionCounts: new Dictionary<PositionKey, int>());

            var (result, events) = RulesEngine.Apply(state, new Move(Occupant.X, 2)); // completes 0,1,2 and reaches MaxTotalMoves

            Assert.AreEqual(config.MaxTotalMoves, result.TotalMoves);
            Assert.IsTrue(result.IsOver);
            Assert.AreEqual(GameEndReason.Win, result.EndReason, "The win must take priority over the move limit.");
            Assert.AreEqual(Occupant.X, result.Winner);
            Assert.IsTrue(events.Any(e => e is GameEndedEvent ended && ended.Reason == GameEndReason.Win));
        }

        [Test]
        public void Apply_WinAndRepetitionOnSameMove_WinTakesPriority()
        {
            var config = GameConfig.Mvp();
            var cells = new Occupant[config.CellCount];
            cells[0] = Occupant.X;
            cells[1] = Occupant.X;

            var queueXBeforeMove = new[] { 0, 1 };
            var queueOBeforeMove = Array.Empty<int>();

            // If this move did NOT win, the resulting position (Xq=[0,1,2],
            // Oq=[], next=O) would already be one repetition away from the
            // limit. Since it does win, that path should never be evaluated.
            var wouldBeKeyAfterMove = PositionKey.Compute(config, new[] { 0, 1, 2 }, queueOBeforeMove, Occupant.O);
            var positionCounts = new Dictionary<PositionKey, int> { [wouldBeKeyAfterMove] = config.RepetitionLimit - 1 };

            var state = new GameState(
                config, cells,
                queueX: queueXBeforeMove, queueO: queueOBeforeMove,
                currentPlayer: Occupant.X,
                totalMoves: 10,
                isOver: false,
                winner: Occupant.None,
                endReason: null,
                winningLine: Array.Empty<int>(),
                positionCounts: positionCounts);

            var (result, _) = RulesEngine.Apply(state, new Move(Occupant.X, 2));

            Assert.IsTrue(result.IsOver);
            Assert.AreEqual(GameEndReason.Win, result.EndReason, "The win must take priority over repetition.");
            Assert.AreEqual(Occupant.X, result.Winner);
        }
    }
}
