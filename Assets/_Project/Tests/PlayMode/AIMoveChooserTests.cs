using System.Linq;
using NUnit.Framework;
using TicTacFade.Core;
using TicTacFade.Game;
using TicTacFade.Solver;

namespace TicTacFade.PlayModeTests
{
    /// <summary>
    /// The AI's move choice (GDD §8.2, ADR 0005) with injected randomness,
    /// on RulesEngine only (no GameManager, no delay).
    /// </summary>
    public class AIMoveChooserTests
    {
        static readonly GameConfig Mvp = GameConfig.Mvp();
        static readonly AIConfig Levels = AIConfig.Default();

        static GameState Initial(Occupant starter = Occupant.X) => GameState.CreateInitial(Mvp, starter);

        // X: 0,1 / O: 3,4, X to move. Cell 2 wins; free cells 2,5,6,7,8.
        static GameState XCanWinAtTwo() => AITestSupport.Play(Initial(), 0, 3, 1, 4);

        // X: 0,1 / O: 3, O to move. X threatens 2; free cells 2,4,5,6,7,8.
        static GameState OMustBlockTwo() => AITestSupport.Play(Initial(), 0, 3, 1);

        // X: [5,0,4] / O: [8,1,6], O to move, 7 moves played. Every O move
        // lets X win at 8 (see SolverTests.UnavoidableLossInTwo_IsFound).
        static GameState OLosesInTwo(GameConfig config) =>
            AITestSupport.Play(GameState.CreateInitial(config, Occupant.X), 2, 8, 5, 1, 0, 6, 4);

        static AIMoveEvaluation Choose(GameState state, AILevelConfig level, IRandomSource random) =>
            AIMoveChooser.Choose(state, AITestSupport.Table, level, random);

        [Test]
        public void AIPlayer_NeverChoosesIllegalMove_AnyLevel()
        {
            var levels = new[] { Levels.Easy, Levels.Normal, Levels.Perfect, AITestSupport.RandomMover };
            int moves = 0;
            for (int seed = 0; seed < 40; seed++)
            {
                foreach (var x in levels)
                {
                    foreach (var o in levels)
                    {
                        AITestSupport.PlayMatch(Mvp, seed % 2 == 0 ? Occupant.X : Occupant.O, x, o, new SystemRandomSource(seed),
                            (state, move) =>
                            {
                                moves++;
                                Assert.IsTrue(RulesEngine.IsLegal(state, move, out var reason), $"Illegal AI move {move.CellIndex}: {reason}.");
                            });
                    }
                }
            }
            Assert.Greater(moves, 0);
        }

        [Test]
        public void Perfect_VsPerfect_StarterWinsIn13Moves([Values(Occupant.X, Occupant.O)] Occupant starter)
        {
            for (int seed = 0; seed < 20; seed++)
            {
                var end = AITestSupport.PlayMatch(Mvp, starter, Levels.Perfect, Levels.Perfect, new SystemRandomSource(seed));

                Assert.AreEqual(starter, end.Winner, $"Seed {seed}: the starter has a forced win (GDD §8.1).");
                Assert.AreEqual(13, end.TotalMoves, $"Seed {seed}: the forced win takes 13 moves.");
            }
        }

        [Test]
        public void Perfect_StartingVsRandom_AlwaysWins([Values(Occupant.X, Occupant.O)] Occupant starter)
        {
            for (int seed = 0; seed < 200; seed++)
            {
                var perfectIsX = starter == Occupant.X;
                var end = AITestSupport.PlayMatch(Mvp, starter,
                    perfectIsX ? Levels.Perfect : AITestSupport.RandomMover,
                    perfectIsX ? AITestSupport.RandomMover : Levels.Perfect,
                    new SystemRandomSource(seed));

                Assert.AreEqual(starter, end.Winner, $"Seed {seed}: Perfect started and must win ({end.EndReason}).");
            }
        }

        [Test]
        public void ErrorRoll_EachLevel_IsDeterministicWithInjectedRandom([Values] AIDifficulty difficulty)
        {
            // Initial position: the best moves are the edges 1,3,5,7 (Win in
            // 13), so the tie-break index 0 gives 1; on error the random
            // pool is every cell, so index 0 gives 0.
            var level = Levels.For(difficulty);

            var atThreshold = Choose(Initial(), level, new ScriptedRandomSource(level.ErrorPercent, 0));
            Assert.AreEqual(1, atThreshold.CellIndex, $"Roll {level.ErrorPercent} is not an error for {difficulty}.");

            if (level.ErrorPercent > 0)
            {
                var below = Choose(Initial(), level, new ScriptedRandomSource(level.ErrorPercent - 1, 0));
                Assert.AreEqual(0, below.CellIndex, $"Roll {level.ErrorPercent - 1} is an error for {difficulty}.");
            }
            else
            {
                var highest = Choose(Initial(), level, new ScriptedRandomSource(99, 0));
                Assert.AreEqual(1, highest.CellIndex, $"{difficulty} never errs.");
            }
        }

