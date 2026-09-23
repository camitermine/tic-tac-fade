namespace TicTacFade.Game
{
    /// <summary>
    /// Why an online session operation failed, in the game's own terms.
    /// The Net layer translates SDK exceptions into these values so neither
    /// Game nor UI depend on the multiplayer SDK (ADR 0002).
    /// </summary>
    public enum SessionFailure
    {
        None = 0,
        InvalidCode = 1,
        SessionFull = 2,
        NoConnection = 3,
        ServiceUnavailable = 4,
        Timeout = 5,
        NotLinked = 6,
        SessionClosed = 7,
        Unknown = 8,

        /// <summary>
        /// The service refused the join without saying why (e.g. a full
        /// room: the SDK doesn't tell it apart from other refusals).
        /// </summary>
        JoinRejected = 9,

        /// <summary>The other player left an online match (or its result screen).</summary>
        OpponentLeft = 10,

        /// <summary>Both devices disagreed on the position; the match was cut.</summary>
        Desync = 11,
    }
}
