using System;
using UnityEngine;
using TicTacFade.Core;

namespace TicTacFade.Game
{
    /// <summary>
    /// Wire format of a <see cref="MatchMessage"/> (ADR 0002). Shared by every
    /// transport so they all accept and reject exactly the same bytes.
    /// <para>
    /// Every frame starts with a header that never changes between versions:
    /// kind(1) + protocol version(2). Decoding never throws: a frame from
    /// another protocol version is decoded from its header alone (whatever
    /// its size), so the receiver can report the version mismatch; a frame
    /// of this version with an unexpected size, or an unknown kind, is
    /// dropped with a warning.
    /// </para>
    /// </summary>
    public static class MatchMessageCodec
    {
        /// <summary>
        /// Bump on any change to the frame layout or to the meaning of a
        /// message. Version 1 is the first versioned protocol; earlier builds
        /// (no version, no Ready) are not compatible.
        /// </summary>
        public const ushort ProtocolVersion = 1;

        public const int HeaderSize = 3;

        // header(3) + player(1) + cell(4) + moveNumber(4) + key(8) + reason(1)
        // + durationMs(4) + absentFlags(1) + cause(1) + matchNumber(4)
        public const int FrameSize = 31;

        public static byte[] Encode(MatchMessage message)
        {
            var data = new byte[FrameSize];
            int offset = 0;
            WriteByte(data, ref offset, (byte)message.Kind);
            WriteUInt16(data, ref offset, message.ProtocolVersion);
            WriteByte(data, ref offset, (byte)message.Player);
            WriteInt32(data, ref offset, message.CellIndex);
            WriteInt32(data, ref offset, message.MoveNumber);
            WriteUInt64(data, ref offset, message.PositionKey);
            WriteByte(data, ref offset, (byte)message.RejectionReason);
            WriteInt32(data, ref offset, message.DurationMs);
            WriteByte(data, ref offset, message.AbsentFlags);
            WriteByte(data, ref offset, (byte)message.Cause);
            WriteInt32(data, ref offset, message.MatchNumber);
            return data;
        }

        /// <summary>
        /// Decodes the first <paramref name="length"/> bytes of
        /// <paramref name="data"/>. Returns false (and logs a warning) when the
        /// frame must be dropped. Never throws.
        /// </summary>
        public static bool TryDecode(byte[] data, int length, out MatchMessage message)
        {
            message = default;
            if (data == null || length < HeaderSize || length > data.Length)
                return Drop($"{(data == null ? 0 : length)} byte(s), shorter than the {HeaderSize}-byte header");

            int offset = 0;
            var kind = (MatchMessageKind)ReadByte(data, ref offset);
            ushort version = ReadUInt16(data, ref offset);

            if (version != ProtocolVersion)
            {
                // Another version's layout is unknown: only the header is
                // trusted, so the receiver can tell the player.
                message = new MatchMessage(kind, protocolVersion: version);
                return true;
            }

            if (!Enum.IsDefined(typeof(MatchMessageKind), kind))
                return Drop($"unknown kind {(byte)kind}");

            if (length != FrameSize)
                return Drop($"{kind} with {length} byte(s), expected {FrameSize}");

            var player = (Occupant)ReadByte(data, ref offset);
            int cellIndex = ReadInt32(data, ref offset);
            int moveNumber = ReadInt32(data, ref offset);
            ulong positionKey = ReadUInt64(data, ref offset);
            var reason = (MoveRejectionReason)ReadByte(data, ref offset);
            int durationMs = ReadInt32(data, ref offset);
            byte absentFlags = ReadByte(data, ref offset);
            var cause = (AbandonmentCause)ReadByte(data, ref offset);
            int matchNumber = ReadInt32(data, ref offset);

            message = new MatchMessage(kind, player, cellIndex, moveNumber, positionKey, reason,
                durationMs, absentFlags, cause, matchNumber, version);
            return true;
        }

        static bool Drop(string detail)
        {
            Debug.LogWarning($"Tic-Tac-Fade: dropped malformed match message ({detail}).");
            return false;
        }

        static void WriteByte(byte[] data, ref int offset, byte value) => data[offset++] = value;

        static void WriteUInt16(byte[] data, ref int offset, ushort value)
        {
            data[offset++] = (byte)value;
            data[offset++] = (byte)(value >> 8);
        }

        static void WriteInt32(byte[] data, ref int offset, int value)
        {
            for (int i = 0; i < 4; i++)
                data[offset++] = (byte)(value >> (8 * i));
        }

        static void WriteUInt64(byte[] data, ref int offset, ulong value)
        {
            for (int i = 0; i < 8; i++)
                data[offset++] = (byte)(value >> (8 * i));
        }

        static byte ReadByte(byte[] data, ref int offset) => data[offset++];

        static ushort ReadUInt16(byte[] data, ref int offset)
        {
            ushort value = (ushort)(data[offset] | (data[offset + 1] << 8));
            offset += 2;
            return value;
        }

        static int ReadInt32(byte[] data, ref int offset)
        {
            int value = 0;
            for (int i = 0; i < 4; i++)
                value |= data[offset++] << (8 * i);
            return value;
        }

        static ulong ReadUInt64(byte[] data, ref int offset)
        {
            ulong value = 0;
            for (int i = 0; i < 8; i++)
                value |= (ulong)data[offset++] << (8 * i);
            return value;
        }
    }
}
