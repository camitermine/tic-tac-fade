# ADR 0002 — Online with Unity Multiplayer Services (sessions + room code)

**Status:** Accepted
**Date:** 2026-09-22

## Context

The MVP needs 1v1 online matches between phones, with a "I create a room, I give you a code, you join" flow. Two phones on different mobile networks can't connect directly because of NAT and firewalls, so an intermediary is needed.

Alternatives considered:

1. **Unity Multiplayer Services (Sessions) + Relay + Netcode for GameObjects.**
2. **Own backend** (for example, WebSockets on our own server).
3. **Third-party services** such as Photon.

## Decision

Use **Unity Multiplayer Services (package `com.unity.services.multiplayer`)** with sessions and Relay.

- The host creates the session and the service generates a short **join code** that is easy to dictate.
- The client joins with `JoinSessionByCodeAsync(joinCode)`.
- The SDK takes care of the lobby, the Relay allocation and establishing the Netcode connection.
- Host-client model, with the host as the authority over the rules and the timer.

**Moves** (`Move`) travel over the network, not the board state. Each client applies the same Core rules and compares the position hash to detect desync.

## Consequences

**For:**

- It is exactly the "room with a code" flow the GDD asks for, with no own backend.
- Relay solves NAT and firewalls: both clients always see the same IP and port.
- It is integrated with Unity 6 and with Netcode for GameObjects.
- It supports Web if Unity Transport 2.0+ is used with WebSockets, which leaves the door open to the WebGL build for the portfolio.

**Against / risks:**

- **Dependency on Unity Gaming Services:** it requires an account, a linked project and anonymous authentication.
- **Usage cost** beyond the free tier. It has to be monitored, although for a portfolio project it should be more than enough.
- **If the host drops, the match drops.** Accepted in the MVP: the client goes back to the menu with a message.
- **Reconnection** is *best effort* in the MVP. The absent mode in GDD §3.5 gives some margin, but doesn't guarantee getting back into the match.

## Coupling mitigation

The `Net` layer hides behind our own interface (`IMatchTransport` or similar) in `Game`. If the provider has to change later, the Core and the UI are not touched.

## Implementation (updated 2026-09-23, online iteration 1: rooms without sync)

The decision stands. How it was implemented and what it makes more precise about the above:

- **Interface:** the one for the room lifecycle is called `ISessionService` (in `Game`): create, join with code, leave, and events for opponent connected/disconnected and room lost. Failures come back as our own enum (`SessionFailure`), never as SDK exceptions. Sending moves (what this ADR called `IMatchTransport`) belongs to the next iteration and may be another interface.
- **Wiring without singletons:** Unity doesn't serialize interface fields, so the scene references an abstract base `SessionServiceBehaviour : MonoBehaviour, ISessionService`. `TicTacFade.Net` provides `UgsSessionService` and the tests a fake with no network.
- **Packages:** `com.unity.services.multiplayer` 2.3.3, `com.unity.netcode.gameobjects` 2.13.3 and `com.unity.multiplayer.playmode` 3.0.0 (builtin in Unity 6.6).
- **Sessions:** private, `MaxPlayers = 2`, with `WithRelayNetwork()`. The SDK starts the Netcode host/client on the scene's `NetworkManager` + `UnityTransport`, and fails if it doesn't exist. Leaving uses `LeaveAsync` (the SDK asks not to call `NetworkManager.Shutdown()` by hand). The host uses `DeleteAsync`, so the other player sees the room closed instead of becoming the migrated host of an empty room. Before each create/join it waits for the `NetworkManager` to be free.
- **Lazy initialization:** Unity Services and anonymous authentication are initialized on the first "Crear sala" or "Unirse", not at startup. Local mode never touches UGS (a PlayMode test verifies it).
- **Multiplayer Play Mode:** each virtual player uses its own authentication profile, derived from its clone's path (`PlayModeAuthProfile`). The Authentication package already does this by reading command-line arguments, but as undocumented internal behavior; here it is made explicit.
- **SDK limitation (verified against the real service):** `LobbyConverter.ToSessionException` discards the original exception and only keeps "lobby not found" (→ `SessionNotFound`). Full room, code with invalid characters, etc. arrive as `SessionError.Unknown` with only a message. How it is handled:
  - **Code format, validated locally** (`JoinCodeFormat`, in Net and behind `ISessionService.IsWellFormedCode`, because the format belongs to the provider): alphabet `6789BCDFGHJKLMNPQRTW`, 6 to 12 characters. The SDK documents it for Relay join codes; for Lobby ones there is no documentation, but the real service is consistent with that rule (see ai-log [13]). A code out of format gives "Código inválido" without going to the network.
  - If the service still returns a format error, it is recognized by the message (the only signal the SDK leaves) and also gives "Código inválido".
  - Any other rejection when joining (e.g. full room) gives a generic message: "No se pudo unir a la sala. Revisá el código o pedí uno nuevo.".

