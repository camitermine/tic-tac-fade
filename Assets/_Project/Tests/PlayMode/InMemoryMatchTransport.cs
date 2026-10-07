using System;
using System.Collections.Generic;
using TicTacFade.Game;

namespace TicTacFade.PlayModeTests
{
    /// <summary>
    /// Network-free <see cref="IMatchTransport"/> pair for tests. Sent
    /// messages are encoded with <see cref="MatchMessageCodec"/> and queued,
    /// in order, and only decoded and delivered on <see cref="Pump"/>, which
    /// models the real transport: delivery is asynchronous but reliable and
    /// sequenced, and the receiver decodes the same bytes the network would
    /// carry. Like the real one, a closed endpoint buffers what it receives
    /// until <see cref="Open"/>.
    /// </summary>
    public sealed class InMemoryMatchTransport : IMatchTransport
    {
        readonly Queue<byte[]> _inFlight = new Queue<byte[]>();
        readonly Queue<MatchMessage> _inbox = new Queue<MatchMessage>();
        InMemoryMatchTransport _peer;
        bool _open;
        bool _dead;
        bool _ignoresIncoming;
        bool _linkUp = true;
        int _sendsToLose;

        public bool IsHost { get; }

        /// <summary>Every message this endpoint sent, for assertions (also the lost ones).</summary>
        public List<MatchMessage> Sent { get; } = new List<MatchMessage>();

        public event Action PeerDisconnected;
        public event Action<MatchMessage> MessageReceived;

        InMemoryMatchTransport(bool isHost) => IsHost = isHost;

        public static void CreatePair(out InMemoryMatchTransport host, out InMemoryMatchTransport client)
        {
            host = new InMemoryMatchTransport(isHost: true);
            client = new InMemoryMatchTransport(isHost: false);
            host._peer = client;
            client._peer = host;
        }

        /// <summary>
        /// This device drops off the network: nothing in flight arrives,
        /// nothing it sends later arrives, and the other endpoint gets
        /// PeerDisconnected (what Netcode reports on a real drop).
        /// </summary>
        public void Drop()
        {
            _dead = true;
            _inFlight.Clear();
            _peer._inFlight.Clear();
            _peer.PeerDisconnected?.Invoke();
        }

        /// <summary>
        /// This device stays connected but stops answering: everything sent
        /// to it is lost, and the network reports nothing (a hung host).
        /// </summary>
        public void StopResponding() => _ignoresIncoming = true;

        /// <summary>The next <paramref name="count"/> messages this endpoint sends never arrive.</summary>
        public void LoseNextSent(int count = 1) => _sendsToLose = count;

        /// <summary>
        /// While down, this endpoint isn't connected yet (a client before
        /// Netcode connected): what it sends is dropped silently, like
        /// <c>NgoMatchTransport</c> does with a Ready.
        /// </summary>
        public void SetLinkUp(bool up) => _linkUp = up;

        /// <summary>Queues a message to the other endpoint as if this one sent it (duplicates, late arrivals).</summary>
        public void InjectToPeer(MatchMessage message) => _inFlight.Enqueue(MatchMessageCodec.Encode(message));

        /// <summary>Queues raw bytes to the other endpoint (malformed frames, other protocol versions).</summary>
        public void DeliverRawToPeer(byte[] frame) => _inFlight.Enqueue(frame);

        public void Send(MatchMessage message)
        {
            Sent.Add(message);
            if (_dead || !_linkUp)
                return;
            if (_sendsToLose > 0)
            {
                _sendsToLose--;
                return;
            }
            _inFlight.Enqueue(MatchMessageCodec.Encode(message));
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
        /// many frames were delivered (dropped malformed ones included).
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

        void Receive(byte[] frame)
        {
            if (_dead || _ignoresIncoming)
                return;
            if (!MatchMessageCodec.TryDecode(frame, frame.Length, out var message))
                return; // dropped with a warning, as on the real transport
            if (_open)
                MessageReceived?.Invoke(message);
            else
                _inbox.Enqueue(message);
        }
    }
}
