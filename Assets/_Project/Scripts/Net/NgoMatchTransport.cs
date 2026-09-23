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
        const int MessageSize = 19;

        [SerializeField] NetworkManager networkManager;

        readonly Queue<MatchMessage> _inbox = new Queue<MatchMessage>();
        bool _open;
        bool _handlerRegistered;

        public override bool IsHost => networkManager != null && networkManager.IsServer;

        public override bool IsPeerConnected =>
            networkManager != null && networkManager.IsServer && FindClientId(out _);

        public override event Action PeerConnected;
        public override event Action<MatchMessage> MessageReceived;

        void OnEnable()
        {
            if (networkManager == null) return;
            networkManager.OnServerStarted += OnNetworkStarted;
            networkManager.OnClientStarted += OnNetworkStarted;
            networkManager.OnClientConnectedCallback += OnClientConnected;
            networkManager.OnServerStopped += OnNetworkStopped;
            networkManager.OnClientStopped += OnNetworkStopped;
        }

        void OnDisable()
        {
            if (networkManager == null) return;
            networkManager.OnServerStarted -= OnNetworkStarted;
            networkManager.OnClientStarted -= OnNetworkStarted;
            networkManager.OnClientConnectedCallback -= OnClientConnected;
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
                networkManager.CustomMessagingManager.SendNamedMessage(MessageName, target, writer, Delivery);
            }
        }

        public override void Open()
        {
            _open = true;
            while (_open && _inbox.Count > 0)
                MessageReceived?.Invoke(_inbox.Dequeue());
        }

        public override void Close()
        {
            _open = false;
            _inbox.Clear();
        }

        // The CustomMessagingManager is created when the network starts, so
        // the handler is registered then (before the client even connects,
        // so nothing the host sends can arrive unhandled).
        void OnNetworkStarted()
        {
            if (_handlerRegistered) return; // a host fires both server and client started
            _inbox.Clear();
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(MessageName, OnNamedMessage);
            _handlerRegistered = true;
        }

        void OnNetworkStopped(bool _)
        {
            _handlerRegistered = false; // the CustomMessagingManager goes away with the network
            _inbox.Clear();
        }

        void OnClientConnected(ulong clientId)
        {
            if (networkManager.IsServer && clientId != NetworkManager.ServerClientId)
                PeerConnected?.Invoke();
        }

        void OnNamedMessage(ulong senderClientId, FastBufferReader reader)
        {
            reader.ReadValueSafe(out byte kind);
            reader.ReadValueSafe(out byte player);
            reader.ReadValueSafe(out int cellIndex);
            reader.ReadValueSafe(out int moveNumber);
            reader.ReadValueSafe(out ulong positionKey);
            reader.ReadValueSafe(out byte reason);

            var message = new MatchMessage((MatchMessageKind)kind, (Occupant)player, cellIndex, moveNumber,
                positionKey, (MoveRejectionReason)reason);

            // Buffered until this device's flow opens the channel: a
            // StartMatch can arrive before the join finished on this side.
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
