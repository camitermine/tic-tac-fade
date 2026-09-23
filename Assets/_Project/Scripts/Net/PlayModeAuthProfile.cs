using UnityEngine;

namespace TicTacFade.Net
{
    /// <summary>
    /// Authentication profile for Multiplayer Play Mode. Every editor
    /// instance must sign in as a different anonymous player, or they all
    /// share one identity and can't join each other's rooms.
    /// The Authentication package already derives a profile from the
    /// virtual player's command line, but only as undocumented internal
    /// behavior; this makes the choice explicit and visible.
    /// </summary>
    public static class PlayModeAuthProfile
    {
        const string VirtualPlayerPrefix = "mppm_";

        /// <summary>
        /// Null for the main editor and for player builds (default profile).
        /// For a virtual player, a stable name derived from its clone path,
        /// which is different for each virtual player.
        /// </summary>
        public static string Resolve()
        {
#if UNITY_EDITOR
            if (Unity.Multiplayer.PlayMode.CurrentPlayer.IsMainEditor)
                return null;

            // Profile names allow letters, digits, '-' and '_', up to 30
            // chars: "mppm_" + 8 hex digits fits.
            return VirtualPlayerPrefix + StableHash(Application.dataPath).ToString("x8");
#else
            return null;
#endif
        }

        // FNV-1a: string.GetHashCode is not guaranteed stable across runs.
        static uint StableHash(string text)
        {
            uint hash = 2166136261;
            foreach (char c in text)
            {
                hash ^= c;
                hash *= 16777619;
            }
            return hash;
        }
    }
}
