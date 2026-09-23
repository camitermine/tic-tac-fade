using System;
using System.Collections.Generic;
using UnityEngine;
using TicTacFade.Core;

namespace TicTacFade.Game
{
    /// <summary>
    /// Runs an online match between two devices over an
    /// <see cref="IMatchTransport"/> (ADR 0002). Host-authoritative:
    /// <list type="bullet">
    /// <item>Every proposal, from a tap on the host or a message from the
    /// client, goes through the same validation on the host
    /// (<see cref="RulesEngine.IsLegal"/> plus "you can only move your own
    /// symbol"). Illegal ones are rejected and change nothing.</item>
    /// <item>On both devices GameManager only receives host-confirmed moves,
    /// through the side's controller (<see cref="OnlineLocalPlayer"/> or
    /// <see cref="RemotePlayer"/>), so both apply the same sequence.
    /// GameManager doesn't know there is a network.</item>
    /// <item>After every move both sides compare the Core position key.
    /// A mismatch is logged as an error and cuts the match
    /// (<see cref="Desynced"/>): never keep playing out of sync.</item>
    /// </list>
    /// The host is always X and the client always O; the first match starts
    /// with X and each rematch swaps who starts (GDD §3.1).
    /// </summary>
    public sealed class OnlineMatch : IDisposable
    {
        readonly GameManager _gameManager;
        readonly IMatchTransport _transport;
        readonly OnlineLocalPlayer _localPlayer;
        readonly RemotePlayer _remotePlayer;

        // Host only: the key after each confirmed move of the current match,
        // to check the client's acks.
        readonly Dictionary<int, ulong> _confirmedKeys = new Dictionary<int, ulong>();

        bool _started;
        bool _firstMatchSent;
        bool _desynced;
        bool _remoteWantsRematch;
        Occupant _lastStartingPlayer = Occupant.None;

        public bool IsHost => _transport.IsHost;

        /// <summary>The symbol this device plays: host X, client O.</summary>
        public Occupant LocalSymbol => IsHost ? Occupant.X : Occupant.O;

        /// <summary>This device asked for a rematch and is waiting for the other one.</summary>
        public bool LocalRematchRequested { get; private set; }

        /// <summary>
        /// A match must start now with this player first. The flow switches
        /// to Playing and calls GameManager.StartNewGame.
        /// </summary>
        public event Action<Occupant> MatchStartRequested;

        public event Action<bool> LocalRematchRequestedChanged;

        /// <summary>Position keys differed; the match must be cut.</summary>
        public event Action Desynced;

        public OnlineMatch(GameManager gameManager, IMatchTransport transport)
        {
            _gameManager = gameManager;
            _transport = transport;
            _localPlayer = new OnlineLocalPlayer(LocalSymbol, ProposeLocalMove);
            _remotePlayer = new RemotePlayer(LocalSymbol == Occupant.X ? Occupant.O : Occupant.X);

            if (LocalSymbol == Occupant.X)
                _gameManager.Initialize(_localPlayer, _remotePlayer);
            else
                _gameManager.Initialize(_remotePlayer, _localPlayer);
        }

        /// <summary>
        /// Starts listening (buffered messages are processed now). On the
        /// host, the first match starts as soon as the client is connected.
        /// </summary>
        public void Start()
        {
            if (_started) return;
            _started = true;

            _transport.MessageReceived += OnMessageReceived;
            _transport.PeerConnected += OnPeerConnected;
            _transport.Open();

            if (IsHost && _transport.IsPeerConnected)
                OnPeerConnected();
        }

        public void Dispose()
        {
            if (!_started) return;
            _started = false;
            _transport.MessageReceived -= OnMessageReceived;
            _transport.PeerConnected -= OnPeerConnected;
            _transport.Close();
        }

        /// <summary>
        /// This device wants a rematch (only after a finished match). The
        /// rematch starts when both asked; the host decides and announces it.
        /// </summary>
        public void RequestRematch()
        {
            var state = _gameManager.CurrentState;
            if (_desynced || LocalRematchRequested || state == null || !state.IsOver)
                return;

            SetLocalRematchRequested(true);

            if (IsHost)
                TryStartRematch();
            else
                _transport.Send(MatchMessage.RematchRequest());
        }

        void OnPeerConnected()
        {
            if (!IsHost || _firstMatchSent) return;
            _firstMatchSent = true;
            BeginMatch(Occupant.X); // first online match: X (the host) starts
        }

        void TryStartRematch()
        {
            if (!LocalRematchRequested || !_remoteWantsRematch) return;
            BeginMatch(_lastStartingPlayer == Occupant.X ? Occupant.O : Occupant.X);
        }

        // Host only: announce, then start locally. Reliable sequenced
        // delivery guarantees the client gets StartMatch before any move.
        void BeginMatch(Occupant startingPlayer)
        {
            _transport.Send(MatchMessage.StartMatch(startingPlayer));
            StartLocalMatch(startingPlayer);
        }

        void StartLocalMatch(Occupant startingPlayer)
        {
            _lastStartingPlayer = startingPlayer;
            _remoteWantsRematch = false;
            _confirmedKeys.Clear();
            _localPlayer.Reset();
            SetLocalRematchRequested(false);
            MatchStartRequested?.Invoke(startingPlayer);
        }

        void ProposeLocalMove(Move move)
        {
            if (IsHost)
                HandleProposal(move, fromRemote: false);
            else
                _transport.Send(MatchMessage.Propose(move));
        }

