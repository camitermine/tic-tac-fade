using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TicTacFade.Core;
using TicTacFade.Game;

namespace TicTacFade.PlayModeTests
{
    /// <summary>
    /// Start handshake (Ready / StartMatch with match numbers), protocol
    /// version check and tolerant decoding (ADR 0002). Two devices over the
    /// in-memory transport, which carries the same bytes as the network,
    /// with a manual clock.
    /// </summary>
    public class OnlineHandshakeTests
    {
        const double TurnTime = 30;

        InMemoryMatchTransport _hostTransport;
        InMemoryMatchTransport _clientTransport;
        ManualClock _clock;
        OnlineTestDevice _host;
        OnlineTestDevice _client;

        [SetUp]
        public void SetUp()
        {
            InMemoryMatchTransport.CreatePair(out _hostTransport, out _clientTransport);
            _clock = new ManualClock();
            _host = new OnlineTestDevice("HostDevice", _hostTransport, _clock);
            _client = new OnlineTestDevice("ClientDevice", _clientTransport, _clock);
        }

        [TearDown]
        public void TearDown()
        {
            _host.Destroy();
            _client.Destroy();
        }

        [Test]
        public void LostReady_RecoveredByResend_BothStart()
        {
            _host.Match.Start();
            _clientTransport.LoseNextSent();
            _client.Match.Start();
            Pump();

            Assert.IsNull(_host.State, "Without a Ready the host must not start.");
            Assert.IsNull(_client.State);

            Advance(OnlineNetworkSettings.Default().ReadyResendIntervalSeconds);

            Assert.IsNotNull(_host.State, "The resent Ready must start the match.");
            Assert.IsNotNull(_client.State);
            Assert.AreEqual(1, _host.MatchStarts);
            Assert.AreEqual(1, _client.MatchStarts);
            AssertIdentical();
        }

        [Test]
        public void DuplicateReady_DoesNotStartSecondMatch()
        {
            StartBoth();
            _host.Tap(0);
            Pump();

            _clientTransport.InjectToPeer(MatchMessage.Ready());
            Pump();

            Assert.AreEqual(1, _host.MatchStarts, "A repeated Ready must not start a match on the host.");
            Assert.AreEqual(1, _client.MatchStarts, "Nor on the client.");
            Assert.AreEqual(1, _host.State.TotalMoves, "The running match keeps its moves.");
            Assert.AreEqual(1, _client.State.TotalMoves);
            Assert.AreEqual(2, _hostTransport.Sent.Count(m => m.Kind == MatchMessageKind.StartMatch),
                "The host answers the repeated Ready with the current StartMatch again.");
            Assert.IsTrue(_hostTransport.Sent.Where(m => m.Kind == MatchMessageKind.StartMatch).All(m => m.MatchNumber == 1));
            AssertIdentical();
        }

        [Test]
        public void LostStartMatch_RecoveredByReadyResend_ClientShowsHostRemainingTime()
        {
            _host.Match.Start();
            _hostTransport.LoseNextSent(2); // StartMatch and the first TurnTimer
            _client.Match.Start();
            Pump();

            Assert.IsNotNull(_host.State, "The host started on the first Ready.");
            Assert.IsNull(_client.State, "The client never got the StartMatch.");

            Advance(3.5); // the client resends Ready a few seconds later

            Assert.IsNotNull(_client.State, "The host's resent StartMatch must start the client.");
            Assert.AreEqual(1, _host.MatchStarts);
            Assert.AreEqual(1, _client.MatchStarts);
            AssertIdentical();
            Assert.AreEqual(27, _host.Match.SecondsRemaining, "Test setup: 26.5 s left on the host.");
            Assert.AreEqual(_host.Match.SecondsRemaining, _client.Match.SecondsRemaining,
                "The resent TurnTimer carries the time left on the host, not a full turn.");

            Advance(TurnTime - 3.5 - 0.1);
            Assert.AreEqual(0, _host.State.TotalMoves, "The turn hasn't expired yet.");
            Assert.AreEqual(1, _client.Match.SecondsRemaining);

            Advance(0.2);
            Assert.AreEqual(1, _host.State.TotalMoves, "The host's timer expired when the client's countdown ran out.");
            AssertIdentical();
        }

