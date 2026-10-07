namespace TicTacFade.Solver
{
    /// <summary>
    /// Solved value of a position for the player to move. <see cref="Distance"/>
    /// counts moves (plies, like <c>GameState.TotalMoves</c>) until the game
    /// ends with perfect play: the winner wins as fast as possible and the
    /// loser delays as long as possible. Win distances are odd (the player to
    /// move makes the last move), Loss distances even; 0 for
    /// <see cref="PositionOutcome.NoForcedWin"/>.
    /// </summary>
    public readonly struct PositionResult
    {
        public PositionOutcome Outcome { get; }
        public int Distance { get; }

        public PositionResult(PositionOutcome outcome, int distance)
        {
            Outcome = outcome;
            Distance = distance;
        }

        public static PositionResult NoForcedWin => new PositionResult(PositionOutcome.NoForcedWin, 0);

        /// <summary>
        /// The value of the position before a move that led here, for the
        /// player who made that move.
        /// </summary>
        public PositionResult SeenByPreviousMover()
        {
            switch (Outcome)
            {
                case PositionOutcome.Win: return new PositionResult(PositionOutcome.Loss, Distance + 1);
                case PositionOutcome.Loss: return new PositionResult(PositionOutcome.Win, Distance + 1);
                default: return NoForcedWin;
            }
        }

        public override string ToString() =>
            Outcome == PositionOutcome.NoForcedWin ? "NoForcedWin" : $"{Outcome} in {Distance}";
    }
}
