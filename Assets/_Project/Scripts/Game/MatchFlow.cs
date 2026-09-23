using System;
using System.Collections.Generic;
using UnityEngine;
using TicTacFade.Core;

namespace TicTacFade.Game
{
    /// <summary>
    /// Screen-flow state machine (GDD §4.3): Menu → Playing → Result →
    /// (Rematch | Menu). Knows nothing about screens (the UI observes
    /// <see cref="StateChanged"/>) and nothing about rules (it only tells
    /// <see cref="GameManager"/> when to start or discard a match).
    /// Adding states later (lobby, join by code) means adding enum values,
    /// rows to <see cref="AllowedTransitions"/> and one intent method per
    /// button; the existing transitions stay untouched.
    /// </summary>
    public class MatchFlow : MonoBehaviour
    {
        [SerializeField] GameManager gameManager;

        static readonly Dictionary<FlowState, FlowState[]> AllowedTransitions = new Dictionary<FlowState, FlowState[]>
        {
            { FlowState.Menu, new[] { FlowState.Playing } },
            { FlowState.Playing, new[] { FlowState.Result, FlowState.Menu } },
            { FlowState.Result, new[] { FlowState.Playing, FlowState.Menu } },
        };

        public FlowState State { get; private set; } = FlowState.Menu;

        /// <summary>Result of the last finished match; null until one ends.</summary>
        public GameEndedEvent LastResult { get; private set; }

        public Occupant CurrentStartingPlayer { get; private set; } = Occupant.X;

        public event Action<FlowState> StateChanged;

        void Awake()
        {
            gameManager.GameEnded += OnGameEnded;
        }

        void OnDestroy()
        {
            if (gameManager != null)
                gameManager.GameEnded -= OnGameEnded;
        }

        void Start()
        {
            // Fired from Start (not Awake) so every screen has already
            // subscribed in its own Awake and starts in sync.
            StateChanged?.Invoke(State);
        }

        /// <summary>
        /// A match started from the menu is a first match: X starts (GDD §3.1).
        /// </summary>
        public void PlayLocal()
        {
            if (!TryTransitionTo(FlowState.Playing))
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

        bool TryTransitionTo(FlowState next)
        {
            if (!AllowedTransitions.TryGetValue(State, out var targets) || Array.IndexOf(targets, next) < 0)
            {
                Debug.LogWarning($"Tic-Tac-Fade: flow transition {State} → {next} is not allowed.");
                return false;
            }

            State = next;
            StateChanged?.Invoke(State);
            return true;
        }
    }
}
