# ADR 0001 — Game rules in a pure C# Core

**Status:** Accepted
**Date:** 2026-09-22

## Context

The initial prototype mixes game logic and presentation. That logic has to serve, unchanged, several uses:

- local mode on one device;
- online, where both clients validate the same moves;
- a future AI and solver, which need to simulate thousands of states per second;
- the roguelike modifiers on the roadmap.

If the rules live in `MonoBehaviour`, each of those uses forces instantiating Unity scenes and objects, and the tests become slow and fragile.

## Decision

- All rules live in the `TicTacFade.Core` assembly, with `noEngineReferences: true` in its asmdef.
- The game state is immutable: `RulesEngine.Apply(state, move) → (newState, events)`.
- The parameters (board size, buffer size, line length, draw limits, timers) come in through `GameConfig`, a pure C# class. The `Game` layer exposes it as a ScriptableObject so it can be edited from the Editor.
- The Core exposes a position hash, used both for the draw-by-repetition rule and to detect desync online.

## Consequences

**For:**

- Fast EditMode tests, with no scenes or Play Mode.
- The compiler prevents coupling Unity to the rules: it doesn't depend on the discipline of whoever writes the code, human or agent.
- The AI, the solver and online reuse exactly the same rules code.
- 4x4 and buffer changes are configuration changes, not code changes.

**Against:**

- A mapping layer between the state and the views is needed, that is, some "bridge" code.
- Immutable states generate memory allocations. It's irrelevant in 3x3, and if the solver needs it, it is optimized there (for example, with a representation packed into an integer).
