using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using TicTacFade.Game;

namespace TicTacFade.Net
{
    /// <summary>
    /// <see cref="ISessionService"/> on Unity Multiplayer Services (ADR 0002):
    /// private 2-player sessions over Relay, joined by code. The SDK starts
    /// the Netcode host/client on the scene's NetworkManager.
    /// Unity Services are initialized and signed in anonymously on the
    /// first create/join, never at startup: local play never touches them.
    /// No operation throws; every failure is mapped to a
    /// <see cref="SessionFailure"/> by <see cref="SessionFailureMapper"/>.
    /// </summary>
    public class UgsSessionService : SessionServiceBehaviour
    {
        const int MaxPlayers = 2;

        [SerializeField] float operationTimeoutSeconds = 15f;
        [SerializeField] float networkShutdownTimeoutSeconds = 5f;

        ISession _session;

        public override string JoinCode => _session?.Code;

        public override event Action OpponentConnected;
        public override event Action OpponentLeft;
        public override event Action SessionLost;

        public override bool IsWellFormedCode(string code) => JoinCodeFormat.IsWellFormed(code);

        public override async Task<SessionResult> CreateAsync()
        {
            var failure = await PrepareAsync();
            if (failure != SessionFailure.None)
                return SessionResult.Fail(failure);

            return await RunSessionOperationAsync(async () =>
            {
                var options = new SessionOptions { MaxPlayers = MaxPlayers, IsPrivate = true }.WithRelayNetwork();
                return await MultiplayerService.Instance.CreateSessionAsync(options);
            }, SessionFailureMapper.Map);
        }

        public override async Task<SessionResult> JoinByCodeAsync(string code)
        {
            var failure = await PrepareAsync();
            if (failure != SessionFailure.None)
                return SessionResult.Fail(failure);

            return await RunSessionOperationAsync(
                () => MultiplayerService.Instance.JoinSessionByCodeAsync(code),
                SessionFailureMapper.MapJoin);
        }

        public override async Task LeaveAsync()
        {
            var session = _session;
            _session = null;

            if (session != null)
            {
                Detach(session);
                try
                {
                    // The host closes the room instead of just leaving it, so
                    // the other player gets Deleted instead of staying in a
                    // room whose host migrated to them.
                    var leaveTask = session.IsHost && session is IHostSession hostSession
                        ? hostSession.DeleteAsync()
                        : session.LeaveAsync();
                    await WithTimeout(leaveTask);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Tic-Tac-Fade: leaving the session failed: {e}");
                }
            }

            await WaitForNetworkIdleAsync();
        }

        void OnDestroy()
        {
            // Scene unloaded or app quitting with a room open: best effort.
            var session = _session;
            _session = null;
            if (session == null)
                return;

            Detach(session);
            session.LeaveAsync().ContinueWith(t =>
            {
                if (t.IsFaulted)
                    Debug.LogWarning($"Tic-Tac-Fade: leaving the session on destroy failed: {t.Exception}");
            });
        }

        /// <summary>
        /// Everything a create/join needs before talking to the session
        /// service: connectivity, a leftover room closed, Unity Services
        /// initialized and signed in, the NetworkManager free.
        /// </summary>
        async Task<SessionFailure> PrepareAsync()
        {
            if (Application.internetReachability == NetworkReachability.NotReachable)
                return SessionFailure.NoConnection;

            if (_session != null)
                await LeaveAsync();

            var signInFailure = await EnsureSignedInAsync();
            if (signInFailure != SessionFailure.None)
                return signInFailure;

            if (!await WaitForNetworkIdleAsync())
                return SessionFailure.Unknown;

            return SessionFailure.None;
        }

        async Task<SessionFailure> EnsureSignedInAsync()
        {
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    var options = new InitializationOptions();
                    var profile = PlayModeAuthProfile.Resolve();
                    if (profile != null)
                        options.SetProfile(profile);

                    if (!await WithTimeout(UnityServices.InitializeAsync(options)))
                        return SessionFailure.Timeout;
                }

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    if (!await WithTimeout(AuthenticationService.Instance.SignInAnonymouslyAsync()))
                        return SessionFailure.Timeout;

                    Debug.Log($"Tic-Tac-Fade: signed in anonymously. PlayerId={AuthenticationService.Instance.PlayerId}, " +
                              $"profile={AuthenticationService.Instance.Profile}.");
                }