## Implementation (updated 2026-09-23, online iteration 2: playable match)

### Move transport: Netcode named messages, not RPCs

- **Decision:** `NgoMatchTransport` (in `TicTacFade.Net`) uses `CustomMessagingManager` with a single named message (`TicTacFade.Match`), identified by a type byte. RPCs were ruled out because they need a `NetworkObject`: spawning it with a registered prefab, or putting it in the scene. The scene is generated by the builder in code, and an in-scene `NetworkObject` needs a `GlobalObjectIdHash` that the editor computes when validating it; generating it by script is fragile and a mistake there silently breaks the connection. Named messages travel directly over the `NetworkManager`, with no objects or prefabs.
- **Delivery:** every message goes with `NetworkDelivery.ReliableSequenced`, explicit in the code (constant `Delivery`) even though it is the method's default. A lost or out-of-order confirmed move can't be left to the hash comparison to detect.
- **Interface:** `IMatchTransport` (in Game), with a serializable base `MatchTransportBehaviour` for the scene, the same as `ISessionService`. The tests use an in-memory transport with a queue and explicit pumping.
- **Messages:** `StartMatch`, `Propose`, `Confirmed` (move, move number, `PositionKey.Value` after applying it), `Rejected`, `Ack` (move number and the client's key), `RematchRequest`, `Desync`. Moves travel, never the board.
- **Inbound buffer:** whatever arrives before the device's flow opens the channel (`Open`) is stored and delivered on opening. It covers the case of a `StartMatch` the host sends as soon as the client connects, before the join has finished on the client side. (Since 2026-10-06 the start no longer depends on this buffer: see the `Ready` handshake below.)

### Authority and sync

- **The host is the authority** (`OnlineMatch`, in Game). Every proposal, whether a tap on the host or a message from the client, goes through the same validation: `RulesEngine.IsLegal` plus "each device only moves its own symbol". An illegal proposal is rejected and no state changes.
- **On both devices `GameManager` only receives confirmed moves**, through each side's controller: `OnlineLocalPlayer` (this device's human, who proposes and waits) or `RemotePlayer`. `GameManager` doesn't know there is a network.
- **Hash comparison:** the host sends its `PositionKey.Value` with each confirmation. The client applies, compares and returns an `Ack` with its key, and the host compares too. On any difference: `Debug.LogError`, `Desync` to the other side, the session is closed and both go back to the menu. Never keep playing out of sync.
- ~~**Start:** it is triggered by the Netcode connection (`OnClientConnectedCallback` on the host), not by the lobby join, because messages can't be sent before that.~~ Replaced by the `Ready` handshake (see "Start handshake and protocol version").
- **Symbols:** the host is always X and the client always O (GDD §3.1). The rematch requires both to ask; the host announces it with `StartMatch`, swapping who starts.
- ~~Pending (iteration 3): if a client proposal gets neither `Confirmed` nor `Rejected` (host down), `OnlineLocalPlayer` stays waiting with the input locked.~~ Solved in iteration 3 (see below).

## Implementation (updated 2026-09-23, online iteration 3: timer, abandonment and disconnections)

- **Host-authoritative timer:** at the start of each turn the host sets the deadline and announces it with `TurnTimer` (player, duration in ms, who is absent). The client only shows the countdown from when it receives the notice, without compensating for latency; it never decides a timeout. On expiry, the host picks the automatic move and broadcasts it as one more `Confirmed`, so the client needs no separate logic to apply it.
- **Injected time and randomness** (`IClock`, `IRandomSource`, in Game): `OnlineMatch` doesn't read Unity's clock or `System.Random` directly. `MatchFlow` advances it with `Tick()` once per frame. The tests use a manual clock.
- **Parameters:** the timer ones (`OnlineTimerConfig`: 30 s, 10 s, 3 timeouts) are game parameters, live in Game and are edited in `GameConfigAsset`. The Core doesn't have them, because the `RulesEngine` never reads them (CLAUDE.md rule 6, rewritten in this iteration). The network waits (`OnlineNetworkSettings`: 10 s without an answer to a proposal, 2 s for the "Salir" ack, 5 s of grace after a disconnection) are `SerializeField`s of `MatchFlow`.
- **Abandonment as a match result, not a board rule:** `MatchResult` (Game) wraps the Core's `GameEndedEvent` or an abandonment with its cause (`Timeouts`, `Quit`, `Disconnected`). `GameManager.Halt()` freezes the match without touching the `GameState`. The Core didn't change.
- **New messages** (same named message, `ReliableSequenced`): `TurnTimer`, `Abandoned` (loser and cause, host → client), `Forfeit` and `ForfeitAck` (in both directions). `MatchMessage` adds `DurationMs`, `AbsentFlags` and `Cause`.
- **"Salir" with confirmation:** whoever leaves sends `Forfeit` and waits for `ForfeitAck` (up to 2 s) before closing the session. If it closed right away, the network shutdown could discard the notice. Without an ack, it closes anyway and the other side falls into the disconnection case.
- **Disconnection detection:** `IMatchTransport.PeerDisconnected`, which in `NgoMatchTransport` comes from `OnClientDisconnectCallback`. The session events also count (`PlayerHasLeft` on the host, `Deleted`/`RemovedFromSession` on the client). Both paths end in `OnlineMatch.NotifyPeerGone()`, which is idempotent.
  - Client down mid-match: the host waits a 5 s grace and then the client loses by abandonment. The 3 timeouts are left for the connected opponent who doesn't play.
  - Host down mid-match, or a proposal without an answer in 10 s: the client goes back to the menu with "Se perdió la conexión con el rival".

