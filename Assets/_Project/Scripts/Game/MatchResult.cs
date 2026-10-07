using TicTacFade.Core;

namespace TicTacFade.Game
{
    public enum AbandonmentCause : byte
    {
        None = 0,

        /// <summary>The player let <see cref="OnlineTimerConfig.MaxConsecutiveTimeouts"/> turns expire in a row.</summary>
        Timeouts = 1,

        /// <summary>The player pressed "Salir" during the match.</summary>
        Quit = 2,

        /// <summary>The player's device disconnected during the match (no reconnection in the MVP).</summary>
        Disconnected = 3,
    }

    /// <summary>
    /// How a match ended. Either the board decided it (<see cref="BoardEnd"/>,
    /// Core's event), or a player abandoned it: abandonment is a match
    /// result, not a board rule, so it lives in Game and Core never sees it.
    /// </summary>
    public sealed class MatchResult
    {
        /// <summary>Set when the board ended the match; null for an abandonment.</summary>
        public GameEndedEvent BoardEnd { get; }

        /// <summary>Who abandoned; None when the board ended the match.</summary>
        public Occupant Abandoner { get; }

        public AbandonmentCause Cause { get; }

        public bool IsAbandonment => BoardEnd == null;

        /// <summary>The winner; None for a draw.</summary>
        public Occupant Winner => IsAbandonment
            ? (Abandoner == Occupant.X ? Occupant.O : Occupant.X)
            : BoardEnd.Winner;

        MatchResult(GameEndedEvent boardEnd, Occupant abandoner, AbandonmentCause cause)
        {
            BoardEnd = boardEnd;
            Abandoner = abandoner;
            Cause = cause;
        }

        public static MatchResult FromBoard(GameEndedEvent boardEnd) =>
            new MatchResult(boardEnd, Occupant.None, AbandonmentCause.None);

        public static MatchResult FromAbandonment(Occupant abandoner, AbandonmentCause cause) =>
            new MatchResult(null, abandoner, cause);
    }
}
