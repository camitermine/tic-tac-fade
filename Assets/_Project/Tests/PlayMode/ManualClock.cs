using TicTacFade.Game;

namespace TicTacFade.PlayModeTests
{
    /// <summary>A clock that only moves when the test says so.</summary>
    public sealed class ManualClock : IClock
    {
        public double Now { get; private set; }

        public void Advance(double seconds) => Now += seconds;
    }
}
