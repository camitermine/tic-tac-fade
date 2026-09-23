using System;
using UnityEngine;

namespace TicTacFade.Game
{
    /// <summary>
    /// Serializable base for <see cref="IMatchTransport"/>, same reason as
    /// <see cref="SessionServiceBehaviour"/>: the scene can't reference an
    /// interface. Net provides the real subclass.
    /// </summary>
    public abstract class MatchTransportBehaviour : MonoBehaviour, IMatchTransport
    {
        public abstract bool IsHost { get; }
        public abstract bool IsPeerConnected { get; }

        public abstract event Action PeerConnected;
        public abstract event Action<MatchMessage> MessageReceived;

        public abstract void Send(MatchMessage message);
        public abstract void Open();
        public abstract void Close();
    }
}
