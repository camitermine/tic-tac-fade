using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using TicTacFade.Core;
using TicTacFade.Game;

namespace TicTacFade.Net
{
    /// <summary>
    /// <see cref="IMatchTransport"/> over Netcode for GameObjects named
    /// messages (CustomMessagingManager). Chosen over RPCs because it needs
    /// no NetworkObject: no spawning, no prefab registration and no in-scene
    /// NetworkObject whose GlobalObjectIdHash the code-built scene would have
    /// to get right (ADR 0002).
    /// Every message goes with <see cref="NetworkDelivery.ReliableSequenced"/>:
    /// a confirmed move that is lost or reordered can't be left for the
    /// position-key check to catch.
    /// </summary>
    public class NgoMatchTransport : MatchTransportBehaviour
    {
        const string MessageName = "TicTacFade.Match";
        const NetworkDelivery Delivery = NetworkDelivery.ReliableSequenced;

        // kind(1) + player(1) + cell(4) + moveNumber(4) + key(8) + reason(1)
        // + durationMs(4) + absentFlags(1) + cause(1)
        const int MessageSize = 25;

        [SerializeField] NetworkManager networkManager;

        readonly Queue<MatchMessage> _inbox = new Queue<MatchMessage>();
        bool _open;
        bool _handlerRegistered;

        public override bool IsHost => networkManager != null && networkManager.IsServer;

        public override bool IsPeerConnected =>
            networkManager != null && networkManager.IsServer && FindClientId(out _);

        public override event Action PeerConnected;
        public override event Action PeerDisconnected;
        public override event Action<MatchMessage> MessageReceived;

        void OnEnable()
        {
            if (networkManager == null) return;
            networkManager.OnServerStarted += OnNetworkStarted;
            networkManager.OnClientStarted += OnNetworkStarted;
            networkManager.OnClientConnectedCallback += OnClientConnected;
            networkManager.OnClientDisconnectCallback += OnClientDisconnected;
            networkManager.OnServerStopped += OnNetworkStopped;
            networkManager.OnClientStopped += OnNetworkStopped;
        }

        void OnDisable()
        {
            if (networkManager == null) return;
            networkManager.OnServerStarted -= OnNetworkStarted;
            networkManager.OnClientStarted -= OnNetworkStarted;
            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            networkManager.OnServerStopped -= OnNetworkStopped;
            networkManager.OnClientStopped -= OnNetworkStopped;
        }

        public override void Send(MatchMessage message)
        {
            if (networkManager == null || !networkManager.IsListening || networkManager.CustomMessagingManager == null)
            {
                Debug.LogWarning($"Tic-Tac-Fade: can't send {message.Kind}: the network is not running.");
                return;
            }

            ulong target;
            if (networkManager.IsServer)
            {
                if (!FindClientId(out target))
                {
                    Debug.LogWarning($"Tic-Tac-Fade: can't send {message.Kind}: no client connected.");
                    return;
                }
            }
            else
            {
                target = NetworkManager.ServerClientId;
            }

            using (var writer = new FastBufferWriter(MessageSize, Allocator.Temp))
            {
                writer.WriteValueSafe((byte)message.Kind);
                writer.WriteValueSafe((byte)message.Player);
                writer.WriteValueSafe(message.CellIndex);
                writer.WriteValueSafe(message.MoveNumber);
                writer.WriteValueSafe(message.PositionKey);
                writer.WriteValueSafe((byte)message.RejectionReason);
                writer.WriteValueSafe(message.DurationMs);
                writer.WriteValueSafe(message.AbsentFlags);
                writer.WriteValueSafe((byte)message.Cause);
                networkManager.CustomMessagingManager.SendNamedMessage(MessageName, target, writer, Delivery);
            }
            NetDiagnostics.Log($"sent {Describe(message)} to client {target}.");
        }

        public override void Open()
        {
            NetDiagnostics.Log($"transport opened ({Role}); delivering {_inbox.Count} buffered message(s).");
            _open = true;
            while (_open && _inbox.Count > 0)
                MessageReceived?.Invoke(_inbox.Dequeue());
        }