## Implementation (updated 2026-10-06: start handshake and protocol version)

**Context:** in two tests with phones, the client stayed in the waiting room while the match started on the host (ai-log [15]). Afterwards it didn't reproduce and the cause wasn't confirmed. Instead of continuing to look for it, the start was changed so that this class of failure can't leave one side waiting forever (ai-log [16]).

- **`Ready` handshake:** the host no longer starts on the Netcode connection.
  - The client sends `Ready` when its flow is ready (`OnlineMatch.Start`) and resends it every 1 s (`OnlineNetworkSettings.ReadyResendIntervalSeconds`, a `SerializeField` of `MatchFlow`) until it accepts the first `StartMatch`.
  - The host starts the first match with the first `Ready`. To every later `Ready` it answers by resending the current `StartMatch`, and it never starts another match because of that. That way, a lost `StartMatch` is recovered with the next `Ready`.
  - `IMatchTransport.PeerConnected` and `IsPeerConnected` were removed, as they ended up unused.
- **Match number:** `StartMatch` carries `MatchNumber`, a field of its own that is 1 in the first match and adds 1 on each rematch. The client accepts only the number it expects and discards any repeated or earlier `StartMatch`. A single rule covers the duplicated `Ready`, the resend and an old `StartMatch` arriving late after a rematch, without depending on when a flag is cleared.
- **Timer on the resend:** if the match is in progress, the resend includes a `TurnTimer` with the **remaining time according to the host's clock**, not the full duration. Otherwise, the client would see more time than it has and the host would cut its turn before its countdown reaches zero.
- **`Ready` without connection:** the client can send `Ready` before Netcode has connected, especially on a cold start. `CustomMessagingManager.SendNamedMessage` doesn't check the connection (NGO 2.13.3), so `NgoMatchTransport.Send` doesn't call it while `!IsConnectedClient`. It silently drops the `Ready`, with a diagnostic log off by default, and the resend takes care of the rest. The other messages keep the usual warning.
- **Frame format** (`MatchMessageCodec`, in Game, shared by `NgoMatchTransport` and the tests' in-memory transport):
  - it starts with a fixed header that never changes: `kind`(1) + protocol version(2), followed by the fields;
  - it is 31 bytes in total;
  - the `MatchMessageKind` values are part of the protocol and are not renumbered.
- **Protocol version:** `MatchMessageCodec.ProtocolVersion = 1` (it is the first version with a number).
  - Every frame carries it in the header. In practice, the first one checked is the client's `Ready`.
  - If it doesn't match, only the header is decoded, regardless of the size, and both sides go back to the menu with "El rival tiene otra versión del juego".
  - The host sends `VersionMismatch` and waits for the client's answer (or its drop, or at most 2 s) before closing, so the client sees that message and not "La sala se cerró". The client answers and leaves right away.
- **Tolerant reading:** the decoder never throws. It discards with a warning ("dropped malformed match message") a frame shorter than the header, an unknown `kind` or a frame of the same version with an unexpected size. It replaces the field-by-field reading that threw `OverflowException` with messages of another size.
- **Compatibility:** previous builds (without version or `Ready`) are **not** compatible. A new host with an old client stays waiting in the room, because a `Ready` never reaches it. From version 1 on, any change of format or meaning bumps `ProtocolVersion`.

## References

- https://docs.unity.com/ugs/en-us/manual/mps-sdk/manual/join-session
- https://docs.unity.com/mps-sdk/working-with-mps
- https://docs.unity.com/relay/relay-and-ngo.html