        void OnMessageReceived(MatchMessage message)
        {
            if (_desynced) return;

            switch (message.Kind)
            {
                case MatchMessageKind.StartMatch when !IsHost:
                    StartLocalMatch(message.Player);
                    break;
                case MatchMessageKind.Propose when IsHost:
                    HandleProposal(message.Move, fromRemote: true);
                    break;
                case MatchMessageKind.Confirmed when !IsHost:
                    HandleConfirmedOnClient(message);
                    break;
                case MatchMessageKind.Rejected when !IsHost:
                    Debug.LogWarning($"Tic-Tac-Fade: host rejected move {message.Player}@{message.CellIndex} ({message.RejectionReason}).");
                    _localPlayer.NotifyRejected();
                    break;
                case MatchMessageKind.Ack when IsHost:
                    HandleAckOnHost(message);
                    break;
                case MatchMessageKind.RematchRequest when IsHost:
                    _remoteWantsRematch = true;
                    TryStartRematch();
                    break;
                case MatchMessageKind.Desync:
                    OnDesync("the other device reported a desync", notifyPeer: false);
                    break;
                default:
                    Debug.LogWarning($"Tic-Tac-Fade: unexpected {message.Kind} message on the {(IsHost ? "host" : "client")}; ignored.");
                    break;
            }
        }

        // Host only: the single validation point for both players' moves.
        void HandleProposal(Move move, bool fromRemote)
        {
            var state = _gameManager.CurrentState;
            var expectedPlayer = fromRemote ? _remotePlayer.Player : _localPlayer.Player;

            MoveRejectionReason reason;
            bool legal;
            if (state == null)
            {
                legal = false;
                reason = MoveRejectionReason.GameAlreadyEnded;
            }
            else if (move.Player != expectedPlayer)
            {
                legal = false; // a device can only move its own symbol
                reason = MoveRejectionReason.NotPlayersTurn;
            }
            else
            {
                legal = RulesEngine.IsLegal(state, move, out reason);
            }

            if (!legal)
            {
                Debug.LogWarning($"Tic-Tac-Fade: host rejected {(fromRemote ? "client" : "host")} move {move.Player}@{move.CellIndex} ({reason}).");
                if (fromRemote)
                    _transport.Send(MatchMessage.Rejected(move, reason));
                else
                    _localPlayer.NotifyRejected();
                return;
            }

            int movesBefore = state.TotalMoves;
            ReceiverFor(move.Player).ApplyConfirmed(move);

            var after = _gameManager.CurrentState;
            if (after == null || after.TotalMoves != movesBefore + 1)
            {
                // Validated with the same rules GameManager uses: this can
                // only mean a bug. Treat it like any other desync.
                OnDesync($"host failed to apply its own validated move {move.Player}@{move.CellIndex}", notifyPeer: true);
                return;
            }

            ulong key = KeyOf(after);
            _confirmedKeys[after.TotalMoves] = key;
            _transport.Send(MatchMessage.Confirmed(move, after.TotalMoves, key));
        }

        void HandleConfirmedOnClient(MatchMessage message)
        {
            var state = _gameManager.CurrentState;
            int expectedNumber = state == null ? -1 : state.TotalMoves + 1;
            if (message.MoveNumber != expectedNumber)
            {
                OnDesync($"confirmed move #{message.MoveNumber} arrived, expected #{expectedNumber}", notifyPeer: true);
                return;
            }

            ReceiverFor(message.Player).ApplyConfirmed(message.Move);

            var after = _gameManager.CurrentState;
            ulong localKey = after == null ? 0 : KeyOf(after);
            if (after == null || after.TotalMoves != message.MoveNumber || localKey != message.PositionKey)
            {
                OnDesync($"after move #{message.MoveNumber} the client key is {localKey}, the host key is {message.PositionKey}", notifyPeer: true);
                return;
            }

            _transport.Send(MatchMessage.Ack(message.MoveNumber, localKey));
        }

        void HandleAckOnHost(MatchMessage message)
        {
            if (!_confirmedKeys.TryGetValue(message.MoveNumber, out var hostKey))
            {
                // An ack for a move of a previous match (a rematch already
                // started): nothing left to compare it with.
                return;
            }

            _confirmedKeys.Remove(message.MoveNumber);
            if (hostKey != message.PositionKey)
                OnDesync($"after move #{message.MoveNumber} the host key is {hostKey}, the client key is {message.PositionKey}", notifyPeer: true);
        }

        void OnDesync(string detail, bool notifyPeer)
        {
            if (_desynced) return;
            _desynced = true;

            Debug.LogError($"Tic-Tac-Fade: online match out of sync ({detail}). Cutting the match.");
            if (notifyPeer)
                _transport.Send(MatchMessage.Desync());
            Desynced?.Invoke();
        }

        IConfirmedMoveReceiver ReceiverFor(Occupant player) =>
            player == _localPlayer.Player ? (IConfirmedMoveReceiver)_localPlayer : _remotePlayer;

        void SetLocalRematchRequested(bool requested)
        {
            if (LocalRematchRequested == requested) return;
            LocalRematchRequested = requested;
            LocalRematchRequestedChanged?.Invoke(requested);
        }

        static ulong KeyOf(GameState state) =>
            PositionKey.Compute(state.Config, state.QueueX, state.QueueO, state.CurrentPlayer).Value;
    }
}
