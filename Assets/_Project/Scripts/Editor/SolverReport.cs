using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using UnityEditor;
using TicTacFade.Core;
using TicTacFade.Solver;
using Debug = UnityEngine.Debug;

/// <summary>
/// Editor menu that solves the MVP configuration and logs the findings
/// recorded in docs/solver-results.md (GDD §8.1): sizes, timing, memory,
/// the initial position, every first move, the outcome distribution and the
/// effect of the move cap.
/// </summary>
public static class SolverReport
{
    [MenuItem("Tic-Tac-Fade/Solver/Solve MVP and report")]
    public static void SolveMvpAndReport()
    {
        var config = GameConfig.Mvp();

        // First run pays for JIT; the second one is the steady-state time.
        long memoryBefore = GC.GetTotalMemory(forceFullCollection: true);
        var coldClock = Stopwatch.StartNew();
        var table = GameSolver.Solve(config);
        coldClock.Stop();
        long memoryAfter = GC.GetTotalMemory(forceFullCollection: true);
        GC.KeepAlive(table);

        var warmClock = Stopwatch.StartNew();
        var warm = GameSolver.Solve(config);
        warmClock.Stop();

        Debug.Log(BuildReport(config, table, coldClock.Elapsed.TotalMilliseconds, warmClock.Elapsed.TotalMilliseconds,
            memoryAfter - memoryBefore, warm.InitialResult));
    }

    static string BuildReport(GameConfig config, SolverTable table, double coldMs, double warmMs, long retainedBytes,
        PositionResult warmInitial)
    {
        var stats = table.Stats;
        var report = new StringBuilder();
        report.AppendLine("Tic-Tac-Fade solver report (3x3, buffer 3, X starts)");
        report.AppendLine($"Upper bound on positions: {GameSolver.EstimatePositionUpperBound(config):N0}");
        report.AppendLine($"Reachable positions: {stats.PositionCount:N0}; legal moves: {stats.MoveCount:N0}");
        report.AppendLine($"Time: cold {coldMs:F0} ms (explore {stats.ExploreMilliseconds:F0} ms + retrograde {stats.RetrogradeMilliseconds:F0} ms), warm {warmMs:F0} ms");
        report.AppendLine($"Managed memory retained by the table: {retainedBytes / (1024.0 * 1024.0):F1} MB");
        report.AppendLine($"Initial position: {table.InitialResult} (second run: {warmInitial})");

        report.AppendLine("First moves (value for X):");
        foreach (var move in table.GetMoveResults(table.InitialPosition).OrderBy(m => m.CellIndex))
            report.AppendLine($"  cell {move.CellIndex}: {move.Result}");

        int total = stats.PositionCount;
        report.AppendLine("Outcome distribution over reachable positions (for the player to move):");
        report.AppendLine($"  Win: {stats.WinCount:N0} ({Percent(stats.WinCount, total)})");
        report.AppendLine($"  Loss: {stats.LossCount:N0} ({Percent(stats.LossCount, total)})");
        report.AppendLine($"  NoForcedWin: {stats.NoForcedWinCount:N0} ({Percent(stats.NoForcedWinCount, total)})");
        report.AppendLine($"Longest forced win: {stats.MaxWinDistance} moves; longest forced loss: {stats.MaxLossDistance} moves");

        report.AppendLine("Win/Loss positions by distance:");
        for (int d = 1; d < stats.PositionsByDistance.Count; d++)
        {
            if (stats.PositionsByDistance[d] > 0)
                report.AppendLine($"  {d}: {stats.PositionsByDistance[d]:N0}");
        }

        int longest = Math.Max(stats.MaxWinDistance, stats.MaxLossDistance);
        report.AppendLine($"Move cap: MaxTotalMoves = {config.MaxTotalMoves}; longest forced line from any reachable position = {longest} moves " +
                          (longest <= config.MaxTotalMoves
                              ? "(fits from the start of a match)."
                              : "(exceeds the cap from the start of a match)."));
        return report.ToString();
    }

    static string Percent(int part, int total) => total == 0 ? "0%" : $"{100.0 * part / total:F1}%";
}
