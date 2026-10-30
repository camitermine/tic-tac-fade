namespace TicTacFade.Game
{
    /// <summary>
    /// Effective value of a move for the AI, worst to best (ADR 0005). Unlike
    /// the raw solver value, it accounts for the match history (repetition)
    /// and the move cap.
    /// </summary>
    public enum AIMoveCategory
    {
        /// <summary>The opponent can force a win before the move cap. Longer is better.</summary>
        Loss = 0,

        /// <summary>Ends the match now in a draw, by repetition or by the move cap.</summary>
        ImmediateDraw = 1,

        /// <summary>
        /// Keeps the match going with no forced win for either side (or one
        /// the move cap cuts off): the opponent may still make a mistake.
        /// </summary>
        NoForcedWin = 2,

        /// <summary>A forced win, but the opponent's reply can draw by a third repetition.</summary>
        WinWithEscape = 3,

        /// <summary>A forced win before the move cap with no repetition escape on the next reply. Shorter is better.</summary>
        Win = 4,

        /// <summary>This move wins on the spot.</summary>
        ImmediateWin = 5,
    }
}
