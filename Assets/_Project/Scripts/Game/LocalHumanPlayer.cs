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
        public event Action<Move> MoveChosen;

        public LocalHumanPlayer(Occupant player) => Player = player;

        public void NotifyTurnStarted(GameState state) { } // waits for UI input, nothing to do proactively

        public void NotifyCellSelected(int cellIndex) => MoveChosen?.Invoke(new Move(Player, cellIndex));
    }
}
