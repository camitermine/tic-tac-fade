using System;
using System.Linq;
using UnityEngine;
using TicTacFade.Core;

namespace TicTacFade.Game
{
    /// <summary>
    /// Orchestrates a local match (two human players on the same device).
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

        IPlayerController _playerX;
        IPlayerController _playerO;
        bool _isAdvancingTurns;

        /// <summary>
        /// Wires the two player controllers. Public so tests (and, later,
        /// online/AI setup flows) can inject non-human controllers.
        /// Re-callable: unsubscribes the previous pair before subscribing
        /// the new one.
        /// </summary>
        public void Initialize(IPlayerController playerX, IPlayerController playerO)
        {
            if (_playerX != null) _playerX.MoveChosen -= OnMoveChosen;
            if (_playerO != null) _playerO.MoveChosen -= OnMoveChosen;

            _playerX = playerX;
            _playerO = playerO;
            _playerX.MoveChosen += OnMoveChosen;
            _playerO.MoveChosen += OnMoveChosen;
        }

        void Awake()
        {
            // Default local-vs-local if nobody injected controllers already
            // (the normal in-game case).
            if (_playerX == null || _playerO == null)
                Initialize(new LocalHumanPlayer(Occupant.X), new LocalHumanPlayer(Occupant.O));
        }

        void Start()
        {
            // Fired from Start (not Awake) to guarantee that any subscriber
            // (BoardView, GameHud) has already subscribed in its own Awake
            // before receiving the first StateChanged.
            StartNewGame();
        }

        public void StartNewGame()
        {
            CurrentState = GameState.CreateInitial(config.ToGameConfig(), Occupant.X);
            StateChanged?.Invoke(CurrentState);
            AdvanceTurns();
        }

        public void OnCellClicked(int cellIndex) => CurrentPlayerController.NotifyCellSelected(cellIndex);

        IPlayerController CurrentPlayerController => CurrentState.CurrentPlayer == Occupant.X ? _playerX : _playerO;

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
