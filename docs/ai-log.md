# AI Iteration Log

> **Language note:** this project was developed in Spanish. This log, like the rest of the documentation, was translated to English on 2026-10-29; the Spanish originals are preserved in the git history. Quoted in-game UI strings stay in Spanish, because they are the game's current text.

Record of the AI-assisted development process. One entry per iteration, the most recent at the top.

The goal is to make visible **what the AI was asked, what it produced and what the supervisor decided or corrected**.

## Template

```markdown
## [N] YYYY-MM-DD — Short title

**Goal:** what was meant to be achieved.
**Request / prompt:** summary of the request (or link to the approved plan).
**What the AI did:** files, tests and main changes.
**Human review:** what was accepted, what was corrected and why.
**Verification:** compiles / tests / tested on device.
**Learnings:** what would change in the instructions, skills or CLAUDE.md as a result of this.
**Commit(s):** hash or PR.
```

---

## [1] 2026-09-22 — Verification of the Core extraction and test fix

**Goal:** pick up the work in progress on the `feat/core-extraction` branch (migration from `Assets/Scripts` to `Assets/_Project/Scripts` with the split into the `TicTacFade.Core`/`Game`/`UI` asmdefs, plus `RulesEngine`, `GameState`, `GameConfig`, events and Core tests) and verify that it compiles and that the tests pass before continuing.

**Request / prompt:** session continuation with no new instruction; it was interpreted as "verify the current state of the pending work on the branch".

**What the AI did:**
- Checked the Unity console via MCP: 0 compilation errors/warnings.
- Ran the 33 EditMode tests of `TicTacFade.Core.Tests`: 1 failure in `RulesEngineConfigurableTests.Apply_NonDefaultConfig_FifoStillWorks` (expected `ActiveCount(X) == 4`, got 3).
- Used `execute_code` (MCP) to reproduce the test's move sequence outside the test runner and isolated the cause: O's cells in the test (`12,13,14,15`) form a winning horizontal line (row 3 of a 4x4 board, `WinLength=3`), so the match ended with O's win before X reached its 4th piece; the following moves were rejected (`GameAlreadyEnded`) and X's queue never grew to 4. The bug was in the test data, not in `RulesEngine`/`WinChecker`.
- Fixed `RulesEngineConfigurableTests.cs`: replaced O's cells with `2,4,11,13` (a "4 non-attacking queens" kind of pattern, with no possible 3 in a line) and added a comment explaining why. Verified the new sequence with `execute_code` before touching the test file.
- Recompiled and ran the full suite: 33/33 tests OK, 0 errors/warnings.

**Human review:**
- Decisions made in plan mode (previous session, before connecting the Unity MCP):
  - Generic end-of-match banner, based on the `GameEndReason` enum (not a different hardcoded message for each case).
  - Don't create the `Net/` asmdef/folder yet.
  - Don't move `Assets/Scenes/` in this task.
- Changes Cami asked for on the plan proposed by the AI:
  - `PositionKey` as an exact key (bit packing), instead of an FNV hash.
  - Public `RulesEngine.IsLegal`.
  - `InternalsVisibleTo` so the tests can access `PositionCounts` without exposing it as public.
  - Add tests for state immutability and event order.
  - Commit before regenerating the scene.
- Test that failed during verification: `RulesEngineConfigurableTests.Apply_NonDefaultConfig_FifoStillWorks`. Root cause: O's cells in the test sequence (`12,13,14,15`) formed a winning horizontal line on a 4x4 board with `WinLength=3`, so the match ended with O's win before the sequence reached X's 4th piece. The test was modified (O's cells replaced with `2,4,11,13`); `RulesEngine.cs` and `WinChecker.cs` were not modified.

**Verification:** compiles with no errors (Unity console via MCP) and the 33 EditMode tests of `TicTacFade.Core.Tests` pass.

**Learnings:** when writing tests with a non-default `GameConfig` (winLength different from boardSize), it is worth picking the "filler" player's cells by hand so they aren't collinear, or adding a test helper that builds sequences guaranteed to have no winning line (e.g. an N-queens pattern) to avoid this kind of false negative.

**Commit(s):** — (not committed; Cami reviews and commits).

**Pending:** closed, see entry [4].

---

## [2] 2026-09-22 — Helper to harden Core tests against contaminated sequences

**Goal:** prevent the problem from entry [1] from happening again (a "filler" move that accidentally completes a line or ends the match, and silently ruins the position the test meant to build).

**Request / prompt:** Cami asked for (1) a helper that applies a sequence of moves and fails if any produces a `MoveRejectedEvent` or if the match ends earlier than planned, (2) using it in the tests that build positions by sequence, and (3) commenting in `RulesEngineConfigurableTests` why O's cells are `2,4,11,13`. Explicit constraint: don't change any rule or any existing assertion that passes today.

**What the AI did:**
- Created `Assets/_Project/Tests/EditMode/MoveSequence.cs` (internal class `MoveSequence`, method `Apply(GameState, params Move[])`): it applies the moves in order and uses `Assert.Fail` if any intermediate one returns a `MoveRejectedEvent`, or if `state.IsOver` becomes `true` before the last move of the sequence (the last one can end the match on purpose, so as not to break tests that build a position and win in the same stroke).
- Used it in the "position building" stretches (sequences of `RulesEngine.Apply` with no interleaved assertions, followed by a separate Act/Assert) of: `RulesEngineFifoTests` (2 tests), `RulesEngineWinTests` (2 tests), `RulesEngineMoveValidationTests` (2 tests) and `RulesEngineConfigurableTests` (2 tests).
- Did **not** apply it where the sequence already had assertions interleaved move by move (`RulesEngineImmutabilityAndEventsTests.Apply_EventOrder_...`, the repetition tests in `RulesEngineDrawTests`, `GameInvariantTests.PlayLongMatch_...`, `RulesEngineFifoTests.GetLife_...`): collapsing those sequences into the helper would have meant deleting assertions that pass today, something Cami explicitly asked not to do. It tried once in `RulesEngineImmutabilityAndEventsTests` and reverted it on noticing it removed 6 existing `Assert.IsFalse(state.IsOver)`.
- Expanded the comment in `RulesEngineConfigurableTests.Apply_NonDefaultConfig_FifoStillWorks` explaining the 4 non-attacking queens pattern of O's cells (2,4,11,13) and why it was needed (reference to the bug in entry [1]).
- Didn't touch `RulesEngine.cs`, `WinChecker.cs` or any Core rule.

**Human review:** pending (to be validated by Cami).

**Verification:** compiles with no errors/warnings (Unity console via MCP) and the 33 EditMode tests of `TicTacFade.Core.Tests` keep passing 33/33 after the changes.

