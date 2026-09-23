using System.Reflection;
using UnityEngine;
using TicTacFade.Core;
using TicTacFade.Game;

namespace TicTacFade.PlayModeTests
{
    /// <summary>
    /// One side of an online match without a scene or UI: a GameManager
    /// (MVP config) plus an <see cref="OnlineMatch"/> on a transport. Starts
    /// the local match when the OnlineMatch asks, the way MatchFlow does.
    /// </summary>
    public sealed class OnlineTestDevice
    {
        public GameObject GameObject { get; }
        public GameManager GameManager { get; }
        public OnlineMatch Match { get; }
        public GameState State => GameManager.CurrentState;

        public OnlineTestDevice(string name, IMatchTransport transport)
        {
            GameObject = new GameObject(name);
            GameManager = GameObject.AddComponent<GameManager>();

            var config = ScriptableObject.CreateInstance<GameConfigAsset>(); // MVP defaults (GDD §9)
            typeof(GameManager)
                .GetField("config", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(GameManager, config);

            Match = new OnlineMatch(GameManager, transport);
            Match.MatchStartRequested += startingPlayer => GameManager.StartNewGame(startingPlayer);
        }

        /// <summary>A confirmed tap on this device (what BoardView's second tap does).</summary>
        public void Tap(int cellIndex) => GameManager.OnCellClicked(cellIndex);

        /// <summary>First cell where the current player can legally play.</summary>
        public int FirstLegalCell()
        {
            for (int cell = 0; cell < State.Config.CellCount; cell++)
            {
                if (RulesEngine.IsLegal(State, new Move(State.CurrentPlayer, cell), out _))
                    return cell;
            }
            return -1;
        }

        public ulong PositionKey => TicTacFade.Core.PositionKey.Compute(State.Config, State.QueueX, State.QueueO, State.CurrentPlayer).Value;

        public void Destroy()
        {
            Match.Dispose();
            Object.Destroy(GameObject);
        }
    }
}
