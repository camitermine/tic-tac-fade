namespace TicTacFade.Core
{
    /// <summary>
    /// Reason a match ended. Core only exposes the reason; the UI layer
    /// decides what text to show for each value.
    /// </summary>
    public enum GameEndReason
    {
        Win,
        DrawByRepetition,
        DrawByMoveLimit
    }
}