        [Test]
        public void DefaultLevels_MatchTheDesign()
        {
            Assert.AreEqual(50, Levels.Easy.ErrorPercent);
            Assert.AreEqual(20, Levels.Normal.ErrorPercent);
            Assert.AreEqual(0, Levels.Perfect.ErrorPercent);
            Assert.IsFalse(Levels.Easy.AlwaysTakesImmediateWin);
            Assert.IsTrue(Levels.Normal.AlwaysTakesImmediateWin);
            Assert.IsTrue(Levels.Perfect.AlwaysTakesImmediateWin);
            Assert.IsFalse(Levels.Easy.BlocksImmediateLoss);
            Assert.IsTrue(Levels.Normal.BlocksImmediateLoss);
            Assert.IsTrue(Levels.Perfect.BlocksImmediateLoss);
        }

        [Test]
        public void Easy_OnError_CanMissImmediateWin()
        {
            // Error roll 0, then index 1 of the free cells 2,5,6,7,8.
            var choice = Choose(XCanWinAtTwo(), Levels.Easy, new ScriptedRandomSource(0, 1));

            Assert.AreEqual(5, choice.CellIndex);
        }

        [Test]
        public void NormalAndPerfect_TakeImmediateWin_WithoutRollingForError(
            [Values(AIDifficulty.Normal, AIDifficulty.Perfect)] AIDifficulty difficulty)
        {
            var random = new ScriptedRandomSource(); // any draw fails the test

            var choice = Choose(XCanWinAtTwo(), Levels.For(difficulty), random);

            Assert.AreEqual(2, choice.CellIndex);
            Assert.AreEqual(AIMoveCategory.ImmediateWin, choice.Category);
        }

        [Test]
        public void Normal_OnError_StillBlocksImmediateLoss()
        {
            // Error roll 0; the only move that doesn't let X win at once is 2,
            // so there is no second draw.
            var random = new ScriptedRandomSource(0);

            var choice = Choose(OMustBlockTwo(), Levels.Normal, random);

            Assert.AreEqual(2, choice.CellIndex);
            Assert.AreEqual(0, random.Remaining);
        }

        [Test]
        public void Easy_OnError_CanLeaveImmediateLossOpen()
        {
            // Error roll 0, then index 1 of the free cells 2,4,5,6,7,8.
            var choice = Choose(OMustBlockTwo(), Levels.Easy, new ScriptedRandomSource(0, 1));

            Assert.AreEqual(4, choice.CellIndex);
            Assert.IsTrue(choice.GivesOpponentImmediateWin);
        }

        [Test]
        public void BlocksImmediateLoss_NoSafeMove_PicksAnyMove()
        {
            var state = OLosesInTwo(Mvp);
            var free = state.GetFreeCellIndices();

            var choice = Choose(state, Levels.Normal, new ScriptedRandomSource(0, 2));

            Assert.AreEqual(free[2], choice.CellIndex, "Every move loses at once: the error pool falls back to all of them.");
        }

        [Test]
        public void Evaluate_LossJustBeforeMoveCap_IsALoss_AndAfterIt_IsADraw()
        {
            // O plays move 8; X would win on move 9.
            var cap9 = new GameConfig(Mvp.BoardSize, Mvp.BufferSize, Mvp.WinLength, maxTotalMoves: 9, Mvp.RepetitionLimit);
            var cap8 = new GameConfig(Mvp.BoardSize, Mvp.BufferSize, Mvp.WinLength, maxTotalMoves: 8, Mvp.RepetitionLimit);

            var withinCap = AIMoveEvaluator.Evaluate(OLosesInTwo(cap9), AITestSupport.Table);
            var pastCap = AIMoveEvaluator.Evaluate(OLosesInTwo(cap8), AITestSupport.Table);

            Assert.IsTrue(withinCap.All(e => e.Category == AIMoveCategory.Loss && e.Distance == 2), string.Join("; ", withinCap));
            Assert.IsTrue(pastCap.All(e => e.Category == AIMoveCategory.ImmediateDraw), string.Join("; ", pastCap));
        }

        [Test]
        public void Evaluate_WinBeyondMoveCap_CountsAsNoForcedWin()
        {
            var cap13 = new GameConfig(Mvp.BoardSize, Mvp.BufferSize, Mvp.WinLength, maxTotalMoves: 13, Mvp.RepetitionLimit);
            var cap12 = new GameConfig(Mvp.BoardSize, Mvp.BufferSize, Mvp.WinLength, maxTotalMoves: 12, Mvp.RepetitionLimit);

            var edgeWithinCap = AIMoveEvaluator.Evaluate(GameState.CreateInitial(cap13, Occupant.X), AITestSupport.Table).Single(e => e.CellIndex == 1);
            var edgePastCap = AIMoveEvaluator.Evaluate(GameState.CreateInitial(cap12, Occupant.X), AITestSupport.Table).Single(e => e.CellIndex == 1);

            Assert.AreEqual(AIMoveCategory.Win, edgeWithinCap.Category);
            Assert.AreEqual(13, edgeWithinCap.Distance);
            Assert.AreEqual(AIMoveCategory.NoForcedWin, edgePastCap.Category, "A win on move 13 can't happen with a cap of 12.");
        }

