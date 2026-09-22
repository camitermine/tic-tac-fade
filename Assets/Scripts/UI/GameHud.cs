using UnityEngine;
using UnityEngine.UI;
using TicTacFade.Core;

namespace TicTacFade.UI
{
    public class GameHud : MonoBehaviour
    {
        [SerializeField] Text turnLabel;
        [SerializeField] Text countLabelX;
        [SerializeField] Text countLabelO;
        [SerializeField] GameObject winBanner;
        [SerializeField] Text winLabel;

        public void SetTurn(Occupant player)
        {
            turnLabel.text = $"Turno: Jugador {(player == Occupant.X ? "X" : "O")}";
        }

        public void UpdateActiveCounts(int countX, int countO)
        {
            countLabelX.text = $"X: {countX}/3" + (countX == GameBoard.MaxActivePieces ? "  ¡Aviso de desvanecimiento!" : "");
            countLabelO.text = $"O: {countO}/3" + (countO == GameBoard.MaxActivePieces ? "  ¡Aviso de desvanecimiento!" : "");
        }

        public void ShowWinBanner(Occupant winner)
        {
            winBanner.SetActive(true);
            winLabel.text = $"¡Jugador {(winner == Occupant.X ? "X" : "O")} gana!";
        }

        public void HideWinBanner()
        {
            winBanner.SetActive(false);
        }
    }
}
