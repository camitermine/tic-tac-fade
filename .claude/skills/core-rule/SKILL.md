---
name: core-rule
description: How to add or change a game rule inside TicTacFade.Core. Use when the task touches the board, the FIFO queues, move validation, win detection, draw conditions, the position hash or GameConfig.
---

# Changing a Core rule

The Core is the heart of the project: the local game, the online game, the AI and the future solver all run through it. Treat every change here as high risk.

## Hard constraints

- `TicTacFade.Core` has `noEngineReferences: true`. No `using UnityEngine`, no `MonoBehaviour`, no `ScriptableObject`, no `Debug.Log`, no coroutines, no `Random.Range`.
- The state is immutable. `Apply` returns a new state; it never mutates its input.
- `Apply` must be **deterministic**: same state plus same move always gives the same result. Any randomness (for example, the timeout auto-move) is injected through an interface or a seed, never taken from a global.
- No hardcoded numbers. Board size, buffer size, win length, `MaxTotalMoves` and `RepetitionLimit` come from `GameConfig`.
- The win check is generic: N in a row on an NxN board. Never nine hardcoded win lines.

## Order of resolution (GDD §3.3)

Any change must preserve this order:

1. Validate the move.
2. Place the new piece.
3. Apply FIFO (remove the oldest piece if the queue overflowed).
4. Check for a win.
5. Check for a draw.

A line that depended on the piece just removed is **not** a win.

## Invariants to preserve

- **Replacement restriction:** a player cannot place on the cell its own expiring piece is currently occupying.
- **There is always a legal move** (GDD §3.6). If a change can break this, stop and ask before implementing.
- **Position identity:** a position is both players' queues *in order*, plus whose turn it is. Two visually identical boards with different queue orders are different positions.

## Tests required for every change

Add EditMode tests covering at least:

- the happy path;
- the replacement restriction;
- a win that would only exist if the removed piece were still there (must not be a win);
- a win formed by the new piece (must be a win);
- repetition counting, including that queue order matters;
- the move cap;
- rejection of invalid moves (occupied cell, out of bounds, not your turn, game already over);
- behavior with a non-default `GameConfig` (for example 4x4, buffer 4), to prove nothing is hardcoded.

## After implementing

- Update `docs/GDD.md` if the rule or a parameter changed.
- Append an entry to `docs/ai-log.md`.
- Report which invariants you verified and how.
