using System.Collections;
using System.Reflection;
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
    public class SceneWiringAndInputTests
    {
        const string ScenePath = "Assets/_Project/Scenes/TicTacFadeGame.unity";
        const float StateReadyTimeoutSeconds = 5f;

        [UnityTest]
        public IEnumerator Scene_AllSerializedReferences_AreAssigned()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);

            GameManager gameManager = null;
            yield return WaitForGameManagerReady(gm => gameManager = gm);

            var boardView = Object.FindFirstObjectByType<BoardView>();
            var gameHud = Object.FindFirstObjectByType<GameHud>();
            var cellViews = Object.FindObjectsByType<CellView>(FindObjectsSortMode.None);

            Assert.IsNotNull(boardView, "BoardView not found in scene.");
            Assert.IsNotNull(gameHud, "GameHud not found in scene.");
            Assert.AreEqual(9, cellViews.Length, "Expected 9 CellView instances.");

            AssertNoNullReferenceFields(gameManager);
            AssertNoNullReferenceFields(boardView);
            AssertNoNullReferenceFields(gameHud);
            foreach (var cellView in cellViews)
                AssertNoNullReferenceFields(cellView);
        }

        [UnityTest]
        public IEnumerator Board_TwoTapsOnSameCell_FirstDoesNotApplyMove_SecondConfirms()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);

            GameManager gameManager = null;
            yield return WaitForGameManagerReady(gm => gameManager = gm);

            var cellViews = Object.FindObjectsByType<CellView>(FindObjectsSortMode.None);
            var button = cellViews[0].GetComponent<Button>();

            Assert.IsTrue(button.interactable, "Expected an empty, interactable cell at game start.");
            int movesBeforeFirstTap = gameManager.CurrentState.TotalMoves;

            button.onClick.Invoke(); // first tap: selects/previews only
            yield return null;
            Assert.AreEqual(movesBeforeFirstTap, gameManager.CurrentState.TotalMoves,
                "The first tap must not apply a move.");

            button.onClick.Invoke(); // second tap on the same cell: confirms
            yield return null;
            Assert.AreEqual(movesBeforeFirstTap + 1, gameManager.CurrentState.TotalMoves,
                "The second tap on the same cell must confirm the move.");
        }

        [UnityTest]
        public IEnumerator GameManager_TwoFakeAutoPlayers_PlayFullMatchToCompletion()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);

            GameManager gameManager = null;
            yield return WaitForGameManagerReady(gm => gameManager = gm);

            // Neither controller is a LocalHumanPlayer: proves GameManager
            // can run a match end to end without any UI/human involvement,
            // and never branches on the concrete IPlayerController type.
            gameManager.Initialize(new FakeAutoPlayer(Occupant.X), new FakeAutoPlayer(Occupant.O));
            gameManager.StartNewGame();

            // GameManager's turn-advancing loop is synchronous (see its
            // AdvanceTurns doc comment), so the match should already be over
            // by the time StartNewGame() returns. The frame cap here is an
            // independent safety net at the test level, not a trust in that
            // implementation detail: if a future regression makes turn
            // advancing asynchronous or reintroduces a stuck loop, this
            // fails clearly instead of hanging the test runner.
            const int maxFrames = 60;
            int frames = 0;
            while (!gameManager.CurrentState.IsOver && frames < maxFrames)
            {
                yield return null;
                frames++;
            }

            Assert.IsTrue(gameManager.CurrentState.IsOver,
                $"Match did not finish within {maxFrames} frames — possible infinite loop between autonomous controllers.");

            var state = gameManager.CurrentState;
            bool validEnd = state.EndReason == GameEndReason.Win
                || state.EndReason == GameEndReason.DrawByRepetition
                || state.EndReason == GameEndReason.DrawByMoveLimit;
            Assert.IsTrue(validEnd, $"Unexpected end reason: {state.EndReason}");

            if (state.EndReason == GameEndReason.Win)
                Assert.IsNotEmpty(state.WinningLine, "A win must report a winning line.");
        }

        // Start()/StartNewGame() run a frame after the scene loads; a single
        // "yield return null" assumes one frame is always enough, which is
        // exactly the kind of timing fragility that makes a test flaky.
        // Wait actively instead, with a timeout and a clear failure message.
        static IEnumerator WaitForGameManagerReady(System.Action<GameManager> onReady)
        {
            float deadline = Time.realtimeSinceStartup + StateReadyTimeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                var gameManager = Object.FindFirstObjectByType<GameManager>();
                if (gameManager != null && gameManager.CurrentState != null)
                {
                    onReady(gameManager);
                    yield break;
                }
                yield return null;
            }

            Assert.Fail($"GameManager.CurrentState was not ready {StateReadyTimeoutSeconds}s after loading the scene.");
        }

        static void AssertNoNullReferenceFields(object target)
        {
            var fields = target.GetType().GetFields(
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            foreach (var field in fields)
            {
                bool isTrackedType = typeof(Object).IsAssignableFrom(field.FieldType) || field.FieldType.IsArray;
                if (!isTrackedType)
                    continue;

                var value = field.GetValue(target);
                if (field.FieldType.IsArray)
                {
                    var array = (System.Array)value;
                    Assert.IsNotNull(array, $"{target.GetType().Name}.{field.Name} is null.");
                    for (int i = 0; i < array.Length; i++)
                        Assert.IsNotNull(array.GetValue(i), $"{target.GetType().Name}.{field.Name}[{i}] is null.");
                }
                else
                {
                    Assert.IsNotNull(value, $"{target.GetType().Name}.{field.Name} is null.");
                }
            }
        }
    }
}
