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
    /// <item>After every move both sides compare the Core position key; a
    /// mismatch cuts the match (<see cref="Desynced"/>).</item>
    /// <item>The host owns the turn timer (GDD §3.5): it announces each turn,
    /// plays an automatic move on expiry, tracks absent mode and ends the
    /// match by abandonment on the last allowed timeout. The client only
    /// displays the countdown.</item>
    /// </list>
    /// The host is always X and the client always O; the first match starts
    /// with X and each rematch swaps who starts (GDD §3.1).
    /// Driven by <see cref="Tick"/> once per frame; time and randomness are
    /// injected (<see cref="IClock"/>, <see cref="IRandomSource"/>).
    /// </summary>
    public sealed class OnlineMatch : IDisposable
    {
        readonly GameManager _gameManager;
        readonly IMatchTransport _transport;
        readonly OnlineTimerConfig _timer;
        readonly OnlineNetworkSettings _network;
        readonly IClock _clock;
        readonly IRandomSource _random;
        readonly OnlineLocalPlayer _localPlayer;
        readonly RemotePlayer _remotePlayer;

        // Host only: the key after each confirmed move, to check the client's acks.
        readonly Dictionary<int, ulong> _confirmedKeys = new Dictionary<int, ulong>();

        // Host only: timer bookkeeping per symbol (index 0 = X, 1 = O).
        readonly bool[] _absent = new bool[2];
        readonly int[] _consecutiveTimeouts = new int[2];
        double _turnDeadline = double.NaN;

        // Both sides: what the HUD shows (on the client, as announced by the host).
        double _displayDeadline = double.NaN;
        byte _displayAbsentFlags;
        int _lastSecondsShown = -1;

        bool _started;
        bool _firstMatchSent;
        bool _desynced;
        bool _remoteWantsRematch;
        bool _matchInProgress;
        bool _peerGone;
        bool _opponentQuit;
        Occupant _lastStartingPlayer = Occupant.None;

        double _proposalSentAt = double.NaN;        // client
        double _disconnectGraceEndsAt = double.NaN; // host
        double _forfeitAckDeadline = double.NaN;    // whoever pressed "Salir"

        public bool IsHost => _transport.IsHost;

        /// <summary>The symbol this device plays: host X, client O.</summary>
        public Occupant LocalSymbol => IsHost ? Occupant.X : Occupant.O;

        Occupant RemoteSymbol => LocalSymbol == Occupant.X ? Occupant.O : Occupant.X;

        /// <summary>This device asked for a rematch and is waiting for the other one.</summary>
        public bool LocalRematchRequested { get; private set; }

        /// <summary>A rematch is possible: the board ended the match and the other device is still here.</summary>
        public bool CanRematch
        {
            get
            {
                var state = _gameManager.CurrentState;
                return !_peerGone && !_opponentQuit && !_desynced
                       && state != null && state.IsOver && !_gameManager.IsHalted;
            }
        }

        /// <summary>A turn countdown is running (online, match in progress).</summary>
        public bool IsTurnTimerRunning => !double.IsNaN(_displayDeadline);

        /// <summary>Whole seconds left in the current turn, rounded up (0 when no timer).</summary>
        public int SecondsRemaining =>
            IsTurnTimerRunning ? Math.Max(0, (int)Math.Ceiling(_displayDeadline - _clock.Now)) : 0;

        public bool IsAbsent(Occupant player) => (_displayAbsentFlags & AbsentFlags.For(player)) != 0;

        /// <summary>
        /// A match must start now with this player first. The flow switches
        /// to Playing and calls GameManager.StartNewGame.
        /// </summary>
        public event Action<Occupant> MatchStartRequested;

        public event Action<bool> LocalRematchRequestedChanged;

        /// <summary>Position keys differed; the match must be cut.</summary>
        public event Action Desynced;

        /// <summary>A player abandoned (timeouts, "Salir" or a dropped device): the match is over.</summary>
        public event Action<MatchResult> Abandoned;

        /// <summary>This device's "Salir" is done (ack received or waited long enough): close the session.</summary>
        public event Action ForfeitCompleted;

        /// <summary>Client: the host is gone during the match (dropped or not answering).</summary>
        public event Action ConnectionLost;

        /// <summary>A turn countdown started or stopped, or absent mode changed.</summary>
        public event Action TurnTimerChanged;

        /// <summary>The whole seconds shown in the countdown changed.</summary>
        public event Action<int> SecondsRemainingChanged;

        public OnlineMatch(GameManager gameManager, IMatchTransport transport, OnlineTimerConfig timer,
            OnlineNetworkSettings network, IClock clock, IRandomSource random)
        {
            _gameManager = gameManager;
            _transport = transport;
            _timer = timer;
            _network = network;
            _clock = clock;
            _random = random;
            _localPlayer = new OnlineLocalPlayer(LocalSymbol, ProposeLocalMove);
            _remotePlayer = new RemotePlayer(RemoteSymbol);

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
            _transport.PeerDisconnected += NotifyPeerGone;
            NetDiagnostics.Log($"online match listening as {(IsHost ? "host (X)" : "client (O)")}; peer already connected: {_transport.IsPeerConnected}.");
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
            _transport.PeerDisconnected -= NotifyPeerGone;
            _transport.Close();
        }

        /// <summary>Advances every timer. Called once per frame.</summary>
        public void Tick()
        {
            double now = _clock.Now;

            if (IsHost && _matchInProgress && !double.IsNaN(_turnDeadline) && now >= _turnDeadline)
                OnTurnExpired();

            if (IsHost && _matchInProgress && !double.IsNaN(_disconnectGraceEndsAt) && now >= _disconnectGraceEndsAt)
            {
                _disconnectGraceEndsAt = double.NaN;
                Debug.LogWarning("Tic-Tac-Fade: the client didn't come back; it loses by abandonment.");
                AbandonOnHost(RemoteSymbol, AbandonmentCause.Disconnected);
            }

            if (!IsHost && !double.IsNaN(_proposalSentAt) && now - _proposalSentAt >= _network.ProposalResponseTimeoutSeconds)
            {
                _proposalSentAt = double.NaN;
                Debug.LogWarning($"Tic-Tac-Fade: no answer from the host in {_network.ProposalResponseTimeoutSeconds}s; treating it as gone.");
                _localPlayer.NotifyRejected(); // unblock input
                LoseConnection();
            }

            if (!double.IsNaN(_forfeitAckDeadline) && now >= _forfeitAckDeadline)
            {
                Debug.LogWarning("Tic-Tac-Fade: no ForfeitAck in time; closing the session anyway.");
                CompleteForfeit();
            }

            int seconds = SecondsRemaining;
            if (seconds != _lastSecondsShown)
            {
                _lastSecondsShown = seconds;
                SecondsRemainingChanged?.Invoke(seconds);
            }
        }

        /// <summary>
        /// This device wants a rematch (only after the board ended a match).
        /// The rematch starts when both asked; the host announces it.
        /// </summary>
        public void RequestRematch()
        {
            if (!CanRematch || LocalRematchRequested)
                return;

            SetLocalRematchRequested(true);

            if (IsHost)
                TryStartRematch();
            else
                SendToPeer(MatchMessage.RematchRequest());
        }

        /// <summary>
        /// "Salir" during a match: this device loses by abandonment. Tells the
        /// other device and waits for its ack (or the timeout) before
        /// <see cref="ForfeitCompleted"/>, so the message isn't lost when the
        /// session closes.
        /// </summary>
        public void Forfeit()
        {
            if (!double.IsNaN(_forfeitAckDeadline)) return;

            EndMatchLocally();
            if (_peerGone)
            {
                CompleteForfeit();
                return;
            }

            SendToPeer(MatchMessage.Forfeit());
            _forfeitAckDeadline = _clock.Now + _network.ForfeitAckTimeoutSeconds;
        }

        /// <summary>
        /// The other device left (network disconnect, or the session says it
        /// left). Host: a match in progress continues for a short grace, then
        /// the client loses by abandonment. Client: the host is gone.
        /// </summary>
        public void NotifyPeerGone()
        {
            if (_peerGone) return;
            _peerGone = true;

            if (!_matchInProgress)
                return; // after a result: nothing to decide here (no rematch possible now)

            if (IsHost)
                _disconnectGraceEndsAt = _clock.Now + _network.DisconnectGraceSeconds;
            else
                LoseConnection();
        }

        void OnPeerConnected()
        {
            NetDiagnostics.Log($"peer connected (host: {IsHost}, first match already sent: {_firstMatchSent}).");
            if (!IsHost || _firstMatchSent) return;
            _firstMatchSent = true;
            BeginMatch(Occupant.X); // first online match: X (the host) starts
        }

        void TryStartRematch()
        {
            if (!LocalRematchRequested || !_remoteWantsRematch) return;
            BeginMatch(_lastStartingPlayer == Occupant.X ? Occupant.O : Occupant.X);
        }

        // Host only: announce, start locally, start the first turn's timer.
        // Reliable sequenced delivery guarantees the client gets StartMatch
        // before the TurnTimer and any move.
        void BeginMatch(Occupant startingPlayer)
        {
            NetDiagnostics.Log($"host starting a match, {startingPlayer} first; sending StartMatch.");
            SendToPeer(MatchMessage.StartMatch(startingPlayer));
            StartLocalMatch(startingPlayer);
            StartTurnTimer();
        }

        void StartLocalMatch(Occupant startingPlayer)
        {
            _lastStartingPlayer = startingPlayer;
            _remoteWantsRematch = false;
            _confirmedKeys.Clear();
            _localPlayer.Reset();
            _proposalSentAt = double.NaN;
            for (int i = 0; i < 2; i++)
            {
                _absent[i] = false;
                _consecutiveTimeouts[i] = 0;
            }
            _matchInProgress = true;
            SetLocalRematchRequested(false);
            MatchStartRequested?.Invoke(startingPlayer);
        }

        void ProposeLocalMove(Move move)
        {
            if (IsHost)
            {
                HandleProposal(move, fromRemote: false);
                return;
            }

            _proposalSentAt = _clock.Now;
            SendToPeer(MatchMessage.Propose(move));
        }

        void OnMessageReceived(MatchMessage message)
        {
            if (_desynced) return;

            switch (message.Kind)
            {
                case MatchMessageKind.StartMatch when !IsHost:
                    NetDiagnostics.Log($"client processing StartMatch, {message.Player} first.");
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
                    _proposalSentAt = double.NaN;
                    _localPlayer.NotifyRejected();
                    break;
                case MatchMessageKind.Ack when IsHost:
                    HandleAckOnHost(message);
                    break;
                case MatchMessageKind.RematchRequest when IsHost:
                    _remoteWantsRematch = true;
                    TryStartRematch();
                    break;
                case MatchMessageKind.TurnTimer when !IsHost:
                    SetDisplayTimer(_clock.Now + message.DurationMs / 1000.0, message.AbsentFlags);
                    break;
                case MatchMessageKind.Abandoned when !IsHost:
                    EndMatchLocally();
                    Abandoned?.Invoke(MatchResult.FromAbandonment(message.Player, message.Cause));
                    break;
                case MatchMessageKind.Forfeit:
                    OnOpponentForfeited();
                    break;
                case MatchMessageKind.ForfeitAck:
                    if (!double.IsNaN(_forfeitAckDeadline))
                        CompleteForfeit();
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
            var expectedPlayer = fromRemote ? RemoteSymbol : LocalSymbol;

            MoveRejectionReason reason;
            bool legal;
            if (state == null || !_matchInProgress || _gameManager.IsHalted)
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
                    SendToPeer(MatchMessage.Rejected(move, reason));
                else
                    _localPlayer.NotifyRejected();
                return;
            }

            // A move of its own: the player is back (GDD §3.5).
            int index = IndexOf(move.Player);
            _consecutiveTimeouts[index] = 0;
            _absent[index] = false;

            ApplyAndBroadcast(move);
        }

        // Host only: plays a validated move on both devices and starts the next turn.
        void ApplyAndBroadcast(Move move)
        {
            int movesBefore = _gameManager.CurrentState.TotalMoves;
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
            SendToPeer(MatchMessage.Confirmed(move, after.TotalMoves, key));

            if (after.IsOver)
                EndMatchLocally();
            else
                StartTurnTimer();
        }

        // Host only.
        void StartTurnTimer()
        {
            var state = _gameManager.CurrentState;
            if (state == null || state.IsOver) return;

            var player = state.CurrentPlayer;
            float duration = _absent[IndexOf(player)] ? _timer.AbsentTurnTimeSeconds : _timer.TurnTimeSeconds;
            _turnDeadline = _clock.Now + duration;

            byte flags = (byte)((_absent[0] ? AbsentFlags.X : 0) | (_absent[1] ? AbsentFlags.O : 0));
            SendToPeer(MatchMessage.TurnTimer(player, state.TotalMoves, (int)(duration * 1000), flags));
            SetDisplayTimer(_turnDeadline, flags);
        }

        // Host only: GDD §3.5. The last allowed timeout ends the match by
        // abandonment without an automatic move; earlier ones play one.
        void OnTurnExpired()
        {
            _turnDeadline = double.NaN;
            var state = _gameManager.CurrentState;
            var player = state.CurrentPlayer;
            int index = IndexOf(player);

            _consecutiveTimeouts[index]++;
            if (_consecutiveTimeouts[index] >= _timer.MaxConsecutiveTimeouts)
            {
                AbandonOnHost(player, AbandonmentCause.Timeouts);
                return;
            }

            _absent[index] = true;
            ApplyAndBroadcast(ChooseAutomaticMove(state, player, _random));
        }

        /// <summary>
        /// The automatic move on a timeout (GDD §3.5): random among the legal
        /// moves that do NOT win the match, so an absent player can't win
        /// without playing. If every legal move wins, any of them.
        /// </summary>
        public static Move ChooseAutomaticMove(GameState state, Occupant player, IRandomSource random)
        {
            var legal = new List<Move>();
            var nonWinning = new List<Move>();

            for (int cell = 0; cell < state.Config.CellCount; cell++)
            {
                var move = new Move(player, cell);
                if (!RulesEngine.IsLegal(state, move, out _))
                    continue;

                legal.Add(move);
                var after = RulesEngine.Apply(state, move).State;
                bool wins = after.IsOver && after.Winner == player;
                if (!wins)
                    nonWinning.Add(move);
            }

            var pool = nonWinning.Count > 0 ? nonWinning : legal;
            return pool[random.Next(pool.Count)];
        }

        // Host only.
        void AbandonOnHost(Occupant abandoner, AbandonmentCause cause)
        {
            EndMatchLocally();
            SendToPeer(MatchMessage.Abandoned(abandoner, cause));
            Abandoned?.Invoke(MatchResult.FromAbandonment(abandoner, cause));
        }

        void OnOpponentForfeited()
        {
            // Ack first: the other side is waiting for it to close its session.
            SendToPeer(MatchMessage.ForfeitAck());
            _opponentQuit = true;
            if (!_matchInProgress)
                return; // after a result: it just leaves (the flow handles the session)

            EndMatchLocally();
            Abandoned?.Invoke(MatchResult.FromAbandonment(RemoteSymbol, AbandonmentCause.Quit));
        }

        void CompleteForfeit()
        {
            _forfeitAckDeadline = double.NaN;
            ForfeitCompleted?.Invoke();
        }

        void LoseConnection()
        {
            if (!_matchInProgress) return;
            EndMatchLocally();
            ConnectionLost?.Invoke();
        }

        // Stops everything that belongs to a running match on this device.
        void EndMatchLocally()
        {
            _matchInProgress = false;
            _turnDeadline = double.NaN;
            _disconnectGraceEndsAt = double.NaN;
            _proposalSentAt = double.NaN;
            if (_gameManager.CurrentState != null && !_gameManager.CurrentState.IsOver)
                _gameManager.Halt();
            SetDisplayTimer(double.NaN, 0);
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

            if (message.Player == LocalSymbol)
                _proposalSentAt = double.NaN;

            ReceiverFor(message.Player).ApplyConfirmed(message.Move);

            var after = _gameManager.CurrentState;
            ulong localKey = after == null ? 0 : KeyOf(after);
            if (after == null || after.TotalMoves != message.MoveNumber || localKey != message.PositionKey)
            {
                OnDesync($"after move #{message.MoveNumber} the client key is {localKey}, the host key is {message.PositionKey}", notifyPeer: true);
                return;
            }

            SendToPeer(MatchMessage.Ack(message.MoveNumber, localKey));

            if (after.IsOver)
                EndMatchLocally();
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
                SendToPeer(MatchMessage.Desync());
            EndMatchLocally();
            Desynced?.Invoke();
        }

        // Messages to a device the network already reported gone are dropped.
        void SendToPeer(MatchMessage message)
        {
            if (!_peerGone)
                _transport.Send(message);
        }

        void SetDisplayTimer(double deadline, byte absentFlags)
        {
            _displayDeadline = deadline;
            _displayAbsentFlags = absentFlags;
            TurnTimerChanged?.Invoke();
        }

        IConfirmedMoveReceiver ReceiverFor(Occupant player) =>
            player == LocalSymbol ? (IConfirmedMoveReceiver)_localPlayer : _remotePlayer;

        static int IndexOf(Occupant player) => player == Occupant.X ? 0 : 1;

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
