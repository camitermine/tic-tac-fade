using System.Collections.Generic;
using NUnit.Framework;
using TicTacFade.Game;

namespace TicTacFade.PlayModeTests
{
    /// <summary>
    /// Returns the given values in order and fails the test if a value is
    /// out of range or more draws are asked for than scripted.
    /// </summary>
    public sealed class ScriptedRandomSource : IRandomSource
    {
        readonly Queue<int> _values;

        public ScriptedRandomSource(params int[] values) => _values = new Queue<int>(values);

        public int Remaining => _values.Count;

        public int Next(int maxExclusive)
        {
            Assert.IsTrue(_values.Count > 0, "More random draws than scripted.");
            int value = _values.Dequeue();
            Assert.That(value, Is.InRange(0, maxExclusive - 1), $"Scripted value {value} is out of [0, {maxExclusive}).");
            return value;
        }
    }
}
