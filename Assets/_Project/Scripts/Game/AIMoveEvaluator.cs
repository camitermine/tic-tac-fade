using System.Collections.Generic;
using TicTacFade.Core;
using TicTacFade.Solver;

namespace TicTacFade.Game
{
    /// <summary>
    /// Effective value of every legal move (ADR 0005). Each move is played
    /// with <see cref="RulesEngine.Apply"/> on the real match state, so a
    /// third repetition or the move cap show up as the draws they are. The
    /// position after the move is looked up in the solver table, and its
    /// forced result only counts if it arrives before
    /// <see cref="GameConfig.MaxTotalMoves"/>. For forced wins it also looks
    /// one reply ahead: a win the opponent can cut with a third repetition
    /// ranks below one it can't. Nothing deeper is analysed.
    /// </summary>
    public static class AIMoveEvaluator
    {
        public static List<AIMoveEvaluation> Evaluate(GameState state, CompactSolverTable table)
        {
            var evaluations = new List<AIMoveEvaluation>();
            Occupant mover = state.CurrentPlayer;

            foreach (int cell in state.GetFreeCellIndices())
            {
                var next = RulesEngine.Apply(state, new Move(mover, cell)).State;
                if (next.IsOver)
                {
                    evaluations.Add(next.Winner == mover
                        ? new AIMoveEvaluation(cell, AIMoveCategory.ImmediateWin, 1, false)
                        : new AIMoveEvaluation(cell, AIMoveCategory.ImmediateDraw, 0, false));
                    continue;
                }

                ScanReplies(next, out bool opponentCanWin, out bool opponentCanRepeat);

                var key = PositionKey.Compute(next.Config, next.QueueX, next.QueueO, next.CurrentPlayer);
                if (!table.TryGetResult(key, out var nextResult))
                {
                    evaluations.Add(new AIMoveEvaluation(cell, AIMoveCategory.NoForcedWin, 0, opponentCanWin, missingFromTable: true));
                    continue;
                }

                var result = nextResult.SeenByPreviousMover();
                bool beforeCap = state.TotalMoves + result.Distance <= state.Config.MaxTotalMoves;
                evaluations.Add(Classify(cell, result, beforeCap, opponentCanWin, opponentCanRepeat));
            }

            return evaluations;
        }

        static AIMoveEvaluation Classify(int cell, PositionResult result, bool beforeCap, bool opponentCanWin, bool opponentCanRepeat)
        {
            if (result.Outcome == PositionOutcome.Win && beforeCap)
            {
                var category = opponentCanRepeat ? AIMoveCategory.WinWithEscape : AIMoveCategory.Win;
                return new AIMoveEvaluation(cell, category, result.Distance, opponentCanWin);
            }

            if (result.Outcome == PositionOutcome.Loss && beforeCap)
                return new AIMoveEvaluation(cell, AIMoveCategory.Loss, result.Distance, opponentCanWin);

            // No forced result, or one the move cap turns into a draw.
            return new AIMoveEvaluation(cell, AIMoveCategory.NoForcedWin, 0, opponentCanWin);
        }

        // The opponent's replies from the position after the AI's move.
        static void ScanReplies(GameState afterMove, out bool canWin, out bool canRepeat)
        {
            canWin = false;
            canRepeat = false;
            Occupant opponent = afterMove.CurrentPlayer;
            foreach (int cell in afterMove.GetFreeCellIndices())
            {
                var reply = RulesEngine.Apply(afterMove, new Move(opponent, cell)).State;
                canWin |= reply.Winner == opponent;
                canRepeat |= reply.EndReason == GameEndReason.DrawByRepetition;
            }
        }
    }
}