        public override void Close()
        {
            NetDiagnostics.Log($"transport closed ({Role}); dropping {_inbox.Count} buffered message(s).");
            _open = false;
            _inbox.Clear();
        }

        // The CustomMessagingManager is created when the network starts, so
        // the handler is registered then (before the client even connects,
        // so nothing the host sends can arrive unhandled).
        void OnNetworkStarted()
        {
            if (_handlerRegistered) return; // a host fires both server and client started
            if (_inbox.Count > 0)
                NetDiagnostics.Log($"network started: dropping {_inbox.Count} stale buffered message(s).");
            _inbox.Clear();
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(MessageName, OnNamedMessage);
            _handlerRegistered = true;

            bool isSingleton = NetworkManager.Singleton == networkManager;
            NetDiagnostics.Log($"network started ({Role}); message handler registered. Transport bound to the NetworkManager singleton: {isSingleton}.");
        }

        void OnNetworkStopped(bool _)
        {
            NetDiagnostics.Log($"network stopped; handler gone, dropping {_inbox.Count} buffered message(s).");
            _handlerRegistered = false; // the CustomMessagingManager goes away with the network
            _inbox.Clear();
        }

        void OnClientConnected(ulong clientId)
        {
            NetDiagnostics.Log($"Netcode connected callback: client {clientId} ({Role}, local id {networkManager.LocalClientId}).");
            if (networkManager.IsServer && clientId != NetworkManager.ServerClientId)
                PeerConnected?.Invoke();
        }

        // Host: the client dropped. Client: this device lost the host (NGO
        // reports its own id). Either way the other device is gone.
        void OnClientDisconnected(ulong clientId)
        {
            bool peerGone = networkManager.IsServer
                ? clientId != NetworkManager.ServerClientId
                : clientId == networkManager.LocalClientId || clientId == NetworkManager.ServerClientId;
            NetDiagnostics.Log($"Netcode disconnect callback: client {clientId} ({Role}); peer gone: {peerGone}.");
            if (peerGone)
                PeerDisconnected?.Invoke();
        }

        string Role => networkManager == null ? "no NetworkManager" : networkManager.IsServer ? "host" : "client";

        static string Describe(MatchMessage m) =>
            $"{m.Kind}(player={m.Player}, cell={m.CellIndex}, move={m.MoveNumber})";

        void OnNamedMessage(ulong senderClientId, FastBufferReader reader)
        {
            reader.ReadValueSafe(out byte kind);
            reader.ReadValueSafe(out byte player);
            reader.ReadValueSafe(out int cellIndex);
            reader.ReadValueSafe(out int moveNumber);
            reader.ReadValueSafe(out ulong positionKey);
            reader.ReadValueSafe(out byte reason);
            reader.ReadValueSafe(out int durationMs);
            reader.ReadValueSafe(out byte absentFlags);
            reader.ReadValueSafe(out byte cause);

            var message = new MatchMessage((MatchMessageKind)kind, (Occupant)player, cellIndex, moveNumber,
                positionKey, (MoveRejectionReason)reason, durationMs, absentFlags, (AbandonmentCause)cause);

            // Buffered until this device's flow opens the channel: a
            // StartMatch can arrive before the join finished on this side.
            NetDiagnostics.Log($"received {Describe(message)} from client {senderClientId}; {(_open ? "delivering" : "buffering (transport not open yet)")}.");
            if (_open)
                MessageReceived?.Invoke(message);
            else
                _inbox.Enqueue(message);
        }

        // 1v1: the host's only peer is the one connected client that isn't itself.
        bool FindClientId(out ulong clientId)
        {
            foreach (var id in networkManager.ConnectedClientsIds)
            {
                if (id != NetworkManager.ServerClientId)
                {
                    clientId = id;
                    return true;
                }
            }
            clientId = 0;
            return false;
        }
    }
}
