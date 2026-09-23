using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using TicTacFade.Core;

namespace TicTacFade.Game
{
    /// <summary>
    /// Screen-flow state machine (GDD §4.3). Knows nothing about screens
    /// (the UI observes its events) and nothing about rules or the network
    /// SDK: it tells <see cref="GameManager"/> when to start or discard a
    /// match, <see cref="ISessionService"/> when to create, join or leave
    /// a room, and runs an <see cref="OnlineMatch"/> while in a room. Adding
    /// states means adding enum values, rows to
    /// <see cref="AllowedTransitions"/> and one intent method per button.
    /// </summary>
    public class MatchFlow : MonoBehaviour
    {
        [SerializeField] GameManager gameManager;
        [SerializeField] SessionServiceBehaviour sessionService;
        [SerializeField] MatchTransportBehaviour matchTransport;

        // Every screen declares explicitly where its cancel/back action goes.
        static readonly Dictionary<FlowState, FlowState[]> AllowedTransitions = new Dictionary<FlowState, FlowState[]>
        {
            { FlowState.Menu, new[] { FlowState.Playing, FlowState.Lobby, FlowState.JoinByCode } },
            { FlowState.Playing, new[] { FlowState.Result, FlowState.Menu } },
            { FlowState.Result, new[] { FlowState.Playing, FlowState.Menu } },
            // Cancel in the waiting room leaves the room and goes to the menu,
            // also for a player who arrived through the join-by-code screen.
            // Playing: the online match starts once both devices are connected.
            { FlowState.Lobby, new[] { FlowState.Menu, FlowState.Playing } },
            // Cancel in the join-by-code screen goes back to the menu.
            { FlowState.JoinByCode, new[] { FlowState.Lobby, FlowState.Menu } },
        };

        ISessionService _session;
        IMatchTransport _transport;
        OnlineMatch _online;

        public FlowState State { get; private set; } = FlowState.Menu;

        /// <summary>Result of the last finished match; null until one ends.</summary>
        public GameEndedEvent LastResult { get; private set; }

        public Occupant CurrentStartingPlayer { get; private set; } = Occupant.X;

        /// <summary>
        /// True while a session operation is in flight. Every intent is
        /// ignored meanwhile, so a double tap can't create two rooms even if
        /// a screen forgot to disable its buttons.
        /// </summary>
        public bool IsBusy { get; private set; }

        /// <summary>Last session error to show; None when there is nothing to show.</summary>
        public SessionFailure LastFailure { get; private set; }

        public bool IsOpponentConnected { get; private set; }

        public string JoinCode => _session?.JoinCode;

        /// <summary>True from entering a room until leaving it (waiting room, match, result).</summary>
        public bool IsOnline => _online != null;

        /// <summary>Online: this device asked for a rematch and waits for the other one.</summary>
        public bool IsWaitingForRematch => _online != null && _online.LocalRematchRequested;

        public event Action<FlowState> StateChanged;
        public event Action<bool> BusyChanged;
        public event Action<SessionFailure> FailureChanged;
        public event Action<bool> OpponentStatusChanged;
        public event Action<bool> RematchWaitingChanged;

        void Awake()
        {
            gameManager.GameEnded += OnGameEnded;
            if (sessionService != null)
                SetSessionService(sessionService);
            if (matchTransport != null)
                SetMatchTransport(matchTransport);
        }

        void OnDestroy()
        {
            if (gameManager != null)
                gameManager.GameEnded -= OnGameEnded;
            SetSessionService(null);
            EndOnlineMatch();
        }

        void Start()
        {
            // Fired from Start (not Awake) so every screen has already
            // subscribed in its own Awake and starts in sync.
            StateChanged?.Invoke(State);
        }

        /// <summary>
        /// Replaces the session service (tests inject a fake without network).
        /// Re-callable: unsubscribes the previous one.
        /// </summary>
        public void SetSessionService(ISessionService service)
        {
            if (_session != null)
            {
                _session.OpponentConnected -= OnOpponentConnected;
                _session.OpponentLeft -= OnOpponentLeft;
                _session.SessionLost -= OnSessionLost;
            }

            _session = service;

            if (_session != null)
            {
                _session.OpponentConnected += OnOpponentConnected;
                _session.OpponentLeft += OnOpponentLeft;
                _session.SessionLost += OnSessionLost;
            }
        }

        /// <summary>
        /// Replaces the match transport (tests inject an in-memory one).
        /// Only between online matches.
        /// </summary>
        public void SetMatchTransport(IMatchTransport transport)
        {
            if (IsOnline)
            {
                Debug.LogError("Tic-Tac-Fade: can't replace the match transport during an online match.");
                return;
            }
            _transport = transport;
        }

        /// <summary>
        /// A match started from the menu is a first match: X starts (GDD §3.1).
        /// </summary>
        public void PlayLocal()
        {
            if (IsBusy || !TryTransitionTo(FlowState.Playing))
                return;
            StartMatch(Occupant.X);
        }