        [Test]
        public void Perfect_Losing_PrefersThirdRepetitionDraw()
        {
            int found = 0;
            foreach (var state in AITestSupport.RepetitionSeekingStates(seed: 1, matches: 1500))
            {
                if (!state.GetFreeCellIndices().Any(cell => AITestSupport.IsThirdRepetition(state, cell)))
                    continue; // cheap filter before the full evaluation

                var evaluations = AIMoveEvaluator.Evaluate(state, AITestSupport.Table);
                var repeating = evaluations.Where(e => AITestSupport.IsThirdRepetition(state, e.CellIndex)).ToList();
                var others = evaluations.Except(repeating).ToList();
                if (repeating.Count == 0 || others.Count == 0 || others.Any(e => e.Category != AIMoveCategory.Loss))
                    continue;

                found++;
                var choice = Choose(state, Levels.Perfect, new SystemRandomSource(found));
                Assert.IsTrue(AITestSupport.IsThirdRepetition(state, choice.CellIndex),
                    $"Losing, Perfect must take the draw by repetition. Moves: {string.Join("; ", evaluations)}.");
            }
            Assert.Greater(found, 0, "The playouts found no losing position with a repetition draw available.");
        }

        [Test]
        public void Perfect_Winning_AvoidsThirdRepetition()
        {
            // Positions where the table alone says the repeating move is a
            // forced win: in this match it is a draw.
            int found = 0;
            foreach (var state in AITestSupport.RepetitionSeekingStates(seed: 2, matches: 1500))
            {
                var repeatingCells = state.GetFreeCellIndices().Where(cell => AITestSupport.IsThirdRepetition(state, cell)).ToList();
                if (!repeatingCells.Any(cell => AITestSupport.RawValue(state, cell).Outcome == PositionOutcome.Win))
                    continue;

                var evaluations = AIMoveEvaluator.Evaluate(state, AITestSupport.Table);
                if (!evaluations.Any(e => e.Category == AIMoveCategory.Win || e.Category == AIMoveCategory.WinWithEscape))
                    continue;

                found++;
                var choice = Choose(state, Levels.Perfect, new SystemRandomSource(found));
                Assert.IsFalse(AITestSupport.IsThirdRepetition(state, choice.CellIndex),
                    $"Winning, Perfect must not draw by repetition. Moves: {string.Join("; ", evaluations)}.");
                Assert.That(choice.Category, Is.EqualTo(AIMoveCategory.Win).Or.EqualTo(AIMoveCategory.WinWithEscape));
            }
            Assert.Greater(found, 0, "The playouts found no winning position where a raw forced win repeats.");
        }

        [Test]
        public void Perfect_Winning_PrefersWinWithoutRepetitionEscape()
        {
            // Some forced wins let the opponent answer with a repetition
            // draw, others don't: one without an escape is chosen, even if
            // it is longer.
            //
            // Why RepetitionLimit = 2: the evaluator never reads the limit.
            // It only sees the DrawByRepetition that RulesEngine reports
            // after a reply, so the logic under test is the same for any
            // limit. Lowering it only makes the case frequent: with the MVP
            // limit of 3 it showed up once in 1500 playouts. The solver
            // table doesn't depend on the limit either (ADR 0004).
            var limitTwo = new GameConfig(Mvp.BoardSize, Mvp.BufferSize, Mvp.WinLength, Mvp.MaxTotalMoves, repetitionLimit: 2);
            int found = 0, anyEscape = 0;
            foreach (var state in AITestSupport.RepetitionSeekingStates(seed: 3, matches: 1500, limitTwo))
            {
                int rawWins = state.GetFreeCellIndices().Count(cell => AITestSupport.RawValue(state, cell).Outcome == PositionOutcome.Win);
                if (rawWins < 2)
                    continue; // cheap filter before the full evaluation

                var evaluations = AIMoveEvaluator.Evaluate(state, AITestSupport.Table);
                var withEscape = evaluations.Where(e => e.Category == AIMoveCategory.WinWithEscape).ToList();
                var clean = evaluations.Where(e => e.Category == AIMoveCategory.Win).ToList();
                if (withEscape.Count > 0)
                    anyEscape++;
                if (withEscape.Count == 0 || clean.Count == 0 || evaluations.Any(e => e.Category == AIMoveCategory.ImmediateWin))
                    continue;

                found++;
                foreach (var escape in withEscape)
                {
                    var afterMove = RulesEngine.Apply(state, new Move(state.CurrentPlayer, escape.CellIndex)).State;
                    Assert.IsTrue(afterMove.GetFreeCellIndices().Any(cell => AITestSupport.IsThirdRepetition(afterMove, cell)),
                        $"Cell {escape.CellIndex} is marked as having an escape, but no reply repeats.");
                }

                var choice = Choose(state, Levels.Perfect, new SystemRandomSource(found));
                Assert.AreEqual(AIMoveCategory.Win, choice.Category, $"Moves: {string.Join("; ", evaluations)}.");
            }
            Assert.Greater(found, 0, $"The playouts found no position with forced wins both with and without a repetition escape ({anyEscape} with an escape only).");
        }
    }
}
