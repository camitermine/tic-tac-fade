using System.Collections.Generic;
using UnityEngine;
using TicTacFade.Core;
using TicTacFade.Game;

namespace TicTacFade.UI
{
    /// <summary>
    /// Renders the board from the <see cref="GameManager"/>'s <see cref="GameState"/>.
    /// Subscribes to <see cref="GameManager.StateChanged"/> instead of having
    /// GameManager call it directly, so the Game layer doesn't need to know
    /// about any UI type (that would create a Game↔UI asmdef cycle).
    /// </summary>
    public class BoardView : MonoBehaviour
    {
        [SerializeField] GameManager gameManager;
        [SerializeField] CellView[] cells = new CellView[9];

        void Awake()
        {
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i].Init(i);
                cells[i].Clicked += OnCellClicked;
            }

            gameManager.StateChanged += OnStateChanged;
        }

        void OnDestroy()
        {
            if (gameManager != null)
                gameManager.StateChanged -= OnStateChanged;
        }

        void OnCellClicked(int index) => gameManager.OnCellClicked(index);

        void OnStateChanged(GameState state)
        {
            Render(state, !state.IsOver);

            if (state.IsOver && state.Winner != Occupant.None)
                HighlightLine(state.WinningLine);
            else
                ClearHighlights();
        }

        void Render(GameState state, bool interactableWhenEmpty)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                var occupant = state.Cells[i];
                if (occupant == Occupant.None)
                {
                    cells[i].SetEmpty();
                    cells[i].SetInteractable(interactableWhenEmpty);
                }
                else
                {
                    cells[i].SetOccupant(occupant, state.GetLife(occupant, i));
                    cells[i].SetInteractable(false);
                }
            }
        }

        void HighlightLine(IReadOnlyList<int> line)
        {
            if (line == null) return;
            foreach (var i in line)
                cells[i].SetHighlighted(true);
        }

        void ClearHighlights()
        {
            foreach (var c in cells)
                c.SetHighlighted(false);
        }
    }
}
