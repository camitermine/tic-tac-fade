using UnityEngine;
using TicTacFade.Core;
using TicTacFade.UI;

namespace TicTacFade.Gameplay
{
    public class GameManager : MonoBehaviour
    {
        [SerializeField] BoardView boardView;
        [SerializeField] GameHud hud;

        GameBoard board;
        Occupant currentPlayer;
        bool gameOver;

        void Awake()
        {
            board = new GameBoard();
            boardView.CellClicked += OnCellClicked;
        }

        void Start()
        {
            StartNewGame();
        }

        public void StartNewGame()
        {
            board.Reset();
            currentPlayer = Occupant.X;
            gameOver = false;
            hud.HideWinBanner();
            boardView.ClearHighlights();
            RefreshView();
        }

        void OnCellClicked(int cellIndex)
        {
            if (gameOver || !board.IsCellEmpty(cellIndex)) return;

            var result = board.PlaceMove(currentPlayer, cellIndex);
            if (!result.Success) return;

            if (result.IsWin)
            {
                gameOver = true;
                boardView.Render(board, false);
                boardView.HighlightLine(result.WinningLine);
                hud.ShowWinBanner(result.Winner);
                return;
            }

            currentPlayer = currentPlayer == Occupant.X ? Occupant.O : Occupant.X;
            RefreshView();
        }

        void RefreshView()
        {
            boardView.Render(board, !gameOver);
            hud.SetTurn(currentPlayer);
            hud.UpdateActiveCounts(board.GetActiveCount(Occupant.X), board.GetActiveCount(Occupant.O));
        }
    }
}
