using UnityEngine;
using UnityEngine.UI;
using TicTacFade.Core;
using TicTacFade.Game;

namespace TicTacFade.UI
{
    /// <summary>
    /// Turn indicator, active-piece counters and, online, the turn countdown
    /// and the absent markers (GDD §4.2). Observes GameManager and MatchFlow
    /// events instead of receiving imperative calls: Core/Game communicate
    /// through events, the UI observes. The end-of-match message lives in
    /// <see cref="ResultScreen"/>.
    /// GameManager never raises StateChanged with a null state, so nothing
    /// here needs a null guard for the menu (no match yet) window.
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        const string LeavingText = "Saliendo...";
        const string AbsentSuffix = " · ausente";

        static readonly Color TimerNormalColor = Color.white;
        static readonly Color TimerAbsentColor = new Color(1f, 0.55f, 0.15f);

        [SerializeField] GameManager gameManager;
        [SerializeField] MatchFlow flow;
        [SerializeField] Text turnLabel;
        [SerializeField] Text countLabelX;
        [SerializeField] Text countLabelO;
        [SerializeField] Text fadeWarningLabel;
        [SerializeField] Text turnTimerLabel;

        void Awake()
        {
            gameManager.StateChanged += OnStateChanged;
            flow.TurnTimerChanged += OnTurnTimerChanged;
            flow.TurnSecondsChanged += OnTurnSecondsChanged;
            flow.BusyChanged += OnBusyChanged;
            turnTimerLabel.text = string.Empty;
        }

        void OnDestroy()
        {
            if (gameManager != null)
                gameManager.StateChanged -= OnStateChanged;
            if (flow != null)
            {
                flow.TurnTimerChanged -= OnTurnTimerChanged;
                flow.TurnSecondsChanged -= OnTurnSecondsChanged;
                flow.BusyChanged -= OnBusyChanged;
            }
        }

        void OnStateChanged(GameState state) => Refresh();

        void OnTurnTimerChanged() => Refresh();

        void OnTurnSecondsChanged(int seconds) => RefreshTimer();

        void OnBusyChanged(bool busy) => Refresh();

        void Refresh()
        {
            var state = gameManager.CurrentState;
            if (state == null) return;

            SetTurn(state.CurrentPlayer);
            UpdateActiveCounts(state.GetActiveCount(Occupant.X), state.GetActiveCount(Occupant.O), state.Config.BufferSize);
            RefreshTimer();
        }

        // Local (both sides human on this device): who moves by symbol.
        // Online (one human here): from this player's point of view.
        void SetTurn(Occupant player)
        {
            if (flow.IsOnline && flow.IsBusy && flow.State == FlowState.Playing)
            {
                turnLabel.text = LeavingText; // "Salir": waiting for the other device's ack
                return;
            }

            bool xHuman = gameManager.IsLocalHuman(Occupant.X);
            bool oHuman = gameManager.IsLocalHuman(Occupant.O);

            if (xHuman != oHuman)
                turnLabel.text = gameManager.IsLocalHuman(player) ? "Tu turno" : "Turno del rival";
            else
                turnLabel.text = $"Turno: Jugador {(player == Occupant.X ? "X" : "O")}";
        }

        void UpdateActiveCounts(int countX, int countO, int bufferSize)
        {
            countLabelX.text = $"X: {countX}/{bufferSize}{(flow.IsAbsent(Occupant.X) ? AbsentSuffix : string.Empty)}";
            countLabelO.text = $"O: {countO}/{bufferSize}{(flow.IsAbsent(Occupant.O) ? AbsentSuffix : string.Empty)}";
            fadeWarningLabel.text = BuildFadeWarning(countX, countO, bufferSize);
        }

        // Online only: "0:23", in another color when the player to move is
        // absent (10 s turns). Empty offline or when no turn is running.
        void RefreshTimer()
        {
            var state = gameManager.CurrentState;
            if (!flow.IsTurnTimerRunning || state == null)
            {
                turnTimerLabel.text = string.Empty;
                return;
            }

            int seconds = flow.TurnSecondsRemaining;
            turnTimerLabel.text = $"{seconds / 60}:{seconds % 60:00}";
            turnTimerLabel.color = flow.IsAbsent(state.CurrentPlayer) ? TimerAbsentColor : TimerNormalColor;
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
