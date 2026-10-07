namespace TicTacFade.Game
{
    /// <summary>
    /// Randomness for the automatic move on a turn timeout. Lives in Game,
    /// never in Core (Core stays deterministic), and is injected so tests
    /// can control it.
    /// </summary>
    public interface IRandomSource
    {
        /// <summary>A value in [0, maxExclusive).</summary>
        int Next(int maxExclusive);
    }
}
