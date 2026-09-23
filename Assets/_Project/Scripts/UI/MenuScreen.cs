using UnityEngine;
using UnityEngine.UI;
using TicTacFade.Game;

namespace TicTacFade.UI
{
    /// <summary>
    /// Main menu: local play, create room, join with code. While a room is
    /// being created every button is disabled and the status line shows
    /// "Conectando..."; a failure shows up in the same line.
    /// </summary>
    public class MenuScreen : MonoBehaviour
    {
        [SerializeField] MatchFlow flow;
        [SerializeField] Button playLocalButton;
        [SerializeField] Button createRoomButton;
        [SerializeField] Button joinByCodeButton;
        [SerializeField] Text statusLabel;

        void Awake()
        {
            playLocalButton.onClick.AddListener(flow.PlayLocal);
            createRoomButton.onClick.AddListener(flow.CreateRoom);
            joinByCodeButton.onClick.AddListener(flow.OpenJoinByCode);
            flow.BusyChanged += OnBusyChanged;
            flow.FailureChanged += OnFailureChanged;
            Refresh();
        }

        void OnDestroy()
        {
            if (flow == null) return;
            playLocalButton.onClick.RemoveListener(flow.PlayLocal);
            createRoomButton.onClick.RemoveListener(flow.CreateRoom);
            joinByCodeButton.onClick.RemoveListener(flow.OpenJoinByCode);
            flow.BusyChanged -= OnBusyChanged;
            flow.FailureChanged -= OnFailureChanged;
        }

        void OnBusyChanged(bool busy) => Refresh();

        void OnFailureChanged(SessionFailure failure) => Refresh();

        void Refresh()
        {
            bool busy = flow.IsBusy;
            playLocalButton.interactable = !busy;
            createRoomButton.interactable = !busy;
            joinByCodeButton.interactable = !busy;
            statusLabel.text = busy ? SessionFailureMessages.Connecting : SessionFailureMessages.ToText(flow.LastFailure);
        }
    }
}
