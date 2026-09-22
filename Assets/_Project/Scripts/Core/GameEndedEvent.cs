using System.Collections.Generic;

namespace TicTacFade.Core
{
    /// <summary>
    /// Raised when the match ends, either by win or by draw.
    /// <see cref="Winner"/> is <see cref="Occupant.None"/> on a draw.
    /// <see cref="WinningLine"/> is an empty list unless <see cref="Reason"/> is <see cref="GameEndReason.Win"/>.
    /// </summary>
    public sealed class GameEndedEvent : IGameEvent
    {
        public Occupant Winner { get; }
        public GameEndReason Reason { get; }
        public IReadOnlyList<int> WinningLine { get; }

        public GameEndedEvent(Occupant winner, GameEndReason reason, IReadOnlyList<int> winningLine)
        {
            Winner = winner;
            Reason = reason;
            WinningLine = winningLine ?? System.Array.Empty<int>();
        }
    }
}
