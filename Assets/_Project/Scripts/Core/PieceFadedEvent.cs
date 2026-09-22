namespace TicTacFade.Core
{
    /// <summary>Raised when the FIFO rule removes a player's oldest piece.</summary>
    public sealed class PieceFadedEvent : IGameEvent
    {
        public Occupant Player { get; }
        public int CellIndex { get; }

        public PieceFadedEvent(Occupant player, int cellIndex)
        {
            Player = player;
            CellIndex = cellIndex;
        }
    }
}
