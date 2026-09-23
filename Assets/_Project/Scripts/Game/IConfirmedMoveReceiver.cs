using TicTacFade.Core;

namespace TicTacFade.Game
{
    /// <summary>
    /// An online controller that plays a move only once the host confirmed
    /// it. <see cref="OnlineMatch"/> routes every confirmed move to the
    /// controller of that move's player through this.
    /// </summary>
    public interface IConfirmedMoveReceiver
    {
        void ApplyConfirmed(Move move);
    }
}
