using System;
using System.Collections.Generic;

namespace TicTacFade.Core
{
    /// <summary>
    /// EXACT key (not a probabilistic hash) of a position, as defined by
    /// GDD §3.4: both players' queues, in order, plus the player who moves
    /// next. Used as the key of the repetition dictionary in
    /// <see cref="GameState"/>/<see cref="RulesEngine"/>. Being an exact
    /// representation (not a hash), two different positions can never
    /// collide.
    ///
    /// <para><b>Packing</b> (all in a single <see cref="ulong"/>):</para>
    /// <list type="bullet">
    /// <item><c>2 × GameConfig.BufferSize</c> "slots" are reserved (one per
    /// possible piece in each queue), each <c>bitsPerSlot</c> bits wide.</item>
    /// <item><c>bitsPerSlot</c> is the minimum number of bits such that
    /// <c>(1 &lt;&lt; bitsPerSlot) - 1 &gt;= BoardSize²</c>: enough to represent
    /// any valid cell index (0..BoardSize²-1) PLUS a sentinel value that never
    /// collides with a real index. For 3x3 (9 cells) that's 4 bits (sentinel
    /// 15); for a future 4x4 (16 cells) it's 5 bits (sentinel 31): the packing
    /// is recomputed purely from <see cref="GameConfig.BoardSize"/>, it's not
    /// hardcoded to 3x3.</item>
    /// <item>A slot for a queue that doesn't have that piece yet (a queue with
    /// fewer than <c>BufferSize</c> elements) is filled with the sentinel.</item>
    /// <item>One final bit is added for the player who moves next (0 = X, 1 = O;
    /// valid because during a match only X or O can be "the next one").</item>
    /// </list>
    /// For the MVP (3x3, buffer 3): 2×3×4 + 1 = 25 bits, well under the 64
    /// available. <see cref="Compute"/> throws if some future configuration
    /// ever made the packing exceed 64 bits.
    /// </summary>
    public readonly struct PositionKey : IEquatable<PositionKey>
    {
        readonly ulong _value;

        PositionKey(ulong value) => _value = value;

        /// <summary>
        /// The packed key as a number, so two peers can compare positions
        /// over the network (online desync check, GDD §5). Equal positions
        /// always give the same value; different positions never do.
        /// </summary>
        public ulong Value => _value;

        public static PositionKey Compute(GameConfig config, IReadOnlyList<int> queueX, IReadOnlyList<int> queueO, Occupant nextPlayer)
        {
            int bitsPerSlot = ComputeBitsPerSlot(config.BoardSize);
            int sentinel = (1 << bitsPerSlot) - 1;
            int totalBits = 2 * config.BufferSize * bitsPerSlot + 1;
            if (totalBits > 64)
            {
                throw new InvalidOperationException(
                    $"PositionKey can't pack this configuration into 64 bits (BoardSize={config.BoardSize}, BufferSize={config.BufferSize} need {totalBits} bits).");
            }

            ulong value = 0;
            int shift = 0;
            PackQueue(ref value, ref shift, queueX, config.BufferSize, bitsPerSlot, sentinel);
            PackQueue(ref value, ref shift, queueO, config.BufferSize, bitsPerSlot, sentinel);

            ulong turnBit = nextPlayer == Occupant.O ? 1UL : 0UL;
            value |= turnBit << shift;

            return new PositionKey(value);
        }

        static void PackQueue(ref ulong value, ref int shift, IReadOnlyList<int> queue, int bufferSize, int bitsPerSlot, int sentinel)
        {
            for (int i = 0; i < bufferSize; i++)
            {
                int slotValue = i < queue.Count ? queue[i] : sentinel;
                value |= (ulong)slotValue << shift;
                shift += bitsPerSlot;
            }
        }

        static int ComputeBitsPerSlot(int boardSize)
        {
            int cellCount = boardSize * boardSize;
            int bits = 1;
            while ((1 << bits) - 1 < cellCount)
                bits++;
            return bits;
        }

        public bool Equals(PositionKey other) => _value == other._value;
        public override bool Equals(object obj) => obj is PositionKey other && Equals(other);
        public override int GetHashCode() => _value.GetHashCode();
        public static bool operator ==(PositionKey left, PositionKey right) => left.Equals(right);
        public static bool operator !=(PositionKey left, PositionKey right) => !left.Equals(right);
    }
}