        [Test]
        public void DuplicateReadyInRematchStartedByO_ClientCountdownMatchesHostTimeLeft()
        {
            StartBoth();
            PlayUntilOver();
            _host.Match.RequestRematch();
            _client.Match.RequestRematch();
            Pump();
            Assert.AreEqual(Occupant.O, _client.State.CurrentPlayer, "Test setup: the rematch starts with O (the client).");

            Advance(10);
            _clientTransport.InjectToPeer(MatchMessage.Ready()); // a late or repeated Ready
            Pump();

            Assert.AreEqual(2, _client.MatchStarts, "The repeated StartMatch #2 must be ignored.");
            Assert.AreEqual(20, _client.Match.SecondsRemaining, "The client must not get a full turn back.");
            Assert.AreEqual(_host.Match.SecondsRemaining, _client.Match.SecondsRemaining);

            Advance(20 - 0.1);
            Assert.AreEqual(0, _host.State.TotalMoves);
            Assert.AreEqual(1, _client.Match.SecondsRemaining);

            Advance(0.2);
            Assert.AreEqual(1, _host.State.TotalMoves, "The host cuts O's turn exactly when the client's countdown ends.");
            AssertIdentical();
        }

        [Test]
        public void StaleStartMatchAfterRematch_Ignored()
        {
            StartBoth();
            PlayUntilOver();
            _host.Match.RequestRematch();
            _client.Match.RequestRematch();
            Pump();
            _client.Tap(_client.FirstLegalCell()); // the rematch starts with O
            Pump();
            Assert.AreEqual(1, _client.State.TotalMoves);

            _hostTransport.InjectToPeer(MatchMessage.StartMatch(Occupant.X, 1)); // late, from the first match
            _hostTransport.InjectToPeer(MatchMessage.StartMatch(Occupant.O, 2)); // repeated, current match
            Pump();

            Assert.AreEqual(2, _client.MatchStarts, "Only StartMatch #1 and #2, once each, may start a match.");
            Assert.AreEqual(1, _client.State.TotalMoves, "The rematch in progress must keep its moves.");
            AssertIdentical();
        }

