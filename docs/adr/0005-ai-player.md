# ADR 0005 — AI: precalculated table, effective evaluation and levels

**Status:** Accepted (phase 1: the AI; phase 2: the UI and the flow).
**Date:** 2026-10-29

## Context

The roadmap (GDD §8.2) asks for a vs-AI mode with difficulties "based on the solver plus noise". The solver (ADR 0004) gives the value of each position without taking into account the match history or the move cap. It also leaves pending the choice between computing the table at startup or storing it as an asset.

## Decision

### Precalculated table
- **Compact binary asset** at `Assets/_Project/Config/AI/SolverTable.bytes`, which the game loads from a serialized field (`TextAsset`), never from `Resources`. **The solver never runs during a match.**
- **Generation:** menu `Tic-Tac-Fade/Solver/Generate AI table`, with the rules of the project's `GameConfigAsset`.
- **Format** (`CompactSolverTable`, in the solver assembly):
  - header: magic, format version, `BoardSize`, `BufferSize` and `WinLength`;
  - the sorted position keys (`ulong`);
  - one byte per position: result (2 bits) and distance (6 bits).
  - The draw rules are not stored, because the solver ignores them.
- **It contains the positions of both possible starting players.** The rematch alternates who starts, and the opening phase with O starting doesn't appear in X's table. That is 133,364 positions and 1.17 MB.
  - Storing the keys as `uint` (25 bits are enough in 3x3) would bring it to about 650 KB. It wasn't done, so as not to tie the format to 3x3.
- **Getting out of sync with the rules:**
  - on load, if the board rules don't match, it fails with a message asking to regenerate;
  - a Slow category test regenerates the table and compares it byte by byte with the asset.
- **Dependencies:** `Game → Solver → Core`. The Core didn't change.

### Effective evaluation of each move
The AI plays each legal move with `RulesEngine.Apply` on the real match state. That way, the third pass through a position and the cap show up as the draws they are, without exposing the Core's repetition count. Then it looks up the resulting position in the table. The order, from best to worst:

1. **Immediate win.**
2. **Forced win without escape:** it arrives before `MaxTotalMoves` and no reply by the opponent produces a third repetition. The shortest is best.
3. **Forced win with escape:** the opponent has a reply that draws by repetition.
4. **Match alive (`NoForcedWin`):** neither side forces the win, or the forced win or loss doesn't arrive before the cap.
5. **Immediate draw,** by repetition or by the cap.
6. **Forced loss before the cap.** The longest is best.

Consequences of the order:
- **When losing**, the AI prefers any draw over the loss.
- **When winning**, it avoids the third repetition, which becomes worth a draw.
- **Between draws** (Cami's decision), it prefers keeping the match alive so the human can make a mistake. It takes the immediate draw only when the alternative is losing.
- **Between equally good moves** it picks at random.
- **Depth:** the escape analysis looks at **a single reply by the opponent**. Nothing deeper is analysed. A forced line can be cut later by a repetition that isn't visible two moves ahead, and in that case the AI finds out when it gets there.

### Levels
- **Fácil (Easy), Normal and Perfecta (Perfect)**, with an error chance of 50 %, 20 % and 0 %, plus two flags. The four values of each level are edited in `GameConfigAsset`:
  - **take the immediate win:** if there is one, it is played without rolling for error;
  - **block the immediate loss:** the random move of an error never leaves the opponent an immediate win, if some move avoids it.
- Normal and Perfecta have both flags on; Fácil, neither.
- **On error**, the AI plays a random legal move, uniform among all of them (it may coincide with the best). Without error, it plays the best according to the evaluation above.
- **Order of the random draws,** so the tests can control it:
  1. none, if it takes an immediate win;
  2. the error roll, `Next(100) < ErrorPercent`;
  3. the random move or the tie-break, only if there is more than one option;
  4. the delay, only if its range is not a single value.

### Delay and cancellation
- **The AI waits a random delay** between a minimum and a maximum (by default between 0.6 and 1.4 s; 0 in the tests). It decides when its turn starts and delivers the move in `Tick()` once the delay has passed, with an injected `IClock`.
- **With delay 0 it plays synchronously,** like any autonomous controller. The `GameManager` turn loop already supports both ways.
- **`IPlayerController.NotifyMatchStopped()`** (Cami's decision):
  - `GameManager` calls it when the match ends, is discarded, is halted (`Halt`), another one starts or the controllers are replaced;
  - the AI drops the pending move there, and the other controllers do nothing.
- **Second safety net:** `GameManager` ignores, with a warning, a move that arrives with no match in progress or with the match over. Before, such a move threw a `NullReferenceException`.

### Flow and UI (phase 2)
- **A single new state, `AILevelSelect = 5`,** at the end of the enum, with "volver" to the menu.
  - The match and the result reuse `Playing` and `Result`, as online does. What changes is which controllers `MatchFlow` plugs in, and the texts the UI reads from `MatchFlow.IsVsAI`.
  - Two more states (`PlayingVsAI`, `ResultVsAI`) would have duplicated the screens' visibility and the transitions without adding behavior.
- **`MatchFlow`:**
  - owns the `AIPlayer` and advances it from its `Update`, the same as with `OnlineMatch`;
  - loads the table only once, the first time the AI is played, from the serialized `TextAsset`;
  - the table is validated when "Jugar contra la IA" is tapped, before offering the levels. If it is missing or doesn't match the rules:
    - the details go to the console;
    - the player sees "La IA no está disponible en esta versión del juego" in the menu's error line;
    - the flow stays in the menu.
  - **Cami's decision:** never a button that does nothing. The error uses the same channel as the menu's other errors (`LastFailure`), with a new value, `SessionFailure.AIUnavailable`. The enum's name fell short ("session"); renaming it is a separate change.
- **When going back to the menu** (from the result or with "Salir"), `MatchFlow` puts the two `LocalHumanPlayer`s back, the same as when closing an online match. "Salir" discards the match, and that cancels the AI's pending move (`NotifyMatchStopped`).
- **The scene is generated by the builder** (`Tic-Tac-Fade/Build Placeholder Scene (1v1)`), which adds:
  - the "Jugar contra la IA" button;
  - the `AILevelScreen` screen;
  - the reference to the table in `MatchFlow`.

## Consequences

**For:**
- The Core didn't change and still doesn't know the solver or the AI.
- The table loads in milliseconds and doesn't depend on the platform (WebGL included).
- Perfecta always wins when it starts: against itself in 13 moves, and against a random opponent in every match tested.
- The levels are data, not code: tuning them by playing doesn't require touching anything.

**Against:**
- The asset has to be regenerated when a board rule changes. The Slow test detects it if someone forgets.
- Evaluating a position costs up to 81 `Apply` calls (each move plus each reply), with a copy of the repetition dictionary. It's negligible once per turn, but it makes the tests that evaluate thousands of positions slow.
- The tests' repetition scenarios are not fixed positions: they are found by seeded matches that try to return to positions already seen. With the limit of 3, the "win with escape" case barely shows up in those matches, so its test uses `RepetitionLimit = 2`. It is valid because the evaluation only reads the Core's `DrawByRepetition`, never the limit.
