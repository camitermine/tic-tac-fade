using System.IO;
using System.Linq;
using NUnit.Framework;
using TicTacFade.Core;
using TicTacFade.Solver;

namespace TicTacFade.Core.Tests
{
    /// <summary>
    /// The compact table the AI ships with (ADR 0005): same values as the
    /// solver, for both starting players, and the committed asset in sync
    /// with the current rules.
    /// </summary>
    public class CompactSolverTableTests
    {
        // Written by the Tic-Tac-Fade/Solver/Generate AI table menu (SolverTableGenerator).
        const string CommittedAssetPath = "Assets/_Project/Config/AI/SolverTable.bytes";

        static readonly GameConfig Mvp = GameConfig.Mvp();

        SolverTable _startX;
        SolverTable _startO;
        CompactSolverTable _compact;

        [OneTimeSetUp]
        public void SolveOnce()
        {
            _startX = GameSolver.Solve(Mvp, Occupant.X);
            _startO = GameSolver.Solve(Mvp, Occupant.O);
            _compact = CompactSolverTable.FromTables(_startX, _startO);
        }

        [Test]
        [Category("Slow")]
        public void CompactTable_MatchesSolver_ForBothStartingPlayers()
        {
            foreach (var table in new[] { _startX, _startO })
            {
                foreach (var entry in table.Entries)
                {
                    Assert.IsTrue(_compact.TryGetResult(entry.Key, out var result), $"Position {entry.Key.Value} is missing.");
                    Assert.AreEqual(entry.Value, result, $"Position {entry.Key.Value}.");
                }
            }

            // Only positions that one of the two solves reached.
            int union = _startX.Entries.Select(e => e.Key).Union(_startO.Entries.Select(e => e.Key)).Count();
            Assert.AreEqual(union, _compact.Count);
        }

        [Test]
        public void CompactTable_ContainsOpeningPositions_WhenOStarts()
        {
            // O opened on 4, X to move: only reachable when O starts.
            var afterO = RulesEngine.Apply(GameState.CreateInitial(Mvp, Occupant.O), new Move(Occupant.O, 4)).State;
            var key = PositionKey.Compute(Mvp, afterO.QueueX, afterO.QueueO, afterO.CurrentPlayer);

            Assert.IsFalse(_startX.Contains(key), "Test setup: the X-start solve doesn't reach it.");
            Assert.IsTrue(_compact.TryGetResult(key, out _));
        }

        [Test]
        public void CompactTable_RoundTrip_PreservesEveryResult()
        {
            var bytes = _compact.ToBytes();
            var read = CompactSolverTable.FromBytes(bytes, Mvp);

            Assert.AreEqual(_compact.Count, read.Count);
            foreach (var entry in _startX.Entries.Concat(_startO.Entries))
            {
                Assert.IsTrue(read.TryGetResult(entry.Key, out var result));
                Assert.AreEqual(entry.Value, result);
            }
            CollectionAssert.AreEqual(bytes, read.ToBytes(), "Serializing twice must give the same bytes.");
        }

        [Test]
        public void CompactTable_UnknownPosition_IsNotFound()
        {
            // X holds 0,1,2: that position never exists (the line would have won).
            var key = PositionKey.Compute(Mvp, new[] { 0, 1, 2 }, new[] { 3, 4, 6 }, Occupant.O);

            Assert.IsFalse(_compact.TryGetResult(key, out _));
        }

        [Test]
        public void CompactTable_FromBytes_DifferentBoardRules_Throws()
        {
            var other = new GameConfig(boardSize: 3, bufferSize: 2, winLength: 3, maxTotalMoves: 40, repetitionLimit: 3);

            var error = Assert.Throws<InvalidDataException>(() => CompactSolverTable.FromBytes(_compact.ToBytes(), other));
            StringAssert.Contains("Regenerate", error.Message);
        }

        [Test]
        public void CompactTable_FromBytes_DrawLimitsDiffer_StillLoads()
        {
            // The solver ignores the draw limits, so the table doesn't depend on them.
            var otherLimits = new GameConfig(Mvp.BoardSize, Mvp.BufferSize, Mvp.WinLength, maxTotalMoves: 60, repetitionLimit: 4);

            Assert.DoesNotThrow(() => CompactSolverTable.FromBytes(_compact.ToBytes(), otherLimits));
        }

        [Test]
        public void CompactTable_FromBytes_TruncatedData_Throws()
        {
            var bytes = _compact.ToBytes();
            var truncated = bytes.Take(bytes.Length - 1).ToArray();

            Assert.Throws<InvalidDataException>(() => CompactSolverTable.FromBytes(truncated, Mvp));
            Assert.Throws<InvalidDataException>(() => CompactSolverTable.FromBytes(new byte[] { 1, 2, 3 }, Mvp));
        }

        [Test]
        [Category("Slow")]
        public void CompactTable_CommittedAsset_MatchesRegeneratedTable()
        {
            Assert.IsTrue(File.Exists(CommittedAssetPath),
                $"{CommittedAssetPath} is missing. Generate it with Tic-Tac-Fade/Solver/Generate AI table.");

            var committed = File.ReadAllBytes(CommittedAssetPath);
            var regenerated = CompactSolverTable.Solve(Mvp).ToBytes();

            Assert.IsTrue(committed.SequenceEqual(regenerated),
                "The committed AI table is out of sync with the rules. Regenerate it with Tic-Tac-Fade/Solver/Generate AI table.");
        }
    }
}
