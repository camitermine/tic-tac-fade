---
name: new-feature
description: End-to-end workflow for implementing any Tic-Tac-Fade feature or bug fix. Use whenever asked to add, change or fix behavior in the game, so that the spec, tests, implementation, verification and documentation all stay in sync.
---

# New feature / fix workflow

Follow these phases in order. Do not skip the documentation phase — this repo is a portfolio piece about the process, not only about the game.

## 1. Understand and plan

- Read `docs/GDD.md` for the rules involved, and `docs/adr/` if the change is architectural.
- Restate the goal in one paragraph.
- **Flag ambiguity.** If the GDD does not specify the behavior, or the code and the GDD disagree, stop and ask. Never invent a rule.
- Confirm the change is inside the current MVP scope (GDD §7). If it isn't, say so and ask.
- Produce a plan:
  - files to create or modify, by layer (Core / Game / Net / UI);
  - the tests to add, listed by name;
  - risks or edge cases;
  - anything needing a scene or prefab change through MCP.
- **Wait for approval** on anything beyond a trivial fix.

## 2. Tests first (for anything touching Core)

- Write the EditMode tests before the implementation. They must fail for the right reason.
- Name them `Method_Scenario_ExpectedResult`.
- Always include the edge cases, not only the happy path.

## 3. Implement

- Smallest change that makes the tests pass, respecting the architecture rules in `CLAUDE.md`.
- Core first, then Game, then Net/UI. Never let a Unity type leak into Core.
- Do not fix unrelated things you notice. Note them for the report.

## 4. Verify

- Check compilation and the console through the Unity MCP server; zero errors.
- Run the EditMode tests; all green.
- For UI or input changes, say explicitly what still needs manual testing on a device.

## 5. Document

- `docs/GDD.md`: update it if the behavior or a parameter changed. Add a row to the version history for anything meaningful.
- `docs/adr/`: add a new numbered ADR for any architectural decision, using the format of the existing ones (Context / Decision / Consequences).
- `docs/ai-log.md`: **always** append an entry using the template at the top of that file. This is not optional.

## 6. Report

Answer, in this order:

1. What changed (files and why).
2. What was verified, and how.
3. What is still open, or needs a human decision or a device test.
4. The suggested commit message, in Conventional Commits format.

Do not commit. Cami reviews and commits.
