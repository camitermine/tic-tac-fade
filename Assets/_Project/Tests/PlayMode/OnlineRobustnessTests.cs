using System.Linq;
using NUnit.Framework;
using TicTacFade.Core;
using TicTacFade.Game;

namespace TicTacFade.PlayModeTests
{
    /// <summary>
    /// Online iteration 3 (GDD §3.5, §5): turn timer, absent mode,
    /// abandonment, "Salir", disconnections and unanswered proposals. Two
    /// devices over the in-memory transport with a manual clock: no test
    /// waits in real time.
    /// </summary>
    public class OnlineRobustnessTests
    {
        const double TurnTime = 30, AbsentTurnTime = 10;

        InMemoryMatchTransport _hostTransport;
        InMemoryMatchTransport _clientTransport;
        ManualClock _clock;
        FixedRandomSource _random;
        OnlineTestDevice _host;
        OnlineTestDevice _client;

        [SetUp]
        public void SetUp()
        {
            InMemoryMatchTransport.CreatePair(out _hostTransport, out _clientTransport);
            _clock = new ManualClock();
            _random = new FixedRandomSource();
            _host = new OnlineTestDevice("HostDevice", _hostTransport, _clock, _random);
            _client = new OnlineTestDevice("ClientDevice", _clientTransport, _clock, _random);

            _host.Match.Start();
            _client.Match.Start();
            _hostTransport.ConnectPeer();
            Pump();
        }

        [TearDown]
        public void TearDown()
        {
            _host.Destroy();
            _client.Destroy();
        }

        [Test]
        public void TurnExpires_HostPlaysLegalMove_SameOnBothDevices_AndMarksAbsent()
        {
            Assert.AreEqual(30, _client.Match.SecondsRemaining, "The client shows the host's announced turn time.");

            Advance(TurnTime - 0.1);
            Assert.AreEqual(0, _host.State.TotalMoves, "Nothing happens before the turn ends.");

            Advance(0.2);

            Assert.AreEqual(1, _host.State.TotalMoves, "The expired turn plays an automatic move.");
            Assert.AreEqual(1, _client.State.TotalMoves);
            Assert.AreEqual(Occupant.O, _host.State.CurrentPlayer);
            Assert.AreEqual(_host.PositionKey, _client.PositionKey, "Both devices applied the same move.");
            Assert.IsTrue(_host.Match.IsAbsent(Occupant.X));
            Assert.IsTrue(_client.Match.IsAbsent(Occupant.X), "The client learns absent mode from the next TurnTimer.");
            Assert.IsNull(_host.AbandonResult);
        }

        [Test]
        public void AutomaticMove_NeverPicksTheWinningMove()
        {
            // X: 0,1 / O: 3,4, X to move. Playing 2 wins; 5,6,7,8 don't.
            var state = GameState.CreateInitial(GameConfig.Mvp(), Occupant.X);
            foreach (var move in new[] { new Move(Occupant.X, 0), new Move(Occupant.O, 3), new Move(Occupant.X, 1), new Move(Occupant.O, 4) })
                state = RulesEngine.Apply(state, move).State;
            Assert.IsTrue(RulesEngine.Apply(state, new Move(Occupant.X, 2)).State.IsOver, "Test setup: cell 2 must win.");

            for (int value = 0; value < 9; value++)
            {
                var chosen = OnlineMatch.ChooseAutomaticMove(state, Occupant.X, new FixedRandomSource(value));

                Assert.AreNotEqual(2, chosen.CellIndex, $"random={value} picked the winning move.");
                Assert.IsTrue(RulesEngine.IsLegal(state, chosen, out _), $"random={value} picked an illegal move.");
            }
        }

        [Test]
        public void AutomaticMove_WhenEveryLegalMoveWins_PicksOneOfThem()
        {
            // 2x2 board, 2 in a row: X at 0 and O at 3, X to move. Both free
            // cells (1 and 2) complete a line for X.
            var config = new GameConfig(boardSize: 2, bufferSize: 3, winLength: 2, maxTotalMoves: 40, repetitionLimit: 3);
            var state = GameState.CreateInitial(config, Occupant.X);
            state = RulesEngine.Apply(state, new Move(Occupant.X, 0)).State;
            state = RulesEngine.Apply(state, new Move(Occupant.O, 3)).State;
            Assert.IsFalse(state.IsOver, "Test setup: the match must still be on.");

            var chosen = OnlineMatch.ChooseAutomaticMove(state, Occupant.X, new FixedRandomSource(1));

            Assert.IsTrue(RulesEngine.IsLegal(state, chosen, out _));
            Assert.IsTrue(RulesEngine.Apply(state, chosen).State.IsOver, "With no non-winning move left, any legal move is fine.");
        }

