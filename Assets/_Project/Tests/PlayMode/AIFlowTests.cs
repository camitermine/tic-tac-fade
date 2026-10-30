using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TicTacFade.Core;
using TicTacFade.Game;
using TicTacFade.UI;

namespace TicTacFade.PlayModeTests
{
    /// <summary>
    /// The vs-AI flow through the real scene and buttons (GDD §8.2): menu →
    /// level → match → result → rematch, plus every "back" path. The AI's
    /// delay runs on a manual clock injected into MatchFlow.
    /// </summary>
    public class AIFlowTests
    {
        const string ScenePath = "Assets/_Project/Scenes/TicTacFadeGame.unity";
        const float TimeoutSeconds = 5f;
        const double PastAnyDelay = 2.0; // longer than the asset's maximum AI delay

        MatchFlow _flow;
        GameManager _gameManager;
        ManualClock _clock;

        [UnityTearDown]
        public IEnumerator TearDown() => NetworkTestCleanup.DestroyAllNetworkManagers();

        [UnityTest]
        public IEnumerator FullFlow_MenuLevelMatchResultRematch()
        {
            yield return LoadSceneAndWaitForMenu();

            Press("PlayVsAIButton");
            yield return WaitForState(FlowState.AILevelSelect);
            Press("AIPerfectButton");
            yield return WaitForState(FlowState.Playing);

            Assert.IsTrue(_flow.IsVsAI);
            Assert.AreEqual(AIDifficulty.Perfect, _flow.AIDifficulty);
            Assert.IsTrue(_gameManager.IsLocalHuman(Occupant.X), "The human plays X.");
            Assert.IsFalse(_gameManager.IsLocalHuman(Occupant.O), "The AI plays O.");
            Assert.AreEqual(Occupant.X, _gameManager.CurrentState.CurrentPlayer, "The human starts the first match.");
            Assert.AreEqual("Tu turno", FindLabel("TurnLabel").text);
            Assert.AreEqual(string.Empty, FindLabel("TurnTimerLabel").text, "No turn timer against the AI.");

            yield return PlayUntilResult();
            StringAssert.IsMatch("Ganaste|Ganó la IA|Empate", FindLabel("ResultLabel").text);

            Press("RematchButton");
            yield return WaitForState(FlowState.Playing);
            Assert.AreEqual(Occupant.O, _flow.CurrentStartingPlayer, "The rematch alternates who starts.");
            Assert.AreEqual(0, _gameManager.CurrentState.TotalMoves);
            Assert.AreEqual("Turno de la IA", FindLabel("TurnLabel").text);

            yield return LetTheAIPlay();
            Assert.AreEqual(1, _gameManager.CurrentState.TotalMoves, "The AI opened the rematch.");
            Assert.AreEqual("Tu turno", FindLabel("TurnLabel").text);

            yield return PlayUntilResult();
            Press("MenuButton");
            yield return WaitForState(FlowState.Menu);
            AssertBackToLocalControllers();
        }

        [UnityTest]
        public IEnumerator LevelButtons_StartTheChosenLevel(
            [Values(AIDifficulty.Easy, AIDifficulty.Normal, AIDifficulty.Perfect)] AIDifficulty difficulty)
        {
            yield return LoadSceneAndWaitForMenu();

            Press("PlayVsAIButton");
            yield return WaitForState(FlowState.AILevelSelect);
            Press(difficulty == AIDifficulty.Easy ? "AIEasyButton" : difficulty == AIDifficulty.Normal ? "AINormalButton" : "AIPerfectButton");
            yield return WaitForState(FlowState.Playing);

            Assert.AreEqual(difficulty, _flow.AIDifficulty);
        }

        [UnityTest]
        public IEnumerator LevelScreen_Back_ReturnsToMenu()
        {
            yield return LoadSceneAndWaitForMenu();

            Press("PlayVsAIButton");
            yield return WaitForState(FlowState.AILevelSelect);
            Press("AILevelBackButton");
            yield return WaitForState(FlowState.Menu);

            Assert.IsFalse(_flow.IsVsAI);
            Assert.IsNull(_gameManager.CurrentState, "No match was started.");
        }

        [UnityTest]
        public IEnumerator UnusableAITable_ShowsErrorAndStaysInMenu([Values(false, true)] bool corrupt)
        {
            yield return LoadSceneAndWaitForMenu();
            // A missing reference, or data that isn't a table for these rules.
            var table = corrupt ? new TextAsset("not a solver table") : null;
            typeof(MatchFlow).GetField("aiTable", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_flow, table);

            LogAssert.Expect(LogType.Error, new Regex(corrupt ? "AI table can't be used" : "no AI table assigned"));
            Press("PlayVsAIButton");
            yield return null;

            Assert.AreEqual(FlowState.Menu, _flow.State, "No level screen without a usable table.");
            Assert.AreEqual(SessionFailure.AIUnavailable, _flow.LastFailure);
            Assert.AreEqual(SessionFailureMessages.ToText(SessionFailure.AIUnavailable), FindLabel("MenuStatus").text,
                "The player sees why, in the menu's error line.");

            Press("PlayLocalButton");
            yield return WaitForState(FlowState.Playing);
            Assert.AreEqual(SessionFailure.None, _flow.LastFailure, "The error clears like any other on the next screen.");
        }

