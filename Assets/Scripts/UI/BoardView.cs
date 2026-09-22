using System;
using UnityEngine;
using TicTacFade.Core;

namespace TicTacFade.UI
{
    public class BoardView : MonoBehaviour
    {
        [SerializeField] CellView[] cells = new CellView[9];

        public event Action<int> CellClicked;

        void Awake()
        {
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i].Init(i);
                cells[i].Clicked += OnCellClicked;
            }
        }

        void OnCellClicked(int index) => CellClicked?.Invoke(index);

        public void Render(GameBoard board, bool interactableWhenEmpty)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                var occupant = board.Cells[i];
                if (occupant == Occupant.None)
                {
                    cells[i].SetEmpty();
                    cells[i].SetInteractable(interactableWhenEmpty);
                }
                else
                {
                    cells[i].SetOccupant(occupant, board.GetLife(occupant, i));
                    cells[i].SetInteractable(false);
                }
            }
        }

        public void HighlightLine(int[] line)
        {
            if (line == null) return;
            foreach (var i in line)
                cells[i].SetHighlighted(true);
        }

        public void ClearHighlights()
        {
            foreach (var c in cells)
                c.SetHighlighted(false);
        }
    }
}
