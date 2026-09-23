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

        int? selectedCell;

        void Awake()
        {
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i].Init(i);
                cells[i].Clicked += OnCellClicked;
            }

            gameManager.StateChanged += OnStateChanged;
            gameManager.LocalInputAvailabilityChanged += OnLocalInputAvailabilityChanged;
        }

        void OnDestroy()
        {
            if (gameManager == null) return;
            gameManager.StateChanged -= OnStateChanged;
            gameManager.LocalInputAvailabilityChanged -= OnLocalInputAvailabilityChanged;
        }

        void OnCellClicked(int index)
        {
            // No local input right now (no match, a finished match, the
            // opponent's turn online, or an online move waiting for the
            // host): no selection, no preview.
            if (!gameManager.CanAcceptLocalInput)
                return;
            var state = gameManager.CurrentState;

            if (selectedCell.HasValue && selectedCell.Value == index)
            {
                // Second tap on the already-selected cell: confirm.
                ClearGhostPreview();
                selectedCell = null;
                gameManager.OnCellClicked(index);
                return;
            }

            // First tap, or a tap on a different cell: (re)select and preview
            // the resulting board (GDD §3.7).
            ClearGhostPreview();
            selectedCell = index;

            var player = state.CurrentPlayer;
            cells[index].ShowGhost(player);

            if (state.GetActiveCount(player) == state.Config.BufferSize)
            {
                var queue = player == Occupant.X ? state.QueueX : state.QueueO;
                cells[queue[0]].SetGhostVictim(true); // oldest in the SAME queue: the one this move would remove
            }
        }

        void ClearGhostPreview()
        {
            if (selectedCell.HasValue)
                cells[selectedCell.Value].HideGhost();
            foreach (var cell in cells)
                cell.SetGhostVictim(false);
        }

        // GameManager never raises StateChanged with a null state, so this
        // path needs no null guard.
        void OnStateChanged(GameState state)
        {
            ClearGhostPreview();
            selectedCell = null;

            Render(state, gameManager.CanAcceptLocalInput);

            if (state.IsOver && state.Winner != Occupant.None)
                HighlightLine(state.WinningLine);
            else
                ClearHighlights();
        }

        // Input can open or close without a state change (an online move sent
        // to the host and not confirmed yet): update the empty cells only.
        void OnLocalInputAvailabilityChanged()
        {
            var state = gameManager.CurrentState;
            if (state == null) return;

            bool canPlay = gameManager.CanAcceptLocalInput;
            if (!canPlay)
            {
                ClearGhostPreview();
                selectedCell = null;
            }

            for (int i = 0; i < cells.Length; i++)
            {
                if (state.Cells[i] == Occupant.None)
                    cells[i].SetInteractable(canPlay);
            }
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
