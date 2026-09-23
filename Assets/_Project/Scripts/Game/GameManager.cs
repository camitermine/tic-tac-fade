using System;
using System.Linq;
using UnityEngine;
using TicTacFade.Core;

namespace TicTacFade.Game
{
    /// <summary>
    /// Orchestrates a match between two <see cref="IPlayerController"/>s.
    /// Doesn't know whether they are local, remote or online: an online match
    /// only differs in which controllers are plugged in (see OnlineMatch).
    /// Doesn't know about any UI type: it exposes state and events, the UI
    /// subscribes (CLAUDE.md, architecture rule 2: UI → Game → Core, never
    /// the other way around).
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [SerializeField] GameConfigAsset config;

        public GameState CurrentState { get; private set; }

        public event Action<GameState> StateChanged;
        public event Action<GameEndedEvent> GameEnded;

        /// <summary>
        /// <see cref="CanAcceptLocalInput"/> may have changed without a state
        /// change (e.g. an online move is waiting for the host).
        /// </summary>
        public event Action LocalInputAvailabilityChanged;

        IPlayerController _playerX;
        IPlayerController _playerO;
        bool _isAdvancingTurns;

        /// <summary>
        /// Whether a board tap on this device can play right now: a match in
        /// progress and the current side's controller taking input.
        /// </summary>
        public bool CanAcceptLocalInput =>
            CurrentState != null && !CurrentState.IsOver && CurrentPlayerController.AcceptsLocalInput;

        /// <summary>Whether a human on this device controls that side.</summary>
        public bool IsLocalHuman(Occupant player) => ControllerFor(player).IsLocalHuman;

        /// <summary>
        /// Wires the two player controllers. Public so tests and the
        /// online/AI setup flows can inject other controllers.
        /// Re-callable: unsubscribes the previous pair before subscribing
        /// the new one.
        /// </summary>
        public void Initialize(IPlayerController playerX, IPlayerController playerO)
        {
            Unsubscribe(_playerX);
            Unsubscribe(_playerO);

            _playerX = playerX;
            _playerO = playerO;
            Subscribe(_playerX);
            Subscribe(_playerO);

            LocalInputAvailabilityChanged?.Invoke();
        }

        void Subscribe(IPlayerController controller)
        {
            controller.MoveChosen += OnMoveChosen;
            controller.AcceptsLocalInputChanged += OnAcceptsLocalInputChanged;
        }

        void Unsubscribe(IPlayerController controller)
        {
            if (controller == null) return;
            controller.MoveChosen -= OnMoveChosen;
            controller.AcceptsLocalInputChanged -= OnAcceptsLocalInputChanged;
        }

        void OnAcceptsLocalInputChanged() => LocalInputAvailabilityChanged?.Invoke();

        void Awake()
        {
            // Default local-vs-local if nobody injected controllers already
            // (the normal in-game case).
            if (_playerX == null || _playerO == null)
                Initialize(new LocalHumanPlayer(Occupant.X), new LocalHumanPlayer(Occupant.O));
        }

        /// <summary>
        /// Starts a match. Called by <see cref="MatchFlow"/>, which decides
        /// who starts; GameManager no longer starts one on its own.
        /// </summary>
        public void StartNewGame(Occupant startingPlayer)
        {
            CurrentState = GameState.CreateInitial(config.ToGameConfig(), startingPlayer);
            StateChanged?.Invoke(CurrentState);
            AdvanceTurns();
        }

        /// <summary>
        /// Drops the match in progress without ending it (no GameEnded, no
        /// StateChanged). Afterwards <see cref="CurrentState"/> is null
        /// again, same as before the first match.
        /// </summary>
        public void DiscardMatch()
        {
            CurrentState = null;
        }

        /// <summary>
        /// Ignored silently when no local input is possible (menu with no
        /// match, a finished match, the opponent's turn online, or an online
        /// move still waiting for the host): a stray tap is not a game error.
        /// </summary>
        public void OnCellClicked(int cellIndex)
        {
            if (!CanAcceptLocalInput)
                return;
            CurrentPlayerController.NotifyCellSelected(cellIndex);
        }

        IPlayerController CurrentPlayerController => ControllerFor(CurrentState.CurrentPlayer);

        IPlayerController ControllerFor(Occupant player) => player == Occupant.X ? _playerX : _playerO;

        void OnMoveChosen(Move move)
        {
            ApplyMove(move);
            AdvanceTurns();
        }

        void ApplyMove(Move move)
        {
            var (newState, events) = RulesEngine.Apply(CurrentState, move);

            var rejected = events.OfType<MoveRejectedEvent>().FirstOrDefault();
            if (rejected != null)
            {
                // Don't re-notify the turn here: AdvanceTurns' loop stops on
                // its own because the state didn't change (see below). That
                // is what prevents an infinite retry of a rejected move.
                Debug.LogWarning($"Tic-Tac-Fade: move rejected ({rejected.Reason}), player={move.Player} cell={move.CellIndex}.");
                return;
            }

            CurrentState = newState;

            foreach (var evt in events)
                if (evt is GameEndedEvent ended)
                    GameEnded?.Invoke(ended);

            StateChanged?.Invoke(CurrentState);
        }

        /// <summary>
        /// Notifies the current player and, while the match continues and
        /// the state actually changes (an autonomous controller that played
        /// synchronously), keeps going to the next one. Re-entrant via a
        /// flag, not via recursion: if <see cref="OnMoveChosen"/> triggers
        /// this from INSIDE a call already in progress (an autonomous
        /// controller replied synchronously to
        /// <see cref="IPlayerController.NotifyTurnStarted"/>), the nested
        /// call doesn't open a new loop — the outer loop, still alive in its
        /// own stack frame, picks up the state change on its next
        /// iteration. That way a whole match between two autonomous
        /// controllers runs in a single stack frame, not one per move.
        /// </summary>
        void AdvanceTurns()
        {
            if (_isAdvancingTurns) return;

            _isAdvancingTurns = true;
            try
            {
                while (!CurrentState.IsOver)
                {
                    var before = CurrentState;
                    CurrentPlayerController.NotifyTurnStarted(CurrentState);
                    if (ReferenceEquals(CurrentState, before))
                        break; // didn't play synchronously (waiting on UI) or the move was rejected — don't retry
                }
            }
            finally
            {
                _isAdvancingTurns = false;
            }
        }
    }
}
