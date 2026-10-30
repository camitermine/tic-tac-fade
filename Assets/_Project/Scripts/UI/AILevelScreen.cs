using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TicTacFade.Game;

namespace TicTacFade.UI
{
    /// <summary>
    /// AI level choice (GDD §8.2): one button per level starts the match,
    /// "Volver" goes back to the menu. Only sends intents to
    /// <see cref="MatchFlow"/>.
    /// </summary>
    public class AILevelScreen : MonoBehaviour
    {
        [SerializeField] MatchFlow flow;
        [SerializeField] Button easyButton;
        [SerializeField] Button normalButton;
        [SerializeField] Button perfectButton;
        [SerializeField] Button backButton;

        UnityAction _playEasy;
        UnityAction _playNormal;
        UnityAction _playPerfect;

        void Awake()
        {
            _playEasy = () => flow.PlayAgainstAI(AIDifficulty.Easy);
            _playNormal = () => flow.PlayAgainstAI(AIDifficulty.Normal);
            _playPerfect = () => flow.PlayAgainstAI(AIDifficulty.Perfect);

            easyButton.onClick.AddListener(_playEasy);
            normalButton.onClick.AddListener(_playNormal);
            perfectButton.onClick.AddListener(_playPerfect);
            backButton.onClick.AddListener(flow.CancelAILevelSelect);
        }

        void OnDestroy()
        {
            if (flow == null) return;
            easyButton.onClick.RemoveListener(_playEasy);
            normalButton.onClick.RemoveListener(_playNormal);
            perfectButton.onClick.RemoveListener(_playPerfect);
            backButton.onClick.RemoveListener(flow.CancelAILevelSelect);
        }
    }
}
