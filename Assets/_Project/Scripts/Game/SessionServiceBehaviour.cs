using System;
using System.Threading.Tasks;
using UnityEngine;

namespace TicTacFade.Game
{
    /// <summary>
    /// Serializable base for <see cref="ISessionService"/> implementations.
    /// Unity can't serialize an interface field, so the scene references
    /// this abstract MonoBehaviour instead; Net provides the real subclass
    /// and tests provide a fake. No DI framework needed (CLAUDE.md rule 8).
    /// </summary>
    public abstract class SessionServiceBehaviour : MonoBehaviour, ISessionService
    {
        public abstract string JoinCode { get; }

        public abstract event Action OpponentConnected;
        public abstract event Action OpponentLeft;
        public abstract event Action SessionLost;

        public abstract bool IsWellFormedCode(string code);
        public abstract Task<SessionResult> CreateAsync();
        public abstract Task<SessionResult> JoinByCodeAsync(string code);
        public abstract Task LeaveAsync();
    }
}
