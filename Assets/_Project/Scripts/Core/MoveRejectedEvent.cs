namespace TicTacFade.Core
{
    /// <summary>Raised instead of applying the move when <see cref="RulesEngine.IsLegal"/> rejects it.</summary>
    public sealed class MoveRejectedEvent : IGameEvent
    {
        public Move Move { get; }
        public MoveRejectionReason Reason { get; }

        public MoveRejectedEvent(Move move, MoveRejectionReason reason)
        {
            Move = move;
            Reason = reason;
        }
    }
}
