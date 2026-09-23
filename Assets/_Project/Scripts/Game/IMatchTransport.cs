using System;

namespace TicTacFade.Game
{
    /// <summary>
    /// Channel between the two devices of an online match (ADR 0002). The
    /// implementation (TicTacFade.Net) must deliver every message reliably
    /// and in order: a lost or reordered confirmed move can't be left for
    /// the position-key check to catch.
    /// Messages received before <see cref="Open"/> are buffered and
    /// delivered on <see cref="Open"/>, so a StartMatch that arrives before
    /// this device's flow is ready isn't lost.
    /// </summary>
    public interface IMatchTransport
    {
        /// <summary>True on the device that hosts the session (plays X, validates moves).</summary>
        bool IsHost { get; }

        /// <summary>Host side: the other device is connected and can receive messages.</summary>
        bool IsPeerConnected { get; }

        /// <summary>Host side: the other device just connected.</summary>
        event Action PeerConnected;

        event Action<MatchMessage> MessageReceived;

        /// <summary>Sends to the other device.</summary>
        void Send(MatchMessage message);

        /// <summary>Starts delivering messages, buffered ones first.</summary>
        void Open();

        /// <summary>Stops delivering and drops anything buffered.</summary>
        void Close();
    }
}
