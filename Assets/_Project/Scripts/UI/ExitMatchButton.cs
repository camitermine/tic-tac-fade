using UnityEngine;
using UnityEngine.UI;
using TicTacFade.Game;

namespace TicTacFade.UI
{
    /// <summary>
    /// "Salir" button on the match screen: leaves the match in progress and
    /// goes back to the menu (<see cref="MatchFlow.ExitMatch"/>).
    /// </summary>
    public class ExitMatchButton : MonoBehaviour
    {
        [SerializeField] MatchFlow flow;
        [SerializeField] Button button;

        void Awake()
        {
            button.onClick.AddListener(flow.ExitMatch);
        }

        void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(flow.ExitMatch);
        }
    }
}