        /// <summary>
        /// Each rematch swaps who starts (GDD §3.1). Online this only asks
        /// for it: the rematch starts when both devices asked.
        /// </summary>
        public void Rematch()
        {
            if (IsOnline)
            {
                if (State == FlowState.Result && !IsBusy)
                    _online.RequestRematch();
                return;
            }

            if (!TryTransitionTo(FlowState.Playing))
                return;
            StartMatch(CurrentStartingPlayer == Occupant.X ? Occupant.O : Occupant.X);
        }

        /// <summary>
        /// Result → menu. Online it closes the session: the other device goes
        /// back to the menu with "El rival salió".
        /// </summary>
        public void BackToMenu()
        {
            if (IsOnline)
            {
                if (State == FlowState.Result && !IsBusy)
                    LeaveOnline(SessionFailure.None);
                return;
            }

            TryTransitionTo(FlowState.Menu);
        }

        /// <summary>
        /// Leaves a match in progress and discards it. Online it also closes
        /// the session, so both devices go back to the menu (counting it as a
        /// loss is iteration 3).
        /// </summary>
        public void ExitMatch()
        {
            if (State != FlowState.Playing || IsBusy)
            {
                Debug.LogWarning($"Tic-Tac-Fade: ExitMatch ignored, flow is in {State}{(IsBusy ? " (busy)" : "")}.");
                return;
            }

            if (IsOnline)
            {
                LeaveOnline(SessionFailure.None);
                return;
            }

            gameManager.DiscardMatch();
            TryTransitionTo(FlowState.Menu);
        }

        /// <summary>Menu → waiting room, once the room exists.</summary>
        public void CreateRoom()
        {
            if (!CanStartSessionIntent(FlowState.Menu))
                return;
            SetFailure(SessionFailure.None);
            RunBusy(CreateRoomAsync);
        }

        /// <summary>Menu → join-by-code screen (no network yet).</summary>
        public void OpenJoinByCode()
        {
            if (!CanStartSessionIntent(FlowState.Menu))
                return;
            TryTransitionTo(FlowState.JoinByCode);
        }

        /// <summary>
        /// Join-by-code screen → waiting room on success. On failure the
        /// flow stays on the same screen with <see cref="LastFailure"/> set.
        /// </summary>
        public void JoinRoom(string rawCode)
        {
            if (!CanStartSessionIntent(FlowState.JoinByCode))
                return;

            var code = NormalizeCode(rawCode);
            if (!_session.IsWellFormedCode(code))
            {
                // Rejected locally: a malformed code never reaches the network.
                SetFailure(SessionFailure.InvalidCode);
                return;
            }

            SetFailure(SessionFailure.None);
            RunBusy(() => JoinRoomAsync(code));
        }

        public void CancelJoinByCode()
        {
            if (!CanStartSessionIntent(FlowState.JoinByCode))
                return;
            TryTransitionTo(FlowState.Menu);
        }

        /// <summary>Waiting room → menu, after the room is left/closed.</summary>
        public void LeaveRoom()
        {
            if (!CanStartSessionIntent(FlowState.Lobby))
                return;
            RunBusy(LeaveRoomAsync);
        }

        public static string NormalizeCode(string rawCode) => (rawCode ?? string.Empty).Trim().ToUpperInvariant();

        async Task CreateRoomAsync()
        {
            var result = await _session.CreateAsync();
            if (this == null) return; // scene unloaded while waiting

            if (!result.Success)
            {
                SetFailure(result.Failure);
                return;
            }

            SetOpponentConnected(false);
            TryTransitionTo(FlowState.Lobby);
            BeginOnlineMatch();
        }

        async Task JoinRoomAsync(string code)
        {
            var result = await _session.JoinByCodeAsync(code);
            if (this == null) return;

            if (!result.Success)
            {
                SetFailure(result.Failure);
                return;
            }

            // The host is already in the room when the join succeeds.
            SetOpponentConnected(true);
            TryTransitionTo(FlowState.Lobby);
            BeginOnlineMatch();
        }

        async Task LeaveRoomAsync()
        {
            EndOnlineMatch();
            await _session.LeaveAsync();
            if (this == null) return;

            SetOpponentConnected(false);
            TryTransitionTo(FlowState.Menu);
        }

        bool CanStartSessionIntent(FlowState requiredState)
        {
            if (IsBusy || State != requiredState)
                return false;

            if (_session == null)
            {
                Debug.LogError("Tic-Tac-Fade: MatchFlow has no session service assigned.");
                return false;
            }

            return true;
        }

        // Last-resort guard: ISessionService must not throw, but if an
        // implementation ever does, the flow logs it and ends in a stable
        // state instead of leaving the screen stuck on "Conectando...".
        async void RunBusy(Func<Task> operation)
        {
            SetBusy(true);
            try
            {
                await operation();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (this != null)
                    SetFailure(SessionFailure.Unknown);
            }
            finally
            {
                if (this != null)
                    SetBusy(false);
            }
        }

