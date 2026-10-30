using System.Collections.Generic;
using TicTacFade.Core;
using TicTacFade.Solver;

namespace TicTacFade.Game
{
    /// <summary>
    /// Picks the AI's move for one level (GDD §8.2, ADR 0005). Random draws,
    /// in this order, so tests can script them:
    /// <list type="number">
    /// <item>none if the level always takes an immediate win and there is one;</item>
    /// <item>the error roll, <c>Next(100) &lt; ErrorPercent</c>;</item>
    /// <item>on error, the random move among the allowed ones; otherwise the
    /// tie-break among the equally best moves (no draw if only one).</item>
    /// </list>
    /// </summary>
    public static class AIMoveChooser
    {
        public static AIMoveEvaluation Choose(GameState state, CompactSolverTable table, AILevelConfig level, IRandomSource random)
        {
            var evaluations = AIMoveEvaluator.Evaluate(state, table);

            if (level.AlwaysTakesImmediateWin)
            {
                var wins = evaluations.FindAll(e => e.Category == AIMoveCategory.ImmediateWin);
                if (wins.Count > 0)
                    return wins[0];
            }

            if (random.Next(100) < level.ErrorPercent)
                return PickRandom(ErrorPool(evaluations, level), random);

            return PickRandom(Best(evaluations), random);
        }

        // Any legal move; with BlocksImmediateLoss, only those that don't
        // hand the opponent a win on the spot, if there are any.
        static List<AIMoveEvaluation> ErrorPool(List<AIMoveEvaluation> evaluations, AILevelConfig level)
        {
            if (!level.BlocksImmediateLoss)
                return evaluations;

            var safe = evaluations.FindAll(e => !e.GivesOpponentImmediateWin);
            return safe.Count > 0 ? safe : evaluations;
        }

        static List<AIMoveEvaluation> Best(List<AIMoveEvaluation> evaluations)
        {
            var best = new List<AIMoveEvaluation>();
            foreach (var evaluation in evaluations)
            {
                int comparison = best.Count == 0 ? 1 : evaluation.CompareTo(best[0]);
                if (comparison > 0)
                    best.Clear();
                if (comparison >= 0)
                    best.Add(evaluation);
            }
            return best;
        }

        static AIMoveEvaluation PickRandom(List<AIMoveEvaluation> moves, IRandomSource random) =>
            moves.Count == 1 ? moves[0] : moves[random.Next(moves.Count)];
    }
}
