using UnityEngine;
using UnityEngine.UI;
using TicTacFade.Core;
using TicTacFade.Game;

namespace TicTacFade.UI
{
    /// <summary>
    /// End-of-match panel: who won (or the draw) and why, plus rematch and
    /// back-to-menu buttons. Reads <see cref="MatchFlow.LastResult"/> when
    /// the flow enters <see cref="FlowState.Result"/>. Online, a rematch
    /// needs both players: whoever asked sees "Esperando al rival...".
    /// </summary>
    public class ResultScreen : MonoBehaviour
    {
        const string WaitingForRematchText = "Esperando al rival...";

        [SerializeField] MatchFlow flow;
        [SerializeField] Text resultLabel;
        [SerializeField] Text rematchStatusLabel;
        [SerializeField] Button rematchButton;
        [SerializeField] Button menuButton;

        void Awake()
        {
            flow.StateChanged += OnFlowStateChanged;
            flow.RematchWaitingChanged += OnRematchWaitingChanged;
            flow.BusyChanged += OnBusyChanged;
            rematchButton.onClick.AddListener(flow.Rematch);
            menuButton.onClick.AddListener(flow.BackToMenu);
        }

        void OnDestroy()
        {
            if (flow == null) return;
            flow.StateChanged -= OnFlowStateChanged;
            flow.RematchWaitingChanged -= OnRematchWaitingChanged;
            flow.BusyChanged -= OnBusyChanged;
            rematchButton.onClick.RemoveListener(flow.Rematch);
            menuButton.onClick.RemoveListener(flow.BackToMenu);
        }

        void OnFlowStateChanged(FlowState state)
        {
            if (state == FlowState.Result)
                resultLabel.text = ResolveMessage(flow.LastResult);
            RefreshButtons();
        }

        void OnRematchWaitingChanged(bool waiting) => RefreshButtons();

        void OnBusyChanged(bool busy) => RefreshButtons();

        void RefreshButtons()
        {
            bool waiting = flow.IsWaitingForRematch;
            rematchButton.interactable = !waiting && !flow.IsBusy;
            menuButton.interactable = !flow.IsBusy;
            rematchStatusLabel.text = waiting ? WaitingForRematchText : string.Empty;
        }

        static string ResolveMessage(GameEndedEvent evt)
        {
            if (evt == null)
                return "Fin de la partida";

            switch (evt.Reason)
            {
                case GameEndReason.Win:
                    return $"¡Jugador {(evt.Winner == Occupant.X ? "X" : "O")} gana!\nTres en línea";
                case GameEndReason.DrawByRepetition:
                    return "Empate\nPosición repetida";
                case GameEndReason.DrawByMoveLimit:
                    return "Empate\nTope de jugadas";
                default:
                    return "Fin de la partida";
            }
        }
    }
}
