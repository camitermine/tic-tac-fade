using UnityEngine;
using UnityEngine.UI;
using TicTacFade.Game;

namespace TicTacFade.UI
{
    /// <summary>
    /// Join-with-code screen: code field, join and cancel (back to the
    /// menu). While joining every control is disabled and the status line
    /// shows "Conectando..."; an invalid code or full room shows an error
    /// without leaving the screen.
    /// </summary>
    public class JoinByCodeScreen : MonoBehaviour
    {
        [SerializeField] MatchFlow flow;
        [SerializeField] InputField codeInput;
        [SerializeField] Button joinButton;
        [SerializeField] Button cancelButton;
        [SerializeField] Text statusLabel;

        void Awake()
        {
            joinButton.onClick.AddListener(SubmitCode);
            cancelButton.onClick.AddListener(flow.CancelJoinByCode);
            flow.StateChanged += OnFlowStateChanged;
            flow.BusyChanged += OnBusyChanged;
            flow.FailureChanged += OnFailureChanged;
        }

        void OnDestroy()
        {
            if (flow == null) return;
            joinButton.onClick.RemoveListener(SubmitCode);
            cancelButton.onClick.RemoveListener(flow.CancelJoinByCode);
            flow.StateChanged -= OnFlowStateChanged;
            flow.BusyChanged -= OnBusyChanged;
            flow.FailureChanged -= OnFailureChanged;
        }

        void SubmitCode() => flow.JoinRoom(codeInput.text);

        void OnFlowStateChanged(FlowState state)
        {
            if (state != FlowState.JoinByCode) return;
            codeInput.text = string.Empty;
            Refresh();
        }

        void OnBusyChanged(bool busy) => Refresh();

        void OnFailureChanged(SessionFailure failure) => Refresh();

        void Refresh()
        {
            bool busy = flow.IsBusy;
            codeInput.interactable = !busy;
            joinButton.interactable = !busy;
            cancelButton.interactable = !busy;
            statusLabel.text = busy ? SessionFailureMessages.Connecting : SessionFailureMessages.ToText(flow.LastFailure);
        }
    }
}
