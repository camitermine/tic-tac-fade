using System;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TicTacFade.Core;
using TicTacFade.Game;

namespace TicTacFade.PlayModeTests
{
    /// <summary>
    /// AIPlayer inside a real GameManager: the delay never blocks or breaks
    /// the turn loop, and a pending move is dropped whenever the match stops.
    /// Time only moves through a manual clock.
    /// </summary>
    public class AIPlayerTests
    {
        const float Delay = 1f;

        GameObject _gameObject;
        GameManager _gameManager;
        ManualClock _clock;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("AIPlayerTests");
            _gameManager = _gameObject.AddComponent<GameManager>();
            var config = ScriptableObject.CreateInstance<GameConfigAsset>(); // MVP defaults (GDD §9)
            typeof(GameManager)
                .GetField("config", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_gameManager, config);
            _clock = new ManualClock();
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.Destroy(_gameObject);

        AIPlayer CreateAI(Occupant player, float delay = Delay, int seed = 0) =>
            new AIPlayer(player, AIDifficulty.Perfect, AITestSupport.Table, AITestSupport.FixedDelay(delay), _clock,
                new SystemRandomSource(seed));

        GameState State => _gameManager.CurrentState;

        int FirstFreeCell() => State.GetFreeCellIndices()[0];

        [Test]
        public void DelayedAI_TurnLoopContinuesAfterLateMove()
        {
            var ai = CreateAI(Occupant.O);
            _gameManager.Initialize(new LocalHumanPlayer(Occupant.X), ai);
            _gameManager.StartNewGame(Occupant.X);

            _gameManager.OnCellClicked(0);
            Assert.AreEqual(1, State.TotalMoves);
            Assert.IsTrue(ai.HasPendingMove);
            Assert.IsFalse(_gameManager.CanAcceptLocalInput, "No taps during the AI's turn.");

            _clock.Advance(Delay / 2);
            ai.Tick();
            Assert.AreEqual(1, State.TotalMoves, "The AI waits for its delay.");

            _clock.Advance(Delay / 2);
            ai.Tick();
            Assert.AreEqual(2, State.TotalMoves, "The AI played once the delay passed.");
            Assert.AreEqual(Occupant.X, State.CurrentPlayer);
            Assert.IsTrue(_gameManager.CanAcceptLocalInput, "The human's turn after the late move.");

            _gameManager.OnCellClicked(FirstFreeCell());
            Assert.AreEqual(3, State.TotalMoves, "The turn loop keeps going.");
            Assert.IsTrue(ai.HasPendingMove);
        }

        [Test]
        public void TwoDelayedPerfectAIs_PlayTheWholeMatch_StarterWinsIn13()
        {
            var x = CreateAI(Occupant.X, seed: 1);
            var o = CreateAI(Occupant.O, seed: 2);
            _gameManager.Initialize(x, o);
            _gameManager.StartNewGame(Occupant.O);

            for (int step = 0; step < 100 && !State.IsOver; step++)
            {
                Assert.IsTrue(x.HasPendingMove || o.HasPendingMove, "Someone is always thinking during the match.");
                _clock.Advance(Delay);
                x.Tick();
                o.Tick();
            }

            Assert.IsTrue(State.IsOver);
            Assert.AreEqual(Occupant.O, State.Winner);
            Assert.AreEqual(13, State.TotalMoves);
            Assert.IsFalse(x.HasPendingMove || o.HasPendingMove);
        }

        [Test]
        public void AIWithoutDelay_PlaysSynchronously()
        {
            var ai = CreateAI(Occupant.X, delay: 0f);
            _gameManager.Initialize(ai, new LocalHumanPlayer(Occupant.O));

            _gameManager.StartNewGame(Occupant.X);

            Assert.AreEqual(1, State.TotalMoves, "Zero delay plays inside StartNewGame, like any autonomous controller.");
            Assert.IsFalse(ai.HasPendingMove);
        }

