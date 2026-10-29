using System.Diagnostics;
using System.IO;
using UnityEditor;
using TicTacFade.Game;
using TicTacFade.Solver;
using Debug = UnityEngine.Debug;

/// <summary>
/// Editor menu that writes the AI's precalculated solver table (ADR 0005).
/// The solve never runs during a match: the game loads this file through a
/// serialized TextAsset field. A Slow EditMode test regenerates the table
/// and compares it with the committed file, so a rules change that forgets
/// to regenerate it fails the suite.
/// </summary>
public static class SolverTableGenerator
{
    // Also read by CompactSolverTableTests.CompactTable_CommittedAsset_MatchesRegeneratedTable.
    public const string AssetPath = "Assets/_Project/Config/AI/SolverTable.bytes";

    const string GameConfigAssetPath = "Assets/_Project/Config/GameConfig.asset";

    [MenuItem("Tic-Tac-Fade/Solver/Generate AI table")]
    public static void Generate()
    {
        var configAsset = AssetDatabase.LoadAssetAtPath<GameConfigAsset>(GameConfigAssetPath);
        if (configAsset == null)
        {
            Debug.LogError($"Tic-Tac-Fade: {GameConfigAssetPath} not found; the AI table is solved for the game's rules.");
            return;
        }

        var clock = Stopwatch.StartNew();
        var table = CompactSolverTable.Solve(configAsset.ToGameConfig());
        var bytes = table.ToBytes();
        clock.Stop();

        Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
        File.WriteAllBytes(AssetPath, bytes);
        AssetDatabase.ImportAsset(AssetPath);

        Debug.Log($"Tic-Tac-Fade: AI table written to {AssetPath}: {table.Count:N0} positions, {bytes.Length / 1024.0:N0} KB, " +
            $"solved in {clock.Elapsed.TotalMilliseconds:F0} ms.");
    }
}
