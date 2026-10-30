# ADR 0003 — Player abstraction via IPlayerController

**Status:** Accepted
**Date:** 2026-09-23

## Context

Until now `GameManager.OnCellClicked(int)` assumed two things: that every move comes from a UI tap, and that the player making it is always `CurrentState.CurrentPlayer` — it built the `Move` right there. That works for local mode (two humans on the same device), but it blocks everything that needs a different source of moves: online (`docs/adr/0002-online-multiplayer-services.md`, where the opponent's moves arrive over the network) and the roadmap AI (GDD §8.2).

CLAUDE.md already declared this rule as non-negotiable (architecture, rule 5: "Players are interchangeable through `IPlayerController`... The match code must never branch on the player type"), but it wasn't implemented.

## Decision

- `IPlayerController` (Game layer) is the only source of moves `GameManager` knows: it exposes `Player`, a `MoveChosen` event and two hooks, `NotifyTurnStarted(GameState)` (so an autonomous controller can act without outside intervention) and `NotifyCellSelected(int)` (so the UI can keep talking to `GameManager.OnCellClicked` exactly as before, without knowing what type of controller is on the other side).
- `LocalHumanPlayer` is the first implementation: it does nothing in `NotifyTurnStarted` (it waits for the tap), and translates `NotifyCellSelected` into a `Move` by raising `MoveChosen`.
- `GameManager` receives the two controllers via `Initialize(IPlayerController, IPlayerController)`, public and re-callable (it unsubscribes the previous ones before subscribing the new ones). If nobody called it before `Awake()`, `GameManager` creates the default `LocalHumanPlayer` pair — that way local mode keeps working with no extra wiring, and a test (or, in the future, an online/AI setup flow) can inject something else.
- The controllers **are not wired from the scene builder**: an interface-typed field with an `event` doesn't survive Unity serialization (it isn't a `UnityEngine.Object`), so trying to assign them by reflection when building the scene would be lost as soon as it is saved/reloaded. That's why they live entirely in C# code (`Awake()`/`Initialize()`), not in the `.unity`.
- Turn advancing (`GameManager.AdvanceTurns`) is a loop with a re-entrancy flag, not a recursive call. The first version notified the next turn by calling directly from inside the handling of the previous move — with two autonomous controllers that stacks one stack frame per move for the whole match, and a rejected move that doesn't break the cycle produces infinite recursion (StackOverflow, hangs the Editor with no catchable exception). The re-entrant loop makes a full match between two autonomous controllers run in a single stack frame, and a rejected move (`MoveRejectedEvent`) breaks the loop instead of retrying.

## Consequences

**For:**

- `GameManager` never does `if (controller is LocalHumanPlayer)`: adding `RemotePlayer`/`AIPlayer` doesn't touch its code, it only implements the interface.
- Local mode keeps working with no configuration changes (default in `Awake()`).
- A full match can be tested with no UI or real scene beyond the `GameManager`: two fake controllers are enough (see `FakeAutoPlayer` in `Assets/_Project/Tests/PlayMode/`).
- The UI (`BoardView`, `GameHud`, `RestartButton`) didn't change a single line: they keep calling `OnCellClicked`/`StartNewGame`/reading `CurrentState` exactly the same.

**Against:**

- The injection point (`Initialize`) is manual: there is no UI/Editor mechanism yet to choose "Local vs Local" / "Local vs AI" ahead of run time. It gets solved when that flow exists (online or AI).
- `GameConfigAsset` (the ScriptableObject wrapper of `GameConfig`, already decided in ADR 0001) doesn't validate the ranges of its fields in the Inspector — noted debt, not blocking for the MVP.
