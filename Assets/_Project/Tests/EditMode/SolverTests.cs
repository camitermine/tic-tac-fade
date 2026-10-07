using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TicTacFade.Core;
using TicTacFade.Solver;

namespace TicTacFade.Core.Tests
{
    /// <summary>
    /// Retrograde solver (GDD §8.1, ADR 0004). The MVP table is solved once
    /// for the whole fixture (about half a second).
    /// </summary>
    public class SolverTests
    {
        static readonly GameConfig Mvp = GameConfig.Mvp();

        SolverTable _table;

        [OneTimeSetUp]
        public void SolveOnce() => _table = GameSolver.Solve(Mvp);

        [Test]
        [Category("Slow")] // ~20 s: walks every reachable position through RulesEngine (CLAUDE.md, Testing)
        public void MoveGeneration_MatchesRulesEngine_OnEveryReachablePosition()
        {
            // No draw limits, so no branch ends by move cap or repetition and
            // the Core reaches every position the solver explores. Neither
            // limit is part of a position or of its PositionKey.
            var unlimited = new GameConfig(Mvp.BoardSize, Mvp.BufferSize, Mvp.WinLength, int.MaxValue, int.MaxValue);
            var table = GameSolver.Solve(unlimited);

            var initial = GameState.CreateInitial(unlimited, Occupant.X);
            var visited = new HashSet<PositionKey> { KeyOf(initial) };
            var frontier = new Queue<GameState>();
            frontier.Enqueue(initial);

            while (frontier.Count > 0)
            {
                var state = frontier.Dequeue();
                var key = KeyOf(state);
                Assert.IsTrue(table.Contains(key), $"The solver is missing a position the Core reaches ({key.Value}).");

                var solverMoves = table.GetMoveResults(key).ToDictionary(m => m.CellIndex);
                for (int cell = 0; cell < unlimited.CellCount; cell++)
                {
                    var move = new Move(state.CurrentPlayer, cell);
                    bool legal = RulesEngine.IsLegal(state, move, out _);
                    Assert.AreEqual(legal, solverMoves.ContainsKey(cell), $"Legality of cell {cell} differs at position {key.Value}.");
                    if (!legal) continue;

                    var next = RulesEngine.Apply(state, move).State;
                    var solverMove = solverMoves[cell];
                    if (next.IsOver)
                    {
                        Assert.AreEqual(state.CurrentPlayer, next.Winner, "With no draw limits a game can only end by a win of the mover.");
                        Assert.IsTrue(solverMove.IsImmediateWin, $"Cell {cell} wins in the Core but not in the solver at {key.Value}.");
                        continue;
                    }

                    Assert.IsFalse(solverMove.IsImmediateWin, $"Cell {cell} wins in the solver but not in the Core at {key.Value}.");
                    var nextKey = KeyOf(next);
                    Assert.AreEqual(nextKey, solverMove.NextPosition, $"Cell {cell} leads to different positions at {key.Value}.");
                    if (visited.Add(nextKey))
                        frontier.Enqueue(next);
                }
            }

            Assert.AreEqual(visited.Count, table.Stats.PositionCount, "Both must reach exactly the same positions.");
        }

        [Test]
        public void EveryPositionValue_IsTheBestOfItsMoves()
        {
            // Self-consistency of the backward pass: Win(d) = the shortest
            // winning move takes d; Loss(d) = every move loses and the
            // longest takes d; NoForcedWin = no winning move, not all losing.
            var initial = _table.InitialPosition;
            var pending = new Queue<PositionKey>();
            var seen = new HashSet<PositionKey> { initial };
            pending.Enqueue(initial);

            while (pending.Count > 0)
            {
                var key = pending.Dequeue();
                Assert.IsTrue(_table.TryGetResult(key, out var result));
                var moves = _table.GetMoveResults(key);
                Assert.IsNotEmpty(moves, "There is always a legal move (GDD §3.6).");

                var wins = moves.Where(m => m.Result.Outcome == PositionOutcome.Win).ToList();
                if (wins.Count > 0)
                {
                    Assert.AreEqual(PositionOutcome.Win, result.Outcome, $"Position {key.Value} has a winning move.");
                    Assert.AreEqual(wins.Min(m => m.Result.Distance), result.Distance, $"Win distance at {key.Value}.");
                }
                else if (moves.All(m => m.Result.Outcome == PositionOutcome.Loss))
                {
                    Assert.AreEqual(PositionOutcome.Loss, result.Outcome, $"Every move loses at {key.Value}.");
                    Assert.AreEqual(moves.Max(m => m.Result.Distance), result.Distance, $"Loss distance at {key.Value}.");
                }
                else
                {
                    Assert.AreEqual(PositionOutcome.NoForcedWin, result.Outcome, $"Position {key.Value}.");
                }

                foreach (var move in moves)
                {
                    if (!move.IsImmediateWin && seen.Add(move.NextPosition))
                        pending.Enqueue(move.NextPosition);
                }
            }
        }

