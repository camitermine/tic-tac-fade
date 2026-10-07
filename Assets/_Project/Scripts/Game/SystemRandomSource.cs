using System;

namespace TicTacFade.Game
{
    public sealed class SystemRandomSource : IRandomSource
    {
        readonly Random _random = new Random();

        public int Next(int maxExclusive) => _random.Next(maxExclusive);
    }
}