        [Test]
        public void DiscardMatch_CancelsPendingAIMove()
        {
            var ai = StartWithAIThinking();

            _gameManager.DiscardMatch();
            Assert.IsFalse(ai.HasPendingMove);

            _clock.Advance(Delay * 2);
            Assert.DoesNotThrow(ai.Tick);
            Assert.IsNull(State, "Nothing was played into the discarded match.");
        }

        [Test]
        public void StartNewGame_CancelsPendingAIMove()
        {
            var ai = StartWithAIThinking();

            _gameManager.StartNewGame(Occupant.X);
            Assert.IsFalse(ai.HasPendingMove);

            _clock.Advance(Delay * 2);
            ai.Tick();
            Assert.AreEqual(0, State.TotalMoves, "The old match's move never reaches the new one.");
        }

        [Test]
        public void Halt_CancelsPendingAIMove()
        {
            var ai = StartWithAIThinking();

            _gameManager.Halt();
            Assert.IsFalse(ai.HasPendingMove);

            _clock.Advance(Delay * 2);
            ai.Tick();
            Assert.AreEqual(1, State.TotalMoves);
        }

        [Test]
        public void Initialize_ReplacingControllers_CancelsPendingAIMove()
        {
            var ai = StartWithAIThinking();

            _gameManager.Initialize(new LocalHumanPlayer(Occupant.X), new LocalHumanPlayer(Occupant.O));

            Assert.IsFalse(ai.HasPendingMove);
        }

        [Test]
        public void GameEnd_NotifiesBothControllersStopped()
        {
            var x = new ManualPlayer(Occupant.X);
            var o = new ManualPlayer(Occupant.O);
            _gameManager.Initialize(x, o);
            _gameManager.StartNewGame(Occupant.X);
            int stopsAtStart = x.StopCount;

            // X wins on the top row.
            x.Play(0);
            o.Play(3);
            x.Play(1);
            o.Play(4);
            x.Play(2);

            Assert.AreEqual(Occupant.X, State.Winner);
            Assert.AreEqual(stopsAtStart + 1, x.StopCount);
            Assert.AreEqual(stopsAtStart + 1, o.StopCount);
        }

        [Test]
        public void LateMove_WithNoMatchInProgress_IsIgnored()
        {
            var x = new ManualPlayer(Occupant.X);
            _gameManager.Initialize(x, new LocalHumanPlayer(Occupant.O));
            _gameManager.StartNewGame(Occupant.X);
            _gameManager.DiscardMatch();

            LogAssert.Expect(LogType.Warning, new Regex("no match in progress"));
            Assert.DoesNotThrow(() => x.Play(0));
            Assert.IsNull(State);
        }

        // Human X played 0; the AI (O) is waiting for its delay.
        AIPlayer StartWithAIThinking()
        {
            var ai = CreateAI(Occupant.O);
            _gameManager.Initialize(new LocalHumanPlayer(Occupant.X), ai);
            _gameManager.StartNewGame(Occupant.X);
            _gameManager.OnCellClicked(0);
            Assert.IsTrue(ai.HasPendingMove, "Test setup.");
            return ai;
        }

        /// <summary>Plays only when the test says so, at any time, and counts match stops.</summary>
        sealed class ManualPlayer : IPlayerController
        {
            public Occupant Player { get; }
            public bool IsLocalHuman => false;
            public bool AcceptsLocalInput => false;
            public int StopCount { get; private set; }
            public event Action<Move> MoveChosen;
            public event Action AcceptsLocalInputChanged { add { } remove { } }

            public ManualPlayer(Occupant player) => Player = player;

            public void Play(int cell) => MoveChosen?.Invoke(new Move(Player, cell));

            public void NotifyTurnStarted(GameState state) { }
            public void NotifyMatchStopped() => StopCount++;
            public void NotifyCellSelected(int cellIndex) { }
        }
    }
}
