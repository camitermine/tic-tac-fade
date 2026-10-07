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
        ManualClock _clock;

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

            // The client drops in the middle of the rematch: after the short
            // grace it loses by abandonment (no reconnection in the MVP).
            _clientTransport.Drop();
            _session.RaiseOpponentLeft();
            LogAssert.Expect(LogType.Warning, new Regex("didn't come back"));
            _clock.Advance(OnlineNetworkSettings.Default().DisconnectGraceSeconds);
            yield return WaitForState(FlowState.Result);
            Assert.AreEqual("Ganaste: el rival abandonó", FindLabel("ResultLabel").text);
            Assert.IsFalse(FindButton("RematchButton").interactable, "No rematch without an opponent.");

            Press("MenuButton");
            yield return WaitForState(FlowState.Menu);
            Assert.AreEqual(string.Empty, FindLabel("MenuStatus").text);
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
        public IEnumerator Client_ReadyHandshakeThenHostClosesRoom_ShowsOpponentLeft()
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

            // The host's device goes away mid-match without "Salir".
            _hostTransport.Drop();
            _session.RaiseSessionLost();
            yield return WaitForState(FlowState.Menu);
            Assert.AreEqual(SessionFailureMessages.ToText(SessionFailure.ConnectionLost), FindLabel("MenuStatus").text);
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
        public IEnumerator Host_ExitDuringMatch_ShowsLeavingThenLosesByAbandonment()
        {
            yield return LoadSceneAsHost();
            yield return ConfirmCell(0);
            Pump();

            Press("ExitButton");
            yield return null;
            Assert.AreEqual("Saliendo...", FindLabel("TurnLabel").text, "While waiting for the ack.");
            Assert.AreEqual(FlowState.Playing, _flow.State);

            Pump(); // Forfeit reaches the client, its ack comes back
            yield return WaitForState(FlowState.Menu);

            Assert.AreEqual(1, _session.LeaveCalls, "\"Salir\" online must close the session.");
            Assert.AreEqual(string.Empty, FindLabel("MenuStatus").text, "Leaving on purpose is not an error.");
            Assert.IsNotNull(_remote.AbandonResult, "The other device must get the abandonment.");
            Assert.AreEqual(Occupant.X, _remote.AbandonResult.Abandoner);
            Assert.AreEqual(AbandonmentCause.Quit, _remote.AbandonResult.Cause);
            Assert.AreEqual(Occupant.O, _remote.AbandonResult.Winner);
        }

        [UnityTest]
        public IEnumerator Host_TurnTimer_CountsDownAndMarksAbsent()
        {
            yield return LoadSceneAsHost();

            yield return null; // a frame for the flow's Tick
            Assert.AreEqual("0:30", FindLabel("TurnTimerLabel").text, "A full 30 s turn for X.");

            _clock.Advance(12.2);
            yield return null;
            Assert.AreEqual("0:18", FindLabel("TurnTimerLabel").text);

            // X (the host, this device) lets its turn expire: automatic move, X absent.
            _clock.Advance(20);
            yield return null;
            Pump();
            Assert.AreEqual(1, _gameManager.CurrentState.TotalMoves, "The expired turn plays an automatic move.");
            StringAssert.EndsWith("· ausente", FindLabel("CountX").text);

            // O plays; X's next turn is a 10 s absent turn.
            _remote.Tap(_remote.FirstLegalCell());
            Pump();
            yield return null;
            Assert.AreEqual("0:10", FindLabel("TurnTimerLabel").text);
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

        [UnityTest]
        public IEnumerator Host_ClientWithOtherProtocolVersion_BothLeaveWithVersionMessage()
        {
            yield return LoadScene();
            InMemoryMatchTransport.CreatePair(out _hostTransport, out _clientTransport);
            _flow.SetMatchTransport(_hostTransport);

            Press("CreateRoomButton");
            yield return WaitForState(FlowState.Lobby);

            // A client of another version: its Ready only shares the stable header.
            LogAssert.Expect(LogType.Warning, new Regex("protocol version"));
            _clientTransport.DeliverRawToPeer(OtherVersionFrame(MatchMessageKind.Ready));
            Pump();
            yield return null;
            Assert.AreEqual(FlowState.Lobby, _flow.State, "The host waits for the client to get the message.");
            Assert.IsTrue(_hostTransport.Sent.Any(m => m.Kind == MatchMessageKind.VersionMismatch));
            Assert.IsNull(_gameManager.CurrentState, "No match may start with another version.");

            // The client answers before leaving: now the host leaves too.
            _clientTransport.DeliverRawToPeer(OtherVersionFrame(MatchMessageKind.VersionMismatch));
            Pump();
            yield return WaitForState(FlowState.Menu);
            Assert.AreEqual("El rival tiene otra versión del juego.", FindLabel("MenuStatus").text);
            Assert.AreEqual(1, _session.LeaveCalls);
            LogAssert.NoUnexpectedReceived();
        }

        static byte[] OtherVersionFrame(MatchMessageKind kind)
        {
            var otherVersion = (ushort)(MatchMessageCodec.ProtocolVersion + 1);
            return MatchMessageCodec.Encode(new MatchMessage(kind, protocolVersion: otherVersion));
        }

        // This device hosts; the remote device is the client.
        IEnumerator LoadSceneAsHost()
        {
            yield return LoadScene();
            InMemoryMatchTransport.CreatePair(out _hostTransport, out _clientTransport);
            _flow.SetMatchTransport(_hostTransport);
            _remote = new OnlineTestDevice("RemoteClientDevice", _clientTransport, _clock);

            Press("CreateRoomButton");
            yield return WaitForState(FlowState.Lobby);

            _remote.Match.Start(); // its Ready reaches this host: the match starts
            Pump();
            yield return WaitForState(FlowState.Playing);
        }

        // This device is the client; the remote device hosts and waits for
        // this device's Ready, sent when the flow opens the online match.
        IEnumerator LoadSceneAsClient()
        {
            yield return LoadScene();
            InMemoryMatchTransport.CreatePair(out _hostTransport, out _clientTransport);
            _flow.SetMatchTransport(_clientTransport);
            _remote = new OnlineTestDevice("RemoteHostDevice", _hostTransport, _clock);

            _remote.Match.Start();
            Pump();
            Assert.IsNull(_remote.State, "The host must not start before the client's Ready.");

            Press("JoinByCodeButton");
            yield return WaitForState(FlowState.JoinByCode);
            FindInput("CodeInput").text = FakeSessionService.FakeCode;
            Press("JoinButton");
            yield return WaitForState(FlowState.Lobby);
            Pump(); // Ready → host, StartMatch → this device
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

            // Time only moves when a test advances it: no timer fires on its own.
            _clock = new ManualClock();
            _flow.SetTimeSources(_clock, new FixedRandomSource());
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
