using System;
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
        public GameState CurrentState { get; private set; }

        public event Action<GameState> StateChanged;
        public event Action<GameEndedEvent> GameEnded;

        void Start()
        {
            // Fired from Start (not Awake) to guarantee that any subscriber
            // (BoardView, GameHud) has already subscribed in its own Awake
            // before receiving the first StateChanged.
            StartNewGame();
        }

        public void StartNewGame()
        {
            CurrentState = GameState.CreateInitial(GameConfig.Mvp(), Occupant.X);
            StateChanged?.Invoke(CurrentState);
        }

        public void OnCellClicked(int cellIndex)
        {
            var move = new Move(CurrentState.CurrentPlayer, cellIndex);
            var (newState, events) = RulesEngine.Apply(CurrentState, move);
            CurrentState = newState;

            foreach (var evt in events)
            {
                if (evt is GameEndedEvent ended)
                    GameEnded?.Invoke(ended);
            }

            StateChanged?.Invoke(CurrentState);
        }
    }
}
