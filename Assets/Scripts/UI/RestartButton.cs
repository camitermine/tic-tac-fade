using UnityEngine;
using UnityEngine.UI;
using TicTacFade.Gameplay;

namespace TicTacFade.UI
{
    [RequireComponent(typeof(Button))]
    public class RestartButton : MonoBehaviour
    {
        [SerializeField] GameManager gameManager;

        public void SetGameManager(GameManager manager) => gameManager = manager;

        void Awake()
        {
            GetComponent<Button>().onClick.AddListener(() => gameManager?.StartNewGame());
        }
    }
}
