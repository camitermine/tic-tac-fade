using System;
using TicTacFade.Core;

namespace TicTacFade.Game
{
    /// <summary>
    /// The opponent's side in an online match. Never takes input on this
    /// device; plays a move when the host confirms one for this side.
    /// </summary>
    public sealed class RemotePlayer : IPlayerController, IConfirmedMoveReceiver
    {
        public Occupant Player { get; }
        public bool IsLocalHuman => false;
        public bool AcceptsLocalInput => false;

        public event Action<Move> MoveChosen;

        // Never raised: AcceptsLocalInput is constant.
        public event Action AcceptsLocalInputChanged { add { } remove { } }

        public RemotePlayer(Occupant player) => Player = player;

        public void NotifyTurnStarted(GameState state) { } // the move arrives through the network

        public void NotifyCellSelected(int cellIndex) { } // taps never drive the opponent

        public void ApplyConfirmed(Move move) => MoveChosen?.Invoke(move);
    }
}
