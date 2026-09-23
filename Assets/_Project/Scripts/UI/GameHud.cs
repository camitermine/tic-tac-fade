using UnityEngine;
using UnityEngine.UI;
using TicTacFade.Core;
using TicTacFade.Game;

namespace TicTacFade.UI
{
    /// <summary>
    /// Turn indicator and active-piece counters. Subscribes to
    /// <see cref="GameManager.StateChanged"/> instead of receiving imperative
    /// calls: Core/Game communicate through events, the UI observes. The
    /// end-of-match message lives in <see cref="ResultScreen"/>.
    /// GameManager never raises StateChanged with a null state, so nothing
    /// here needs a null guard for the menu (no match yet) window.
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        [SerializeField] GameManager gameManager;
        [SerializeField] Text turnLabel;
        [SerializeField] Text countLabelX;
        [SerializeField] Text countLabelO;
        [SerializeField] Text fadeWarningLabel;

        void Awake()
        {
            gameManager.StateChanged += OnStateChanged;
        }

        void OnDestroy()
        {
            if (gameManager != null)
                gameManager.StateChanged -= OnStateChanged;
        }

        void OnStateChanged(GameState state)
        {
            SetTurn(state.CurrentPlayer);
            UpdateActiveCounts(state.GetActiveCount(Occupant.X), state.GetActiveCount(Occupant.O), state.Config.BufferSize);
        }

        // Local (both sides human on this device): who moves by symbol.
        // Online (one human here): from this player's point of view.
        void SetTurn(Occupant player)
        {
            bool xHuman = gameManager.IsLocalHuman(Occupant.X);
            bool oHuman = gameManager.IsLocalHuman(Occupant.O);

            if (xHuman != oHuman)
                turnLabel.text = gameManager.IsLocalHuman(player) ? "Tu turno" : "Turno del rival";
            else
                turnLabel.text = $"Turno: Jugador {(player == Occupant.X ? "X" : "O")}";
        }

        void UpdateActiveCounts(int countX, int countO, int bufferSize)
        {
            countLabelX.text = $"X: {countX}/{bufferSize}";
            countLabelO.text = $"O: {countO}/{bufferSize}";
            fadeWarningLabel.text = BuildFadeWarning(countX, countO, bufferSize);
        }

        static string BuildFadeWarning(int countX, int countO, int bufferSize)
        {
            bool xFading = countX == bufferSize;
            bool oFading = countO == bufferSize;
            if (!xFading && !oFading) return string.Empty;
            if (xFading && oFading) return "¡X y O desvaneciendo!";
            return xFading ? "¡X desvaneciendo!" : "¡O desvaneciendo!";
        }
    }
}
