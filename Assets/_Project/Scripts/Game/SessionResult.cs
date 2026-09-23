namespace TicTacFade.Game
{
    /// <summary>
    /// Outcome of a session operation. Failures are values, not exceptions:
    /// an <see cref="ISessionService"/> never lets an exception escape.
    /// </summary>
    public readonly struct SessionResult
    {
        public SessionFailure Failure { get; }

        public bool Success => Failure == SessionFailure.None;

        SessionResult(SessionFailure failure)
        {
            Failure = failure;
        }

        public static SessionResult Ok() => new SessionResult(SessionFailure.None);

        public static SessionResult Fail(SessionFailure failure) => new SessionResult(failure);
    }
}
