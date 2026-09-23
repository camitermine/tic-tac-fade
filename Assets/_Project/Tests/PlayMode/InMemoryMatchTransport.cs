using System;
using System.Collections.Generic;
using TicTacFade.Game;

namespace TicTacFade.PlayModeTests
{
    /// <summary>
    /// Network-free <see cref="IMatchTransport"/> pair for tests. Sent
    /// messages are queued, in order, and only delivered on
    /// <see cref="Pump"/>, which models the real transport: delivery is
    /// asynchronous but reliable and sequenced. Like the real one, a closed
    /// endpoint buffers what it receives until <see cref="Open"/>.
    /// </summary>
    public sealed class InMemoryMatchTransport : IMatchTransport
    {
        readonly Queue<MatchMessage> _inFlight = new Queue<MatchMessage>();
        readonly Queue<MatchMessage> _inbox = new Queue<MatchMessage>();
        InMemoryMatchTransport _peer;
        bool _open;
        bool _peerConnected;

        public bool IsHost { get; }
        public bool IsPeerConnected => IsHost && _peerConnected;

        /// <summary>Every message this endpoint sent, for assertions.</summary>
        public List<MatchMessage> Sent { get; } = new List<MatchMessage>();

        public event Action PeerConnected;
        public event Action<MatchMessage> MessageReceived;

        InMemoryMatchTransport(bool isHost) => IsHost = isHost;

        public static void CreatePair(out InMemoryMatchTransport host, out InMemoryMatchTransport client)
        {
            host = new InMemoryMatchTransport(isHost: true);
            client = new InMemoryMatchTransport(isHost: false);
            host._peer = client;
            client._peer = host;
        }

        /// <summary>Host side: the client connects (what Netcode's connection callback does).</summary>
        public void ConnectPeer()
        {
            _peerConnected = true;
            PeerConnected?.Invoke();
        }

        public void Send(MatchMessage message)
        {
            Sent.Add(message);
            _inFlight.Enqueue(message);
        }

        public void Open()
        {
            _open = true;
            while (_open && _inbox.Count > 0)
                MessageReceived?.Invoke(_inbox.Dequeue());
        }

        public void Close()
        {
            _open = false;
            _inbox.Clear();
        }

        /// <summary>
        /// Delivers everything in flight both ways, including messages sent
        /// while delivering, until both directions are empty. Returns how
        /// many messages were delivered.
        /// </summary>
        public static int Pump(InMemoryMatchTransport a, InMemoryMatchTransport b, int maxMessages = 1000)
        {
            int delivered = 0;
            while (a._inFlight.Count > 0 || b._inFlight.Count > 0)
            {
                if (delivered >= maxMessages)
                    throw new InvalidOperationException($"More than {maxMessages} messages: the two sides are probably ping-ponging forever.");

                var from = a._inFlight.Count > 0 ? a : b;
                from._peer.Receive(from._inFlight.Dequeue());
                delivered++;
            }
            return delivered;
        }

        void Receive(MatchMessage message)
        {
            if (_open)
                MessageReceived?.Invoke(message);
            else
                _inbox.Enqueue(message);
        }
    }
}
