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
        const float ReadyTimeoutSeconds = 5f;

        // X at 0,1,2 (top row) and O at 3,4: the starting player wins with
        // the 5th move whoever that is, since each player places on its own
        // turn in the given order.
        static readonly int[] StarterWinsSequence = { 0, 3, 1, 4, 2 };

        [UnityTest]
        public IEnumerator Scene_AllSerializedReferences_AreAssigned()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);

            MatchFlow flow = null;
            yield return WaitForMenu(f => flow = f);

            var gameManager = Object.FindFirstObjectByType<GameManager>();
            var boardView = Object.FindFirstObjectByType<BoardView>();
            var gameHud = Object.FindFirstObjectByType<GameHud>();
            var menuScreen = Object.FindFirstObjectByType<MenuScreen>();
            var resultScreen = Object.FindFirstObjectByType<ResultScreen>();
            var exitMatchButton = Object.FindFirstObjectByType<ExitMatchButton>();
            var cellViews = Object.FindObjectsByType<CellView>(FindObjectsSortMode.None);
            var visibilities = Object.FindObjectsByType<FlowScreenVisibility>(FindObjectsSortMode.None);

            Assert.IsNotNull(gameManager, "GameManager not found in scene.");
            Assert.IsNotNull(boardView, "BoardView not found in scene.");
            Assert.IsNotNull(gameHud, "GameHud not found in scene.");
            Assert.IsNotNull(menuScreen, "MenuScreen not found in scene.");
            Assert.IsNotNull(resultScreen, "ResultScreen not found in scene.");
            Assert.IsNotNull(exitMatchButton, "ExitMatchButton not found in scene.");
            Assert.AreEqual(9, cellViews.Length, "Expected 9 CellView instances.");
            Assert.AreEqual(4, visibilities.Length, "Expected 4 FlowScreenVisibility (3 screens + exit button).");

            AssertNoNullReferenceFields(gameManager);
            AssertNoNullReferenceFields(flow);
            AssertNoNullReferenceFields(boardView);
            AssertNoNullReferenceFields(gameHud);
            AssertNoNullReferenceFields(menuScreen);
            AssertNoNullReferenceFields(resultScreen);
            AssertNoNullReferenceFields(exitMatchButton);
            foreach (var visibility in visibilities)
                AssertNoNullReferenceFields(visibility);
            foreach (var cellView in cellViews)
                AssertNoNullReferenceFields(cellView);
        }

        [UnityTest]
        public IEnumerator Board_TwoTapsOnSameCell_FirstDoesNotApplyMove_SecondConfirms()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);

            MatchFlow flow = null;
            yield return WaitForMenu(f => flow = f);
            yield return PressAndWaitFor(flow, "PlayLocalButton", FlowState.Playing);

            var gameManager = Object.FindFirstObjectByType<GameManager>();
            var button = FindCellButton(0);

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

            MatchFlow flow = null;
            yield return WaitForMenu(f => flow = f);
            var gameManager = Object.FindFirstObjectByType<GameManager>();

            // Neither controller is a LocalHumanPlayer: proves GameManager
            // can run a match end to end without any UI/human involvement,
            // and never branches on the concrete IPlayerController type.
            gameManager.Initialize(new FakeAutoPlayer(Occupant.X), new FakeAutoPlayer(Occupant.O));
            PressButton("PlayLocalButton");

            // GameManager's turn-advancing loop is synchronous (see its
            // AdvanceTurns doc comment), so the match should already be over
            // by the time the button press returns. The frame cap here is an
            // independent safety net at the test level, not a trust in that
            // implementation detail: if a future regression makes turn
            // advancing asynchronous or reintroduces a stuck loop, this
            // fails clearly instead of hanging the test runner.
            const int maxFrames = 60;
            int frames = 0;
            while ((gameManager.CurrentState == null || !gameManager.CurrentState.IsOver) && frames < maxFrames)
            {
                yield return null;
                frames++;
            }

            Assert.IsNotNull(gameManager.CurrentState, "Pressing 'Jugar local' did not start a match.");
            Assert.IsTrue(gameManager.CurrentState.IsOver,
                $"Match did not finish within {maxFrames} frames — possible infinite loop between autonomous controllers.");

            var state = gameManager.CurrentState;
            bool validEnd = state.EndReason == GameEndReason.Win
                || state.EndReason == GameEndReason.DrawByRepetition
                || state.EndReason == GameEndReason.DrawByMoveLimit;
            Assert.IsTrue(validEnd, $"Unexpected end reason: {state.EndReason}");

            if (state.EndReason == GameEndReason.Win)
                Assert.IsNotEmpty(state.WinningLine, "A win must report a winning line.");

            // The match ended synchronously inside PlayLocal: the flow must
            // still land in Result (Playing is set before the match starts).
            Assert.AreEqual(FlowState.Result, flow.State, "The flow must reach Result after a synchronous match.");
        }

        [UnityTest]
        public IEnumerator Flow_MenuPlayRematchMenuExit_StartingPlayerAlternatesAndResetsOnMenu()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);

            MatchFlow flow = null;
            yield return WaitForMenu(f => flow = f);
            var gameManager = Object.FindFirstObjectByType<GameManager>();

            // 1. Menu, never played: only the menu is visible, there is no
            // match state, and a stray tap on a cell is harmless.
            Assert.IsTrue(IsScreenVisible("MenuScreen"), "The menu must be visible at startup.");
            Assert.IsFalse(IsScreenVisible("GameScreen"), "The game screen must be hidden at startup.");
            Assert.IsFalse(IsScreenVisible("ResultScreen"), "The result screen must be hidden at startup.");
            Assert.IsNull(gameManager.CurrentState, "There must be no match state before the first match.");

            var strayCell = FindCellButton(0);
            strayCell.onClick.Invoke();
            strayCell.onClick.Invoke();
            for (int i = 0; i < 3; i++)
                yield return null;
            Assert.AreEqual(FlowState.Menu, flow.State, "A stray tap in the menu must not change the flow.");
            Assert.IsNull(gameManager.CurrentState, "A stray tap in the menu must not create a match.");
            LogAssert.NoUnexpectedReceived();

            // 2. First match from the menu: X starts (GDD §3.1).
            yield return PressAndWaitFor(flow, "PlayLocalButton", FlowState.Playing);
            AssertFreshMatch(gameManager, Occupant.X, "first match from the menu");
            Assert.IsTrue(IsScreenVisible("GameScreen"), "The game screen must be visible while playing.");
            Assert.IsFalse(IsScreenVisible("MenuScreen"), "The menu must be hidden while playing.");

            // 3. The starter (X) wins → Result.
            yield return PlayMoves(StarterWinsSequence);
            yield return WaitForFlowState(flow, FlowState.Result);
            Assert.AreEqual(Occupant.X, gameManager.CurrentState.Winner, "X should have won the first match.");
            Assert.IsTrue(IsScreenVisible("ResultScreen"), "The result screen must be visible after the match ends.");
            Assert.IsTrue(IsScreenVisible("GameScreen"), "The board must stay visible under the result panel.");
            Assert.IsNotEmpty(FindText("ResultLabel").text, "The result label must describe the outcome.");

            // 4. Rematch: the starting player is inverted.
            yield return PressAndWaitFor(flow, "RematchButton", FlowState.Playing);
            AssertFreshMatch(gameManager, Occupant.O, "rematch");

            // 5. The starter (now O) wins.
            yield return PlayMoves(StarterWinsSequence);
            yield return WaitForFlowState(flow, FlowState.Result);
            Assert.AreEqual(Occupant.O, gameManager.CurrentState.Winner, "O should have won the rematch.");

            // 6-7. Back to the menu, and a new match starts with X again.
            yield return PressAndWaitFor(flow, "MenuButton", FlowState.Menu);
            yield return PressAndWaitFor(flow, "PlayLocalButton", FlowState.Playing);
            AssertFreshMatch(gameManager, Occupant.X, "new match from the menu after a rematch");

            // 8. Leave a match halfway through.
            yield return PlayMoves(new[] { 0, 3 });
            Assert.AreEqual(2, gameManager.CurrentState.TotalMoves, "Two moves should have been applied before leaving.");
            yield return PressAndWaitFor(flow, "ExitButton", FlowState.Menu);
            Assert.IsNull(gameManager.CurrentState, "Leaving a match must discard it.");
            Assert.IsTrue(IsScreenVisible("MenuScreen"), "The menu must be visible after leaving a match.");
            LogAssert.NoUnexpectedReceived();

            // 9. A new match starts from scratch, with X.
            yield return PressAndWaitFor(flow, "PlayLocalButton", FlowState.Playing);
            AssertFreshMatch(gameManager, Occupant.X, "new match after leaving one");
        }

        static void AssertFreshMatch(GameManager gameManager, Occupant expectedStarter, string context)
        {
            Assert.IsNotNull(gameManager.CurrentState, $"No match state after {context}.");
            Assert.AreEqual(0, gameManager.CurrentState.TotalMoves, $"The {context} must start with an empty board.");
            Assert.AreEqual(expectedStarter, gameManager.CurrentState.CurrentPlayer, $"Wrong starting player for the {context}.");
        }

        // Each move is a real two-tap confirmation on the cell's button.
        static IEnumerator PlayMoves(int[] cells)
        {
            foreach (var cell in cells)
            {
                var button = FindCellButton(cell);
                button.onClick.Invoke();
                yield return null;
                button.onClick.Invoke();
                yield return null;
            }
        }

        static IEnumerator PressAndWaitFor(MatchFlow flow, string buttonName, FlowState expected)
        {
            PressButton(buttonName);
            yield return WaitForFlowState(flow, expected);
        }

        // Buttons are looked up by the GameObject names the scene builder
        // assigns: a string lookup, acceptable in a test (not in game code).
        static void PressButton(string buttonName)
        {
            var go = GameObject.Find(buttonName);
            Assert.IsNotNull(go, $"Button '{buttonName}' not found in scene.");
            var button = go.GetComponent<Button>();
            Assert.IsNotNull(button, $"'{buttonName}' has no Button component.");
            button.onClick.Invoke();
        }

        static Button FindCellButton(int index)
        {
            var go = GameObject.Find($"Cell_{index}");
            Assert.IsNotNull(go, $"Cell_{index} not found in scene.");
            return go.GetComponent<Button>();
        }

        static Text FindText(string name)
        {
            var go = GameObject.Find(name);
            Assert.IsNotNull(go, $"'{name}' not found in scene.");
            return go.GetComponent<Text>();
        }

        static bool IsScreenVisible(string screenName)
        {
            var go = GameObject.Find(screenName);
            Assert.IsNotNull(go, $"Screen '{screenName}' not found in scene.");
            return go.GetComponent<FlowScreenVisibility>().IsVisible;
        }

        static IEnumerator WaitForFlowState(MatchFlow flow, FlowState expected)
        {
            float deadline = Time.realtimeSinceStartup + ReadyTimeoutSeconds;
            while (flow.State != expected && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(expected, flow.State, $"Flow did not reach {expected} within {ReadyTimeoutSeconds}s.");
        }

        // The flow emits its first state in Start, a frame after the scene
        // loads; a single "yield return null" assumes one frame is always
        // enough, which is exactly the kind of timing fragility that makes a
        // test flaky. Wait actively instead, with a timeout and a clear
        // failure message.
        static IEnumerator WaitForMenu(System.Action<MatchFlow> onReady)
        {
            float deadline = Time.realtimeSinceStartup + ReadyTimeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                var flow = Object.FindFirstObjectByType<MatchFlow>();
                if (flow != null && flow.State == FlowState.Menu)
                {
                    // One extra frame so every Start (including MatchFlow's
                    // first StateChanged) has run.
                    yield return null;
                    onReady(flow);
                    yield break;
                }
                yield return null;
            }

            Assert.Fail($"MatchFlow was not in Menu {ReadyTimeoutSeconds}s after loading the scene.");
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
