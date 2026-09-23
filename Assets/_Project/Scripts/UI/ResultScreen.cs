using UnityEngine;
using UnityEngine.UI;
using TicTacFade.Core;
using TicTacFade.Game;

namespace TicTacFade.UI
{
    /// <summary>
    /// End-of-match panel: who won (or the draw) and why, plus rematch and
    /// back-to-menu buttons. Reads <see cref="MatchFlow.LastResult"/> when
    /// the flow enters <see cref="FlowState.Result"/>.
    /// </summary>
    public class ResultScreen : MonoBehaviour
    {
        [SerializeField] MatchFlow flow;
        [SerializeField] Text resultLabel;
        [SerializeField] Button rematchButton;
        [SerializeField] Button menuButton;

        void Awake()
        {
            flow.StateChanged += OnFlowStateChanged;
            rematchButton.onClick.AddListener(flow.Rematch);
            menuButton.onClick.AddListener(flow.BackToMenu);
        }

        void OnDestroy()
        {
            if (flow == null) return;
            flow.StateChanged -= OnFlowStateChanged;
            rematchButton.onClick.RemoveListener(flow.Rematch);
            menuButton.onClick.RemoveListener(flow.BackToMenu);
        }

        void OnFlowStateChanged(FlowState state)
        {
            if (state == FlowState.Result)
                resultLabel.text = ResolveMessage(flow.LastResult);
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
