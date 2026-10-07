using TicTacFade.Game;

namespace TicTacFade.PlayModeTests
{
    /// <summary>Returns <see cref="Value"/> (clamped to the range) every time.</summary>
    public sealed class FixedRandomSource : IRandomSource
    {
        public int Value { get; set; }

        public FixedRandomSource(int value = 0) => Value = value;

        public int Next(int maxExclusive) => System.Math.Min(Value, maxExclusive - 1);
    }
}
