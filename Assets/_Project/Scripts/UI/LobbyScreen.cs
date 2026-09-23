using UnityEngine;
using UnityEngine.UI;
using TicTacFade.Game;

namespace TicTacFade.UI
{
    /// <summary>
    /// Waiting room: the join code in large type (readable enough to copy
    /// by hand if the clipboard fails), a copy button, the opponent status
    /// and cancel, which closes the room and goes back to the menu.
    /// </summary>
    public class LobbyScreen : MonoBehaviour
    {
        const string WaitingText = "Esperando rival...";
        const string ConnectedText = "Rival conectado";
        const string ClosingText = "Cerrando sala...";
        const string CopiedText = "Código copiado";

        [SerializeField] MatchFlow flow;
        [SerializeField] Text codeLabel;
        [SerializeField] Button copyButton;
        [SerializeField] Text statusLabel;
        [SerializeField] Button cancelButton;

        bool _justCopied;

        void Awake()
        {
            copyButton.onClick.AddListener(CopyCode);
            cancelButton.onClick.AddListener(flow.LeaveRoom);
            flow.StateChanged += OnFlowStateChanged;
            flow.BusyChanged += OnBusyChanged;
            flow.OpponentStatusChanged += OnOpponentStatusChanged;
        }

        void OnDestroy()
        {
            if (flow == null) return;
            copyButton.onClick.RemoveListener(CopyCode);
            cancelButton.onClick.RemoveListener(flow.LeaveRoom);
            flow.StateChanged -= OnFlowStateChanged;
            flow.BusyChanged -= OnBusyChanged;
            flow.OpponentStatusChanged -= OnOpponentStatusChanged;
        }

        void OnFlowStateChanged(FlowState state)
        {
            if (state != FlowState.Lobby) return;
            _justCopied = false;
            codeLabel.text = flow.JoinCode ?? string.Empty;
            Refresh();
        }

        void OnBusyChanged(bool busy) => Refresh();

        void OnOpponentStatusChanged(bool connected)
        {
            _justCopied = false;
            Refresh();
        }

        void CopyCode()
        {
            GUIUtility.systemCopyBuffer = flow.JoinCode ?? string.Empty;
            _justCopied = true;
            Refresh();
        }

        void Refresh()
        {
            bool busy = flow.IsBusy;
            copyButton.interactable = !busy;
            cancelButton.interactable = !busy;

            if (busy)
                statusLabel.text = ClosingText;
            else if (flow.IsOpponentConnected)
                statusLabel.text = ConnectedText;
            else
                statusLabel.text = _justCopied ? $"{CopiedText}\n{WaitingText}" : WaitingText;
        }
    }
}
