using TicTacFade.Core;

namespace TicTacFade.Game
{
    public enum MatchMessageKind : byte
    {
        /// <summary>Host → client: a match starts; <see cref="MatchMessage.Player"/> starts it.</summary>
        StartMatch = 1,

        /// <summary>Client → host: a move the client wants to play.</summary>
        Propose = 2,

        /// <summary>Host → client: a validated move, its number and the host's position key after it.</summary>
        Confirmed = 3,

        /// <summary>Host → client: the client's proposal was illegal.</summary>
        Rejected = 4,

        /// <summary>Client → host: the client applied move <see cref="MatchMessage.MoveNumber"/>; its position key.</summary>
        Ack = 5,

        /// <summary>Client → host: the client wants a rematch.</summary>
        RematchRequest = 6,

        /// <summary>Either way: position keys differ; the match is cut.</summary>
        Desync = 7,

        /// <summary>
        /// Host → client: a turn started. <see cref="MatchMessage.Player"/> moves,
        /// <see cref="MatchMessage.DurationMs"/> is its time, <see cref="MatchMessage.AbsentFlags"/>
        /// who is absent. Only for display: the host alone decides expiry.
        /// </summary>
        TurnTimer = 8,

        /// <summary>Host → client: <see cref="MatchMessage.Player"/> abandoned (<see cref="MatchMessage.Cause"/>).</summary>
        Abandoned = 9,

        /// <summary>Either way: the sender pressed "Salir" and loses the match.</summary>
        Forfeit = 10,

        /// <summary>Either way: the Forfeit was received; the sender may close the session.</summary>
        ForfeitAck = 11,
    }

    /// <summary>Bits of <see cref="MatchMessage.AbsentFlags"/>.</summary>
    public static class AbsentFlags
    {
        public const byte X = 1 << 0;
        public const byte O = 1 << 1;

        public static byte For(Occupant player) => player == Occupant.X ? X : player == Occupant.O ? O : (byte)0;
    }

    /// <summary>
    /// Everything that travels over the network during an online match.
    /// Moves (and control messages), never the board (ADR 0002). Fields a
    /// kind doesn't use stay at their defaults.
    /// </summary>
    public readonly struct MatchMessage
    {
        public MatchMessageKind Kind { get; }
        public Occupant Player { get; }
        public int CellIndex { get; }
        public int MoveNumber { get; }
        public ulong PositionKey { get; }
        public MoveRejectionReason RejectionReason { get; }
        public int DurationMs { get; }
        public byte AbsentFlags { get; }
        public AbandonmentCause Cause { get; }

        public MatchMessage(MatchMessageKind kind, Occupant player = Occupant.None, int cellIndex = -1,
            int moveNumber = 0, ulong positionKey = 0, MoveRejectionReason rejectionReason = default,
            int durationMs = 0, byte absentFlags = 0, AbandonmentCause cause = AbandonmentCause.None)
        {
            Kind = kind;
            Player = player;
            CellIndex = cellIndex;
            MoveNumber = moveNumber;
            PositionKey = positionKey;
            RejectionReason = rejectionReason;
            DurationMs = durationMs;
            AbsentFlags = absentFlags;
            Cause = cause;
        }

        public Move Move => new Move(Player, CellIndex);

        public static MatchMessage StartMatch(Occupant startingPlayer) =>
            new MatchMessage(MatchMessageKind.StartMatch, player: startingPlayer);

        public static MatchMessage Propose(Move move) =>
            new MatchMessage(MatchMessageKind.Propose, move.Player, move.CellIndex);

        public static MatchMessage Confirmed(Move move, int moveNumber, ulong positionKey) =>
            new MatchMessage(MatchMessageKind.Confirmed, move.Player, move.CellIndex, moveNumber, positionKey);

        public static MatchMessage Rejected(Move move, MoveRejectionReason reason) =>
            new MatchMessage(MatchMessageKind.Rejected, move.Player, move.CellIndex, rejectionReason: reason);

        public static MatchMessage Ack(int moveNumber, ulong positionKey) =>
            new MatchMessage(MatchMessageKind.Ack, moveNumber: moveNumber, positionKey: positionKey);

        public static MatchMessage RematchRequest() => new MatchMessage(MatchMessageKind.RematchRequest);

        public static MatchMessage Desync() => new MatchMessage(MatchMessageKind.Desync);

        public static MatchMessage TurnTimer(Occupant player, int moveNumber, int durationMs, byte absentFlags) =>
            new MatchMessage(MatchMessageKind.TurnTimer, player, moveNumber: moveNumber, durationMs: durationMs, absentFlags: absentFlags);

        public static MatchMessage Abandoned(Occupant abandoner, AbandonmentCause cause) =>
            new MatchMessage(MatchMessageKind.Abandoned, abandoner, cause: cause);

        public static MatchMessage Forfeit() => new MatchMessage(MatchMessageKind.Forfeit);

        public static MatchMessage ForfeitAck() => new MatchMessage(MatchMessageKind.ForfeitAck);
    }
}
