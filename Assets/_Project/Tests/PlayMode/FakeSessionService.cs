using System;
using System.Threading.Tasks;
using TicTacFade.Game;
using TicTacFade.Net;

namespace TicTacFade.PlayModeTests
{
    /// <summary>
    /// Network-free <see cref="SessionServiceBehaviour"/> for flow tests.
    /// Counts calls, returns <see cref="NextResult"/>, and can hold an
    /// operation open (<see cref="HoldOperations"/>) to test the busy state.
    /// The code format is the real one (<see cref="JoinCodeFormat"/>): only
    /// the network is faked.
    /// </summary>
    public class FakeSessionService : SessionServiceBehaviour
    {
        public const string FakeCode = "BCD678";

        TaskCompletionSource<SessionResult> _pending;
        bool _inRoom;

        public int CreateCalls { get; private set; }
        public int JoinCalls { get; private set; }
        public int LeaveCalls { get; private set; }
        public string LastJoinCode { get; private set; }

        public SessionResult NextResult { get; set; } = SessionResult.Ok();

        /// <summary>When true, create/join stay pending until <see cref="CompletePending"/>.</summary>
        public bool HoldOperations { get; set; }

        public override string JoinCode => _inRoom ? FakeCode : null;

        public override event Action OpponentConnected;
        public override event Action OpponentLeft;
        public override event Action SessionLost;

        public override bool IsWellFormedCode(string code) => JoinCodeFormat.IsWellFormed(code);

        public override Task<SessionResult> CreateAsync()
        {
            CreateCalls++;
            return Respond();
        }

        public override Task<SessionResult> JoinByCodeAsync(string code)
        {
            JoinCalls++;
            LastJoinCode = code;
            return Respond();
        }

        public override Task LeaveAsync()
        {
            LeaveCalls++;
            _inRoom = false;
            return Task.CompletedTask;
        }

        public void CompletePending(SessionResult result)
        {
            _inRoom = result.Success;
            var pending = _pending;
            _pending = null;
            pending.SetResult(result);
        }

        public void RaiseOpponentConnected() => OpponentConnected?.Invoke();

        public void RaiseOpponentLeft() => OpponentLeft?.Invoke();

        public void RaiseSessionLost()
        {
            _inRoom = false;
            SessionLost?.Invoke();
        }

        Task<SessionResult> Respond()
        {
            if (HoldOperations)
            {
                _pending = new TaskCompletionSource<SessionResult>();
                return _pending.Task;
            }

            _inRoom = NextResult.Success;
            return Task.FromResult(NextResult);
        }
    }
}