        void OnOpponentConnected()
        {
            if (State == FlowState.Lobby)
                SetOpponentConnected(true);
        }

        void OnOpponentLeft()
        {
            if (State == FlowState.Lobby)
                SetOpponentConnected(false);
            else if (IsOnlineMatchScreen && !IsBusy)
                LeaveOnline(SessionFailure.OpponentLeft); // host side: the client left
        }

        void OnSessionLost()
        {
            // While busy we are the ones leaving: that is not a lost session.
            if (IsBusy)
                return;

            if (State == FlowState.Lobby)
            {
                EndOnlineMatch();
                SetOpponentConnected(false);
                TryTransitionTo(FlowState.Menu);
                SetFailure(SessionFailure.SessionClosed);
            }
            else if (IsOnlineMatchScreen)
            {
                LeaveOnline(SessionFailure.OpponentLeft); // client side: the host closed the room
            }
        }

        bool IsOnlineMatchScreen => IsOnline && (State == FlowState.Playing || State == FlowState.Result);

        void BeginOnlineMatch()
        {
            if (_transport == null)
            {
                Debug.LogError("Tic-Tac-Fade: MatchFlow has no match transport assigned; the online match can't start.");
                return;
            }

            _online = new OnlineMatch(gameManager, _transport);
            _online.MatchStartRequested += OnOnlineMatchStartRequested;
            _online.LocalRematchRequestedChanged += OnLocalRematchRequestedChanged;
            _online.Desynced += OnDesynced;
            _online.Start();
        }

        /// <summary>
        /// Stops the online match and puts the local controllers back, so
        /// "Jugar local" works as always afterwards.
        /// </summary>
        void EndOnlineMatch()
        {
            if (_online == null) return;

            _online.MatchStartRequested -= OnOnlineMatchStartRequested;
            _online.LocalRematchRequestedChanged -= OnLocalRematchRequestedChanged;
            _online.Desynced -= OnDesynced;
            _online.Dispose();
            _online = null;

            if (gameManager != null)
            {
                gameManager.DiscardMatch();
                gameManager.Initialize(new LocalHumanPlayer(Occupant.X), new LocalHumanPlayer(Occupant.O));
            }
            RematchWaitingChanged?.Invoke(false);
        }

        // Closes the session and goes back to the menu, showing why.
        void LeaveOnline(SessionFailure reason)
        {
            RunBusy(async () =>
            {
                EndOnlineMatch();
                await _session.LeaveAsync();
                if (this == null) return;

                SetOpponentConnected(false);
                TryTransitionTo(FlowState.Menu);
                if (reason != SessionFailure.None)
                    SetFailure(reason);
            });
        }

        void OnOnlineMatchStartRequested(Occupant startingPlayer)
        {
            if (State != FlowState.Lobby && State != FlowState.Result)
            {
                Debug.LogWarning($"Tic-Tac-Fade: online match start ignored, flow is in {State}.");
                return;
            }

            if (TryTransitionTo(FlowState.Playing))
                StartMatch(startingPlayer);
        }

        void OnLocalRematchRequestedChanged(bool waiting) => RematchWaitingChanged?.Invoke(waiting);

        void OnDesynced()
        {
            if (!IsBusy)
                LeaveOnline(SessionFailure.Desync);
        }

        // The state switches to Playing BEFORE the match starts: with
        // autonomous controllers the match can end synchronously inside
        // StartNewGame, and its GameEnded → Result must come after Playing.
        void StartMatch(Occupant startingPlayer)
        {
            CurrentStartingPlayer = startingPlayer;
            gameManager.StartNewGame(startingPlayer);
        }

        void OnGameEnded(GameEndedEvent evt)
        {
            LastResult = evt;
            TryTransitionTo(FlowState.Result);
        }

        void SetBusy(bool busy)
        {
            if (IsBusy == busy) return;
            IsBusy = busy;
            BusyChanged?.Invoke(busy);
        }

        void SetFailure(SessionFailure failure)
        {
            LastFailure = failure;
            FailureChanged?.Invoke(failure);
        }

        void SetOpponentConnected(bool connected)
        {
            if (IsOpponentConnected == connected) return;
            IsOpponentConnected = connected;
            OpponentStatusChanged?.Invoke(connected);
        }

        // A successful transition clears any error shown on the previous
        // screen; errors caused by the transition itself are set after it.
        bool TryTransitionTo(FlowState next)
        {
            if (!AllowedTransitions.TryGetValue(State, out var targets) || Array.IndexOf(targets, next) < 0)
            {
                Debug.LogWarning($"Tic-Tac-Fade: flow transition {State} → {next} is not allowed.");
                return false;
            }

            State = next;
            if (LastFailure != SessionFailure.None)
                SetFailure(SessionFailure.None);
            StateChanged?.Invoke(State);
            return true;
        }
    }
}
