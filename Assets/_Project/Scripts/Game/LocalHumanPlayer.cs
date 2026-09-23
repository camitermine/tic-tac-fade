using System;
using TicTacFade.Core;

namespace TicTacFade.Game
{
    /// <summary>
    /// A player driven by touch input on this device. Waits passively for
    /// <see cref="NotifyCellSelected"/>; never acts on its own.
    /// </summary>
    public class LocalHumanPlayer : IPlayerController
    {
        public Occupant Player { get; }
        public bool IsLocalHuman => true;
        public bool AcceptsLocalInput => true; // local moves apply at once: nothing to wait for

        public event Action<Move> MoveChosen;

        // Never raised: AcceptsLocalInput is constant.
        public event Action AcceptsLocalInputChanged { add { } remove { } }

        public LocalHumanPlayer(Occupant player) => Player = player;

        public void NotifyTurnStarted(GameState state) { } // waits for UI input, nothing to do proactively

        public void NotifyCellSelected(int cellIndex) => MoveChosen?.Invoke(new Move(Player, cellIndex));
    }
}
