using System;
using System.Threading.Tasks;

namespace TicTacFade.Game
{
    /// <summary>
    /// Online room (session) lifecycle as the game sees it: create a room
    /// and get a code, join one with a code, leave. The implementation
    /// (TicTacFade.Net) hides the multiplayer SDK (ADR 0002).
    /// Contract: operations never throw; failures come back as
    /// <see cref="SessionResult"/>. Services are initialized lazily, on the
    /// first create/join, never at startup.
    /// </summary>
    public interface ISessionService
    {
        /// <summary>Join code of the current room; null when not in one.</summary>
        string JoinCode { get; }

        /// <summary>The other player joined the room (host side).</summary>
        event Action OpponentConnected;

        /// <summary>The other player left the room (host side).</summary>
        event Action OpponentLeft;

        /// <summary>The room was closed or this player was removed from it.</summary>
        event Action SessionLost;

        /// <summary>
        /// Whether a code (already trimmed and upper-cased) has the format
        /// the provider uses. Checked before going to the network; the
        /// format belongs to the provider, so it lives behind this interface.
        /// </summary>
        bool IsWellFormedCode(string code);

        Task<SessionResult> CreateAsync();

        Task<SessionResult> JoinByCodeAsync(string code);

        /// <summary>
        /// Leaves (or, as host, closes) the current room and waits until the
        /// network layer is fully shut down, so a new create/join can start
        /// right after. Never throws.
        /// </summary>
        Task LeaveAsync();
    }
}
