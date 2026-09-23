using System;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using TicTacFade.Game;

namespace TicTacFade.Net
{
    /// <summary>
    /// Translates SDK exceptions into <see cref="SessionFailure"/>.
    /// Multiplayer Services 2.3.3 converts lobby errors into a
    /// SessionException WITHOUT keeping the original exception: only "lobby
    /// not found" survives as <see cref="SessionError.SessionNotFound"/>;
    /// a full room, a code with invalid characters, etc. all arrive as
    /// <see cref="SessionError.Unknown"/> with just a message. That is why a
    /// full room can't be told apart (verified against the real service,
    /// see ai-log).
    /// </summary>
    public static class SessionFailureMapper
    {
        public static SessionFailure Map(Exception exception)
        {
            var session = FindInChain<SessionException>(exception);
            if (session != null && MapSession(session) != SessionFailure.Unknown) return MapSession(session);

            var request = FindInChain<RequestFailedException>(exception);
            if (request != null && MapRequest(request) != SessionFailure.Unknown) return MapRequest(request);

            var initialization = FindInChain<ServicesInitializationException>(exception);
            if (initialization != null) return MapInitialization(initialization);

            return SessionFailure.Unknown;
        }

        /// <summary>
        /// Same as <see cref="Map"/>, plus the join-specific cases hidden
        /// behind <see cref="SessionError.Unknown"/>: a code-format error
        /// becomes <see cref="SessionFailure.InvalidCode"/>, anything else
        /// the service refused becomes <see cref="SessionFailure.JoinRejected"/>
        /// (e.g. a full room), not a connection problem.
        /// </summary>
        public static SessionFailure MapJoin(Exception exception)
        {
            var failure = Map(exception);
            if (failure != SessionFailure.Unknown)
                return failure;

            var session = FindInChain<SessionException>(exception);
            if (session == null || session.Error != SessionError.Unknown)
                return SessionFailure.Unknown;

            return IsCodeFormatError(session.Message) ? SessionFailure.InvalidCode : SessionFailure.JoinRejected;
        }

        // The SDK drops the lobby error reason (see class summary), so a
        // format error can only be recognized by its message, e.g. "lobby
        // code 'ZZZZZZ' contains an invalid character 'Z' ...". Fragile by
        // nature: JoinCodeFormat rejects these codes locally first, so this
        // only matters for format rules it doesn't cover.
        static bool IsCodeFormatError(string message) =>
            message != null
            && message.IndexOf("code", StringComparison.OrdinalIgnoreCase) >= 0
            && message.IndexOf("invalid", StringComparison.OrdinalIgnoreCase) >= 0;

        static SessionFailure MapSession(SessionException e)
        {
            switch (e.Error)
            {
                case SessionError.SessionNotFound: return SessionFailure.InvalidCode;
                case SessionError.RateLimitExceeded: return SessionFailure.ServiceUnavailable;
                default: return SessionFailure.Unknown;
            }
        }

        static SessionFailure MapRequest(RequestFailedException e)
        {
            switch (e.ErrorCode)
            {
                case CommonErrorCodes.TransportError: return SessionFailure.NoConnection;
                case CommonErrorCodes.Timeout: return SessionFailure.Timeout;
                case CommonErrorCodes.ServiceUnavailable:
                case CommonErrorCodes.TooManyRequests: return SessionFailure.ServiceUnavailable;
                default: return SessionFailure.Unknown;
            }
        }

        // UnityProjectNotLinkedException is internal to the SDK, so it is
        // recognized by name; any other initialization failure means the
        // service could not be reached.
        static SessionFailure MapInitialization(ServicesInitializationException e) =>
            e.GetType().Name == "UnityProjectNotLinkedException"
                ? SessionFailure.NotLinked
                : SessionFailure.ServiceUnavailable;

        static T FindInChain<T>(Exception exception) where T : Exception
        {
            for (var current = exception; current != null; current = current.InnerException)
            {
                if (current is T typed)
                    return typed;

                if (current is AggregateException aggregate)
                {
                    foreach (var inner in aggregate.InnerExceptions)
                    {
                        var found = FindInChain<T>(inner);
                        if (found != null)
                            return found;
                    }
                }
            }
            return null;
        }
    }
}
