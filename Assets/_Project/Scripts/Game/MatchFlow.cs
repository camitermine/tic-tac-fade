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
    /// match and <see cref="ISessionService"/> when to create, join or leave
    /// a room. Adding states means adding enum values, rows to
    /// <see cref="AllowedTransitions"/> and one intent method per button.
    /// </summary>
    public class MatchFlow : MonoBehaviour
    {
        [SerializeField] GameManager gameManager;
        [SerializeField] SessionServiceBehaviour sessionService;

        // Every screen declares explicitly where its cancel/back action goes.
        static readonly Dictionary<FlowState, FlowState[]> AllowedTransitions = new Dictionary<FlowState, FlowState[]>
        {
            { FlowState.Menu, new[] { FlowState.Playing, FlowState.Lobby, FlowState.JoinByCode } },
            { FlowState.Playing, new[] { FlowState.Result, FlowState.Menu } },
            { FlowState.Result, new[] { FlowState.Playing, FlowState.Menu } },
            // Cancel in the waiting room leaves the room and goes to the menu,
            // also for a player who arrived through the join-by-code screen.
            { FlowState.Lobby, new[] { FlowState.Menu } },
            // Cancel in the join-by-code screen goes back to the menu.
            { FlowState.JoinByCode, new[] { FlowState.Lobby, FlowState.Menu } },
        };

        ISessionService _session;

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

        public event Action<FlowState> StateChanged;
        public event Action<bool> BusyChanged;
        public event Action<SessionFailure> FailureChanged;
        public event Action<bool> OpponentStatusChanged;

        void Awake()
        {
            gameManager.GameEnded += OnGameEnded;
            if (sessionService != null)
                SetSessionService(sessionService);
        }

        void OnDestroy()
        {
            if (gameManager != null)
                gameManager.GameEnded -= OnGameEnded;
            SetSessionService(null);
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
        /// A match started from the menu is a first match: X starts (GDD §3.1).
        /// </summary>
        public void PlayLocal()
        {
            if (IsBusy || !TryTransitionTo(FlowState.Playing))
                return;
            StartMatch(Occupant.X);
        }

        /// <summary>Each rematch swaps who starts (GDD §3.1).</summary>
        public void Rematch()
        {
            if (!TryTransitionTo(FlowState.Playing))
                return;
            StartMatch(CurrentStartingPlayer == Occupant.X ? Occupant.O : Occupant.X);
        }

        public void BackToMenu()
        {
            TryTransitionTo(FlowState.Menu);
        }

        /// <summary>
        /// Leaves a match in progress and discards it. Locally that is all it
        /// does; online this same transition will be "abandon" (a loss).
        /// </summary>
        public void ExitMatch()
        {
            if (State != FlowState.Playing)
            {
                Debug.LogWarning($"Tic-Tac-Fade: ExitMatch ignored, flow is in {State}.");
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
        }

        async Task LeaveRoomAsync()
        {
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
        }

        void OnSessionLost()
        {
            // While busy we are the ones leaving: that is not a lost session.
            if (State != FlowState.Lobby || IsBusy)
                return;

            SetOpponentConnected(false);
            TryTransitionTo(FlowState.Menu);
            SetFailure(SessionFailure.SessionClosed);
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
