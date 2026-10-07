using UnityEngine;

namespace TicTacFade.Game
{
    /// <summary>
    /// Real-time clock, unaffected by Time.timeScale: a turn timer must not
    /// slow down if the game ever scales time.
    /// </summary>
    public sealed class UnityClock : IClock
    {
        public double Now => Time.realtimeSinceStartupAsDouble;
    }
}
