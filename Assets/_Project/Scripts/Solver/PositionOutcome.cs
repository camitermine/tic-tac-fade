namespace TicTacFade.Solver
{
    /// <summary>
    /// Value of a position for the player who moves next, with perfect play
    /// from both sides (ADR 0004).
    /// </summary>
    public enum PositionOutcome
    {
        /// <summary>
        /// Neither side can force a win: with perfect play the game cycles
        /// and ends by repetition (or by the move limit).
        /// </summary>
        NoForcedWin = 0,

        /// <summary>The player to move can force a win.</summary>
        Win = 1,

        /// <summary>The opponent can force a win whatever the player to move does.</summary>
        Loss = 2,
    }
}
