using System;

namespace TicTacFade.Game
{
    /// <summary>
    /// Channel between the two devices of an online match (ADR 0002). The
    /// implementation (TicTacFade.Net) must deliver every message reliably
    /// and in order: a lost or reordered confirmed move can't be left for
    /// the position-key check to catch.
    /// Messages received before <see cref="Open"/> are buffered and
    /// delivered on <see cref="Open"/>. The match doesn't depend on it to
    /// start: the host only starts when the client's Ready arrives.
    /// Bytes go through <see cref="MatchMessageCodec"/>, so a malformed
    /// frame is dropped with a warning, never with an exception.
    /// </summary>
    public interface IMatchTransport
    {
        /// <summary>True on the device that hosts the session (plays X, validates moves).</summary>
        bool IsHost { get; }

        /// <summary>
        /// The network reported that the other device is gone (host side:
        /// the client dropped; client side: the connection to the host
        /// dropped). Explicit, unlike a turn simply not being played.
        /// </summary>
        event Action PeerDisconnected;

        event Action<MatchMessage> MessageReceived;

        /// <summary>
        /// Sends to the other device. A client Ready sent before the
        /// connection exists is dropped silently (it is resent).
        /// </summary>
        void Send(MatchMessage message);

        /// <summary>Starts delivering messages, buffered ones first.</summary>
        void Open();

        /// <summary>Stops delivering and drops anything buffered.</summary>
        void Close();
    }
}
