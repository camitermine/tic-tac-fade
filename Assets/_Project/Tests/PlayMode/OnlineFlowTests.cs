using System.Collections;
using NUnit.Framework;
using Unity.Services.Core;
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
    /// Online room flow (create / join by code / cancel / errors) driven
    /// through the real screens and buttons, with a network-free
    /// <see cref="FakeSessionService"/> injected into <see cref="MatchFlow"/>.
    /// The real SDK path is verified manually with Multiplayer Play Mode
    /// (steps in docs/ai-log.md).
    /// </summary>
    public class OnlineFlowTests
    {
        const string ScenePath = "Assets/_Project/Scenes/TicTacFadeGame.unity";
        const float TimeoutSeconds = 5f;

        MatchFlow _flow;
        FakeSessionService _session;

        [UnityTearDown]
        public IEnumerator TearDown() => NetworkTestCleanup.DestroyAllNetworkManagers();

        [UnityTest]
        public IEnumerator LocalPlay_FullRound_NeverInitializesUnityServices()
        {
            // No fake injected: the scene's real UgsSessionService is in
            // place, and local play must never reach it.
            yield return LoadSceneAndWaitForMenu(injectFake: false);

            Press("PlayLocalButton");
            yield return WaitForState(FlowState.Playing);
            foreach (var cell in new[] { 0, 3, 1, 4, 2 })
                yield return ConfirmCell(cell);
            yield return WaitForState(FlowState.Result);
            Press("RematchButton");
            yield return WaitForState(FlowState.Playing);
            Press("ExitButton");
            yield return WaitForState(FlowState.Menu);

            Assert.AreEqual(ServicesInitializationState.Uninitialized, UnityServices.State,
                "Local play must never initialize Unity Services.");
        }

        [UnityTest]
        public IEnumerator CreateRoom_DoubleTapWhileConnecting_CreatesOneRoomAndReachesLobby()
        {
            yield return LoadSceneAndWaitForMenu();
            _session.HoldOperations = true;

            Press("CreateRoomButton");
            Press("CreateRoomButton");
            yield return null;

            Assert.AreEqual(1, _session.CreateCalls, "A double tap must create only one room.");
            Assert.IsTrue(_flow.IsBusy, "The flow must be busy while the room is being created.");
            Assert.IsFalse(FindButton("CreateRoomButton").interactable, "Menu buttons must be disabled while connecting.");
            Assert.IsFalse(FindButton("PlayLocalButton").interactable, "Menu buttons must be disabled while connecting.");
            Assert.AreEqual(SessionFailureMessages.Connecting, FindLabel("MenuStatus").text);

            _session.CompletePending(SessionResult.Ok());
            yield return WaitForState(FlowState.Lobby);

            Assert.IsFalse(_flow.IsBusy);
            Assert.AreEqual(FakeSessionService.FakeCode, FindLabel("CodeLabel").text, "The waiting room must show the join code.");
            Assert.AreEqual("Esperando rival...", FindLabel("LobbyStatus").text);
        }

        [UnityTest]
        public IEnumerator CreateRoom_NetworkFailure_StaysInMenuWithReadableMessage()
        {
            yield return LoadSceneAndWaitForMenu();
            _session.NextResult = SessionResult.Fail(SessionFailure.Timeout);

            Press("CreateRoomButton");
            yield return null;

            Assert.AreEqual(FlowState.Menu, _flow.State);
            Assert.IsFalse(_flow.IsBusy, "A failure must not leave the flow busy.");
            Assert.IsTrue(FindButton("CreateRoomButton").interactable, "Buttons must be usable again after a failure.");
            Assert.AreEqual(SessionFailureMessages.ToText(SessionFailure.Timeout), FindLabel("MenuStatus").text);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator JoinByCode_EmptyCode_RejectedLocallyWithoutNetwork()
        {
            yield return LoadSceneAndWaitForMenu();
            Press("JoinByCodeButton");
            yield return WaitForState(FlowState.JoinByCode);

            FindInput("CodeInput").text = "   ";
            Press("JoinButton");
            yield return null;

            Assert.AreEqual(0, _session.JoinCalls, "An empty code must not reach the network.");
            Assert.AreEqual(FlowState.JoinByCode, _flow.State, "An invalid code must not leave the screen.");
            Assert.AreEqual(SessionFailureMessages.ToText(SessionFailure.InvalidCode), FindLabel("JoinStatus").text);
        }

        [UnityTest]
        public IEnumerator JoinByCode_CharacterOutsideAlphabet_RejectedLocallyAsInvalidCode()
        {
            yield return LoadSceneAndWaitForMenu();
            Press("JoinByCodeButton");
            yield return WaitForState(FlowState.JoinByCode);

            // 'Z' is alphanumeric but not in the join-code alphabet: the
            // real service rejected exactly this code.
            FindInput("CodeInput").text = "ZZZZZZ";
            Press("JoinButton");
            yield return null;

            Assert.AreEqual(0, _session.JoinCalls, "A code outside the alphabet must not reach the network.");
            Assert.AreEqual(FlowState.JoinByCode, _flow.State, "An invalid code must not leave the screen.");
            Assert.AreEqual(SessionFailure.InvalidCode, _flow.LastFailure);
            Assert.AreEqual(SessionFailureMessages.ToText(SessionFailure.InvalidCode), FindLabel("JoinStatus").text);
        }

        [UnityTest]
        public IEnumerator JoinByCode_CodeRejectedByService_StaysOnScreenWithError()
        {
            yield return LoadSceneAndWaitForMenu();
            Press("JoinByCodeButton");
            yield return WaitForState(FlowState.JoinByCode);
            _session.NextResult = SessionResult.Fail(SessionFailure.SessionFull);

            FindInput("CodeInput").text = " bcd789 ";
            Press("JoinButton");
            yield return null;

            Assert.AreEqual(1, _session.JoinCalls);
            Assert.AreEqual("BCD789", _session.LastJoinCode, "The code must be sent trimmed and upper-cased.");
            Assert.AreEqual(FlowState.JoinByCode, _flow.State, "A rejected code must not leave the screen.");
            Assert.AreEqual(SessionFailureMessages.ToText(SessionFailure.SessionFull), FindLabel("JoinStatus").text);
            Assert.IsTrue(FindButton("JoinButton").interactable, "The player must be able to retry.");
        }

        [UnityTest]
        public IEnumerator JoinByCode_Success_ShowsOpponentConnectedInLobby()
        {
            yield return LoadSceneAndWaitForMenu();
            Press("JoinByCodeButton");
            yield return WaitForState(FlowState.JoinByCode);

            FindInput("CodeInput").text = FakeSessionService.FakeCode;
            Press("JoinButton");
            yield return WaitForState(FlowState.Lobby);

            Assert.AreEqual("Rival conectado", FindLabel("LobbyStatus").text, "The host is already in the room when the join succeeds.");
        }

        [UnityTest]
        public IEnumerator Lobby_OpponentConnectsAndLeaves_StatusFollows()
        {
            yield return LoadSceneAndWaitForMenu();
            Press("CreateRoomButton");
            yield return WaitForState(FlowState.Lobby);

            _session.RaiseOpponentConnected();
            yield return null;
            Assert.AreEqual("Rival conectado", FindLabel("LobbyStatus").text);

            _session.RaiseOpponentLeft();
            yield return null;
            Assert.AreEqual("Esperando rival...", FindLabel("LobbyStatus").text);
        }

        [UnityTest]
        public IEnumerator JoinByCode_Cancel_ReturnsToMenu()
        {
            yield return LoadSceneAndWaitForMenu();
            Press("JoinByCodeButton");
            yield return WaitForState(FlowState.JoinByCode);

            Press("JoinCancelButton");
            yield return WaitForState(FlowState.Menu);

            Assert.AreEqual(0, _session.JoinCalls);
            Assert.AreEqual(0, _session.LeaveCalls, "Nothing to leave: no room was joined.");
        }

        [UnityTest]
        public IEnumerator Lobby_CancelTwiceInARow_LeavesEachRoomAndLocalPlayStillWorks()
        {
            yield return LoadSceneAndWaitForMenu();

            for (int round = 1; round <= 2; round++)
            {
                Press("CreateRoomButton");
                yield return WaitForState(FlowState.Lobby);
                Press("LobbyCancelButton");
                yield return WaitForState(FlowState.Menu);
                Assert.AreEqual(round, _session.LeaveCalls, $"Cancel #{round} must leave the room.");
            }

            for (int round = 1; round <= 2; round++)
            {
                Press("JoinByCodeButton");
                yield return WaitForState(FlowState.JoinByCode);
                FindInput("CodeInput").text = FakeSessionService.FakeCode;
                Press("JoinButton");
                yield return WaitForState(FlowState.Lobby);
                Press("LobbyCancelButton");
                yield return WaitForState(FlowState.Menu);
            }

            Assert.AreEqual(2, _session.CreateCalls);
            Assert.AreEqual(2, _session.JoinCalls);
            Assert.AreEqual(4, _session.LeaveCalls);

            Press("PlayLocalButton");
            yield return WaitForState(FlowState.Playing);
            var gameManager = Object.FindAnyObjectByType<GameManager>();
            Assert.AreEqual(Occupant.X, gameManager.CurrentState.CurrentPlayer, "Local play after leaving rooms starts with X.");
            Assert.AreEqual(0, gameManager.CurrentState.TotalMoves);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Lobby_SessionLost_ReturnsToMenuWithMessage()
        {
            yield return LoadSceneAndWaitForMenu();
            Press("JoinByCodeButton");
            yield return WaitForState(FlowState.JoinByCode);
            FindInput("CodeInput").text = FakeSessionService.FakeCode;
            Press("JoinButton");
            yield return WaitForState(FlowState.Lobby);

            _session.RaiseSessionLost();
            yield return WaitForState(FlowState.Menu);

            Assert.AreEqual(SessionFailureMessages.ToText(SessionFailure.SessionClosed), FindLabel("MenuStatus").text);
            LogAssert.NoUnexpectedReceived();
        }

        IEnumerator LoadSceneAndWaitForMenu(bool injectFake = true)
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);

            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            _flow = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                _flow = Object.FindAnyObjectByType<MatchFlow>();
                if (_flow != null)
                    break;
                yield return null;
            }
            Assert.IsNotNull(_flow, "MatchFlow not found after loading the scene.");
            yield return null; // let every Start (MatchFlow's first StateChanged included) run

            if (injectFake)
            {
                _session = new GameObject("FakeSessionService").AddComponent<FakeSessionService>();
                _flow.SetSessionService(_session);
            }
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
            var button = FindButton($"Cell_{index}");
            button.onClick.Invoke();
            yield return null;
            button.onClick.Invoke();
            yield return null;
        }

        // Scene objects are looked up by the names the scene builder assigns:
        // a string lookup, acceptable in a test (not in game code).
        static void Press(string name) => FindButton(name).onClick.Invoke();

        static Button FindButton(string name) => Find<Button>(name);

        static Text FindLabel(string name) => Find<Text>(name);

        static InputField FindInput(string name) => Find<InputField>(name);

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
