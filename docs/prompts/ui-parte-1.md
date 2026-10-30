UI iteration, part 1: leave the scene and the HUD healthy.

Bug 1: the HUD shows overlapping texts (the turn reads with X and O on top of each other, and the counters overlap with the fade warning). My hypothesis: TicTacFadeSceneBuilder is not idempotent and re-running it duplicated the HUD objects, leaving old, non-updated labels on top of the live ones. Verify it by inspecting the hierarchy through MCP before touching anything.

Work to do:

Make the builder idempotent: it must clean up or reuse the existing objects instead of duplicating them. Running it twice in a row has to give exactly the same hierarchy.
Add a Main Camera to the scene (fixes the "No cameras rendering" and the black background).
Redo the HUD layout so it is impossible for things to overlap: anchors and layout groups, not fixed positions. Portrait orientation, Canvas Scaler in Scale With Screen Size with 1080x1920 and match 0.5, and the HUD inside a container that respects the safe area.

Verify with the Simulator in at least two aspect ratios (16:9 and 20:9) that nothing overlaps or goes off screen. Out of scope: piece life indicators, ghost piece and winning line, which go in part 2.