        [Test]
        public void AbsentMode_ShortTurnsUntilTheirOwnMove()
        {
            Advance(TurnTime);                          // X expires → absent
            ClientPlays();

            Assert.AreEqual(10, _host.Match.SecondsRemaining, "An absent player gets 10 s turns.");
            Advance(AbsentTurnTime - 0.1);
            Assert.AreEqual(2, _host.State.TotalMoves, "The absent turn hasn't ended yet.");
            Advance(0.2);                               // X expires again (2nd in a row)
            Assert.AreEqual(3, _host.State.TotalMoves);
            ClientPlays();

            _host.Tap(_host.FirstLegalCell());          // X plays by itself: back from absent mode
            Pump();
            Assert.IsFalse(_host.Match.IsAbsent(Occupant.X));
            Assert.IsFalse(_client.Match.IsAbsent(Occupant.X));
            ClientPlays();

            Assert.AreEqual(30, _host.Match.SecondsRemaining, "Back to normal 30 s turns.");
            Advance(TurnTime - 1);
            Assert.AreEqual(6, _host.State.TotalMoves, "A normal turn doesn't expire at 10 s.");
            Assert.IsNull(_host.AbandonResult, "The own move reset the consecutive count.");
        }

        [Test]
        public void ThirdConsecutiveTimeout_AbandonmentWithoutAutomaticMove()
        {
            Advance(TurnTime);          // 1st: automatic move
            ClientPlays();
            Advance(AbsentTurnTime);    // 2nd: automatic move
            ClientPlays();
            int movesBefore = _host.State.TotalMoves;

            Advance(AbsentTurnTime);    // 3rd: abandonment

            Assert.AreEqual(movesBefore, _host.State.TotalMoves, "The third timeout must not play an automatic move.");
            AssertAbandoned(_host, Occupant.X, AbandonmentCause.Timeouts);
            AssertAbandoned(_client, Occupant.X, AbandonmentCause.Timeouts);
            Assert.IsFalse(_host.Match.CanRematch, "No rematch after an abandonment.");
        }

        [Test]
        public void Forfeit_OtherDeviceWinsAndAcknowledges()
        {
            _client.Match.Forfeit();
            Assert.IsFalse(_client.ForfeitCompleted, "It waits for the ack.");
            Assert.IsTrue(_client.GameManager.IsHalted, "The leaving device stops playing at once.");

            Pump();

            AssertAbandoned(_host, Occupant.O, AbandonmentCause.Quit);
            Assert.AreEqual(Occupant.X, _host.AbandonResult.Winner);
            Assert.IsTrue(_client.ForfeitCompleted, "The ack lets the leaving device close the session.");
        }

        [Test]
        public void Forfeit_WithoutAck_CompletesAfterTheWait()
        {
            _hostTransport.StopResponding();
            _client.Match.Forfeit();
            Pump();
            Advance(OnlineNetworkSettings.Default().ForfeitAckTimeoutSeconds - 0.1);
            Assert.IsFalse(_client.ForfeitCompleted);

            Advance(0.2);

            Assert.IsTrue(_client.ForfeitCompleted, "Without an ack it closes anyway after the wait.");
        }

        [Test]
        public void HostDrops_ClientLosesConnection()
        {
            _hostTransport.Drop();

            Assert.IsTrue(_client.ConnectionLost, "The client must go back to the menu with the connection-lost message.");
            Assert.IsTrue(_client.GameManager.IsHalted);
        }

        [Test]
        public void ClientDrops_HostWinsByAbandonmentAfterTheGrace()
        {
            _host.Tap(0);
            Pump();
            _clientTransport.Drop();

            Advance(OnlineNetworkSettings.Default().DisconnectGraceSeconds - 0.1);
            Assert.IsNull(_host.AbandonResult, "A short grace, in case the drop was instantaneous.");

            Advance(0.2);

            AssertAbandoned(_host, Occupant.O, AbandonmentCause.Disconnected);
            Assert.AreEqual(Occupant.X, _host.AbandonResult.Winner,
                "A dropped client loses right after the grace, without waiting for 3 timeouts.");
        }

        [Test]
        public void UnansweredProposal_UnblocksInputAndIsTreatedAsHostDrop()
        {
            _host.Tap(0);
            Pump();
            _hostTransport.StopResponding(); // connected but silent

            _client.Tap(4);
            Pump();
            Assert.IsFalse(_client.GameManager.CanAcceptLocalInput, "Waiting for the host.");

            Advance(OnlineNetworkSettings.Default().ProposalResponseTimeoutSeconds - 0.1);
            Assert.IsFalse(_client.ConnectionLost);

            Advance(0.2);

            Assert.IsTrue(_client.ConnectionLost, "No answer in time means the host is gone.");
            Assert.AreEqual(1, _client.State.TotalMoves, "The unanswered move was never applied.");
        }

        void ClientPlays()
        {
            _client.Tap(_client.FirstLegalCell());
            Pump();
        }

        void Advance(double seconds)
        {
            _clock.Advance(seconds);
            _host.Match.Tick();
            _client.Match.Tick();
            Pump();
        }

        static void AssertAbandoned(OnlineTestDevice device, Occupant abandoner, AbandonmentCause cause)
        {
            Assert.IsNotNull(device.AbandonResult, $"{device.GameObject.name} got no abandonment.");
            Assert.AreEqual(abandoner, device.AbandonResult.Abandoner);
            Assert.AreEqual(cause, device.AbandonResult.Cause);
            Assert.IsTrue(device.GameManager.IsHalted, $"{device.GameObject.name} must stop the match.");
        }

        void Pump() => InMemoryMatchTransport.Pump(_hostTransport, _clientTransport);
    }
}
