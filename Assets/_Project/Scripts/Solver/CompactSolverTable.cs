using System;
using System.Collections.Generic;
using System.IO;
using TicTacFade.Core;

namespace TicTacFade.Solver
{
    /// <summary>
    /// The solved value of every position, without the move graph: what the
    /// AI ships with (ADR 0005). Covers both starting players, because a
    /// rematch alternates who starts (GDD §3.1) and the opening positions
    /// differ. Lookups are a binary search over sorted keys.
    ///
    /// <para><b>Binary format</b> (little-endian, deterministic for the same
    /// rules): magic "TTFT", format version, BoardSize, BufferSize,
    /// WinLength, count (all <see cref="int"/>); then <c>count</c> keys
    /// (<see cref="PositionKey.Value"/>, ascending); then <c>count</c> value
    /// bytes, <c>outcome &lt;&lt; 6 | distance</c>. The draw limits are not
    /// stored: the solver ignores them (ADR 0004).</para>
    /// </summary>
    public sealed class CompactSolverTable
    {
        const int Magic = 0x54465454; // "TTFT"
        public const int FormatVersion = 1;
        const int DistanceBits = 6;
        const int MaxDistance = (1 << DistanceBits) - 1;

        readonly ulong[] _keys;
        readonly byte[] _values;

        public int BoardSize { get; }
        public int BufferSize { get; }
        public int WinLength { get; }
        public int Count => _keys.Length;

        CompactSolverTable(int boardSize, int bufferSize, int winLength, ulong[] keys, byte[] values)
        {
            BoardSize = boardSize;
            BufferSize = bufferSize;
            WinLength = winLength;
            _keys = keys;
            _values = values;
        }

        /// <summary>Solves the configuration from both starting players and merges the results.</summary>
        public static CompactSolverTable Solve(GameConfig config) =>
            FromTables(GameSolver.Solve(config, Occupant.X), GameSolver.Solve(config, Occupant.O));

        /// <summary>
        /// Merges solved tables of the same board rules. A position's value
        /// depends only on the position, so a position present in several
        /// tables must have the same value in all of them.
        /// </summary>
        public static CompactSolverTable FromTables(params SolverTable[] tables)
        {
            if (tables == null || tables.Length == 0)
                throw new ArgumentException("At least one solved table is needed.", nameof(tables));

            var config = tables[0].Config;
            var merged = new Dictionary<ulong, PositionResult>();
            foreach (var table in tables)
            {
                if (!SameBoardRules(table.Config, config.BoardSize, config.BufferSize, config.WinLength))
                    throw new ArgumentException("Every table must be solved with the same board rules.", nameof(tables));

                foreach (var entry in table.Entries)
                {
                    if (merged.TryGetValue(entry.Key.Value, out var existing) && !existing.Equals(entry.Value))
                    {
                        throw new InvalidOperationException(
                            $"Position {entry.Key.Value} is {existing} in one table and {entry.Value} in another.");
                    }
                    merged[entry.Key.Value] = entry.Value;
                }
            }

            var keys = new ulong[merged.Count];
            merged.Keys.CopyTo(keys, 0);
            Array.Sort(keys);
            var values = new byte[keys.Length];
            for (int i = 0; i < keys.Length; i++)
                values[i] = Encode(merged[keys[i]]);

            return new CompactSolverTable(config.BoardSize, config.BufferSize, config.WinLength, keys, values);
        }

        public byte[] ToBytes()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Magic);
                writer.Write(FormatVersion);
                writer.Write(BoardSize);
                writer.Write(BufferSize);
                writer.Write(WinLength);
                writer.Write(_keys.Length);
                foreach (ulong key in _keys)
                    writer.Write(key);
                writer.Write(_values);
                writer.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>
        /// Reads a table written by <see cref="ToBytes"/>. Throws when the
        /// data is not a table of this format or was solved for different
        /// board rules than <paramref name="expected"/>.
        /// </summary>
        public static CompactSolverTable FromBytes(byte[] bytes, GameConfig expected)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            try
            {
                using (var reader = new BinaryReader(new MemoryStream(bytes)))
                {
                    if (reader.ReadInt32() != Magic)
                        throw new InvalidDataException("The data is not a Tic-Tac-Fade solver table.");
                    int version = reader.ReadInt32();
                    if (version != FormatVersion)
                        throw new InvalidDataException($"Solver table format {version}, expected {FormatVersion}. Regenerate the table.");

                    int boardSize = reader.ReadInt32();
                    int bufferSize = reader.ReadInt32();
                    int winLength = reader.ReadInt32();
                    if (!SameBoardRules(expected, boardSize, bufferSize, winLength))
                    {
                        throw new InvalidDataException(
                            $"The solver table is for BoardSize={boardSize}, BufferSize={bufferSize}, WinLength={winLength}, " +
                            $"but the game uses BoardSize={expected.BoardSize}, BufferSize={expected.BufferSize}, " +
                            $"WinLength={expected.WinLength}. Regenerate the table.");
                    }

                    int count = reader.ReadInt32();
                    if (count < 0 || bytes.Length != 24 + count * 9L)
                        throw new InvalidDataException("The solver table is truncated or corrupt.");

                    var keys = new ulong[count];
                    for (int i = 0; i < count; i++)
                        keys[i] = reader.ReadUInt64();
                    var values = reader.ReadBytes(count);
                    return new CompactSolverTable(boardSize, bufferSize, winLength, keys, values);
                }
            }
            catch (EndOfStreamException e)
            {
                throw new InvalidDataException("The solver table is truncated or corrupt.", e);
            }
        }

        public bool TryGetResult(PositionKey key, out PositionResult result)
        {
            int index = Array.BinarySearch(_keys, key.Value);
            if (index < 0)
            {
                result = default;
                return false;
            }

            result = Decode(_values[index]);
            return true;
        }

        static bool SameBoardRules(GameConfig config, int boardSize, int bufferSize, int winLength) =>
            config.BoardSize == boardSize && config.BufferSize == bufferSize && config.WinLength == winLength;

        static byte Encode(PositionResult result)
        {
            if (result.Distance < 0 || result.Distance > MaxDistance)
                throw new InvalidOperationException($"Distance {result.Distance} doesn't fit in {DistanceBits} bits.");
            return (byte)(((int)result.Outcome << DistanceBits) | result.Distance);
        }

        static PositionResult Decode(byte value) =>
            new PositionResult((PositionOutcome)(value >> DistanceBits), value & MaxDistance);
    }
}
