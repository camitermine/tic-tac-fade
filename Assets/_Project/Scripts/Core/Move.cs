namespace TicTacFade.Core
{
    /// <summary>
    /// The command that represents a move. It's the only thing that travels
    /// over the network (CLAUDE.md, architecture rule 4): both peers run the
    /// same Core rules over the same <see cref="Move"/>.
    /// </summary>
    public readonly struct Move
    {
        public Occupant Player { get; }
        public int CellIndex { get; }

        public Move(Occupant player, int cellIndex)
        {
            Player = player;
            CellIndex = cellIndex;
        }
    }
}
