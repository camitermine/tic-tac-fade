namespace TicTacFade.Core
{
    /// <summary>
    /// Reason <see cref="RulesEngine.IsLegal"/> rejects a move.
    /// <see cref="None"/> is the value returned when the move is valid.
    /// </summary>
    public enum MoveRejectionReason
    {
        None = 0,
        GameAlreadyEnded,
        NotPlayersTurn,
        CellOutOfRange,
        CellOccupied
    }
}
