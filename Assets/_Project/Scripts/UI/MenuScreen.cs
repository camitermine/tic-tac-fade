using UnityEngine;
using UnityEngine.UI;
using TicTacFade.Game;

namespace TicTacFade.UI
{
    /// <summary>
    /// Main menu. Only "Jugar local" for now; the online buttons (create
    /// room, join with code) go next to it in the same layout group.
    /// </summary>
    public class MenuScreen : MonoBehaviour
    {
        [SerializeField] MatchFlow flow;
        [SerializeField] Button playLocalButton;

        void Awake()
        {
            playLocalButton.onClick.AddListener(flow.PlayLocal);
        }

        void OnDestroy()
        {
            if (playLocalButton != null)
                playLocalButton.onClick.RemoveListener(flow.PlayLocal);
        }
    }
}