        [Test]
        public void ReadyBeforeConnected_SilentNoOp_ResendRecovers()
        {
            _host.Match.Start();
            _clientTransport.SetLinkUp(false); // Netcode not connected yet (cold start)
            _client.Match.Start();
            for (int i = 0; i < 3; i++)
                Advance(OnlineNetworkSettings.Default().ReadyResendIntervalSeconds);

            Assert.AreEqual(4, _clientTransport.Sent.Count(m => m.Kind == MatchMessageKind.Ready), "Ready keeps being resent.");
            Assert.IsNull(_host.State);
            Assert.IsNull(_client.State);

            _clientTransport.SetLinkUp(true);
            Advance(OnlineNetworkSettings.Default().ReadyResendIntervalSeconds);

            Assert.IsNotNull(_host.State, "The first Ready after connecting must start the match.");
            Assert.IsNotNull(_client.State);
            AssertIdentical();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void DifferentProtocolVersion_BothSidesReportMismatch()
        {
            _host.Match.Start();

            // A client of another version sends its Ready; the host tells it
            // and waits for the answer before leaving.
            LogAssert.Expect(LogType.Warning, new Regex("protocol version"));
            _clientTransport.DeliverRawToPeer(OtherVersionFrame(MatchMessageKind.Ready));
            Pump();
            Assert.IsTrue(_hostTransport.Sent.Any(m => m.Kind == MatchMessageKind.VersionMismatch));
            Assert.IsFalse(_host.VersionMismatch, "The host waits for the client's answer.");

            // The client side receives the host's VersionMismatch, answers and leaves.
            LogAssert.Expect(LogType.Warning, new Regex("protocol version"));
            _client.Match.Start();
            Assert.IsTrue(_client.VersionMismatch);
            Assert.AreEqual(0, _clientTransport.Sent.Count(m => m.Kind == MatchMessageKind.Ready),
                "A client that already knows the version differs doesn't announce itself.");

            Pump(); // the client's answer reaches the host
            Assert.IsTrue(_host.VersionMismatch);
            Assert.IsNull(_host.State, "No match may start between different versions.");
            Assert.IsNull(_client.State);
            Assert.AreEqual(0, _hostTransport.Sent.Count(m => m.Kind == MatchMessageKind.StartMatch));
        }

        [Test]
        public void DifferentProtocolVersion_ClientNeverAnswers_HostLeavesAfterTimeout()
        {
            _host.Match.Start();
            LogAssert.Expect(LogType.Warning, new Regex("protocol version"));
            _clientTransport.DeliverRawToPeer(OtherVersionFrame(MatchMessageKind.Ready));
            Pump();

            Advance(OnlineNetworkSettings.Default().ForfeitAckTimeoutSeconds - 0.1);
            Assert.IsFalse(_host.VersionMismatch);
            Advance(0.2);
            Assert.IsTrue(_host.VersionMismatch, "The host leaves anyway after a short wait.");
        }

        [Test]
        public void TruncatedMessage_DroppedWithWarning_NoException()
        {
            StartBoth();
            var truncated = MatchMessageCodec.Encode(MatchMessage.Propose(new Move(Occupant.O, 4))).Take(10).ToArray();

            LogAssert.Expect(LogType.Warning, new Regex("dropped malformed match message"));
            _clientTransport.DeliverRawToPeer(truncated);
            Assert.DoesNotThrow(Pump);
            Assert.AreEqual(0, _host.State.TotalMoves, "A dropped frame changes nothing.");

            _host.Tap(0);
            Pump();
            _client.Tap(4);
            Pump();
            Assert.AreEqual(2, _host.State.TotalMoves, "The match stays playable.");
            AssertIdentical();
        }

        [Test]
        public void Codec_RejectsEmptyNullAndUnknownKind_RoundTripsAllFields()
        {
            LogAssert.Expect(LogType.Warning, new Regex("dropped malformed match message"));
            Assert.IsFalse(MatchMessageCodec.TryDecode(new byte[0], 0, out _));
            LogAssert.Expect(LogType.Warning, new Regex("dropped malformed match message"));
            Assert.IsFalse(MatchMessageCodec.TryDecode(null, 0, out _));

            var unknownKind = MatchMessageCodec.Encode(MatchMessage.Ready());
            unknownKind[0] = 200;
            LogAssert.Expect(LogType.Warning, new Regex("unknown kind"));
            Assert.IsFalse(MatchMessageCodec.TryDecode(unknownKind, unknownKind.Length, out _));

            var original = new MatchMessage(MatchMessageKind.TurnTimer, Occupant.O, 7, 12, 0xF00DCAFE12345678UL,
                MoveRejectionReason.NotPlayersTurn, 25000, AbsentFlags.X, AbandonmentCause.Quit, 3);
            var frame = MatchMessageCodec.Encode(original);
            Assert.AreEqual(MatchMessageCodec.FrameSize, frame.Length);
            Assert.IsTrue(MatchMessageCodec.TryDecode(frame, frame.Length, out var decoded));
            Assert.AreEqual(original.Kind, decoded.Kind);
            Assert.AreEqual(original.Player, decoded.Player);
            Assert.AreEqual(original.CellIndex, decoded.CellIndex);
            Assert.AreEqual(original.MoveNumber, decoded.MoveNumber);
            Assert.AreEqual(original.PositionKey, decoded.PositionKey);
            Assert.AreEqual(original.RejectionReason, decoded.RejectionReason);
            Assert.AreEqual(original.DurationMs, decoded.DurationMs);
            Assert.AreEqual(original.AbsentFlags, decoded.AbsentFlags);
            Assert.AreEqual(original.Cause, decoded.Cause);
            Assert.AreEqual(original.MatchNumber, decoded.MatchNumber);
            Assert.AreEqual(MatchMessageCodec.ProtocolVersion, decoded.ProtocolVersion);
        }

        static byte[] OtherVersionFrame(MatchMessageKind kind)
        {
            // Another version's frame may have another size: only the header is shared.
            var otherVersion = (ushort)(MatchMessageCodec.ProtocolVersion + 1);
            var frame = MatchMessageCodec.Encode(new MatchMessage(kind, protocolVersion: otherVersion));
            return frame.Take(MatchMessageCodec.HeaderSize + 5).ToArray();
        }

        void StartBoth()
        {
            _host.Match.Start();
            _client.Match.Start();
            Pump();
        }

        void PlayUntilOver()
        {
            const int maxMoves = 100;
            for (int i = 0; i < maxMoves && !_host.State.IsOver; i++)
            {
                var mover = _host.State.CurrentPlayer == Occupant.X ? _host : _client;
                mover.Tap(mover.FirstLegalCell());
                Pump();
            }
            Assert.IsTrue(_host.State.IsOver, "Test setup: the match must end.");
        }

        void Advance(double seconds)
        {
            _clock.Advance(seconds);
            _host.Match.Tick();
            _client.Match.Tick();
            Pump();
        }

        void Pump() => InMemoryMatchTransport.Pump(_hostTransport, _clientTransport);

        void AssertIdentical()
        {
            Assert.AreEqual(_host.State.TotalMoves, _client.State.TotalMoves);
            Assert.AreEqual(_host.State.CurrentPlayer, _client.State.CurrentPlayer);
            Assert.AreEqual(_host.PositionKey, _client.PositionKey, "Both devices must be in the same position.");
        }
    }
}
