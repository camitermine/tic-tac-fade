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
    /// Host-authoritative online match between two GameManagers connected
    /// by an in-memory transport (no network, no scene): one host, one
    /// client. Messages are only delivered on Pump, like a real network.
    /// </summary>
    public class OnlineMatchTests
    {
        InMemoryMatchTransport _hostTransport;
        InMemoryMatchTransport _clientTransport;
        OnlineTestDevice _host;
        OnlineTestDevice _client;

        [SetUp]
        public void SetUp()
        {
            InMemoryMatchTransport.CreatePair(out _hostTransport, out _clientTransport);
            _host = new OnlineTestDevice("HostDevice", _hostTransport);
            _client = new OnlineTestDevice("ClientDevice", _clientTransport);
        }

        [TearDown]
        public void TearDown()
        {
            _host.Destroy();
            _client.Destroy();
        }

        [Test]
        public void FullMatch_BothDevicesApplySameMoves_EndInIdenticalStates()
        {
            StartBothAndConnect();

            Assert.IsTrue(_host.Match.IsHost);
            Assert.AreEqual(Occupant.X, _host.Match.LocalSymbol, "The host always plays X.");
            Assert.AreEqual(Occupant.O, _client.Match.LocalSymbol, "The client always plays O.");
            Assert.AreEqual(Occupant.X, _host.State.CurrentPlayer, "The first online match starts with X.");
            AssertIdentical();

            PlayUntilOver();

            Assert.IsTrue(_host.State.IsOver);
            AssertIdentical();
        }

        [Test]
        public void IllegalClientProposals_RejectedByHost_NeitherStateChanges()
        {
            StartBothAndConnect();
            _host.Tap(0);
            Pump();
            Assert.AreEqual(Occupant.O, _host.State.CurrentPlayer);

            // Occupied cell, O's turn.
            AssertRejectedWithoutChange(new Move(Occupant.O, 0));
            // The client tries to move the host's symbol.
            AssertRejectedWithoutChange(new Move(Occupant.X, 5));
            // Out of range.
            AssertRejectedWithoutChange(new Move(Occupant.O, 42));

            _client.Tap(4);
            Pump();
            Assert.AreEqual(Occupant.X, _host.State.CurrentPlayer);

            // Out of turn: it's X's turn now.
            AssertRejectedWithoutChange(new Move(Occupant.O, 5));

            Assert.AreEqual(4, _hostTransport.Sent.Count(m => m.Kind == MatchMessageKind.Rejected));
        }

        [Test]
        public void StartMatchArrivesBeforeClientIsReady_BufferedAndProcessedOnStart()
        {
            _host.Match.Start();
            _hostTransport.ConnectPeer();
            Pump(); // StartMatch reaches the client before its OnlineMatch listens

            Assert.IsNull(_client.State, "The client must not start until its flow is ready.");
            Assert.IsNotNull(_host.State);

            _client.Match.Start(); // opens the transport: the buffered StartMatch is processed now

            Assert.IsNotNull(_client.State, "The buffered StartMatch must start the client's match.");
            Assert.AreEqual(Occupant.X, _client.State.CurrentPlayer);
            AssertIdentical();

            _host.Tap(0);
            Pump();
            _client.Tap(4);
            Pump();

            Assert.AreEqual(2, _client.State.TotalMoves, "The match must be playable after the buffered start.");
            AssertIdentical();
        }

        [Test]
        public void DoubleTapWhileWaitingForHost_SendsOneProposal()
        {
            StartBothAndConnect();
            _host.Tap(0);
            Pump();

            _client.Tap(4);
            Assert.IsFalse(_client.GameManager.CanAcceptLocalInput, "Input must be blocked while the host hasn't confirmed.");
            _client.Tap(4);
            _client.Tap(5);

            Assert.AreEqual(1, _clientTransport.Sent.Count(m => m.Kind == MatchMessageKind.Propose),
                "Only one proposal may be in flight.");

            Pump();

            Assert.AreEqual(2, _host.State.TotalMoves);
            AssertIdentical();
        }

        [Test]
        public void Rematch_NeedsBothRequests_AndSwapsWhoStarts()
        {
            StartBothAndConnect();
            PlayUntilOver();

            _host.Match.RequestRematch();
            Pump();
            Assert.IsTrue(_host.Match.LocalRematchRequested);
            Assert.IsTrue(_host.State.IsOver, "One request alone must not start the rematch.");

            _client.Match.RequestRematch();
            Pump();

            Assert.IsFalse(_host.State.IsOver);
            Assert.AreEqual(0, _host.State.TotalMoves);
            Assert.AreEqual(Occupant.O, _host.State.CurrentPlayer, "The rematch starts with O.");
            Assert.AreEqual(Occupant.X, _host.Match.LocalSymbol, "Symbols don't change on a rematch.");
            Assert.IsFalse(_host.Match.LocalRematchRequested);
            Assert.IsFalse(_client.Match.LocalRematchRequested);
            AssertIdentical();

            PlayUntilOver();
            AssertIdentical();
        }

        [Test]
        public void PositionKeysDiffer_BothSidesCutTheMatch()
        {
            StartBothAndConnect();
            _host.Tap(0);
            Pump();

            bool hostDesynced = false, clientDesynced = false;
            _host.Match.Desynced += () => hostDesynced = true;
            _client.Match.Desynced += () => clientDesynced = true;

            // A confirmation whose key doesn't match what the client computes.
            LogAssert.Expect(LogType.Error, new Regex("out of sync"));
            LogAssert.Expect(LogType.Error, new Regex("out of sync"));
            _hostTransport.Send(MatchMessage.Confirmed(new Move(Occupant.O, 4), 2, positionKey: 12345));
            Pump();

            Assert.IsTrue(clientDesynced, "The client must detect the mismatch.");
            Assert.IsTrue(hostDesynced, "The host must be told and cut the match too.");
        }

        void StartBothAndConnect()
        {
            _host.Match.Start();
            _client.Match.Start();
            _hostTransport.ConnectPeer();
            Pump();
        }

        void PlayUntilOver()
        {
            const int maxMoves = 100;
            for (int i = 0; i < maxMoves && !_host.State.IsOver; i++)
            {
                var mover = _host.State.CurrentPlayer == _host.Match.LocalSymbol ? _host : _client;
                int before = _host.State.TotalMoves;

                mover.Tap(mover.FirstLegalCell());
                Pump();

                Assert.AreEqual(before + 1, _host.State.TotalMoves, "Each confirmed tap must be one move on the host.");
                Assert.AreEqual(before + 1, _client.State.TotalMoves, "Each confirmed tap must be one move on the client.");
            }
            Assert.IsTrue(_host.State.IsOver, $"The match didn't end within {maxMoves} moves.");
        }

        void AssertRejectedWithoutChange(Move proposal)
        {
            ulong hostKey = _host.PositionKey, clientKey = _client.PositionKey;
            int hostMoves = _host.State.TotalMoves, clientMoves = _client.State.TotalMoves;
            int rejectedBefore = _hostTransport.Sent.Count(m => m.Kind == MatchMessageKind.Rejected);

            _clientTransport.Send(MatchMessage.Propose(proposal)); // bypasses the client's UI guards
            Pump();

            Assert.AreEqual(rejectedBefore + 1, _hostTransport.Sent.Count(m => m.Kind == MatchMessageKind.Rejected),
                $"The host must reject {proposal.Player}@{proposal.CellIndex}.");
            Assert.AreEqual(hostMoves, _host.State.TotalMoves, "The host state must not change.");
            Assert.AreEqual(clientMoves, _client.State.TotalMoves, "The client state must not change.");
            Assert.AreEqual(hostKey, _host.PositionKey);
            Assert.AreEqual(clientKey, _client.PositionKey);
        }

        void AssertIdentical()
        {
            var h = _host.State;
            var c = _client.State;
            CollectionAssert.AreEqual(h.Cells, c.Cells, "Cells differ.");
            CollectionAssert.AreEqual(h.QueueX, c.QueueX, "X queue differs.");
            CollectionAssert.AreEqual(h.QueueO, c.QueueO, "O queue differs.");
            Assert.AreEqual(h.CurrentPlayer, c.CurrentPlayer, "Current player differs.");
            Assert.AreEqual(h.TotalMoves, c.TotalMoves, "Move count differs.");
            Assert.AreEqual(h.IsOver, c.IsOver, "IsOver differs.");
            Assert.AreEqual(h.Winner, c.Winner, "Winner differs.");
            Assert.AreEqual(h.EndReason, c.EndReason, "End reason differs.");
            Assert.AreEqual(_host.PositionKey, _client.PositionKey, "Position keys differ.");
        }

        void Pump() => InMemoryMatchTransport.Pump(_hostTransport, _clientTransport);
    }
}
