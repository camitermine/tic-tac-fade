namespace TicTacFade.Core
{
    /// <summary>
    /// Game parameters (GDD §9). None of these rule values should be
    /// hardcoded anywhere else: anything that varies with board size,
    /// buffer length or draw limits is read from here.
    /// </summary>
    public readonly struct GameConfig
    {
        public int BoardSize { get; }
        public int BufferSize { get; }
        public int WinLength { get; }
        public int MaxTotalMoves { get; }
        public int RepetitionLimit { get; }

        public GameConfig(int boardSize, int bufferSize, int winLength, int maxTotalMoves, int repetitionLimit)
        {
            BoardSize = boardSize;
            BufferSize = bufferSize;
            WinLength = winLength;
            MaxTotalMoves = maxTotalMoves;
            RepetitionLimit = repetitionLimit;
        }

        /// <summary>MVP starting values (GDD §9).</summary>
        public static GameConfig Mvp() => new GameConfig(
            boardSize: 3,
            bufferSize: 3,
            winLength: 3,
            maxTotalMoves: 40,
            repetitionLimit: 3);

        public int CellCount => BoardSize * BoardSize;
    }
}
