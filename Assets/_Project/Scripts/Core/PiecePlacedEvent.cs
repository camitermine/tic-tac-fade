namespace TicTacFade.Core
{
    /// <summary>Raised when a new piece is placed on the board.</summary>
    public sealed class PiecePlacedEvent : IGameEvent
    {
        public Occupant Player { get; }
        public int CellIndex { get; }

        public PiecePlacedEvent(Occupant player, int cellIndex)
        {
            Player = player;
            CellIndex = cellIndex;
        }
    }
}
