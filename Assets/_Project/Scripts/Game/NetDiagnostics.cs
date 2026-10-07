using UnityEngine;

namespace TicTacFade.Game
{
    /// <summary>
    /// Diagnostic log for the online start-up sequence (rooms, connection,
    /// StartMatch). Every line has the same prefix and the time since
    /// startup, so a device's log (editor console or `adb logcat -s Unity`)
    /// can be filtered and the two sides lined up. Info level only: never
    /// changes behavior.
    /// </summary>
    public static class NetDiagnostics
    {
        /// <summary>
        /// On in the game. The PlayMode test suite turns it off: its tests
        /// assert "no unexpected log", which also counts info lines.
        /// </summary>
        public static bool Enabled { get; set; } = true;

        public static void Log(string message)
        {
            if (Enabled)
                Debug.Log($"Tic-Tac-Fade [net t={Time.realtimeSinceStartup:F2}] {message}");
        }
    }
}
