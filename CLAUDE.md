# CLAUDE.md — Tic-Tac-Fade

You are working on **Tic-Tac-Fade**, a 2D Unity 6 game. It is tic-tac-toe with a twist: each player can have at most 3 pieces on the board, and placing a 4th removes that player's oldest piece (FIFO).

The project owner, Cami, is an experienced Unity/C# developer who supervises every change. This repo is also a portfolio piece that documents an iterative, AI-assisted workflow, so **process and documentation matter as much as code**.

## Source of truth

- **Game rules and scope:** `docs/GDD.md`. If the code and the GDD disagree, stop and ask. Never silently pick one.
- **Architecture decisions:** `docs/adr/` (one decision per file).
- **Iteration log:** `docs/ai-log.md`.
- **Current phase:** MVP (see GDD §7). Do not build features that are outside the MVP scope unless explicitly asked.

## Environment

- Unity 6, C#, Windows + PowerShell.
- Unity Editor access is through the **Unity MCP server**. Use it to inspect scenes, create or modify GameObjects, prefabs and components, read the console, and check compilation.
- The target platform is Android (portrait). Mobile-first: touch input and safe areas.

## Architecture rules (non-negotiable)

```
Assets/_Project/
  Scripts/
    Core/        TicTacFade.Core        pure C#, noEngineReferences = true
    Game/        TicTacFade.Game        match flow, player controllers, state machine
    Net/         TicTacFade.Net         Multiplayer Services sessions, move sync
    UI/          TicTacFade.UI          views, input, HUD
  Tests/
    EditMode/    TicTacFade.Core.Tests  NUnit tests for Core
  Scenes/ Prefabs/ Config/ Art/ Audio/
```

1. **Core has zero Unity dependencies.** Its asmdef has `noEngineReferences: true`. All rules live here: board, FIFO queues, move validation, win check, draw rules, and the position hash.
2. **Dependency direction:** `UI → Game → Core` and `Net → Game → Core`. Core depends on nothing. UI never talks to Net directly.
3. **The game state is immutable.** `RulesEngine.Apply(state, move)` returns a new state plus the resulting events. No hidden mutation.
4. **Moves are commands.** A `Move` is the only thing sent over the network. Both peers run the same Core rules.
5. **Players are interchangeable** through `IPlayerController` (`LocalHumanPlayer`, `RemotePlayer`, and later `AIPlayer`). The match code must never branch on the player type.
6. **Parameters are configurable, never hardcoded.** Board size, buffer size, win length, timers and draw limits come from `GameConfig`: a plain C# class in Core, wrapped by a ScriptableObject in Game. The win check must be generic (N in a row on an NxN board).
7. **Core communicates outward through events** (e.g. `PiecePlaced`, `PieceFaded`, `GameEnded`). UI observes; it does not poll or own rules.
8. **No premature abstraction.** Use a pattern only when it solves a current problem or an item already on the GDD roadmap. No DI framework for the MVP.

## Coding conventions

- All text inside `.cs` files goes in **English**: identifiers, comments, XML docs, and assertion/log messages. Design documentation (`docs/`) goes in Spanish. Commit messages are in English.
- PascalCase for types, methods and properties; `_camelCase` for private fields; one type per file; namespaces match the asmdef.
- Use `[SerializeField] private` over public fields. Avoid `FindObjectOfType` and string-based lookups.
- No singletons, except a single composition root or bootstrap if one is truly needed.
- Keep methods small and name them for behavior (`TryPlacePiece`, not `DoStuff`).

## Testing

- Every Core rule change needs EditMode tests, written **before** or together with the implementation.
- Cover the edge cases from the GDD explicitly: the replacement restriction, a win that depends on the removed piece, repetition counting with ordered queues, the move cap, and the always-a-legal-move invariant.
- Running tests:
  - Use the MCP test tool if the server exposes one.
  - Otherwise, in batch mode (with the Editor closed):
    ```powershell
    & "<UnityEditorPath>\Unity.exe" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults ./TestResults/editmode.xml
    ```
- A task is **not done** until:
  - the project compiles with no console errors (check via MCP), and
  - the tests pass.

## Working with the Unity Editor

- **Never** hand-edit `.unity`, `.prefab` or `.asset` YAML files. Use the MCP tools.
- **Never** delete or regenerate `.meta` files, and never touch `Library/`, `Temp/`, `Logs/` or `UserSettings/`.
- After creating scripts, wait for recompilation and check the console before continuing.
- Scene or prefab changes must be described in the summary, because they are hard to review in a diff.

## Workflow for every task

1. **Plan first.** Restate the goal, list the files you will touch, name the tests you will add, and flag any GDD ambiguity. Wait for approval on anything non-trivial.
2. **Work in small steps.** One feature or fix per branch/commit, following Conventional Commits (`feat(core): ...`, `fix(ui): ...`, `test(core): ...`, `docs: ...`).
3. **Verify.** Compile, run the tests, and check the console via MCP.
4. **Document.**
   - Update the GDD if the behavior changed.
   - Add an ADR if an architectural decision was made.
   - **Always** append an entry to `docs/ai-log.md` (template inside that file).
5. **Report back.** Summarize what changed, what you verified, and what is left open.

## Do not

- Do not add packages or assets without asking.
- Do not change GDD rules on your own. Propose the change instead.
- Do not commit or push unless asked. Cami reviews and commits.
- Do not "fix" unrelated code while working on a task. Note it in the report instead.
