---
name: unity-scene-work
description: Rules for touching Unity scenes, prefabs, ScriptableObjects, UI or the Editor in Tic-Tac-Fade. Use for any task that creates or modifies GameObjects, components, prefabs, canvases, input or build settings.
---

# Working inside the Unity Editor

Everything in the Editor goes through the **Unity MCP server**. Scene and prefab files are generated YAML with GUID references: editing them by hand corrupts the project and produces diffs nobody can review.

## Never

- Never hand-edit `.unity`, `.prefab`, `.asset`, `.controller` or `.inputactions` files as text.
- Never create, delete or rewrite `.meta` files.
- Never touch `Library/`, `Temp/`, `Logs/`, `obj/` or `UserSettings/`.
- Never add a package, asset or third-party dependency without asking first.

## Always

- Use the MCP tools to inspect the hierarchy before changing it. Do not assume a GameObject exists.
- After creating or editing scripts, wait for recompilation and **read the console** before continuing. Compile errors block everything downstream.
- When wiring a component reference, do it through MCP and confirm the reference is not null.
- Prefer prefabs over objects loose in the scene, so the change is reusable and reviewable.

## Mobile-first UI

- Target: Android, **portrait**.
- Use Canvas Scaler in `Scale With Screen Size`, reference resolution 1080x1920, match 0.5.
- Respect the **safe area** — notches and gesture bars. Anchor the HUD to a safe-area container, never to hardcoded pixel offsets.
- Minimum touch target: about 48 dp. The board cells are much larger; the concern is menu buttons and the back button.
- Test at a couple of aspect ratios (16:9 and 20:9) in the Game view.
- Input goes through the Input System, never the legacy `Input` class.

## Separation of concerns

- A UI script may **read** the state and **send** an intent (for example, "the player wants to play cell 4"). It must never decide whether a move is legal, or what happens next.
- Views subscribe to Core events (`PiecePlaced`, `PieceFaded`, `GameEnded`). They never poll and never own rules.
- Animations and visual feedback live in the view. The Core does not know that a fade takes 0.3 s.

## Reporting

Scene and prefab changes are almost invisible in a diff, so the report must spell out:

- which scene or prefab changed;
- which GameObjects were created, renamed, reparented or deleted;
- which components were added and how they were wired;
- what needs checking manually in Play Mode.
