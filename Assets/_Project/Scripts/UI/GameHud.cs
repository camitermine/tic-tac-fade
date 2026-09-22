using UnityEngine;
using UnityEngine.UI;
using TicTacFade.Core;
using TicTacFade.Game;

namespace TicTacFade.UI
{
    /// <summary>
    /// Turn indicator, active-piece counters and end-of-match banner.
    /// Subscribes to <see cref="GameManager.StateChanged"/> and
    /// <see cref="GameManager.GameEnded"/> instead of receiving imperative
    /// calls: Core/Game communicate through events, the UI observes.
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        [SerializeField] GameManager gameManager;
        [SerializeField] Text turnLabel;
        [SerializeField] Text countLabelX;
        [SerializeField] Text countLabelO;
        [SerializeField] GameObject gameEndedBanner;
        [SerializeField] Text gameEndedLabel;

        void Awake()
        {
            gameManager.StateChanged += OnStateChanged;
            gameManager.GameEnded += OnGameEnded;
        }

        void OnDestroy()
        {
            if (gameManager == null) return;
            gameManager.StateChanged -= OnStateChanged;
            gameManager.GameEnded -= OnGameEnded;
        }

        void OnStateChanged(GameState state)
        {
            SetTurn(state.CurrentPlayer);
            UpdateActiveCounts(state.GetActiveCount(Occupant.X), state.GetActiveCount(Occupant.O), state.Config.BufferSize);

            if (!state.IsOver)
                HideGameEndedBanner();
        }

        void OnGameEnded(GameEndedEvent evt) => ShowGameEndedBanner(evt);

        void SetTurn(Occupant player)
        {
            turnLabel.text = $"Turno: Jugador {(player == Occupant.X ? "X" : "O")}";
        }

        void UpdateActiveCounts(int countX, int countO, int bufferSize)
        {
            countLabelX.text = $"X: {countX}/{bufferSize}" + (countX == bufferSize ? "  ¡Desvaneciendo!" : "");
            countLabelO.text = $"O: {countO}/{bufferSize}" + (countO == bufferSize ? "  ¡Desvaneciendo!" : "");
        }

        void ShowGameEndedBanner(GameEndedEvent evt)
        {
            gameEndedBanner.SetActive(true);
            gameEndedLabel.text = ResolveMessage(evt);
        }

        void HideGameEndedBanner()
        {
            gameEndedBanner.SetActive(false);
        }

        static string ResolveMessage(GameEndedEvent evt)
        {
            switch (evt.Reason)
            {
                case GameEndReason.Win:
                    return $"¡Jugador {(evt.Winner == Occupant.X ? "X" : "O")} gana!";
                case GameEndReason.DrawByRepetition:
                    return "Empate por repetición de posición";
                case GameEndReason.DrawByMoveLimit:
                    return "Empate por tope de jugadas";
                default:
                    return "Fin de la partida";
            }
        }
    }
}
