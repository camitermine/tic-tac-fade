namespace TicTacFade.Game
{
    /// <summary>
    /// Time source for the online match (turn timer, timeouts). Injected so
    /// tests advance time by hand instead of waiting in real time.
    /// </summary>
    public interface IClock
    {
        /// <summary>Seconds, monotonic.</summary>
        double Now { get; }
    }
}