**Learnings:** when a test "builds a position" with a sequence of moves and the real focus is on the final move, it is worth explicitly separating the building (which shouldn't be able to end the match or be rejected) from the Act — that way a contamination like the one in entry [1] fails loudly in the building instead of producing a confusing assertion later. A test with poorly chosen data can trigger another rule by accident and fail with a message that doesn't point to the cause; tests that build scenarios by move sequence must first verify their own premise.

**Commit(s):** — (not committed; Cami reviews and commits).

---

## [3] 2026-09-22 — All the text in the .cs files in English

**Goal:** comply with the `CLAUDE.md` convention that C# code goes in English, which had not been respected so far (comments, XML docs and `Assert.Fail`/log messages in Spanish across the whole branch).

**Request / prompt:** Cami asked to fix, before the commit, all the Spanish text left in the branch's `.cs` files (comments, XML docs, `Assert.Fail`/log messages), without touching logic or names; to make the `CLAUDE.md` conventions line explicit about which text goes in English; and to record in the ai-log why the rule was made more precise.

**What the AI did:**
- Translated into English every comment, XML doc and `Assert.Fail`/`Debug.Log`/exception message in the 30 `.cs` files of `Assets/_Project/` (Core, Game, UI, Editor and Tests), without changing logic or identifiers.
- Left untouched the UI text the player sees in the game (`GameHud.cs`: "Turno: Jugador X", "Empate por repetición de posición", etc., and the "Reintentar" label of the restart button in `TicTacFadeSceneBuilder.cs`): it is product content, not developer text, and Cami's instruction specifically listed comments/XML docs/assertion and log messages, not UI text.
- Verified with `grep` (accents, ñ, ¿¡ marks, and Spanish keywords) that no text was left outside those UI strings.
- Made the `CLAUDE.md` conventions line precise: "All text inside `.cs` files goes in English: identifiers, comments, XML docs, and assertion/log messages. Design documentation (`docs/`) goes in Spanish. Commit messages are in English."
- Recompiled and ran the full suite: 33/33 EditMode tests of `TicTacFade.Core.Tests` OK, 0 errors/warnings.

**Human review:** pending (to be validated by Cami).

**Verification:** compiles with no errors/warnings (Unity console via MCP) and the 33 EditMode tests of `TicTacFade.Core.Tests` pass.

**Learnings:** the language rule was in `CLAUDE.md` but was ambiguous about XML docs and assertion messages; it was made precise after the deviation was detected in the review.

**Commit(s):** — (not committed; Cami reviews and commits).

---

## [4] 2026-09-22 — Reorganization of Assets/Scenes/ and stale scene fix

**Goal:** close the pending item from entry [1]: move `Assets/Scenes/` to `Assets/_Project/Scenes/`, update Build Settings and remove the hardcoded path from `TicTacFadeSceneBuilder.cs`.

**Request / prompt:** Cami asked to move `Assets/Scenes/` (with its `.meta` files) to `Assets/_Project/Scenes/`, update Build Settings, remove the builder's hardcoded path (the scene paths should come from a single place, a constant or Build Settings), and verify that the scene opens, that the game runs and that the builder still works. Closed scope: without touching gameplay or UI scripts. On the proposed plan, Cami asked for two changes: (1) `SampleScene.unity` (a template leftover, with no references, first in the build order) is not migrated: it is deleted together with its Build Settings entry, which is left with a single entry; (2) verify at the end with `git status --porcelain` that the scene shows up as a rename and not as delete+add.

**What the AI did:**
- Moved `Assets/Scenes/TicTacFadeGame.unity` to `Assets/_Project/Scenes/TicTacFadeGame.unity` with `manage_asset(action="move")` (it preserves the GUID and `.meta` via `AssetDatabase.MoveAsset`, not by hand on the filesystem).
- Deleted `Assets/Scenes/SampleScene.unity` (+ `.meta`) with `manage_asset(action="delete")`, and deleted the now empty `Assets/Scenes/` folder.
- Updated Build Settings to a single entry (`Assets/_Project/Scenes/TicTacFadeGame.unity`) with `manage_build(action="scenes")`.
- Updated `TicTacFadeSceneBuilder.cs` line 18: `ScenePath` now points to `Assets/_Project/Scenes/TicTacFadeGame.unity` (it is still the only constant the 3 uses come from: `SaveScene`, the Build Settings check and the log).
- When testing Play Mode it found 2 `NullReferenceException`s (`BoardView.cs:27`, `GameHud.cs:25`, both in `Awake` when doing `gameManager.StateChanged += ...`). It diagnosed the cause by reading the scene YAML (read only): the scene was **stale**, built before the Core refactor — the serialized `GameManager` had `m_EditorClassIdentifier: TicTacFade.Gameplay.GameManager` with old `boardView`/`hud` fields that no longer exist in the current class (`TicTacFade.Game.GameManager`), and `BoardView`/`GameHud` didn't have the `gameManager` field serialized at all (only the current `GameManager.cs` has the reverse relationship). It wasn't a bug caused by the move: the scene's content isn't touched when moving it, so it had already been broken since 9/17, untested in Play mode until now.
- Re-ran the builder (`Tic-Tac-Fade/Build Placeholder Scene (1v1)`), which regenerates the scene from the current classes and solved the problem: 0 errors in Play mode afterwards.
- Verified Build Settings after the builder re-run: still 1 single entry (it didn't duplicate), confirming that the `alreadyInBuild` check works with the fixed constant.
- Ran `git status --porcelain` (with `git add -A` so similarity is computed, then reverted with `git reset` so nothing is left staged): `TicTacFadeGame.unity`, its `.meta` and `Assets/Scenes.meta`→`Assets/_Project/Scenes.meta` show up as **R** (rename); `SampleScene.unity` + `.meta` show up as **D** (deleted). The `.meta` traveled with the file, as it should.
- Didn't touch `GameManager.cs`, `BoardView.cs`, `CellView.cs`, `GameHud.cs` or `RestartButton.cs`.
- Left `ProjectSettings/ProjectSettings.asset` untouched (`templateDefaultScene: Assets/Scenes/SampleScene.unity`): inert metadata from the original template, read neither by the game nor by the build, and there is no MCP tool that exposes that specific field (editing that YAML by hand would violate the CLAUDE.md rule).

**Human review:** pending (to be validated by Cami).

**Verification:** the scene loads with no errors (hierarchy with `EventSystem`/`Canvas`/`GameManager` intact); Play mode with no exceptions after regenerating the scene; the builder recreated the scene at the new path without duplicating the Build Settings entry; `git status --porcelain` confirms a rename (not delete+add) for the moved scene.

**Learnings:** a `.unity` scene can silently get out of sync with a script refactor (the old serialized fields are simply lost when deserializing under the new class, with no compilation error) — it is worth testing Play mode after a Core/Game/UI refactor, not just compiling and running EditMode tests. Moving an asset with `manage_asset(move)` doesn't rewrite its content, so a bug of this kind survives the move intact and has to be diagnosed separately. The Core extraction commit was left with the serialized scene out of date (NullReference in Play mode), detected only in the following iteration. The manual smoke test in Play Mode is mandatory before closing any task that touches serialized types, and regenerating the scene with the builder is part of that verification.

**Commit(s):** — (not committed; Cami reviews and commits).

---

## [5] 2026-09-22 — UI part 1: healthy scene and HUD (camera, safe area, layout)

**Goal:** `docs/promps/ui-parte-1.md` — make the scene builder idempotent, add a Main Camera, and redo the HUD layout with anchors/layout groups + safe area (portrait, Canvas Scaler 1080x1920 match 0.5), verified with the Simulator in 16:9 and 20:9. Out of scope: life indicators, ghost piece and winning line (part 2).

**Request / prompt:** Cami reported a HUD with overlapping texts and a hypothesis of her own: `TicTacFadeSceneBuilder` would not be idempotent and re-running it would duplicate HUD objects, leaving old, non-updated labels on top of the live ones. She asked to verify that hypothesis through MCP before touching anything. She approved the plan with 4 adjustments: (1) the portrait lock is not optional, GDD §2.3 asks for it; (2) `SafeArea.Apply()` also had to set `offsetMin`/`offsetMax` to zero explicitly; (3) add this ai-log entry documenting the ruling-out of her hypothesis with evidence; (4) note as part 2 debt that the fade warning is still concatenated in the same `Text` as the counter.

**What the AI did:**
- **Verified the duplication hypothesis (ruled out with evidence) before touching anything:** it inspected the live hierarchy through MCP (`get_hierarchy`, `find_gameobjects`) and the saved scene's YAML — exactly 1 `TurnLabel`, 1 `CountX`, 1 `CountO`, with no orphans. Structural cause: `BuildScene()` always starts with `EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single)`, which wipes the whole scene before rebuilding; there is no code path that can duplicate objects between runs. The real cause of the "overlapping" was another: `GameHud.cs` concatenated the fade warning inside the same `Text` as the counter, in a fixed box with no overflow configured.
- Added `CreateMainCamera()` to `TicTacFadeSceneBuilder.cs` (Camera + AudioListener + `UniversalAdditionalCameraData` for URP, tag `MainCamera`, `clearFlags=SolidColor`, near-black background).
- Redid `CreateHud()`: `SafeAreaHUD` (it uses Unity's **built-in** `UnityEngine.UI.SafeArea` component, not a handmade one — a custom `SafeArea.cs` had been written first, but the compiler flagged a name ambiguity against the `UnityEngine.UI.SafeArea` already existing in the package, and on inspecting it by reflection it turned out to be a more complete implementation than ours; the custom script was deleted and Unity's was used) → `HUD` (`VerticalLayoutGroup` + vertical `ContentSizeFitter`) → `TurnLabel` + `CountsRow` (`HorizontalLayoutGroup`) → `CountX`/`CountO`, all with `Wrap`/`Overflow` instead of fixed boxes.
- Locked portrait orientation via `PlayerSettings` (Unity API, not YAML editing): `defaultInterfaceOrientation=Portrait`, autorotate to landscape/upside-down portrait disabled. It is a GDD §2.3 requirement, not an inference.
- Shortened the fade warning in `GameHud.cs` from "¡Aviso de desvanecimiento!" to "¡Desvaneciendo!" (Cami's request after seeing the overflow on a narrow device) — it reduces the text width needed, it doesn't replace the layout fix.
- **Found and fixed 2 real nested-layout bugs during the Simulator verification** (they weren't in the original plan, they showed up when testing on `Wide Pill Cutout 1200x2640`, with a capture from Cami showing the text cut and stacked vertically):
  1. `CountsRow` (a `HorizontalLayoutGroup`, child of `HUD`) had no `flexibleWidth` in its own `LayoutElement`: being an `ILayoutElement` too, it reported its own preferred width (the sum of its children) to the outer group instead of expanding to the full available width, overflowing ~22px per side. Fix: `flexibleWidth = 1f` on the `LayoutElement` of `CountsRow` (and of `TurnLabel`, for consistency).
  2. `HUD` never set `sizeDelta` to `(0, height)` when switching to stretch anchors: the residual `sizeDelta` of a freshly created `RectTransform` was added as extra width outside the safe area (it overflowed ~62px per side). Fix: an explicit `hudRT.sizeDelta = new Vector2(0f, hudRT.sizeDelta.y);`.
  - Both were diagnosed with exact `RectTransform.GetWorldCorners()` measurements in Play Mode via `execute_code`, not by eye — Cami's visual capture on `Wide Pill Cutout` was what triggered the investigation.
- Tried a first attempt at fixing bug (1) using `childControlWidth=false` + manual stretch anchors instead of letting the `VerticalLayoutGroup` control the width; it didn't work: Unity collapses the children's anchors to a point in `SetLayoutHorizontal()` regardless of the control flags, because the layout group always controls the *position* on both axes even if it doesn't control the *size*. That attempt was reverted (the `StretchHorizontal` helper that was left unused was deleted) and the real fix (`flexibleWidth`) was applied.
- Verified idempotence by running the builder 3 times at different moments of the task (before and after the layout fixes): always 1 single instance of each GameObject, with no duplicates.

**Human review:**
- The 4 adjustments to the plan (see Request/prompt) are already incorporated above.
- Cami verified visually in the Simulator with `iOS Classic (750x1334)` and `Wide Pill Cutout (1200x2640)`, in the real worst case (X=3/3, O=3/3, warning visible on both at the same time) — she confirmed "anda perfecto" [works perfectly] after the 2 layout fixes.

**Verification:** compiles with no errors/warnings; idempotent builder (3 runs, no duplicates); Play mode with no exceptions; exact `RectTransform` measurements at runtime confirm 0 overlaps and 0 safe-area overflow on `Wide Pill Cutout (1200x2640)`; Cami's visual confirmation on `iOS Classic (750x1334)` and `Wide Pill Cutout (1200x2640)`.

**Learnings:**
- A bug hypothesis can be wrong, and the "verify it before touching anything" request is exactly what avoids chasing the false cause (duplication) instead of the real one (fixed box + overflow not configured).
- Unity's nested layout groups (`HorizontalLayoutGroup` inside `VerticalLayoutGroup`) need an explicit `flexibleWidth` on the `LayoutElement` of the child-that-is-also-a-group, because that child reports its own preferred size like any `ILayoutElement` and can override the parent's attempt to expand.
- A freshly created `RectTransform` keeps its default `sizeDelta` when its anchors are changed to stretch in code; it has to be set to `(0,0)` (or the appropriate value) explicitly, otherwise the residual value leaks as extra size.
- `UnityEngine.UI` already ships its own `SafeArea` component (more complete than a homemade implementation): it is worth checking with `unity_reflect` before writing a component that sounds "standard" — the compiler flagged it by itself (name ambiguity), but it would have been better to look for it before writing our own.
- The Unity 6 Simulator comes integrated in the Editor (no package) and already ships generic profiles with notch/punch-hole/pill-cutout good enough to test the safe area without installing `com.unity.device-simulator.devices`.
- The `manage_camera(action="screenshot")` capture is no use for debugging `ScreenSpaceOverlay` UI: it renders only through the camera and excludes overlay canvases. Measuring `RectTransform.GetWorldCorners()` at runtime via `execute_code` is more reliable than chasing an automated screenshot.

**Known debt for part 2 (not solved here, see `GameHud.cs`):** the fade warning is still concatenated inside the same `Text` as the counter (`UpdateActiveCounts`). It works with the wrap and the shortened text, but when the piece life indicators are added it should become its own UI element.

**Commit(s):** — (not committed; Cami reviews and commits).

---

## [6] 2026-09-22 — UI part 2: life per piece, full ghost piece, separate warning

**Goal:** `docs/prompts/ui-parte-2.md` — life indicator per piece (GDD §4.1, both players), separate the fade warning from the counter (pays off the debt from entry [5]), ghost piece with two-tap confirmation that previews the full resulting board (GDD §3.7), and a regression check of the winning line highlight. Explicit constraint: don't touch Core.

**Confirmed before touching anything:** all the needed information was already public in `GameState`/`RulesEngine` (`GetLife`, `QueueX`/`QueueO`, `Config.BufferSize`, `GetActiveCount`, `IsLegal`, `WinningLine`) — no change in Core was needed. The winning line highlight was already implemented end to end from before; this task only re-verified it.

**Self-correction during the plan, pointed out by Cami:** the first version of my plan assumed that the life 1 critical mark was already enough to cover the ghost piece's "piece that goes, marked", because mathematically they are the same cell (the oldest of a full queue always has life 1). Cami rightly objected: that mark shows up on the pieces of **both** players at the same time if both queues are full, so during the preview it doesn't tell which of the two badges corresponds to the current move. It was fixed by adding `CellView.SetGhostVictim(bool)`, a separate mark (opacity at minimum, 0.12) applied only to the active player's own piece that the FIFO would remove with the previewed move — independent of the passive critical mark. Verified in Play mode: with both queues full, the opponent's old piece shows the critical badge but does **not** drop to minimum opacity; only the previewing player's does.

**What the AI did:**
- `CellView.cs`: new fields `criticalMark` (opaque badge, tinted with the owner's color — not a fixed red, so X's and O's badges can be told apart) and `ghostOverlay` (translucent new piece of the preview). Slow pulse at life 2 via `Update()` (alpha modulated with a sine, without touching life 1/3). New `SetGhostVictim(bool)` (see the correction above).
- `BoardView.cs`: selection state (`selectedCell`) and a rewrite of `OnCellClicked` for the two-tap flow; `ClearGhostPreview()` clears ghost + victim mark on the 9 cells before each new selection or on confirming.
- `GameHud.cs`: `UpdateActiveCounts` stops concatenating the warning; new field `fadeWarningLabel` and `BuildFadeWarning()` builds a shared message ("¡X desvaneciendo!" / "¡O desvaneciendo!" / "¡X y O desvaneciendo!").
- `TicTacFadeSceneBuilder.cs`: `CreateCell` adds `CriticalMark`/`GhostOverlay` (children order: Label → CriticalMark → GhostOverlay → Highlight, so the win border always stays on top). `CreateHud` adds `FadeWarning` as the third child of `HUD`; `CountsRow.minHeight` goes down from 110 to 60 (it no longer needs room for 2 lines in the same Text).
- **New PlayMode test** (an explicit request from Cami, the first time this repo has one): `Assets/_Project/Tests/PlayMode/` with its own asmdef (`TicTacFade.PlayModeTests`, `defineConstraints: ["UNITY_INCLUDE_TESTS"]` so it doesn't ship in the Android build) and `SceneWiringAndInputTests.cs` with 2 tests: (1) it loads the scene and verifies by reflection that no serialized reference of `CellView`/`BoardView`/`GameHud` was left unassigned; (2) it simulates two real taps on a cell's `Button` (`onClick.Invoke()`, not `GameManager.OnCellClicked` directly) and confirms that the first doesn't apply the move and the second does. It actively waits for `GameManager.CurrentState` to exist (with a timeout and a clear message) instead of a fixed `yield return null`, so as not to leave a flaky test.
- Updated `docs/GDD.md` (v0.2 → v0.3): §4.1 documents the life 1 badge tinted by owner; §3.7 documents that the preview also marks the player's own piece the FIFO would remove, different from the passive critical mark. New row in the version history.

**Human review (two rounds of adjustments on the plan, both incorporated):**
- Round 1: the ghost victim mark has to be different from the passive critical mark (see the correction above); the life 1 badge has to be tinted by owner, not a fixed red; add the PlayMode test described above.
- Round 2: add the GDD update (done above); clean up a sentence of the plan that had become outdated after round 1; change the test's fixed `yield return null` for an active wait with a timeout.

**Verification:**
- Compiles with no errors/warnings.
- `run_tests(mode="PlayMode")`: 2/2 new tests OK. `run_tests(mode="EditMode")`: 33/33 Core with no regression.
- Play mode: a sequence of 6 confirmed moves (two taps each, via `Button.onClick.Invoke()`, not `GameManager` directly) leaves life 1/2/3 visible on both players at the same time; one more tap on an empty cell shows the ghost preview with the new piece and, simultaneously, X's old piece drops to minimum opacity while O's old one (same moment, not being the current move) keeps only the normal critical badge — it confirms that the two marks are independent. Winning line regression: a short match forcing X's win, `highlightBorder` turns on in the 3 cells and the banner appears. 0 console errors in the whole session.
- **Visual confirmation in the Simulator (`iOS Classic`, `Wide Pill Cutout`): pending from Cami** — the worst-case state was left set up in Play mode for her to review.

**Learnings:** a reading of one's own that "this is already enough" can be mathematically right and still insufficient in terms of UX/clarity — Cami's correction about the ghost piece is exactly that case. When the code ends up diverging from an implicit description in the GDD, the GDD should be updated explicitly instead of leaving the real specification only in the code (that is why v0.3 was added).

**Note for the future, unresolved for now (an explicit request from Cami):** `SceneWiringAndInputTests.AssertNoNullReferenceFields` assumes that every serialized reference of `CellView`/`BoardView`/`GameHud` is mandatory. The day a legitimately optional field exists, a way to exclude it from the check will be needed (for example a marker attribute or a list of exceptions by field name).

**Commit(s):** — (not committed; Cami reviews and commits).

---

## [7] 2026-09-22 — PlayerSettings configuration for the first Android build

**Goal:** leave the project configured for the first Android build via the `PlayerSettings` API (never editing YAML by hand), in a reproducible Editor method under the Tic-Tac-Fade menu — it doesn't build anything, Cami runs that.

**Request / prompt:** real company name and product name; package `com.<company>.tictacfade` in lowercase; version "0.1.0", bundleVersionCode 1; IL2CPP + ARM64; minimum API 23 or higher; confirm portrait orientation and a single scene in Build Settings; everything reproducible in a menu method; document it in the ai-log; don't run the build.

**What the AI did:**
- Before touching anything, it searched whether "company name" was already established anywhere in the repo's documentation (because of the root folder `E:\HeuMila\`); it found no reference in `docs/`/`CLAUDE.md`, so it **asked** instead of assuming. Cami confirmed `HeuMila`, and `Tic-Tac-Fade` (with hyphens, as in the GDD) for product name.
- Checked the real `PlayerSettings` API with `unity_reflect` before writing code: `AndroidApiLevel23` (and the whole 16–25 range) is **obsolete** in this Unity version (6000.6.1f1). It used `AndroidApiLevel26`, the lowest non-obsolete level that still meets "23 or higher" — it points it out explicitly in a comment and in the tool's own log, so it isn't a surprise.
- Created `Assets/_Project/Scripts/Editor/AndroidBuildConfigurator.cs`, menu `Tic-Tac-Fade/Configure Android Player Settings`, method `ConfigureForAndroid()`: `companyName`/`productName`, `SetApplicationIdentifier` (`com.heumila.tictacfade`), `bundleVersion`/`bundleVersionCode`, `SetScriptingBackend(IL2CPP)`, `Android.targetArchitectures = ARM64`, `Android.minSdkVersion`, it re-asserts the portrait lock (GDD §2.3) instead of relying on it being left from a previous session, and it logs a warning if `EditorBuildSettings.scenes.Length != 1` instead of touching it on its own (there is no safe way to "fix" that without assuming which scene you want).
- **Didn't touch anything about the keystore/signing** — it wasn't asked for, and generating or touching a keystore is a sensitive action (credentials) that shouldn't be assumed.
- Ran the menu twice (confirming that it is idempotent) and verified each final value separately via `execute_code`, not only the tool's own log.

**Human review:** pending (to be validated by Cami).

**Verification:** compiles with no errors/warnings. Run 2 times with no error and no change of result. Values confirmed one by one: `companyName=HeuMila`, `productName=Tic-Tac-Fade`, `applicationIdentifier=com.heumila.tictacfade`, `bundleVersion=0.1.0`, `bundleVersionCode=1`, `scriptingBackend=IL2CPP`, `targetArchitectures=ARM64`, `minSdkVersion=AndroidApiLevel26`, `defaultInterfaceOrientation=Portrait`, autorotate to landscape/upside-down portrait disabled, 1 single scene in Build Settings (`TicTacFadeGame.unity`, enabled). No build was run.

**Learnings:** a value asked for literally ("API 23") may no longer be valid in the current Unity/Editor version (deprecated); it is worth checking with `unity_reflect` before setting an enum by hand, and leaving documented why the final value differs from the literal request instead of applying it blindly or changing it silently.

**Commit(s):** — (not committed; Cami reviews and commits).

---

## [8] 2026-09-22 — Audit of the side effects of the Android config (entry [7])

**Goal:** Cami reviewed the uncommitted changes after entry [7] and found two things `AndroidBuildConfigurator` didn't explain at first sight: a new `Assets/Resources/` folder, and changes in `Assets/Settings/UniversalRP.asset` / `UniversalRenderPipelineGlobalSettings.asset`. She asked to diagnose both before assuming they were intentional.

**Request / prompt:** first question — what created `Resources/`, what is inside, whether it was `AndroidBuildConfigurator`, and whether the URP changes were intentional or an accidental touch by Unity when switching platforms. My first answer proposed an active platform switch as the cause; Cami corrected that the platform had already been Android since before this session (confirmed by earlier captures), and asked specifically to check whether the configurator's Player Settings changes (IL2CPP, ARM64, graphics API) were the real trigger, and to correct the diagnosis here if appropriate. In a third round, she asked: (1) to verify whether the `InputSystem_Actions.inputactions` asset referenced in `preloadedAssets` still exists or is a dangling reference to a deleted asset, and to remove it via `PlayerSettings.SetPreloadedAssets` if appropriate; (2) to close the URP topic without investigating further — record the cause as undetermined with the documented rule-outs, and treat it as benign internal re-serialization.

**What the AI did:**
- `Assets/Resources/`: identified by content (`PerformanceTestRunInfo.json`, `PerformanceTestRunSettings.json`) and by the `com.unity.test-framework.performance` package (confirmed in `packages-lock.json`) as an automatic Unity artifact of running PlayMode tests — not something `AndroidBuildConfigurator` creates (the script has not a single line that writes to disk outside `PlayerSettings`/`EditorBuildSettings`). Cami already deleted it and added the rule to `.gitignore`.
- URP — first hypothesis (active platform switch) **ruled out**: Cami confirmed that the active platform was already Android before this session.
- Second hypothesis asked for (IL2CPP/ARM64/graphics API) **ruled out with evidence**, not only by reading the script: I compared the real diff of `ProjectSettings/ProjectSettings.asset` field by field against the current values. `scriptingBackend`, `AndroidTargetArchitectures` and `AndroidMinSdkVersion` **don't appear in the diff at all** — they already had those exact values (IL2CPP, ARM64, 26) before the configurator ran, so they couldn't have triggered it. `m_BuildTargetGraphicsAPIs` is still `[]` (Auto), untouched. The only genuinely new thing on the Android side was `applicationIdentifier.Android` (that entry didn't exist before, only `Standalone`).
- `preloadedAssets` (`InputSystem_Actions.inputactions`) — investigated thoroughly in the third round: the asset **exists** on disk (`Assets/Settings/InputSystem_Actions.inputactions`, the GUID matches, the referenced `fileID` resolves to a real `InputActionAsset` inside the file, it's not a broken reference at the asset level). But it **is not the one used** by `InputSystemUIInputModule` in the scene: that component, added without explicitly assigning any `.inputactions` (`TicTacFadeSceneBuilder` only does `AddComponent<InputSystemUIInputModule>()`), ends up using an object autogenerated at runtime called `DefaultInputActions` — confirmed by reading `inputModule.actionsAsset.name` with the scene open. Also, `PlayerSettings.GetPreloadedAssets()` in memory already returned 0 elements even though the YAML on disk still had the entry — a real mismatch between the Editor's live state and what was persisted. It didn't meet the condition Cami had set to keep it ("if it is the one the InputSystemUIInputModule uses"), so it was removed via `PlayerSettings.SetPreloadedAssets(new Object[0])` + `AssetDatabase.SaveAssets()` (API, not YAML editing). Verified afterwards: `preloadedAssets: []` on disk.
- URP — **cause undetermined, topic closed at Cami's explicit request.** Three hypotheses evaluated, none confirmed:
  1. Active platform switch — ruled out (the platform had already been Android since before this session).
  2. Player Settings changes by the Android configurator (IL2CPP/ARM64/graphics API) — ruled out with evidence (those fields don't appear in the `ProjectSettings.asset` diff, they already had those values before running `AndroidBuildConfigurator`).
  3. The `Main Camera`/`UniversalAdditionalCameraData` added in part 1, recomputed in some later domain reload — a hypothesis with some circumstantial support (timestamps, genuinely new URP content) but **not provable**, Unity doesn't leave a log of this internal recomputation.
  The diff of `UniversalRP.asset`/`UniversalRenderPipelineGlobalSettings.asset` is treated as benign internal re-serialization of shader prefiltering — it doesn't block anything, it is not investigated further.

**Human review:** Cami corrected my first diagnosis (active platform) with her own evidence (captures from before the session) before I verified it with data; that led to the second round, where I did verify with the real diff instead of repeating a plausible but unchecked hypothesis. In the third round, she set the explicit limit of not continuing to investigate URP with an unprovable cause, and asked to resolve `preloadedAssets` with a concrete rule (exists + the module uses it → keep it; if not → remove it through the API).

**Verification:** full diff of `ProjectSettings/ProjectSettings.asset` line by line; grep of the specific fields asked for against the current file vs. the diff; comparison of file timestamps between `AndroidBuildConfigurator.cs` and the two URP `.asset` files (consistent with hypothesis 3, it doesn't prove it conclusively); for `preloadedAssets`: `AssetDatabase.LoadAllAssetsAtPath` confirmed that the referenced fileID resolves to a real asset, `InputSystemUIInputModule.actionsAsset.name` confirmed that the module uses a different object, `GetPreloadedAssets()` confirmed the memory/disk mismatch, and a re-read of the YAML after the fix confirmed `preloadedAssets: []`.

**Learnings:** `git diff` against the last commit mixes the changes of the whole session (several tasks on top of an uncommitted branch), not only those of the specific task being reported — before attributing an asset change to a specific script you have to confirm that the relevant field really changed value in the diff, not only that the file shows up as modified. My first answer assumed a plausible cause (platform switch) without verifying it against the real previous state; the correct diagnosis came from comparing values field by field, not from an explanation that sounded reasonable. An asset can exist on disk and still not be the one a component really uses at runtime — "does the file exist?" and "is it in use?" are different questions, and only the second matters to decide whether a `preloadedAssets` entry is correct. Not every root cause is provable with the available tools; when it isn't, it has to be said explicitly instead of presenting the most plausible hypothesis as if it were the conclusion.

**Commit(s):** — (not committed; Cami reviews and commits).

---

## [9] 2026-09-23 — Visual direction in the GDD + source sprites not wired

**Goal:** record in the GDD the visual direction decided for the art pass, and note that the source sprites are already in the repo but not yet connected to any component.

**Request / prompt:** Cami asked to add to "GDD §5" the visual direction (neon on a dark background, X cyan/O magenta, white shapes tinted in code, glow through URP Bloom never baked into the sprite, disintegration by dissolve shader + particles), the technical constraint that Bloom doesn't affect a Screen Space - Overlay Canvas, and a new row in the version history. She also asked to note in the ai-log that the source sprites (`piece_x`, `piece_o`, `shard`, `cell_frame`, `badge`) are in the repo as material for the art pass but `CellView` keeps the text labels until that iteration.

**What the AI did:**
- Detected a mismatch before writing: the GDD's current §5 is "Online (MVP)" (networking), not visual direction — that content belongs to §6 "Diseño audiovisual (post-MVP)" [Audiovisual design (post-MVP)], which already had an open entry ("Arte: minimalista de alto contraste. Candidatos: neón cyberpunk o flat vector" [Art: high-contrast minimalist. Candidates: cyberpunk neon or flat vector]) waiting precisely for this decision. It pointed it out and put it in §6 instead of silently assuming or writing it in the wrong place.
- Updated `docs/GDD.md` §6: it replaced the art "candidates" with the decided direction (neon, cyan/magenta, white shapes tinted in code, URP Bloom), added the Canvas technical constraint (the current Screen Space - Overlay doesn't receive Bloom; the art pass will need to switch to Screen Space - Camera, a UI architecture change, not a cosmetic one) and made the disintegration precise as dissolve shader + particles (before it only said "particles"). v0.3 → v0.4, new row in the history.
- Verified that the 5 source sprites mentioned are really in the repo before noting it (it didn't take Cami's word without checking): `Assets/_Project/Art/Source/{piece_x,piece_o,shard,cell_frame,badge}.svg` + their `@2x.png` exports, all 5 present.
- Confirmed that `CellView.cs` still doesn't wire any sprite — it still uses `label.text = "X"/"O"` over a flat-color `Image` (`XColor`/`OColor`), with no reference to the `Art/Source/` sprites. The wiring is left for a future art iteration, which also requires the Canvas switch to Screen Space - Camera documented above.

**Human review:** pending (to be validated by Cami) — in particular, confirming that §6 was the right place (not §5 as literally asked).

**Verification:** direct reading of the GDD after the edit; listing of the files in `Assets/_Project/Art/Source/` confirming the 5 sprites; reading of `CellView.cs` confirming it doesn't reference any sprite yet.

**Learnings:** when a request specifies a section number that doesn't match the document's real content, it is worth pointing out the mismatch and placing the content where it belongs semantically, instead of creating a new section or forcing it into the place asked for — the GDD already had an open "candidate" in §6 waiting for exactly this decision.

**Commit(s):** — (not committed; Cami reviews and commits).

---

## [10] 2026-09-23 — IPlayerController + GameConfig as a ScriptableObject

**Goal:** `docs/prompts/player-controllers.md` — implement two architecture rules already declared but not built: CLAUDE.md rule 5 ("Players are interchangeable through IPlayerController...") and ADR 0001 ("The Game layer exposes GameConfig as a ScriptableObject"). `GameManager` stops assuming that every move comes from the UI and stops calling a hardcoded `GameConfig.Mvp()`.

**Request / prompt:** create `IPlayerController` + `LocalHumanPlayer`, have `GameManager` receive two controllers without branching on type; `GameConfig` as a ScriptableObject (wrapper, Core untouched); the scene builder creates/wires the new config, idempotent; the UI adapts with no visual changes; the existing EditMode/PlayMode tests keep passing + a local match the same as before + a new test with a non-human controller; ai-log and ADR if appropriate.

**What the AI did:**
- Confirmed before writing code that there was no need to touch Core: everything needed was already public (`Occupant`, `Move`, `GameState.GetFreeCellIndices()`).
- Created `IPlayerController.cs` (`Player`, `MoveChosen` event, `NotifyTurnStarted`, `NotifyCellSelected`), `LocalHumanPlayer.cs` (passively waits for the tap) and `GameConfigAsset.cs` (ScriptableObject wrapper of `GameConfig`, with a different name on purpose so as not to repeat the name ambiguity that already happened once with `SafeArea`).
- Rewrote `GameManager.cs`: public and re-callable `Initialize(IPlayerController, IPlayerController)`, default controllers (`LocalHumanPlayer` x2) created in `Awake()` if nobody injected others before — they can't be wired from the scene builder because an interface field with an `event` doesn't survive Unity serialization. `OnCellClicked`, `StartNewGame`, `CurrentState`, `StateChanged`, `GameEnded` keep exactly the same public signature.
- **Confirmed and verified that the UI didn't need any change**: `BoardView.cs`, `GameHud.cs`, `RestartButton.cs` stayed intact — zero lines touched, all of them keep calling the same `GameManager` API they already used.
- `TicTacFadeSceneBuilder.cs`: `GetOrCreateGameConfigAsset()` (looks in `Assets/_Project/Config/GameConfig.asset`, creates it only if missing) + wiring of the `GameManager`'s `config` field. Verified idempotent by running the builder 2 times: the asset isn't duplicated.
- **Design correction found by Cami in the plan review, before writing code:** the first design notified the next turn with a recursive call from inside the handling of the previous move. With two autonomous controllers (the test asked for), that stacks one stack frame per move for the whole match, and a rejected move that doesn't break the cycle produces infinite recursion → StackOverflow → hangs the Editor with no catchable exception. It was fixed with a re-entrancy flag (`_isAdvancingTurns`) + a loop in a single method (`AdvanceTurns`): a nested call doesn't open a new loop, the outermost one picks up the state change on its next iteration. `ApplyMove` stops dead on a `MoveRejectedEvent` (logs and leaves, doesn't notify again).
- Created `FakeAutoPlayer.cs` (test only, `Assets/_Project/Tests/PlayMode/`): a non-human controller that plays the first free cell as soon as it is notified of its turn.
- Extended `SceneWiringAndInputTests.cs`: added `GameManager` to the check for unassigned serialized references (it protects the new `config` field). Added `GameManager_TwoFakeAutoPlayers_PlayFullMatchToCompletion` — two `FakeAutoPlayer`s (not one fake + one human, an explicit request from Cami in the second review) play a full match until `IsOver`, with a cap of 60 frames that fails the test if exceeded (an independent test protection, it doesn't rely only on the production re-entrancy flag being right) and it verifies that it ended in a valid result (win with a line, or a draw by repetition/move cap).
- Added `docs/adr/0003-player-controller-abstraction.md` — only for the controllers part; the `GameConfig` as ScriptableObject part was already decided in ADR 0001, it didn't call for a new ADR.
- **Tooling bug, not a code bug:** when creating `LocalHumanPlayer.cs`, `GameManager.cs` couldn't resolve the type (`CS0246`) even though `IPlayerController.cs` and `GameConfigAsset.cs` —created in the same batch— compiled fine, and `validate_script` found no error in the file. Neither forcing `refresh_unity` several times nor explicitly reimporting the asset solved it. It was solved by deleting the file (`delete_script`) and recreating it identically — something was left badly registered in the asset database for that specific path/GUID on the first attempt. `unity_reflect search` (by text) still doesn't find it after the fix, but `unity_reflect get_type` (by full name) does confirm it — the text search index has its own cache, separate from the real compilation.

**Human review:** Cami approved the plan with a fundamental correction (the recursion/rejection above) before a single line was written, and broadened the requested test so that it would be two autonomous controllers playing a whole match (not one fake + one human) with explicit protection against loops.

**Verification:** compiles with no errors/warnings. `run_tests(EditMode)`: 33/33 Core, no regression. Scene builder run 2 times: `GameConfig.asset` is created only once, not duplicated. `run_tests(PlayMode)`: 3/3 (wiring extended to `GameManager`, double tap, full match with two `FakeAutoPlayer`s). Manual Play mode with real taps (double `Button.onClick.Invoke()` per cell): a local match plays identically to how it played before the refactor.

**Learnings:** a new file can fail to resolve as a type with a misleading compiler error (`CS0246`, "a using is missing") even though the file is syntactically perfect and its siblings created in the same batch compile fine — it is worth deleting and recreating the file before chasing a code cause that doesn't exist. Reviewing an architecture plan BEFORE writing code (as Cami did here) is much cheaper than finding a StackOverflow afterwards: recursion disguised as "turn notification" doesn't look like recursion until you think specifically about the case of two chained autonomous controllers.

**Commit(s):** — (not committed; Cami reviews and commits).

---

## [11] 2026-09-23 — Known issue: HUD covered by the punch-hole on a real device

**Goal:** record a real bug found on hardware (not in the Simulator) without fixing it yet, and warn in the next iteration's prompt that its verification has to include a real device, not only the Simulator.

**Request / prompt:** Cami tested the APK on a real device and found that the HUD text is covered by the camera's punch-hole — the safe area container isn't excluding it. She asked to note it as a known issue in the ai-log without fixing it (it gets solved in the menu and screen flow iteration), and to add a line to `docs/prompts/menu-flow.md` asking that iteration to verify the HUD outside the punch-hole on a real device, not only in the Simulator.

**What the AI did:**
- Added the requested line to the Verification section of `docs/prompts/menu-flow.md`.
- Before recording the probable cause Cami gave, it confirmed it in the project instead of taking it blindly as good: `ProjectSettings/ProjectSettings.asset` has `androidRenderOutsideSafeArea: 1` — confirmed, it is the project's current value (it had already been seen in the UI part 1 investigation, it is unchanged).
- Didn't investigate or try to fix the bug itself — that is explicitly what Cami asked not to do yet.

**Known issue (unresolved):**
- **Symptom:** on a real Android device, the HUD text (turn, counters, fade warning) is partially or totally covered by the camera cutout (punch-hole). The `SafeArea` component (`UnityEngine.UI.SafeArea`, see entry [5]) isn't excluding it from the HUD's visible area.
- **Probable cause (confirmed as the current configuration, not as a full diagnosis of the bug):** `PlayerSettings.Android.renderOutsideSafeArea` is `true` (`androidRenderOutsideSafeArea: 1` in `ProjectSettings.asset`) — with that the app draws under the camera cutout, and apparently `Screen.safeArea` isn't compensating that zone as expected on this device. The exact root cause wasn't confirmed (it could be that option, it could also/instead be a problem of the `SafeArea` component itself or of how it is nested in `TicTacFadeSceneBuilder.CreateHud`) — it still needs to be reproduced and diagnosed calmly.
- **Not reproducible in the Simulator:** it had already been seen in UI part 1 that the Simulator's notch/punch-hole simulation is approximate; this confirms that it isn't enough as the only verification method for safe area on devices with a camera integrated into the screen.
- **Why it isn't fixed now:** Cami's explicit request — it gets solved in the menu and screen flow iteration (`docs/prompts/menu-flow.md`), which already touches the HUD/screens layout, and it makes sense to solve it there instead of as an isolated patch.

> **Solved and corrected in [12]** (verified on a real device, Samsung Galaxy S21 FE): the probable cause above was wrong. The cause is that the `SafeArea` component had `Edges = 0` (it didn't respect any edge). `renderOutsideSafeArea` wasn't the cause. It isn't true either that the Simulator didn't reproduce it: the `Wide Pill Cutout` capture from part 1 already showed the turn at the height of the pill. See [12].

**Human review:** N/A — a pure documentation task, with no code changes.

**Verification:** `grep` confirming the current value of `androidRenderOutsideSafeArea` in `ProjectSettings.asset`; reading of `docs/prompts/menu-flow.md` after the edit.

**Learnings:** Unity's Simulator approximates the notch/punch-hole cutout but doesn't replace testing on real hardware — it is worth, as Cami did, getting an APK out early specifically to find this kind of difference before building more on top. Confirming in the project a technical fact someone gives as a "probable cause" (instead of assuming it) is cheap and prevents an unverified hypothesis from being recorded as if it were a proven fact.

**Commit(s):** — (not committed; Cami reviews and commits).

---

## [12] 2026-09-23 — Menu and screen flow + real cause of the punch-hole

**Goal:** `docs/prompts/menu-flow.md`. The game stops starting straight on the board and gets the GDD §4.3 flow trimmed down to local: Menu → Match → Result → (Rematch | Menu). It also solves the known issue from [11].

**Request / prompt:** flow state machine in Game (Menu, Playing, Result) with no screen logic in `GameManager`; menu with "Jugar local" ready for two more buttons; result screen with the reason and "Revancha"/"Menú" buttons that replaces the banner; rematch alternating who starts; all portrait, safe area, layout groups, no fixed positions; Core untouched; idempotent builder; PlayMode test of the full flow; Simulator in both profiles; HUD verified outside the punch-hole on a real device.

**What the AI did:**
- **Real cause of the punch-hole (found while putting the plan together):** `UnityEngine.UI.SafeArea` has no `Reset()` and no initial value for `m_Edges`. If it is added in code it stays at `0` = no edge respected, and the component does nothing. The previous scene had `m_Edges: 0` (read, not edited). Fix: the builder sets `Edges = Top | Right | Bottom | Left` in a single helper, `CreateSafeAreaScreen`, used by the three screens. `renderOutsideSafeArea` stays `true`.
- Game: `FlowState.cs` (enum with explicit values, because it is serialized in the scene), `MatchFlow.cs` (table of allowed transitions + intent methods `PlayLocal`, `Rematch`, `BackToMenu`, `ExitMatch`; it switches to `Playing` **before** starting the match, because with autonomous controllers the match can end inside `StartNewGame`). `GameManager`: it doesn't start on its own, `StartNewGame(Occupant startingPlayer)`, new `DiscardMatch()`, `OnCellClicked` silently ignores if there is no match in progress.
- UI: `FlowScreenVisibility` (CanvasGroup, not `SetActive`, so `BoardView`/`GameHud` run `Awake` and subscribe when the scene loads whatever screen is visible), `MenuScreen`, `ResultScreen` (it absorbs the end message that was in `GameHud`), `ExitMatchButton`. `GameHud` was left with only turn/counters/warning. `BoardView.OnCellClicked` has a guard against `CurrentState == null` or a finished match. `RestartButton.cs` was deleted (via MCP).
- Builder: `GameScreen` (board centered without the fixed (0,60) offset + HUD with `TopBar`/"Salir"), `ResultScreen` (panel anchored at the bottom, so the winning line stays visible), `MenuScreen` (title + `MenuButtons`, where "Crear sala"/"Unirse" will go), `MatchFlow`. `CreateWinBanner` was deleted.
- PlayMode tests adapted (now everything starts by tapping "Jugar local"). New test `Flow_MenuPlayRematchMenuExit_...`: a stray tap in Menu without having played (no exceptions, `CurrentState` null) → play (X) → X wins → rematch (O) → O wins → Menu → play (X) → two moves → "Salir" (Menu, match discarded, no exceptions) → play (X, empty board). The two `FakeAutoPlayer`s test now also verifies that the flow ends in `Result` even if the match ends synchronously.
- GDD v0.5: §3.1 rule of who starts from the menu; §4.3 full flow with MVP / later and the decision of "Crear sala" without configuration.

**Human review:**
- First adjustment to the plan: (1) the plan's hierarchy seemed to list screens twice. It was the notation ("ResultScreen … + ResultScreen" = GameObject + component of the same name), not the builder; it was rewritten as `GameObject [components]` and counting objects after two runs was added to the verification. (2) That the match from the menu starts with X went into GDD §3.1 as a rule, with a version row. (3) Define what each reader of `CurrentState` does while it is null in Menu, with guards, and verify it in the test.
- She asked to update GDD §4.3 with the full flow (waiting room, code entry) marking MVP / later, only as documentation, and to design `FlowState`/`MatchFlow` so that adding room states is adding cases.
- Second adjustment: `Playing → Menu` transition with a discreet "Salir" button at the top, which discards the match; case added to the test.

**Design items noted as pending:**
- Each future screen (waiting room, code entry) has to define explicitly where its cancel/back button returns. It is a row of `MatchFlow`'s `AllowedTransitions` table, not a generic "back".
- Online, `Playing → Menu` ("Salir") will be "abandon", which counts as a loss. Today in local it only discards the match.

**Verification:**
- Compiles with no errors or warnings.
- EditMode 33/33.
- PlayMode 4/4. The MCP job gave an initialization timeout, but the run did execute: results read from `TestResults.xml`, with that run's timestamp.
- Builder run 2 times: `GameScreen`, `ResultScreen`, `MenuScreen`, `MatchFlow`, `GameManager` appear once each, and the three `SafeArea`s end up with `Edges = Top, Right, Bottom, Left`.
- Play Mode in the Game view (no cutout): HUD at the top with "Salir" on the right, board centered, result panel at the bottom without overlapping the board.
- Simulator (Cami): the game is no longer covered by the camera cutout.
- Real device (Cami, APK on a Samsung Galaxy S21 FE): the HUD stays outside the punch-hole and everything looks fine. Tested on a single model.

**Learnings:**
- A component added in code doesn't get the defaults the Inspector gives it (`Reset()`); if the type doesn't initialize its fields, they stay at zero. After adding a Unity component in code, check its serialized fields in the generated scene.
- The diagnosis in [11] was recorded as a "probable cause" without measuring; the measurement that would have detected it (the screen's rect against `Screen.safeArea` in Y, not only in X) is now part of the UI verification.

**Commit(s):** — (not committed; Cami reviews and commits).

---

## [13] 2026-09-23 — Online iteration 1: rooms with a code, without game sync

**Goal:** two players meet in a room with a code and end up connected. On connecting, both see "Rival conectado" and nothing else. Move sync is the next iteration.

**Request / prompt:** `TicTacFade.Net` layer behind an interface in Game; Unity Services + anonymous authentication; different profiles per Multiplayer Play Mode instance; new states at the end of `FlowState` with their rows in the table; "Crear sala" → waiting room (big code, copy, cancel); "Unirse con código" → screen with field, join, cancel, readable errors without leaving; each cancel with an explicit destination; loading states with disabled buttons and "Conectando..."; network errors with a message and a return to a stable state. Authorized packages: Multiplayer Services, Netcode for GameObjects, Multiplayer Play Mode.

**What the AI did:**
- **Packages:** Multiplayer Services 2.3.3 was already there (installed by Cami). It installed Netcode for GameObjects 2.13.3 and Multiplayer Play Mode 3.0.0, the versions the registry resolves for Unity 6000.6. MPPM 3.0.0 is *builtin* in 6.6. As dependencies, `transport` 2.7.3, `services.core` 1.18.0, `services.authentication` 3.7.4, `qos`, `wire`, `deployment` and `deployment.api` came in.
- Before designing, it read the code of the installed packages instead of assuming the API:
  - `CurrentPlayer.IsMainEditor`/`Tags` is MPPM's identity API (there is no player index).
  - Authentication already derives a profile per instance from the `-editor-mode -name` arguments, but only with the "default" profile and without documenting it.
  - Services Core doesn't auto-initialize.
  - `WithRelayNetwork` requires `NetworkManager.Singleton`.
  - The SDK's `StopAsync` waits for the `NetworkManager` shutdown, and the SDK asks not to call `Shutdown()` by hand.
- **Game:**
  - `ISessionService`, `SessionServiceBehaviour` (serializable base), `SessionResult` and `SessionFailure`.
  - `FlowState` adds `Lobby = 3` and `JoinByCode = 4`.
  - `MatchFlow`: new rows in the table (`Menu → {Playing, Lobby, JoinByCode}`, `Lobby → {Menu}`, `JoinByCode → {Lobby, Menu}`). Intents `CreateRoom`, `OpenJoinByCode`, `JoinRoom`, `CancelJoinByCode`, `LeaveRoom`. `IsBusy` flag: the flow ignores every intent while an operation is in progress, so the double-tap protection doesn't depend on the UI. Local validation of the code (empty or non-alphanumeric doesn't go to the network), normalized to uppercase.
- **Net** (new asmdef `TicTacFade.Net`, `Net → Game → Core`):
  - `UgsSessionService`: private sessions of 2 with Relay, lazy initialization and login, its own timeout (`SerializeField`, 15 s). If an operation finishes after the timeout, the orphaned room is abandoned. The host closes the room with `DeleteAsync`. Before each create/join it waits for the `NetworkManager` to be free, and if the SDK left it running it shuts it down as a last resort.
  - `SessionFailureMapper` translates the SDK exceptions.
  - `PlayModeAuthProfile` picks the authentication profile of each MPPM virtual player.
- **UI:**
  - `MenuScreen` adds "Crear sala", "Unirse con código" and a status line ("Conectando..." / error).
  - `LobbyScreen`: code at 120 pt, "Copiar", status ("Esperando rival..." / "Rival conectado" / "Cerrando sala...") and "Cancelar".
  - `JoinByCodeScreen`: alphanumeric `InputField`, "Unirse", "Cancelar" and error.
  - `SessionFailureMessages` has the texts in Spanish.
- **Builder:** `NetworkManager` + `UnityTransport`, `SessionService`, `LobbyScreen`, `JoinByCodeScreen` (both with `CreateSafeAreaScreen`) and the helpers `CreateCenteredColumn`/`CreateLabel`/`CreateCodeInput`.
- **New PlayMode tests** (`OnlineFlowTests`, with a network-free `FakeSessionService` injected with `MatchFlow.SetSessionService`):
  - full local match + rematch + exit without initializing Unity Services (`UnityServices.State == Uninitialized`, Cami's request);
  - double tap on "Crear sala" creates a single room;
  - a network failure stays in the menu with a message;
  - an empty code doesn't go to the network;
  - a rejected code stays on the screen with an error;
  - joining shows "Rival conectado";
  - opponent joins and leaves;
  - cancel from the code screen;
  - create → cancel twice and join → cancel twice, and then "Jugar local" starts with X;
  - a lost room goes back to the menu with a message.
  - `SceneWiringAndInputTests` adds the new screens to the references check.
- **Finding against the real service:** a nonexistent code with a valid format (`BBBBBB`) arrives as `SessionNotFound` and "Código inválido" is shown, fine. But `ZZZZZZ` arrived as `SessionError.Unknown` ("contains an invalid character 'Z'"): the lobby code alphabet doesn't include every letter. Reading `LobbyConverter.ToSessionException`, the SDK discards the original `LobbyServiceException` and only translates `LobbyNotFound`, so a full room and a malformed code are indistinguishable. The first version of the mapper looked for `LobbyServiceException` in the inner exception chain: dead code, it was removed. `SessionFailure.JoinRejected` was added with a generic join message ("No se pudo unir a la sala. Revisá el código o pedí uno nuevo.") instead of "No se pudo conectar", which suggested a network problem. The alphabet wasn't validated locally because it isn't documented.

**Human review:**
- She approved the plan with three answers:
  - lazy initialization (Cami corrected her own request to initialize at startup) plus a test confirming that local mode never initializes Unity Services;
  - full room with a generic message if the SDK doesn't tell it apart;
  - timeout as a `SerializeField` of the service.
- She added to the manual verification: create → cancel → create and join → cancel → join twice in a row (in case the `NetworkManager` doesn't shut down completely), "Jugar local" after cancelling a room, and "Copiar" on the phone (if the clipboard fails, the code has to stay readable to copy it by hand).

**Verification:**
- Compiles with no errors. `CS0618` warnings of `FindFirstObjectByType`/`FindObjectsSortMode` (obsolete in Unity 6.6) remain in `SceneWiringAndInputTests.cs`, code from before this task: noted, not touched. In the new file `FindAnyObjectByType` was used.
- EditMode 33/33. PlayMode 14/14 (4 existing + 10 new); 31/31 after the second round (see below).
- Builder run 2 times: one instance of each screen, `NetworkManager` and `SessionService`; the five `SafeArea`s with the four edges; `NetworkManager` with `UnityTransport`.
- **Against the real service, in the main editor (a single instance):**
  - anonymous login OK (`default` profile);
  - creating a room gives the code `LD7987` with the `NetworkManager` as host;
  - cancel goes back to the menu with the `NetworkManager` completely shut down (`IsListening=false`, `ShutdownInProgress=false`);
  - a second creation works (code `B6DRRR`);
  - `BBBBBB` → "Código inválido";
  - cancelling the code screen → menu, and "Jugar local" starts with X;
  - no errors in the console.
- **Real device (Cami, APK on two different devices):** creating a room and joining with the code works, and both end up connected in the waiting room. The match doesn't start, which is expected in this iteration (the `Lobby → Playing` transition belongs to the next one).
- **Pending (Cami), with two players in Multiplayer Play Mode:** steps below.
- **Pending (Cami):** "Copiar" on the phone.

**How to reproduce the test with Multiplayer Play Mode** (it is repeated in every online iteration):
1. Open `Window > Multiplayer > Multiplayer Play Mode` (MPPM's documented path; in Unity 6.6 the package is builtin and the menu path couldn't be confirmed in code: if it differs, correct this step).
2. Enable **Player 2** (and **Player 3** only for the full-room test). Wait until its window finishes opening.
3. Have the `Assets/_Project/Scenes/TicTacFadeGame.unity` scene open and enter Play in the main editor: the virtual players enter Play with it.
4. Different identities: in each instance's console, on the first create/join, `Tic-Tac-Fade: signed in anonymously. PlayerId=..., profile=...` has to appear. The main editor uses `profile=default`; each virtual player, `profile=mppm_xxxxxxxx`. The `PlayerId`s have to differ between instances.
5. **Create and join:** Player 1 → "Crear sala". "Conectando..." has to appear with the buttons disabled and then the waiting room with the code and "Esperando rival...". Player 2 → "Unirse con código" → type the code → "Unirse" → waiting room with "Rival conectado". Player 1 has to switch to "Rival conectado".
6. **Invalid code:** Player 2 → "Unirse con código" → `BBBBBB` (valid format, nonexistent room) → "Código inválido...", it stays on the screen. With a code with characters outside the alphabet (e.g. `ZZZZZZ`) → also "Código inválido...", and this time without going to the network.
7. **Cancel from the waiting room:** with both inside, Player 1 → "Cancelar" → menu. Player 2 has to go back to the menu with "La sala se cerró.".
8. **Cancel from the code screen:** Player 2 → "Unirse con código" → "Cancelar" → menu, with no message.
9. **Twice in a row:** Player 1: create → cancel → create → cancel (the second creation has to give another code with no error). Player 2: join → cancel → join → cancel, against an open room of Player 1.
10. **Local after online:** after cancelling a room, "Jugar local" starts a normal match with X.
11. **Full room (optional, Player 3):** Player 1 creates, Player 2 joins, Player 3 tries to join with the same code → generic message "No se pudo unir a la sala..." (the SDK doesn't tell a full room apart, see above).
12. There must be no console errors in any instance. The "session operation failed" `LogWarning`s are expected in the error cases.

**Second round (after Cami's test on a device and the warnings she captured):**
- **Device (Cami):** "Copiar" works on the phone and the flow is correct.
- **Warning `[Netcode] Singleton is not null after invoking OnDestroy`:**
  - Cause: `NetworkManager` moves itself to `DontDestroyOnLoad` and the Singleton stays as the first one. Each scene reload brings a new one and the previous one survives. On leaving Play, each extra one throws the warning.
  - Reproduced by hand: one reload leaves 2 `NetworkManager`s in DDOL and the warning shows up on leaving; Play → Stop without a reload, it doesn't show up.
  - The first attempt to reproduce it gave a false negative: it counted 1 `NetworkManager` because the reload hadn't happened yet. It was repeated verifying that the scene had really reloaded.
  - The game loads the scene only once; the PlayMode tests reload it in each test.
  - Fix, in tests only: `NetworkTestCleanup` and a `[UnityTearDown]` in the two PlayMode classes that destroys every `NetworkManager` after each test.
  - Verified: full suite (about 15 reloads) without the warning. The suite wasn't run again without the fix to compare; the manual reproduction covers the "before".
  - If some day going back to the menu becomes a scene reload, this has to be solved in the game, not only in the tests.
- **`ZZZZZZ` warning:** it was from the code before the `JoinRejected` fix (line 186, `(Unknown)`), expected.
- **Code alphabet (Cami's request):**
  - The "invalid character" message isn't in the SDK: the server returns it. The SDK doesn't document the Lobby code format.
  - It does document the Relay one (`Relay/Models/JoinRequest.cs`): case-insensitive, 6 to 12 characters, only `6789BCDFGHJKLMNPQRTW`.
  - The real service is consistent with that: it generated `LD7987` and `B6DRRR`, rejected the `Z` and accepted the format of `BBBBBB`.
  - `JoinCodeFormat` was implemented in Net, exposed as `ISessionService.IsWellFormedCode`, because the format belongs to the provider and Game must not know it. It replaces `MatchFlow`'s previous alphanumeric validation, which was more permissive than the service.
  - `MapJoin`: a format error that still comes from the service is recognized by the message (the only signal the SDK leaves) and gives `InvalidCode`, not `JoinRejected`.
  - The fake uses the real format rule; the tests' codes became `BCD678`/`BCD789`.
- **New tests:** `JoinByCode_CharacterOutsideAlphabet_RejectedLocallyAsInvalidCode` (`ZZZZZZ` doesn't go to the network and shows "Código inválido"), and `NetSessionRulesTests`: 16 format and mapping cases, including the exact message the real service returned.
- **MCP runner with 0 tests:**
  - For a while, `run_tests(PlayMode)` via MCP ran 0 tests (three attempts: all, by assembly and by name), while the editor listed the 14 and EditMode via MCP worked.
  - The AI's hypothesis (active MPPM virtual players) turned out to be false: with MPPM off it kept giving 0, and Cami ran the tests by hand and they passed.
  - After recompiling it worked again (31/31). Cause undetermined; probably on the MCP runner's side.
- **Verification of this round:** compiles with no errors; EditMode 33/33; PlayMode 31/31 via MCP; console with no errors or warnings after the suite.

**Third round:**
- **CLAUDE.md rule about the MCP runner:**
  - Cami had proposed: "Don't run the PlayMode tests with Multiplayer Play Mode virtual players active: the main editor's runner runs 0 tests."
  - It was ruled out because the evidence contradicted it: with MPPM off the MCP runner kept giving 0, the tests run by hand passed, and the runner worked again after recompiling without touching MPPM.
  - The AI's wording, approved by Cami, was added instead (in English, like the rest of CLAUDE.md): if `run_tests` for PlayMode via MCP reports 0 tests, don't count it as passed; recompile and retry; if it persists, Cami runs the suite from the editor's Test Runner. Cause undetermined.
- **Safety net for the alphabet (Cami's request):**
  - When creating a room, the code the service returns is validated against `JoinCodeFormat`. If it doesn't pass, an explicit error is logged: the local rule became stricter than the service.
  - The risk is asymmetric: a rule that is too strict prevents joining real rooms, and without this check there would be no signal at all.
  - Implemented as `JoinCodeFormat.CheckGeneratedCode`, testable without a network, and called from `UgsSessionService.Attach` only for the host.
  - The log message is in English because of the `.cs` convention: "the room code format changed: local validation would reject the generated code '…'".
  - Tests: two real codes (one in lowercase) pass without logging; `AB12CD` returns false and logs the error (`LogAssert.Expect`, so the test fails if the check stops logging).
- **Verification:** compiles with no errors; EditMode 33/33; PlayMode 34/34 via MCP. The only error in the console is the one the negative-case test generates on purpose.

**Design items noted as pending:**
- The `Lobby → Playing` transition (starting the online match when the opponent connects) belongs to the next iteration.
- Online, "Salir" from a match will be "abandon" (see [12]).
- ~~There is no automated test of `SessionFailureMapper`~~: solved in the second round (`NetSessionRulesTests`). `SessionException` has a public 3-argument constructor.

**Learnings:**
- Reading the installed package's code before designing found three things that would have been bugs or false assumptions: MPPM's hidden automatic profile, that `WithRelayNetwork` requires a `NetworkManager`, and that the SDK discards the original lobby exception.
- Testing against the real service, even with a single instance, detected that a malformed code showed "No se pudo conectar". The tests with a fake couldn't see it, because the fake returns what the test tells it.

**Commit(s):** — (not committed; Cami reviews and commits).

---

## [14] 2026-09-23 — Online iteration 2: playable match between two devices

**Goal:** `docs/prompts/online-2-moves.md`. When the opponent connects to the room, the match starts on its own on both devices and is played to the result over the network, with rematch. Timer, mid-match disconnections and abandonment are left for iteration 3.

**Request / prompt:**
- Host-authoritative model: the client never applies its own move; the host validates with `RulesEngine.IsLegal` and broadcasts; on both devices `GameManager` only receives confirmed moves, through a `RemotePlayer` that is one more `IPlayerController`.
- Position hash comparison after each move; if it differs, the match is cut.
- Host always X and client always O; the rematch alternates who starts.
- Choose between RPCs and named messages, and justify it.
- Board with input only on your turn and no double send; HUD "Tu turno" / "Turno del rival".
- Rematch requested by both; "Menú" and "Salir" close the session and the other sees "El rival salió".
- Test with two `GameManager`s and an in-memory transport.

**What the AI did:**
- **Plan with decisions to approve:** expose `PositionKey.Value` (Core) and add three new members to `IPlayerController`. Cami approved with adjustments (see Human review).
- **Transport:** named messages (`CustomMessagingManager`), not RPCs: they don't need a `NetworkObject`, prefab or spawn, and a `NetworkObject` in a scene generated in code is fragile (`GlobalObjectIdHash`). A single message with a type byte; all with an explicit `NetworkDelivery.ReliableSequenced`. Justified in ADR 0002. The signatures of `RegisterNamedMessageHandler`, `SendNamedMessage` and the connection events were verified in the NGO 2.13.3 code before using them.
- **Core:** public `PositionKey.Value` (no rule changes) and an EditMode test that verifies that the value follows key equality exactly.
- **Game:**
  - `IPlayerController` adds `IsLocalHuman`, `AcceptsLocalInput` and `AcceptsLocalInputChanged`. `LocalHumanPlayer` stays the same in behavior.
  - New `OnlineLocalPlayer` (a tap proposes and locks the input until confirmation or rejection) and `RemotePlayer`, both with `IConfirmedMoveReceiver`.
  - `GameManager` exposes `CanAcceptLocalInput`, `IsLocalHuman(Occupant)` and `LocalInputAvailabilityChanged`, and `OnCellClicked` uses that guard. Its match logic didn't change and it doesn't know there is a network.
  - New `MatchMessage`, `IMatchTransport` + `MatchTransportBehaviour` (with an inbound buffer until `Open`) and `OnlineMatch` (authority, confirmations, `Ack` with key, desync, two-player rematch).
  - `MatchFlow`: `Lobby → Playing` row; it opens the `OnlineMatch` on entering the room; online rematch, "Menú" and "Salir"; "El rival salió" and "Se perdió la sincronización" as new `SessionFailure`s. On leaving online it restores the two `LocalHumanPlayer`s.
- **Net:** `NgoMatchTransport` registers the handler when the network starts (before the client finishes connecting) and raises `PeerConnected` with `OnClientConnectedCallback`.
- **UI:**
  - `BoardView`: no input out of turn or while a move is pending.
  - `GameHud`: "Tu turno" / "Turno del rival" when there is a single human on the device; in local it stays "Turno: Jugador X".
  - `ResultScreen`: "Esperando al rival..." after asking for a rematch.
- **Builder:** `MatchTransport` connected to the `NetworkManager` and to `MatchFlow`; `RematchStatus` line in the result panel. Idempotent: one instance of each object after two runs.
- **New tests:**
  - `OnlineMatchTests` (two `GameManager`s with no scene, in-memory transport with a queue and explicit pumping):
    - full match with identical states at the end (cells, queues, turn, moves, result and hash);
    - four illegal client proposals (occupied cell, the host's symbol, out of range, out of turn) rejected without changing any state;
    - a `StartMatch` that arrives before the client is ready, processed from the buffer (Cami's request);
    - a double tap during the wait sends a single proposal;
    - a rematch that needs both requests and starts with O;
    - a different hash that cuts on both sides.
  - `OnlineMatchFlowTests` (real scene against a simulated device):
    - full host through the screens: HUD, input locked on the opponent's turn, win, rematch with waiting, "El rival salió" and then a normal "Jugar local";
    - client with a buffered start and the host closing the room;
    - "Menú" from the result;
    - "Salir" mid-match;
    - desync by an `Ack` with a different key.

**Human review:**
- Cami approved the plan with adjustments:
  - `IsLocal` becomes `IsLocalHuman`: when the AI exists it will run on the device without being human, and the HUD depends on whether a human controls that side.
  - `ReliableSequenced` explicit in code and documented in the ADR.
  - Test of the `StartMatch` that arrives before the client's flow is ready.
  - Note the pending item of the client locked with no answer from the host.

**Verification:**
- Compiles with no errors.
- EditMode 34/34 (33 + the `PositionKey.Value` test).
- PlayMode 45/45 via MCP (34 + 6 from `OnlineMatchTests` + 5 from `OnlineMatchFlowTests`), including local mode unchanged.
- Builder run 2 times with no duplicates.
- The errors and warnings left in the console after the suite are generated by the tests on purpose (rejections, forced desync, code `AB12CD`).
- **Real device (Cami, APK on two devices):** creating a room, joining by code, full online match, winning, losing, rematch and the rest of the result flow work.

**Pending for iteration 3:**
- **Client locked with no answer from the host:** if a client proposal never receives `Confirmed` or `Rejected` (host down), `OnlineLocalPlayer` stays pending forever with the input locked. It needs a timeout or recovery together with the disconnection handling.
- "Salir" and "Menú" during an online match count as a loss (today they only close the session).
- Turn timer with absent mode (GDD §3.5).

**Learnings:**
- Separating the authority (`OnlineMatch`) from the transport (`IMatchTransport`) made it possible to test the full model, including desync and buffer, with no network or scene. An in-memory transport that delivers only on `Pump` reproduces the real asynchrony; one that delivered instantly would have hidden the case of the `StartMatch` that arrives early.

**Commit(s):** — (not committed; Cami reviews and commits).

---

## [15] 2026-09-23 — Online iteration 3: turn timer, absent mode, disconnections and abandonment

**Goal:** `docs/prompts/online-3-robustness.md`. Host-authoritative turn timer with an automatic move on expiry, absent mode, abandonment by 3 timeouts and by "Salir", handling of host and client drops, and the pending item from iteration 2 (proposal without answer).

**Request / prompt:**
- Cami confirms that 3 consecutive timeouts are a loss by abandonment.
- The host owns the timer (30 s / 10 s) and the client only shows the countdown.
- The random move lives in Game, with an injectable source.
- "Salir" is a loss ("Ganaste: el rival abandonó" for the one who stays). If modelling the abandonment requires touching the Core, stop and propose first.
- Client drop: the match goes on. Host drop: menu with "Se perdió la conexión con el rival".
- HUD with time and "absent".
- Injectable time in tests and coverage of every case with the in-memory transport.

**What the AI did:**
- **Plan with decisions to approve.** The first message of the plan reached Cami incomplete and mixed with the closing of iteration 2, so points 1 to 7 were resent in full.
- **Core:** no changes. Abandonment was modelled in Game (`MatchResult`, `GameManager.Halt()`), so there was no need to stop and propose. The first proposal put the timer parameters in the Core's `GameConfig` following CLAUDE.md rule 6 as it was written; Cami corrected it (see Human review).
- **Game:**
  - New: `OnlineTimerConfig` (exposed by `GameConfigAsset.ToOnlineTimerConfig()`), `OnlineNetworkSettings`, `IClock` + `UnityClock`, `IRandomSource` + `SystemRandomSource`, `MatchResult` + `AbandonmentCause`.
  - `OnlineMatch`:
    - authoritative timer with `Tick()`;
    - `ChooseAutomaticMove`: at random among the legal moves that don't win, simulated with `RulesEngine.Apply`, without touching the Core; if all of them win, any of them;
    - absent mode and count of consecutive timeouts; the 3rd abandons without playing;
    - "Salir" with `Forfeit`/`ForfeitAck` and a bounded wait;
    - `NotifyPeerGone`: 5 s grace on the host, connection lost on the client;
    - proposal timeout on the client;
    - dropping sends to a device that is already down.
  - `MatchFlow`:
    - tick per frame and `SetTimeSources` for tests;
    - `LastResult` becomes `MatchResult`;
    - online `ExitMatch` with "Saliendo..." (busy) until the ack;
    - distinction between the opponent leaving mid-match, on a result by abandonment (the screen stays) or on a board result ("El rival salió");
    - `SessionFailure.ConnectionLost`.
- **Net:** `NgoMatchTransport` serializes the new fields and exposes `PeerDisconnected` from `OnClientDisconnectCallback`.
- **UI:**
  - `GameHud`: countdown "0:23" in another color if the player to move is absent, " · ausente" next to the counter and "Saliendo...". No polling: it reacts to events of the shown second and of the timer.
  - `ResultScreen`: "Ganaste: el rival abandonó" / "Perdiste: 3 turnos sin jugar", and rematch disabled without an opponent.
- **Builder:** `TurnTimerLabel` in the HUD and `GameHud` connected to `MatchFlow`; idempotent.
- **CLAUDE.md:** rule 6 rewritten with Cami's wording, and the folder tree adds `Tests/PlayMode/`.
- **New tests:**
  - `OnlineRobustnessTests` (10, in-memory transport and manual clock):
    - timeout with the same automatic move on both sides;
    - the automatic move never picks the winning one (tested with every value of the randomness) and, if all win, it picks one;
    - absent mode with 10 s turns until the player's own move;
    - 3rd timeout: abandonment with no automatic move;
    - "Salir" with ack and without ack;
    - host drop;
    - client drop, with a 5 s grace;
    - proposal without answer.
  - `OnlineTimerConfigTests` (2): values from GDD §9.
  - `OnlineMatchFlowTests`: new test of countdown and absent in the HUD. Three existing ones were adapted:
    - "Salir" now shows "Saliendo..." and is a loss;
    - the client drop on the rematch ends in "Ganaste: el rival abandonó";
    - the host drop shows "Se perdió la conexión".

**Human review:**
- Timer outside the Core: the `RulesEngine` never reads those parameters, so they don't go in `GameConfig`. Cami rewrote CLAUDE.md rule 6, which was the one saying the opposite, and asked to add `Tests/PlayMode/` to the tree.
- Approved that the 3rd timeout doesn't play at random.
- New rule (GDD §3.5): the automatic move never wins, unless every legal one wins, so an absent player doesn't win without playing. With a test.
- Approved the ack of up to 2 s for "Salir", showing "Saliendo...".
- Explicit client disconnection: a short 5 s grace and abandonment, without waiting for the 3 timeouts (about 50 s that wait for nobody, because there is no reconnection). The timeouts are left for the connected opponent who doesn't play.

**Verification:**
- Compiles with no errors.
- EditMode 34/34. PlayMode 58/58 via MCP (45 previous + 10 robustness + 2 config + 1 HUD).
- Builder run 2 times with no duplicates.
- First PlayMode run: 55/56. The host test ended with `LogAssert.NoUnexpectedReceived()` and now there is an intentional warning when the grace expires; it was declared as expected (`LogAssert.Expect`). It wasn't a game bug.
- The errors left in the console after the suite are generated on purpose by the desync tests and the code `AB12CD` test.
- **Pending (Cami), two devices, one on wifi and the other on mobile data:**
  - let a turn expire (automatic move and "absent");
  - play again and leave absent mode;
  - abandon with "Salir" ("Saliendo..." on one, "Ganaste: el rival abandonó" on the other);
  - close the client app mid-match and see the host win by abandonment after about 5 s.

**Pending items and known limits:**
- The client's countdown doesn't compensate for latency: it can show a few milliseconds too many. Only the host decides.
- Accepted race: if the host's timer expires while a client proposal is in flight, the timer wins and the proposal arrives out of turn, so it is rejected.
- No reconnection in the MVP.

**Learnings:**
- A CLAUDE.md rule can be badly written: rule 6 pushed toward putting in the Core parameters the Core doesn't use. Following it to the letter led to the first proposal; Cami corrected the rule, not only the plan.
- Separating "disconnected" from "connected but not playing" changes the experience quite a bit (5 s against about 50 s of waiting) and it isn't visible if everything is modelled as timeouts.

**Bug under investigation (reported by Cami after testing iteration 2 on two phones):**
- **Symptom:** in the first room created, the client stays in the waiting room while the match starts on the host. It happened twice, with both phones on wifi.
- **Another error, separate:** in a test of a phone (iteration 2) against the editor (iteration 3), the editor threw `OverflowException: Reading past the end of the buffer` in `NgoMatchTransport.OnNamedMessage`. It is consistent with the message size difference between versions (19 against 25 bytes), untested; it couldn't be tied to the exact line.
- **What was confirmed in the NGO 2.13.3 code:**
  - a named message that arrives with no handler registered is silently discarded;
  - the client registers its handler inside `StartClient()`, before connecting;
  - with scene management active, the host gets "client connected" only after the client synchronized scenes;
  - the active scene isn't reloaded when synchronizing.
  - With that, the obvious race shouldn't happen: **cause undetermined**.
- **Open hypotheses:**
  - the single `StartMatch` the host sends is lost, with no retry;
  - the first cold join exceeds the 15 s timeout.
- **Step 1 (done):** diagnostic logs with no behavior change. `NetDiagnostics.Log` with the prefix `Tic-Tac-Fade [net t=…]`, in:
  - handler registered, connected and disconnected;
  - sent and received, the latter with whether it was delivered or stayed in the buffer;
  - opening and closing of the transport;
  - `StartMatch` sent and processed, and the flow's switch to Playing;
  - duration of create and join.
- The PlayMode suite turns them off, with a `SetUpFixture`: `LogAssert.NoUnexpectedReceived()` also counts normal `Log`s, and five tests failed until they were turned off.
- Verified in the editor against the real service: create room → close → create again leaves the full sequence in the log. PlayMode 58/58.
- **Step 2 (done, Cami):** APK with the same code on two phones. **The bug didn't reproduce.**
- **Step 3 (done, see [16]):** structural fix with no confirmed cause: `Ready` handshake with resend, match number in `StartMatch`, protocol version and tolerant reading.

**Commit(s):** — (not committed; Cami reviews and commits).

---

## [16] 2026-10-06 — Online start: Ready handshake, match number and protocol version

**Goal:** close the "Bug under investigation" of [15] (the client stays in the waiting room while the match starts on the host) with a fix that eliminates the whole class of bug.
- **The bug didn't reproduce:** after the two initial times, Cami couldn't provoke it again on two phones.
- **The cause is not confirmed:** it was decided not to keep investigating it and to apply a structural fix.
- If a similar symptom shows up again, the diagnostic logs from [15] are still in the code, off by default.

**Request / prompt:** step 3 of [15], with an approved plan and three rounds of adjustments by Cami.
- `Ready` handshake: the client resends it until it gets `StartMatch`, and the host starts with the `Ready`, not with the Netcode connection. A duplicated `Ready` doesn't start two matches.
- Protocol version in the first message, with "El rival tiene otra versión del juego" on both sides.
- Tolerant reading: a message of unexpected size is discarded with a warning, never with an exception.
- Tests with the in-memory transport. The diagnostic logs stay as they are.

**What the AI did:**
- **Game:**
  - **New `MatchMessageCodec`**, the frame format shared by both transports:
    - fixed `kind`+version header, 31 bytes in total;
    - `TryDecode` never throws;
    - a frame of another version is decoded only by the header, and the rest of what is invalid is discarded with a warning.
  - **`MatchMessage`:** kinds `Ready` (12) and `VersionMismatch` (13), and fields `MatchNumber` and `ProtocolVersion`.
  - **`OnlineMatch`:**
    - resend of `Ready` in `Tick` and the host's start with the first `Ready`;
    - `ResendCurrentStart` on a later `Ready`, with the `TurnTimer` of the host's remaining time;
    - the client only accepts the `MatchNumber` it expects;
    - exit on a different version, in which the host waits for the client's answer up to 2 s.
  - **`MatchFlow`:** `readyResendIntervalSeconds` (1 s) and `SessionFailure.VersionMismatch`, which leads to the menu.
  - `IMatchTransport.PeerConnected` and `IsPeerConnected` were removed.
- **Net:** `NgoMatchTransport` serializes with the codec and reads exactly the bytes that arrived, without reading past the end. A `Ready` without connection is silently discarded, only with `NetDiagnostics.Log`.
- **UI:** text "El rival tiene otra versión del juego.".
- **Tests:**
  - The in-memory transport now passes everything through the codec (the same bytes as the network) and adds `LoseNextSent`, `SetLinkUp`, `InjectToPeer` and `DeliverRawToPeer`.
  - **New `OnlineHandshakeTests` (10):**
    - a lost `Ready` that is recovered by resending;
    - a duplicated `Ready` that doesn't start another match;
    - a lost `StartMatch` that is recovered, with the client's countdown equal to the host's remaining time;
    - a duplicated `Ready` in a rematch that O starts: the client sees 20 s and the host expires the turn when the countdown reaches 0;
    - an old `StartMatch` after a rematch, ignored;
    - a `Ready` without connection as a silent no-op (no logs) that recovers on connecting;
    - different version on both sides;
    - different version with no answer: the host leaves after 2 s;
    - a truncated message discarded with no exception;
    - codec: empty, null, unknown `kind` and round trip of every field.
  - **`OnlineMatchFlowTests`:** new test of a different version in the scene (menu with the text). The setups were adapted (no `ConnectPeer`, with `Pump` after the client's join).
  - The test of the `StartMatch` in the buffer was replaced by "the `Ready` arrives before the host listens".
- **Docs:**
  - ADR 0002 with a new section;
  - GDD §5 with the different-version line;
  - GDD v0.8: row in the history and the header "MVP implementado — en verificación en dispositivos" [MVP implemented — under verification on devices], at Cami's request.

**Human review:**
- Approved always answering the `Ready` by resending the current `StartMatch`, so a lost `StartMatch` doesn't leave the client resending forever.
- **Adjustment 1:** instead of a "match in progress" check, a match number in `StartMatch`. The client accepts only the one it expects, so the duplicated `Ready` and the rematch are solved with the same rule. Test of an old `StartMatch` after a rematch.
- **Adjustment 2:** the `Ready` before Netcode connects must be a silent no-op, not a warning per second. It was verified in NGO that `SendNamedMessage` doesn't check the connection, so the guard goes in the transport.
- **Adjustment 3:** the resent `TurnTimer` carries the remaining time, not the full duration (the case of the rematch that O starts). `MatchNumber` becomes a field of its own instead of reusing `MoveNumber`: with version 1 breaking compatibility, changing the format was free.

**Verification:**
- Compiles with no errors. The only warnings are CS0618 in `SceneWiringAndInputTests`, which were already there.
- EditMode 34/34. PlayMode 69/69 via MCP (58 previous, 10 handshake and 1 flow).
- Only the intentional errors of the desync tests and the code `AB12CD` test are left in the console.
- **On devices (Cami, 2026-10-07, two phones):** several tests of room creation and match start.
  - The bug of the client that stayed in the waiting room **didn't reproduce after the structural fix**.
  - This doesn't confirm the cause: it hadn't reproduced either in the tests of [15] before the fix.
- **Not tested:** the APK with `ProtocolVersion` changed against a normal one, to see the different-version message on both sides. This verification was optional; the case is covered by the tests.

**Pending items and known limits:**
- **Incompatible with the previous APKs:** a new host with an old client stays in the waiting room, because no `Ready` reaches it.
- The host has no timeout if the `Ready` never arrives. The user can cancel from the room.
- **If the host played before the client recovers a lost `StartMatch`**, the client would receive a `Confirmed` with no match and it would be detected as a desync, not silently. With reliable, ordered delivery it shouldn't happen.
- **The guard for `Ready` without connection in `NgoMatchTransport` has no automated test**, because it needs a network. The test covers the equivalent behavior of the in-memory transport.

**Learnings:**
- When a network bug doesn't reproduce, a protocol that recovers by itself (idempotent resend with sequence numbers) is cheaper than continuing to look for a race that may not happen again.
- A monotonic number in the message solves with a single rule cases that with flags needed reasoning about the moment they are cleared.
- Resending a state with time (the timer) forces sending what is left, not what there was at the beginning. The human review detected it, not the plan.

**Commit(s):** — (not committed; Cami reviews and commits).

---

## [17] 2026-10-07 — GDD v0.9: "Room options" in the roadmap

**Goal:** document in the roadmap (GDD §8) the room options after the MVP. Documentation only, no code.

**Request / prompt:** design given by Cami.
- Location: new item after the vs-AI mode.
- Independent options: indicators yes/no, turn time 10/20/30 s and move cap according to the solver.
- "Normal" and "Difícil" (Hard) are presets, not modes with their own rules.
- Without indicators all the information about which piece goes is hidden, which makes the "memory mode" of §4.1 concrete.
- The host picks and the configuration travels to the client. The move cap is a Core rule and has to be identical on both sides.

**What the AI did:**
- GDD §8: item 3 "Room options" with the design as Cami asked for it. The following items were renumbered from 4 to 7.
- Row 0.9 in the history and header at version 0.9. The status stays "MVP v0.1.0", because the GDD version is independent of the game's.

**Human review:** pending.

**Verification:** not applicable (no code).

**Open questions for when it gets planned** (not resolved in the GDD):
- **Turn time vs. turn in absent mode.** With a 10 s turn, absent mode's 10 s turn (§3.5, §9) stops being shorter. It still has to be decided whether the absent time scales with the option or stays fixed.
- **Scope of the options.** The timer is online only today (§3.5). It still has to be defined whether the room options also apply to local mode and the vs-AI mode, or only to online rooms.
- **Protocol version.** Sending the configuration to the client changes the protocol, so it bumps `ProtocolVersion` (ADR 0002).

**Commit(s):** — (not committed; Cami reviews and commits).

---

## [18] 2026-10-07 — Solver by retrograde analysis (GDD §8.1)

**Goal:** solve the full 3x3 game with buffer 3 and answer with data. Is the initial position a forced win for whoever starts, for the second player, or for neither? If there is a forced win, in how many moves? Only the solver and its answers: no AI or UI.

**Request / prompt:** Cami's request, with an approved plan.
- New assembly `TicTacFade.Solver` (pure C#), without modifying the Core.
- Identity by `PositionKey`.
- Equivalence test with `RulesEngine` on every reachable position.
- Check of the cap of 40.
- Measurement of size, time and memory, and a criterion between computing at startup or storing an asset.
- Rejection of large configurations.

Cami's answers to the plan: distances in plies, measurement in the editor for now and a threshold of 5 million positions. Cami's additions:
- report each first move and the distribution of results;
- consider WebGL (no threads) in the runtime vs. asset criterion, with no computation during a match;
- note for the AI that the forced win assumes the path doesn't repeat positions already seen in the match.

**What the AI did:**
- **`Scripts/Solver/`** (`TicTacFade.Solver.asmdef`, `noEngineReferences: true`, depends only on the Core):
  - `GameSolver`: validation with a bound on the state space, forward exploration and retrograde with a queue by distance.
  - `SolverPosition`: move generation mirroring `RulesEngine`.
  - `SolverTable`: lookup by `PositionKey` or `GameState`, and `GetMoveResults` with the value of each move.
  - `SolverStats`, `PositionResult`, `PositionOutcome`, `MoveResult` and `SolverMoveEdge`.
- **The Core didn't change.** Nothing needed exposing: the public API was enough.
- **`Scripts/Editor/SolverReport.cs`:** menu `Tic-Tac-Fade/Solver/Solve MVP and report`.
- **EditMode tests (`SolverTests`, 7):**
  - equivalence with `RulesEngine.IsLegal` and `Apply` on every reachable position (legality per cell, immediate win, resulting position and total count);
  - consistency of the retrograde pass: each position's value is the best of its moves;
  - win in 1;
  - unavoidable loss in 2, also verified by hand with the Core: the blocking cell is occupied by the defender's oldest piece;
  - stable initial position: `Win in 13` in two runs and also with O starting, with edges that win and corners and center with no forced win;
  - forced lines within the cap;
  - rejection of 4x4 with buffer 4.
- **Docs:**
  - `docs/solver-results.md` (new);
  - ADR 0004 (assembly, `NoForcedWin` criterion, notes for the AI, runtime vs. asset criterion);
  - answer in GDD §8.1.

**Findings:**
- **Whoever starts has a forced win in 13 moves** (plies). It requires opening on an edge (1, 3, 5 or 7); opening on a corner or the center, neither side can force it.
- **116,074 reachable positions** (the bound was 204,086) and 369,801 moves.
- **Distribution:** Win 67.7 %, Loss 20.9 % and NoForcedWin 11.4 %.
- **Longest lines:** the longest forced win is 17 and the longest loss 16.
- **Cap of 40:** it doesn't cut any forced line from the start.
- **Performance:** about 0.5 s in the editor and about 30 MB retained by the full table.
- **Leaning for the AI:** a compact precalculated asset (around 1 MB) for WebGL and mobile. The final decision is made in the AI iteration.

**Human review:** plan approved with the three additions above. The result hasn't been reviewed yet.

**Verification:**
- Compiles with no errors.
- EditMode 41/41 via MCP (34 previous + 7 from the solver). The suite takes about 23 s; almost all of it is the equivalence test.
- **PlayMode via MCP: it didn't run.** "Test job failed to initialize", 0 tests, twice, also after forcing the recompilation. Per CLAUDE.md, it doesn't count as passed.
- **PlayMode from the Test Runner (Cami): 69/69.**
  - The console left 5 errors and 20 warnings, reviewed one by one. All of them are provoked by the tests on purpose:
    - errors: the code `AB12CD` and four "out of sync" from the two desync tests;
    - warnings: malformed messages, different version, illegal proposals, disconnection grace, "Salir" without ack and proposal without answer.
  - There are no compilation errors or unexpected logs.
- The Editor report was obtained by running the menu via `execute_code`, because `read_console` didn't return the `Debug.Log`.

**Pending items and known limits:**
- ~~**The result depends on the solver's move generation being correct.** It is covered by the equivalence test with the Core and the retrograde consistency one. There is no independent verification by forward search.~~ Solved: see "Independent verification" below.
- **Measurement only in the editor.** Measuring on Android and, when it exists, on WebGL is still missing.
- **The game is solved in favor of whoever starts.** For balance and the AI difficulties, to be decided.
- **Cap values for "Room options":** choosing them with data requires another run with the cap as part of the state.

**Independent verification (added before the commit, at Cami's request):**
- **`tools/verify_solver.py`:** a solver in Python written from the GDD rules. It was written by Claude in the project's planning conversation (not the Claude Code agent), with no access to the C# code. Cami added it to the repo. It has no dependencies.
- The AI read it in full before adding it and ran it (`python3 -I tools/verify_solver.py`, ~1.5 s of exploration).
- **It reproduces exactly every number:**
  - 116,074 positions and 369,801 moves;
  - `Win in 13` in the initial position;
  - the 9 openings (edges `Win in 13`, corners and center `NoForcedWin`);
  - Win 78,613, Loss 24,268, NoForcedWin 13,193;
  - maximum distances 17 and 16, and the full distance histogram.
- **It doesn't cover** the symmetry when O starts (it doesn't explore that position); the C# test verifies it.
- It also rebuilds a forced line of 13 moves with no repeated positions: X1 O8 X4 O7 X6 O2 X0 O3 X8 O4 X5 O7 X2.
- It is mentioned in `docs/solver-results.md` and in row 0.10 of the GDD.

**Other changes before the commit:**
- **GDD 0.10:** row in the history with the solver results and updated header. Cami asked not to use 1.0.
- **Equivalence test marked `[Category("Slow")]`.**
- **CLAUDE.md, Testing:** "Run the Slow category tests whenever the Core or the Solver is touched."

**Learnings:**
- Measuring in an Editor menu leaves the numbers reproducible, without the pure assembly needing logging.
- When the result is strong and surprising (the center forces nothing), an internal consistency test is worth having on top of the equivalence test with the rules.
- A second implementation written from the specification (the GDD), with no access to the code and in another language, is the strongest verification of a result that can't be checked by hand.
  - That both histograms match number by number rules out implementation errors in the C# code.
  - It doesn't rule out a wrong reading of the GDD shared by both implementations. Both were written by Claude, in different contexts, so the independence is of code, not of judgment.

**Commit(s):** — (not committed; Cami reviews and commits).

---

## [19] 2026-10-29 — Vs-AI mode, phase 1: the AI without UI (GDD §8.2)

**Goal:** an `AIPlayer` that plays with the solver table (ADR 0004), with three levels and a delay that doesn't jam the turn loop. The UI is left for phase 2.

**Request / prompt:** `docs/prompts/ai-player.md`, with an approved plan. Cami's answers to the plan:
- `NotifyMatchStopped()` in `IPlayerController`, called by `GameManager` on every path, plus a guard for late moves as a second safety net.
- Between draws, prefer `NoForcedWin` to keep the match alive; the immediate draw, only when the alternative is losing.

Cami's additions:
- two-move analysis: win without escape > win with escape > draw, and document that nothing deeper is analysed;
- a per-level flag "block the immediate loss", on in Normal and Perfecta (Perfect);
- `*.bytes binary` in `.gitattributes`.

**What the AI did:**
- **Solver:**
  - `CompactSolverTable` (new): binary format with a rules header, the sorted keys and one byte per position. It merges the tables of both starting players and looks up by binary search.
  - `SolverTable`: exposes `Entries` (the positions with their value), which is what the compact table needs.
- **Game:**
  - new: `AIDifficulty`, `AILevelConfig`, `AIConfig`, `AIMoveCategory`, `AIMoveEvaluation`, `AIMoveEvaluator`, `AIMoveChooser` and `AIPlayer`;
  - `GameConfigAsset`: levels section (error and the two flags per level) and the delay;
  - `IPlayerController.NotifyMatchStopped()`, implemented empty in the other controllers;
  - `GameManager`: calls it in `Initialize`, `StartNewGame`, `DiscardMatch`, `Halt` and when the match ends, and adds the late-move guard;
  - `SystemRandomSource`: constructor with a seed;
  - the asmdef adds the reference to `TicTacFade.Solver`.
- **Editor:** `SolverTableGenerator`, menu `Tic-Tac-Fade/Solver/Generate AI table`. It generated `Config/AI/SolverTable.bytes`: 133,364 positions, 1.17 MB, around 1.2 s.
- **EditMode tests (`CompactSolverTableTests`, 8):**
  - matches the solver for both starting players [Slow];
  - contains O's openings;
  - bytes round trip, deterministic;
  - unknown position;
  - different rules: error asking to regenerate;
  - other draw limits: loads anyway;
  - truncated data;
  - the committed asset matches a regenerated one [Slow].
- **PlayMode tests (`AIMoveChooserTests` 20, `AIPlayerTests` 9):**
  - the AI never plays an illegal move (the 4×4 level pairings, 40 seeds);
  - Perfecta against Perfecta: whoever starts wins, in 13, with both starting players;
  - Perfecta starting against a random opponent wins the 200 matches of each starting player;
  - deterministic error roll per level;
  - immediate win: Fácil (Easy) can let it pass; Normal and Perfecta take it without rolling;
  - immediate loss: Normal blocks even on error; Fácil can leave it open; with no safe move, any;
  - cap at the exact limit (loss and win), on both sides;
  - repetition: when losing, it takes it; when winning, it avoids it; it prefers a win without escape;
  - with a real `GameManager` and a manual clock:
    - the loop goes on after a late move;
    - a whole match between two AIs with a delay;
    - synchronous delay 0;
    - the pending delay is cancelled with `DiscardMatch`, `StartNewGame`, `Halt` and `Initialize`;
    - `NotifyMatchStopped` when the match ends;
    - late move with no match, ignored.
- **Docs:** draft of ADR 0005; `CLAUDE.md` (dependencies `Game → Solver → Core`, the solver assembly and `AIPlayer` in the list of controllers); `.gitattributes`.

**Findings:**
- **The table has to cover both starting players.** The rematch starts with O, and the opening phase of those matches doesn't appear in X's table. On merging both tables, the values match on every shared position.
- **There was no need to touch the Core.** `GameState.PositionCounts` stays `internal`: the AI plays each candidate with `Apply` and reads the result.
- **Latent bug in `GameManager`:** a move received after `DiscardMatch` threw a `NullReferenceException`. No controller provoked it until now; now the cancellation and the guard cover it.
- **The repetition scenarios weren't written by hand.** The MCP's `execute_code` wasn't responding during the implementation (see Verification), so fixed positions couldn't be searched for from the Editor. The tests find them by themselves, with seeded matches that try to return to seen positions, and they require finding at least one.
  - With limit 3, "win with and without escape at the same time" showed up only once in 1500 matches; that's why that test uses limit 2.

**Human review:** plan approved with the additions above. Cami approved phase 1 with the adjustments listed in Verification and asked to commit it as a checkpoint before phase 2.

**Verification:**
- Compiles with no errors; the console was left clean. The CS0618 warnings in `SceneWiringAndInputTests` were already there.
- EditMode 49/49 via MCP, Slow included (around 28 s).
- PlayMode via MCP: the first run gave 0 tests and wasn't counted. After forcing the recompilation and filtering by assembly: **98/98** (69 previous + 29 new), in around 23 s. The "0 tests" rule in `CLAUDE.md` was updated with this step.
- **The MCP's `execute_code` wasn't responding** during the implementation: it returned `success: false` with no message, even with `return 1 + 1`. The rest of the MCP tools (refresh, console, tests, menus) worked. It responded again during the review, and there it was used to force the save of `GameConfig.asset`.
- **Independent verification with O starting:** `tools/verify_solver.py` was extended to also explore that initial position and the union of both graphs.
  - It gives 133,364 positions (the same as the asset) and `Win in 13` for both starting players.
  - It matches the X-only computation on every shared position.
  - Cami had verified it the same way from the planning conversation.
  - It is recorded in `docs/solver-results.md`.

**Adjustments from Cami's review (before the commit):**
- `GameConfig.asset` was force-saved, so the new AI fields land in this commit. The three online timer fields, which were missing in the YAML, were also written; they have the same default values.
- Comment in the "win with escape" test about why it uses `RepetitionLimit = 2`.
- `CLAUDE.md`: the "0 tests" rule now says to recompile and filter by assembly.

**Learnings:**
- When an exploration tool fails, a test that looks for its own scenario and requires finding at least one replaces a fixed position, and it also doesn't depend on how the table turned out.
- For a case that barely shows up, it is worth looking for the parameter that makes it frequent without changing the logic under test (here, the repetition limit), instead of blindly increasing the number of matches.

**Commit(s):** — (not committed; Cami reviews and commits).

---

## [20] 2026-10-29 — Vs-AI mode, phase 2: menu, level, HUD and result (GDD §8.2)

**Goal:** play against the AI from the menu: Menu → level choice → Match → Result → Rematch | Menu.

**Request / prompt:** phase 2 of `docs/prompts/ai-player.md`, after committing phase 1 as a checkpoint (`e3942f2`).

**What the AI did:**
- **Game:**
  - `FlowState.AILevelSelect = 5`, at the end of the enum, with "volver" to the menu.
  - `MatchFlow`:
    - `OpenAILevelSelect`, `CancelAILevelSelect` and `PlayAgainstAI(AIDifficulty)`;
    - loads the table once from a serialized `TextAsset` and advances the AI from its `Update`;
    - exposes `IsVsAI`, `AIDifficulty` and `AISymbol`;
    - when going back to the menu (from the result or with "Salir") puts the two `LocalHumanPlayer`s back.
  - `GameManager`: exposes `AIConfig` and `Rules` (the board rules also before the first match, to validate the table).
- **UI:**
  - `AILevelScreen` (new): three level buttons and "Volver";
  - `MenuScreen`: "Jugar contra la IA" button;
  - `GameHud`: "Turno de la IA";
  - `ResultScreen`: "¡Ganaste!" / "Ganó la IA".
- **Scene** (`TicTacFadeGame.unity`), regenerated by the builder via MCP and run twice with no duplicates:
  - `MenuScreen/.../MenuButtons/PlayVsAIButton` (new), below "Jugar local";
  - `AILevelScreen` (new, with `SafeArea` on the four edges, `CanvasGroup` and `FlowScreenVisibility` in `AILevelSelect`), with `AILevelTitle`, `AILevelSubtitle`, `AIEasyButton`, `AINormalButton`, `AIPerfectButton` and `AILevelBackButton`;
  - `MatchFlow.aiTable` points to `Config/AI/SolverTable.bytes`.
- **PlayMode tests:**
  - `AIFlowTests` (new, 6), on the real scene and with a manual clock:
    - full flow menu → level → match → result → rematch (the AI starts and opens) → menu;
    - each button starts its level (3);
    - "Volver" from the level screen;
    - "Salir" while the AI is thinking cancels its move, and afterwards "Jugar local" is two humans again and X starts.
  - `SceneWiringAndInputTests`: `AILevelScreen` with no null references and 7 `FlowScreenVisibility`. The `MatchFlow` check now also covers `aiTable`.
- **Docs:** GDD v0.11 (§4.3 and §8.2 with the final design), ADR 0005 accepted with the flow section.

**Decisions made by the AI (to review):**
- **A single new state.** The match and the result reuse `Playing` and `Result`, like online. Two more states would have duplicated the screens' visibility without adding behavior.
- **The human is always X and the AI O.** The symbol doesn't alternate; what alternates is who starts.
- **Result texts:** "¡Ganaste!" / "Ganó la IA", instead of "¡Jugador X gana!".
- **Missing or invalid table:** error in the console and the match doesn't start. There was no on-screen message, because with the committed asset and the Slow test it shouldn't happen. Cami corrected it in the review (below).

**Human review:** phase 2 approved, with two adjustments:
- **Missing or invalid AI table:** an on-screen message and staying in the menu, like with the other errors. Never a button that does nothing.
  - The table is validated in `OpenAILevelSelect`, before showing the levels.
  - New value `SessionFailure.AIUnavailable` = 14, with the text "La IA no está disponible en esta versión del juego.", shown in `MenuStatus`.
  - Test `UnusableAITable_ShowsErrorAndStaysInMenu`, with two cases: missing reference and data that isn't a table. It verifies that the flow stays in the menu, that the message appears and that the error is cleared on moving to another screen.
  - The name `SessionFailure` fell short for this case; renaming it is noted as a separate change.
- **`CLAUDE.md`:** the "0 tests" rule was generalized to EditMode and PlayMode, with the order of retries (recompile and filter by assembly; larger `init_timeout`; Cami's Test Runner) and the note about copying the exact `job_id`.

After the adjustments: compiles with no errors, PlayMode 106/106 (filtered by assembly) and EditMode 49/49 on the first attempt.

**Manual test (Cami):** she played against the AI on the three levels; it works well. Cami asked to commit phase 2.

**Verification:**
- Compiles with no errors; the console was left clean.
- PlayMode 104/104 via MCP, filtered by assembly (98 previous + 6 new).
- EditMode 49/49 via MCP, Slow included. **The first two runs failed to initialize** ("tests did not start within timeout", 0 tests), one without a filter and another filtered by assembly with a previous recompilation. The scene wasn't *dirty*. The third, without a filter and with a 120 s `init_timeout`, ran in full. The failed ones weren't counted.
- **Still to be tested by hand:**
  - the Simulator in the usual two profiles (menu with one more button and the level screen, inside the safe area);
  - a match against each level on the device, to feel the delay and tune the error percentages.

**Learnings:**
- The MCP runner's initialization failure isn't exclusive to PlayMode: it also happened in EditMode, in a run right after a PlayMode one. Retrying with a larger `init_timeout` was enough.
- The exact `job_id` from the `run_tests` result has to be copied: several queries with mistranscribed ids returned "Unknown job_id".

**Commit(s):** — (not committed; Cami reviews and commits).

---

## [0] 2026-09-22 — MVP definition and documentation setup

**Goal:** review GDD v0.1, close the pending decisions and prepare the repo for working with agents.

**Request / prompt:** analysis of the GDD (what is good, what is missing, long-term gameplay risks) and a proposal for the architecture and the AI setup.

**What the AI did:**
- Detected the document's problems:
  - infinite loop between good players;
  - possible first-player advantage;
  - the definition of "life" only worked with the queue full;
  - the indicator of the opponent's expiring piece was missing;
  - accessibility of the opacity-based indicators;
  - the legal-move invariant was missing.
- Proposed the architecture: Core in pure C#, `IPlayerController`, `Move` as a command and a parameterizable `GameConfig`.
- Wrote GDD v0.2, `CLAUDE.md` and ADRs 0001 and 0002.

**Human review (Cami's decisions):**
- Draw: repetition + move cap.
- Timeout: random move + absent mode with a reduced timer until the player plays again (inspired by Hearthstone), to give a chance to reconnect.
- MVP with full visual aids.
- The GDD moves to the repo as the source of truth.

**Verification:** N/A (documentation).

**Learnings:**
- Closing the ambiguous rules before writing code keeps the agent from deciding them on its own.
- The rule "if the code and the GDD differ, ask" went into `CLAUDE.md`.

**Commit(s):** —
