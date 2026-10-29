using System;
using System.Collections.Generic;
using TicTacFade.Core;
using TicTacFade.Game;
using TicTacFade.Solver;

namespace TicTacFade.PlayModeTests
{
    /// <summary>Shared table, levels and playouts for the AI tests.</summary>
    public static class AITestSupport
    {
        static readonly Lazy<CompactSolverTable> MvpTable = new Lazy<CompactSolverTable>(() => CompactSolverTable.Solve(GameConfig.Mvp()));

        /// <summary>The MVP table, solved once per test run (about a second).</summary>
        public static CompactSolverTable Table => MvpTable.Value;

        /// <summary>A "level" that always plays a uniformly random legal move.</summary>
        public static readonly AILevelConfig RandomMover = new AILevelConfig(100, alwaysTakesImmediateWin: false, blocksImmediateLoss: false);

        public static AIConfig NoDelay(AIConfig config) =>
            new AIConfig(config.Easy, config.Normal, config.Perfect, minDelaySeconds: 0f, maxDelaySeconds: 0f);

        public static AIConfig FixedDelay(float seconds) =>
            new AIConfig(AIConfig.Default().Easy, AIConfig.Default().Normal, AIConfig.Default().Perfect, seconds, seconds);

        /// <summary>
        /// Plays a whole match with RulesEngine, each side picking with
        /// <see cref="AIMoveChooser"/>. Asserts nothing; returns the final state.
        /// </summary>
        public static GameState PlayMatch(GameConfig config, Occupant starter, AILevelConfig x, AILevelConfig o, IRandomSource random,
            Action<GameState, Move> beforeEachMove = null)
        {
            var state = GameState.CreateInitial(config, starter);
            while (!state.IsOver)
            {
                var level = state.CurrentPlayer == Occupant.X ? x : o;
                var move = new Move(state.CurrentPlayer, AIMoveChooser.Choose(state, Table, level, random).CellIndex);
                beforeEachMove?.Invoke(state, move);
                state = RulesEngine.Apply(state, move).State;
            }
            return state;
        }

        /// <summary>
        /// Every in-progress state of seeded playouts that try to revisit
        /// positions: they avoid moves that end the match and, most of the
        /// time, pick a move into a position already seen in it. That is how
        /// repetition situations show up within the move cap. MVP rules
        /// unless <paramref name="config"/> says otherwise.
        /// </summary>
        public static IEnumerable<GameState> RepetitionSeekingStates(int seed, int matches, GameConfig? config = null)
        {
            var rules = config ?? GameConfig.Mvp();
            var random = new Random(seed);
            for (int match = 0; match < matches; match++)
            {
                var state = GameState.CreateInitial(rules, match % 2 == 0 ? Occupant.X : Occupant.O);
                var seen = new HashSet<PositionKey> { KeyOf(state) };
                while (!state.IsOver)
                {
                    yield return state;

                    var revisits = new List<int>();
                    var quiet = new List<int>();
                    foreach (int cell in state.GetFreeCellIndices())
                    {
                        var next = RulesEngine.Apply(state, new Move(state.CurrentPlayer, cell)).State;
                        if (next.IsOver) continue;
                        quiet.Add(cell);
                        if (seen.Contains(KeyOf(next))) revisits.Add(cell);
                    }
                    if (quiet.Count == 0)
                        quiet.AddRange(state.GetFreeCellIndices());

                    var pool = revisits.Count > 0 && random.NextDouble() < 0.8 ? revisits : quiet;
                    state = RulesEngine.Apply(state, new Move(state.CurrentPlayer, pool[random.Next(pool.Count)])).State;
                    seen.Add(KeyOf(state));
                }
            }
        }

        /// <summary>Whether that move ends the match in a draw by repetition.</summary>
        public static bool IsThirdRepetition(GameState state, int cell) =>
            RulesEngine.Apply(state, new Move(state.CurrentPlayer, cell)).State.EndReason == GameEndReason.DrawByRepetition;

        /// <summary>The raw solver value of a move, ignoring history and the cap (what a table-only AI would see).</summary>
        public static PositionResult RawValue(GameState state, int cell)
        {
            var next = RulesEngine.Apply(state, new Move(state.CurrentPlayer, cell)).State;
            if (next.Winner == state.CurrentPlayer)
                return new PositionResult(PositionOutcome.Win, 1);
            Table.TryGetResult(KeyOf(next), out var result);
            return result.SeenByPreviousMover();
        }

        /// <summary>Plays the cells in turn order; fails if a move is rejected or the match ends early.</summary>
        public static GameState Play(GameState state, params int[] cells)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                NUnit.Framework.Assert.IsFalse(state.IsOver, $"Test setup: the match ended before move #{i + 1}.");
                var next = RulesEngine.Apply(state, new Move(state.CurrentPlayer, cells[i])).State;
                NUnit.Framework.Assert.AreNotSame(state, next, $"Test setup: move #{i + 1} (cell {cells[i]}) was rejected.");
                state = next;
            }
            return state;
        }

        static PositionKey KeyOf(GameState state) =>
            PositionKey.Compute(state.Config, state.QueueX, state.QueueO, state.CurrentPlayer);
    }
}