                return SessionFailure.None;
            }
            catch (Exception e)
            {
                var failure = SessionFailureMapper.Map(e);
                Debug.LogWarning($"Tic-Tac-Fade: Unity Services sign-in failed ({failure}): {e}");
                return failure;
            }
        }

        async Task<SessionResult> RunSessionOperationAsync(Func<Task<ISession>> operation, Func<Exception, SessionFailure> mapFailure)
        {
            Task<ISession> task;
            try
            {
                task = operation();
                var finished = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(operationTimeoutSeconds)));
                if (finished != task)
                {
                    Debug.LogWarning($"Tic-Tac-Fade: session operation timed out after {operationTimeoutSeconds}s.");
                    LeaveWhenCompleted(task);
                    return SessionResult.Fail(SessionFailure.Timeout);
                }

                var session = await task;
                if (this == null)
                {
                    LeaveWhenCompleted(task);
                    return SessionResult.Fail(SessionFailure.Unknown);
                }

                Attach(session);
                return SessionResult.Ok();
            }
            catch (Exception e)
            {
                var failure = mapFailure(e);
                Debug.LogWarning($"Tic-Tac-Fade: session operation failed ({failure}): {e}");
                return SessionResult.Fail(failure);
            }
        }

        // A create/join that finishes after we gave up on it would leave an
        // orphan room (and a running NetworkManager): leave it as soon as it
        // completes.
        static async void LeaveWhenCompleted(Task<ISession> task)
        {
            try
            {
                var session = await task;
                await session.LeaveAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Tic-Tac-Fade: cleaning up an abandoned session failed: {e}");
            }
        }

        void Attach(ISession session)
        {
            _session = session;
            session.PlayerJoined += OnPlayerJoined;
            session.PlayerHasLeft += OnPlayerHasLeft;
            session.Deleted += OnSessionGone;
            session.RemovedFromSession += OnSessionGone;

            Debug.Log($"Tic-Tac-Fade: {(session.IsHost ? "created" : "joined")} session, code={session.Code}, players={session.PlayerCount}.");

            if (session.IsHost)
                JoinCodeFormat.CheckGeneratedCode(session.Code);

            // The other player may have joined before we subscribed.
            if (session.IsHost && session.PlayerCount >= MaxPlayers)
                OpponentConnected?.Invoke();
        }

        void Detach(ISession session)
        {
            session.PlayerJoined -= OnPlayerJoined;
            session.PlayerHasLeft -= OnPlayerHasLeft;
            session.Deleted -= OnSessionGone;
            session.RemovedFromSession -= OnSessionGone;
        }

        void OnPlayerJoined(string playerId)
        {
            if (playerId != AuthenticationService.Instance.PlayerId)
                OpponentConnected?.Invoke();
        }

        void OnPlayerHasLeft(string playerId)
        {
            if (playerId != AuthenticationService.Instance.PlayerId)
                OpponentLeft?.Invoke();
        }

        void OnSessionGone()
        {
            var session = _session;
            _session = null;
            if (session != null)
                Detach(session);

            // The SDK stops its own network session when the room goes away;
            // report the loss once the NetworkManager is free again.
            _ = NotifySessionLostWhenIdleAsync();
        }

        async Task NotifySessionLostWhenIdleAsync()
        {
            await WaitForNetworkIdleAsync();
            if (this != null)
                SessionLost?.Invoke();
        }

        /// <summary>
        /// Waits until the NetworkManager is neither listening nor shutting
        /// down, so the next create/join can start it again. If the SDK left
        /// it running past the timeout, it is shut down here as a fallback.
        /// </summary>
        async Task<bool> WaitForNetworkIdleAsync()
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null)
                return true;

            float deadline = Time.realtimeSinceStartup + networkShutdownTimeoutSeconds;
            while (IsNetworkBusy(networkManager) && Time.realtimeSinceStartup < deadline)
                await Task.Yield();

            if (!IsNetworkBusy(networkManager))
                return true;

            if (!networkManager.ShutdownInProgress)
            {
                Debug.LogWarning("Tic-Tac-Fade: NetworkManager still running after leaving the session; shutting it down.");
                networkManager.Shutdown();
            }

            deadline = Time.realtimeSinceStartup + networkShutdownTimeoutSeconds;
            while (IsNetworkBusy(networkManager) && Time.realtimeSinceStartup < deadline)
                await Task.Yield();

            if (IsNetworkBusy(networkManager))
            {
                Debug.LogWarning("Tic-Tac-Fade: NetworkManager did not shut down in time.");
                return false;
            }
            return true;
        }

        static bool IsNetworkBusy(NetworkManager networkManager) =>
            networkManager != null && (networkManager.IsListening || networkManager.ShutdownInProgress);

        async Task<bool> WithTimeout(Task task)
        {
            var finished = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(operationTimeoutSeconds)));
            if (finished != task)
                return false;

            await task; // propagate its exception, if any
            return true;
        }
    }
}
