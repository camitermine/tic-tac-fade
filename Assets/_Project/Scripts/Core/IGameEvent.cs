namespace TicTacFade.Core
{
    /// <summary>
    /// Marker for the events <see cref="RulesEngine.Apply"/> returns.
    /// Core communicates outward exclusively through these events; the UI
    /// observes, it doesn't poll the state.
    /// </summary>
    public interface IGameEvent
    {
    }
}
