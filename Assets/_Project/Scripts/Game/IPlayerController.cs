using System;
using TicTacFade.Core;

namespace TicTacFade.Game
{
    /// <summary>
    /// A source of moves for one player slot (X or O). GameManager never
    /// branches on the concrete type: LocalHumanPlayer, and later
    /// RemotePlayer/AIPlayer, are all driven through this same surface
    /// (CLAUDE.md, architecture rule 5).
    /// </summary>
    public interface IPlayerController
    {
        Occupant Player { get; }

        /// <summary>Raised when this controller decided its move.</summary>
        event Action<Move> MoveChosen;

        /// <summary>GameManager calls this when it becomes this controller's turn.</summary>
        void NotifyTurnStarted(GameState state);

        /// <summary>
        /// Routes a raw board-cell tap from the UI. No-ops for controllers
        /// that don't take UI input directly (Remote/AI).
        /// </summary>
        void NotifyCellSelected(int cellIndex);
    }
}
