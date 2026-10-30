using System;

namespace TicTacFade.Game
{
    public sealed class SystemRandomSource : IRandomSource
    {
        readonly Random _random;

        public SystemRandomSource() => _random = new Random();

        /// <summary>Repeatable sequence (tests that play many seeded matches).</summary>
        public SystemRandomSource(int seed) => _random = new Random(seed);

        public int Next(int maxExclusive) => _random.Next(maxExclusive);
    }
}