        [Test]
        public void WinInOne_IsFound()
        {
            // X: 0,1 / O: 3,4, X to move: cell 2 wins.
            var state = MoveSequence.Apply(GameState.CreateInitial(Mvp, Occupant.X),
                new Move(Occupant.X, 0), new Move(Occupant.O, 3), new Move(Occupant.X, 1), new Move(Occupant.O, 4));

            Assert.IsTrue(_table.TryGetResult(state, out var result));
            Assert.AreEqual(new PositionResult(PositionOutcome.Win, 1), result);
            var winning = _table.GetMoveResults(KeyOf(state)).Single(m => m.IsImmediateWin);
            Assert.AreEqual(2, winning.CellIndex);
        }

        [Test]
        public void UnavoidableLossInTwo_IsFound()
        {
            // X: [5,0,4] / O: [8,1,6] (oldest first), O to move. X threatens
            // 0-4-8, but 8 holds O's oldest piece, so O can't block it (the
            // replacement restriction). Whatever O plays, its piece on 8
            // fades, X plays 8, X's FIFO removes 5 and 0-4-8 wins.
            var state = MoveSequence.Apply(GameState.CreateInitial(Mvp, Occupant.X),
                new Move(Occupant.X, 2), new Move(Occupant.O, 8), new Move(Occupant.X, 5), new Move(Occupant.O, 1),
                new Move(Occupant.X, 0), new Move(Occupant.O, 6), new Move(Occupant.X, 4));
            CollectionAssert.AreEqual(new[] { 5, 0, 4 }, state.QueueX, "Test setup.");
            CollectionAssert.AreEqual(new[] { 8, 1, 6 }, state.QueueO, "Test setup.");
            Assert.AreEqual(Occupant.O, state.CurrentPlayer, "Test setup.");

            // By hand, with the Core: every O move lets X win at 8.
            foreach (int cell in state.GetFreeCellIndices())
            {
                var afterO = MoveSequence.Apply(state, new Move(Occupant.O, cell));
                var afterX = RulesEngine.Apply(afterO, new Move(Occupant.X, 8)).State;
                Assert.AreEqual(Occupant.X, afterX.Winner, $"O plays {cell}: X must win at 8.");
            }

            Assert.IsTrue(_table.TryGetResult(state, out var result));
            Assert.AreEqual(new PositionResult(PositionOutcome.Loss, 2), result);
        }

        [Test]
        public void InitialPosition_IsAStableForcedWinInThirteen_ForWhoeverStarts()
        {
            var expected = new PositionResult(PositionOutcome.Win, 13);
            Assert.AreEqual(expected, _table.InitialResult, "Regression: value found when the solver was written (docs/solver-results.md).");
            Assert.AreEqual(expected, GameSolver.Solve(Mvp).InitialResult, "A second solve must give the same value.");
            Assert.AreEqual(expected, GameSolver.Solve(Mvp, Occupant.O).InitialResult, "By symmetry, the same when O starts.");

            // Edges win; corners and centre don't force a win.
            var firstMoves = _table.GetMoveResults(_table.InitialPosition).ToDictionary(m => m.CellIndex, m => m.Result);
            foreach (int edge in new[] { 1, 3, 5, 7 })
                Assert.AreEqual(expected, firstMoves[edge], $"First move on edge {edge}.");
            foreach (int other in new[] { 0, 2, 4, 6, 8 })
                Assert.AreEqual(PositionOutcome.NoForcedWin, firstMoves[other].Outcome, $"First move on {other}.");
        }

        [Test]
        public void ForcedLinesFitUnderTheMoveCap()
        {
            int longest = Math.Max(_table.Stats.MaxWinDistance, _table.Stats.MaxLossDistance);
            Assert.LessOrEqual(longest, Mvp.MaxTotalMoves,
                "A forced line longer than the cap would turn a forced win into a draw by move limit.");
        }

        [Test]
        public void TooLargeConfiguration_IsRejectedWithAClearError()
        {
            var big = new GameConfig(boardSize: 4, bufferSize: 4, winLength: 4, maxTotalMoves: 40, repetitionLimit: 3);

            var error = Assert.Throws<ArgumentException>(() => GameSolver.Solve(big));
            StringAssert.Contains("too large", error.Message);
            Assert.Greater(GameSolver.EstimatePositionUpperBound(big), GameSolver.MaxPositions);
        }

        static PositionKey KeyOf(GameState state) =>
            PositionKey.Compute(state.Config, state.QueueX, state.QueueO, state.CurrentPlayer);
    }
}