        [UnityTest]
        public IEnumerator ExitWhileAIThinks_CancelsItsMove_AndLocalPlayWorksAfterwards()
        {
            yield return LoadSceneAndWaitForMenu();
            Press("PlayVsAIButton");
            yield return WaitForState(FlowState.AILevelSelect);
            Press("AINormalButton");
            yield return WaitForState(FlowState.Playing);

            yield return ConfirmCell(_gameManager.CurrentState.GetFreeCellIndices()[0]);
            Assert.AreEqual(Occupant.O, _gameManager.CurrentState.CurrentPlayer, "Test setup: the AI is thinking.");

            Press("ExitButton");
            yield return WaitForState(FlowState.Menu);
            yield return LetTheAIPlay();
            Assert.IsNull(_gameManager.CurrentState, "The cancelled AI move never reached a match.");
            AssertBackToLocalControllers();

            Press("PlayLocalButton");
            yield return WaitForState(FlowState.Playing);
            Assert.IsTrue(_gameManager.IsLocalHuman(Occupant.O), "Local play is two humans again.");
            Assert.AreEqual(Occupant.X, _flow.CurrentStartingPlayer, "Back from the menu, X starts (GDD §3.1).");
        }

        // Human: the first free cell, through the real board buttons. AI:
        // the manual clock moves past its delay.
        IEnumerator PlayUntilResult()
        {
            for (int step = 0; step < 200 && _flow.State == FlowState.Playing; step++)
            {
                var state = _gameManager.CurrentState;
                if (_gameManager.IsLocalHuman(state.CurrentPlayer))
                {
                    Assert.IsTrue(_gameManager.CanAcceptLocalInput, "The board takes taps on the human's turn.");
                    yield return ConfirmCell(state.GetFreeCellIndices()[0]);
                }
                else
                {
                    Assert.IsFalse(_gameManager.CanAcceptLocalInput, "No taps during the AI's turn.");
                    Assert.AreEqual("Turno de la IA", FindLabel("TurnLabel").text);
                    yield return LetTheAIPlay();
                }
            }
            yield return WaitForState(FlowState.Result);
        }

        IEnumerator LetTheAIPlay()
        {
            _clock.Advance(PastAnyDelay);
            yield return null; // MatchFlow.Update ticks the AI
            yield return null;
        }

        void AssertBackToLocalControllers()
        {
            Assert.IsFalse(_flow.IsVsAI);
            Assert.IsTrue(_gameManager.IsLocalHuman(Occupant.X) && _gameManager.IsLocalHuman(Occupant.O),
                "Leaving the AI match puts the two local controllers back.");
        }

        IEnumerator LoadSceneAndWaitForMenu()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);

            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            _flow = null;
            while (_flow == null && Time.realtimeSinceStartup < deadline)
            {
                _flow = Object.FindAnyObjectByType<MatchFlow>();
                yield return null;
            }
            Assert.IsNotNull(_flow, "MatchFlow not found after loading the scene.");
            yield return null; // let every Start (MatchFlow's first StateChanged included) run

            _gameManager = Object.FindAnyObjectByType<GameManager>();
            _clock = new ManualClock();
            _flow.SetTimeSources(_clock, new FixedRandomSource(0));
            Assert.AreEqual(FlowState.Menu, _flow.State);
        }

        IEnumerator WaitForState(FlowState expected)
        {
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (_flow.State != expected && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(expected, _flow.State, $"Flow did not reach {expected} within {TimeoutSeconds}s.");
        }

        static IEnumerator ConfirmCell(int index)
        {
            var button = Find<Button>($"Cell_{index}");
            button.onClick.Invoke(); // first tap: preview
            yield return null;
            button.onClick.Invoke(); // second tap: confirm
            yield return null;
        }

        // Scene objects are looked up by the names the scene builder assigns:
        // a string lookup, acceptable in a test (not in game code).
        static void Press(string name) => Find<Button>(name).onClick.Invoke();

        static Text FindLabel(string name) => Find<Text>(name);

        static T Find<T>(string name) where T : Component
        {
            var go = GameObject.Find(name);
            Assert.IsNotNull(go, $"'{name}' not found in scene.");
            var component = go.GetComponent<T>();
            Assert.IsNotNull(component, $"'{name}' has no {typeof(T).Name}.");
            return component;
        }
    }
}
