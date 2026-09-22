using System.Linq;
using NUnit.Framework;
using TicTacFade.Core;

namespace TicTacFade.Core.Tests
{
    /// <summary>
    /// Applies a sequence of moves to build a match position. Fails the test
    /// if any intermediate move is rejected (<see cref="MoveRejectedEvent"/>)
    /// or if the match ends before the last move of the sequence: a
    /// "filler" move that accidentally aligns <c>WinLength</c> pieces, or
    /// that triggers a draw, would silently contaminate the scenario the
    /// test is trying to build.
    /// </summary>
    internal static class MoveSequence
    {
        public static GameState Apply(GameState state, params Move[] moves)
        {
            for (int i = 0; i < moves.Length; i++)
            {
                var move = moves[i];
                var (nextState, events) = RulesEngine.Apply(state, move);

                var rejected = events.OfType<MoveRejectedEvent>().FirstOrDefault();
                if (rejected != null)
                {
                    Assert.Fail($"Move #{i + 1} ({move.Player} -> cell {move.CellIndex}) was rejected: {rejected.Reason}.");
                }

                state = nextState;

                bool isLastMove = i == moves.Length - 1;
                if (!isLastMove && state.IsOver)
                {
                    Assert.Fail($"The match ended earlier than expected, on move #{i + 1} ({move.Player} -> cell {move.CellIndex}).");
                }
            }

            return state;
        }
    }
}
