using System;
using System.Collections.Generic;
using NUnit.Framework;
using TicTacFade.Core;

namespace TicTacFade.Core.Tests
{
    public class RulesEngineDrawTests
    {
        // Rotation that never completes a line: every 3-cell subset of
        // {0,1,6,8} (X) and of {2,3,5,7} (O) was manually verified to be
        // free of a winning line. Once each player's buffer of 3 fills up,
        // they keep adding the cell the FIFO freed 3 moves ago, so the full
        // position (queues + turn) repeats every 8 moves (4 per player).
        static readonly int[] XRotation = { 0, 1, 8, 6, 0, 1, 8, 6, 0, 1, 8 };
        static readonly int[] ORotation = { 2, 3, 5, 7, 2, 3, 5, 7, 2, 3, 5 };

        [Test]
        public void Apply_SamePositionOnlyTwice_DoesNotDraw()
        {
            var state = GameState.CreateInitial(GameConfig.Mvp(), Occupant.X);

            // 7 moves each = up to the 2nd time the reference position repeats
            // (after the O move that closes the 2nd cycle).
            for (int i = 0; i < 7; i++)
            {
                (state, _) = RulesEngine.Apply(state, new Move(Occupant.X, XRotation[i]));
                Assert.IsFalse(state.IsOver, $"Should not end on X move #{i + 1}");
                (state, _) = RulesEngine.Apply(state, new Move(Occupant.O, ORotation[i]));
                Assert.IsFalse(state.IsOver, $"Should not end on O move #{i + 1}");
            }
        }

        [Test]
        public void Apply_SamePositionThreeTimes_DrawsByRepetitionOnThirdOccurrence()
        {
            var state = GameState.CreateInitial(GameConfig.Mvp(), Occupant.X);

            for (int i = 0; i < XRotation.Length; i++)
            {
                (state, _) = RulesEngine.Apply(state, new Move(Occupant.X, XRotation[i]));
                Assert.IsFalse(state.IsOver, $"Should not end on X move #{i + 1}");

                bool isClosingThirdCycle = i == XRotation.Length - 1;
                IReadOnlyList<IGameEvent> events;
                (state, events) = RulesEngine.Apply(state, new Move(Occupant.O, ORotation[i]));

                if (isClosingThirdCycle)
                {
                    Assert.IsTrue(state.IsOver);
                    Assert.AreEqual(GameEndReason.DrawByRepetition, state.EndReason);
                    Assert.AreEqual(Occupant.None, state.Winner);
                    Assert.IsTrue(System.Linq.Enumerable.Any(events, e => e is GameEndedEvent));
                }
                else
                {
                    Assert.IsFalse(state.IsOver, $"Should not end on O move #{i + 1}");
                }
            }
        }

        [Test]
        public void Apply_SameCellsDifferentQueueOrder_DoesNotCountAsSamePosition()
        {
            var config = GameConfig.Mvp();
            var cells = new Occupant[config.CellCount];
            cells[0] = Occupant.X;
            cells[1] = Occupant.X;

            // Key that would correspond to a DIFFERENT QUEUE ORDER ([1,0,8])
            // than the one actually produced by playing on 8 ([0,1,8]), for
            // the same final set of cells {0,1,8}.
            var differentOrderKey = PositionKey.Compute(config, new[] { 1, 0, 8 }, Array.Empty<int>(), Occupant.O);
            var positionCounts = new Dictionary<PositionKey, int> { [differentOrderKey] = config.RepetitionLimit - 1 };

            var state = new GameState(
                config, cells,
                queueX: new[] { 0, 1 }, queueO: Array.Empty<int>(),
                currentPlayer: Occupant.X,
                totalMoves: 10,
                isOver: false,
                winner: Occupant.None,
                endReason: null,
                winningLine: Array.Empty<int>(),
                positionCounts: positionCounts);

            var (result, _) = RulesEngine.Apply(state, new Move(Occupant.X, 8));

            Assert.IsFalse(result.IsOver, "A different queue order should not count as the same position.");
        }

        [Test]
        public void Apply_ReachingMaxTotalMoves_DrawsByMoveLimitExactlyOnThatMove()
        {
            var config = GameConfig.Mvp();

            var oneMoveBeforeLimit = BuildEmptyBoardState(config, totalMoves: config.MaxTotalMoves - 2);
            var (stillPlaying, _) = RulesEngine.Apply(oneMoveBeforeLimit, new Move(Occupant.X, 0));
            Assert.IsFalse(stillPlaying.IsOver, "Should not reach the limit yet.");
            Assert.AreEqual(config.MaxTotalMoves - 1, stillPlaying.TotalMoves);

            var lastMoveBeforeLimit = BuildEmptyBoardState(config, totalMoves: config.MaxTotalMoves - 1);
            var (drawState, events) = RulesEngine.Apply(lastMoveBeforeLimit, new Move(Occupant.X, 0));
            Assert.IsTrue(drawState.IsOver);
            Assert.AreEqual(GameEndReason.DrawByMoveLimit, drawState.EndReason);
            Assert.AreEqual(Occupant.None, drawState.Winner);
            Assert.AreEqual(config.MaxTotalMoves, drawState.TotalMoves);
            Assert.IsTrue(System.Linq.Enumerable.Any(events, e => e is GameEndedEvent ended && ended.Reason == GameEndReason.DrawByMoveLimit));
        }

        static GameState BuildEmptyBoardState(GameConfig config, int totalMoves)
        {
            var cells = new Occupant[config.CellCount];
            return new GameState(
                config, cells,
                queueX: Array.Empty<int>(), queueO: Array.Empty<int>(),
                currentPlayer: Occupant.X,
                totalMoves: totalMoves,
                isOver: false,
                winner: Occupant.None,
                endReason: null,
                winningLine: Array.Empty<int>(),
                positionCounts: new Dictionary<PositionKey, int>());
        }
    }
}
