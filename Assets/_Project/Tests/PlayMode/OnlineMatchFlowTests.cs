using System.Collections;
using System.Linq;
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
    /// Online match through the real scene (screens, board buttons, HUD) on
    /// one side, with the other device simulated by an
    /// <see cref="OnlineTestDevice"/> over an in-memory transport. The
    /// session is the network-free <see cref="FakeSessionService"/>.
    /// </summary>
    public class OnlineMatchFlowTests
    {
        const string ScenePath = "Assets/_Project/Scenes/TicTacFadeGame.unity";
        const float TimeoutSeconds = 5f;

        MatchFlow _flow;
        GameManager _gameManager;
        FakeSessionService _session;
        InMemoryMatchTransport _hostTransport;
        InMemoryMatchTransport _clientTransport;
        OnlineTestDevice _remote;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _remote?.Destroy();
            _remote = null;
            yield return NetworkTestCleanup.DestroyAllNetworkManagers();
        }

        [UnityTest]
        public IEnumerator Host_PlaysRematchesAndSeesOpponentLeave_ThroughTheScreens()
        {
            yield return LoadSceneAsHost();

            Assert.AreEqual(FlowState.Playing, _flow.State, "The match must start when the client connects.");
            Assert.AreEqual(Occupant.X, _gameManager.CurrentState.CurrentPlayer);
            Assert.AreEqual("Tu turno", FindLabel("TurnLabel").text, "The host plays X, which starts.");
            Assert.IsTrue(FindButton("Cell_1").interactable, "The board takes input on your turn.");

            yield return ConfirmCell(0);
            Pump();
            Assert.AreEqual("Turno del rival", FindLabel("TurnLabel").text);
            Assert.IsFalse(FindButton("Cell_1").interactable, "The board must not take input on the opponent's turn.");

            // X wins on the top row: X 0,1,2 / O 3,4.
            _remote.Tap(3); Pump();
            Assert.AreEqual("Tu turno", FindLabel("TurnLabel").text);
            yield return ConfirmCell(1); Pump();
            _remote.Tap(4); Pump();
            yield return ConfirmCell(2); Pump();
            yield return WaitForState(FlowState.Result);
            Assert.AreEqual(Occupant.X, _gameManager.CurrentState.Winner);
            Assert.AreEqual(Occupant.X, _remote.State.Winner, "Both devices must reach the same result.");

            // Rematch needs both: the host asks first and waits.
            Press("RematchButton");
            yield return null;
            Assert.AreEqual(FlowState.Result, _flow.State, "One request alone must not start the rematch.");
            Assert.AreEqual("Esperando al rival...", FindLabel("RematchStatus").text);
            Assert.IsFalse(FindButton("RematchButton").interactable);

            _remote.Match.RequestRematch();
            Pump();
            yield return WaitForState(FlowState.Playing);
            Assert.AreEqual(Occupant.O, _gameManager.CurrentState.CurrentPlayer, "The rematch starts with O.");
            Assert.AreEqual("Turno del rival", FindLabel("TurnLabel").text, "The host is still X.");
            Assert.AreEqual(string.Empty, FindLabel("RematchStatus").text);

            // The client leaves in the middle of the rematch.
            _session.RaiseOpponentLeft();
            yield return WaitForState(FlowState.Menu);
            Assert.AreEqual(SessionFailureMessages.ToText(SessionFailure.OpponentLeft), FindLabel("MenuStatus").text);
            Assert.AreEqual(1, _session.LeaveCalls, "The host must close the session.");

            // Local play is back to normal: both sides on this device.
            Press("PlayLocalButton");
            yield return WaitForState(FlowState.Playing);
            Assert.AreEqual("Turno: Jugador X", FindLabel("TurnLabel").text);
            yield return ConfirmCell(0);
            Assert.AreEqual(1, _gameManager.CurrentState.TotalMoves, "Local moves must apply at once again.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Client_BufferedStartThenHostClosesRoom_ShowsOpponentLeft()
        {
            yield return LoadSceneAsClient();

            Assert.AreEqual(FlowState.Playing, _flow.State);
            Assert.AreEqual("Turno del rival", FindLabel("TurnLabel").text, "The client plays O; X starts.");
            Assert.IsFalse(FindButton("Cell_0").interactable);

            _remote.Tap(0); Pump();
            Assert.AreEqual("Tu turno", FindLabel("TurnLabel").text);

            yield return ConfirmCell(4);
            Assert.IsFalse(_gameManager.CanAcceptLocalInput, "Input stays blocked until the host confirms.");
            Assert.AreEqual(1, _gameManager.CurrentState.TotalMoves, "The client never applies its own move before the host confirms it.");
            Pump();
            Assert.AreEqual(2, _gameManager.CurrentState.TotalMoves);
            Assert.AreEqual(_remote.PositionKey, KeyOf(_gameManager.CurrentState));

            _session.RaiseSessionLost(); // the host closed the room
            yield return WaitForState(FlowState.Menu);
            Assert.AreEqual(SessionFailureMessages.ToText(SessionFailure.OpponentLeft), FindLabel("MenuStatus").text);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Host_MenuFromResult_ClosesSessionWithoutError()
        {
            yield return LoadSceneAsHost();
            yield return PlayHostWinsTopRow();

            Press("MenuButton");
            yield return WaitForState(FlowState.Menu);

            Assert.AreEqual(1, _session.LeaveCalls, "\"Menú\" online must close the session.");
            Assert.AreEqual(string.Empty, FindLabel("MenuStatus").text, "Leaving on purpose is not an error.");
        }

        [UnityTest]
        public IEnumerator Host_ExitDuringMatch_ClosesSession()
        {
            yield return LoadSceneAsHost();
            yield return ConfirmCell(0);
            Pump();

            Press("ExitButton");
            yield return WaitForState(FlowState.Menu);

            Assert.AreEqual(1, _session.LeaveCalls, "\"Salir\" online must close the session.");
            Assert.IsNull(_gameManager.CurrentState);
        }

        [UnityTest]
        public IEnumerator Host_ClientReportsDifferentKey_BothGoToMenuWithMessage()
        {
            yield return LoadSceneAsHost();
            yield return ConfirmCell(0);

            // Both sides log it: the host detects it, the remote client is told.
            LogAssert.Expect(LogType.Error, new Regex("out of sync"));
            LogAssert.Expect(LogType.Error, new Regex("out of sync"));
            _clientTransport.Send(MatchMessage.Ack(1, positionKey: 999)); // a client that computed something else
            Pump();

            yield return WaitForState(FlowState.Menu);
            Assert.AreEqual(SessionFailureMessages.ToText(SessionFailure.Desync), FindLabel("MenuStatus").text);
            Assert.IsTrue(_hostTransport.Sent.Any(m => m.Kind == MatchMessageKind.Desync), "The other device must be told.");
            Assert.AreEqual(1, _session.LeaveCalls);
        }

        // This device hosts; the remote device is the client.
        IEnumerator LoadSceneAsHost()
        {
            yield return LoadScene();
            InMemoryMatchTransport.CreatePair(out _hostTransport, out _clientTransport);
            _flow.SetMatchTransport(_hostTransport);
            _remote = new OnlineTestDevice("RemoteClientDevice", _clientTransport);

            Press("CreateRoomButton");
            yield return WaitForState(FlowState.Lobby);

            _remote.Match.Start();
            _hostTransport.ConnectPeer();
            Pump();
            yield return WaitForState(FlowState.Playing);
        }

        // This device is the client; the remote device hosts. The host's
        // StartMatch reaches this device before its join finished, so it is
        // buffered until the flow opens the online match.
        IEnumerator LoadSceneAsClient()
        {
            yield return LoadScene();
            InMemoryMatchTransport.CreatePair(out _hostTransport, out _clientTransport);
            _flow.SetMatchTransport(_clientTransport);
            _remote = new OnlineTestDevice("RemoteHostDevice", _hostTransport);

            _remote.Match.Start();
            _hostTransport.ConnectPeer();
            Pump(); // StartMatch waits in this device's buffer

            Press("JoinByCodeButton");
            yield return WaitForState(FlowState.JoinByCode);
            FindInput("CodeInput").text = FakeSessionService.FakeCode;
            Press("JoinButton");
            yield return WaitForState(FlowState.Playing);
        }

        IEnumerator PlayHostWinsTopRow()
        {
            foreach (var (hostCell, clientCell) in new[] { (0, 3), (1, 4) })
            {
                yield return ConfirmCell(hostCell); Pump();
                _remote.Tap(clientCell); Pump();
            }
            yield return ConfirmCell(2); Pump();
            yield return WaitForState(FlowState.Result);
        }

        IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);

            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while ((_flow = Object.FindAnyObjectByType<MatchFlow>()) == null && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsNotNull(_flow, "MatchFlow not found after loading the scene.");
            yield return null; // let every Start run

            _gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
            _session = new GameObject("FakeSessionService").AddComponent<FakeSessionService>();
            _flow.SetSessionService(_session);
        }

        IEnumerator WaitForState(FlowState expected)
        {
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while ((_flow.State != expected || _flow.IsBusy) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(expected, _flow.State, $"Flow did not reach {expected} within {TimeoutSeconds}s.");
        }

        void Pump() => InMemoryMatchTransport.Pump(_hostTransport, _clientTransport);

        static ulong KeyOf(GameState s) => PositionKey.Compute(s.Config, s.QueueX, s.QueueO, s.CurrentPlayer).Value;

        // A confirmed tap: select, then confirm on the same cell (GDD §3.7).
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
