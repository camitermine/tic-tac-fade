using System;
using TicTacFade.Core;
using TicTacFade.Game;

namespace TicTacFade.PlayModeTests
{
    /// <summary>
    /// Non-human IPlayerController used to prove GameManager doesn't need a
    /// human/UI-driven controller to run a match. Always plays the first
    /// free cell as soon as it's notified that its turn started.
    /// </summary>
    public class FakeAutoPlayer : IPlayerController
    {
        public Occupant Player { get; }
        public event Action<Move> MoveChosen;

        public FakeAutoPlayer(Occupant player) => Player = player;

        public void NotifyTurnStarted(GameState state) => MoveChosen?.Invoke(new Move(Player, state.GetFreeCellIndices()[0]));

        public void NotifyCellSelected(int cellIndex) { } // autonomous, ignores UI input
    }
}
