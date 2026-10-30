# ADR 0006 — Neon art: Canvas in Screen Space - Camera and HDR intensity through a UI shader

**Status:** Accepted
**Date:** 2026-10-30

## Context

The art direction (GDD §6) is neon on a dark background: white shapes tinted in code, with the glow coming from URP Bloom, never painted into the sprite. Two technical facts stood in the way:

- **Bloom doesn't reach a Screen Space - Overlay canvas.** Overlay canvases are drawn after the camera, outside its post-processing. The whole game UI lived in one Overlay canvas.
- **UI vertex colors can't go above 1.** A `Graphic`'s color is stored per vertex as `Color32`, so it is clamped to 1. With a Bloom threshold of 1, nothing in the UI would ever glow; with a lower threshold, everything bright would.

A spike on `feat/art-neon-spike` (ai-log [22]) tried the approach below on the board and pieces and validated it on the phone: the glow reads as in the references, and the game holds 60 FPS with Bloom on.

## Decision

### Canvas and camera
- **The single game Canvas renders in Screen Space - Camera**, through the Main Camera (`planeDistance` 10). The menu is going neon too, so the whole canvas moves, not just the board.
- **The camera has HDR and post-processing on**, and the background is `#0B1026`.
- **A global Volume** (`Art/Neon/NeonVolumeProfile.asset`) holds Bloom: threshold 1, intensity 1.5, scatter 0.7, no high-quality filtering (mobile). The profile and the material are created once by the scene builder and then kept, so tuning them in the Inspector survives a rebuild.
- **The `GraphicRaycaster` uses the canvas camera as its event camera.** A PlayMode test (`CanvasCameraTests`) raycasts from each button's on-screen position on every screen and checks that every visible screen still fills `Screen.safeArea`. The flow tests call `onClick` directly and would not catch a canvas that can't be tapped.

### HDR intensity
- **`TicTacFade/UI Neon`** (`Art/Neon/UI-Neon.shader`) is a copy of Unity's `UI/Default` that multiplies the output RGB by `_Intensity`. One shared material, `NeonUI.mat` (2.5), on the neon elements only.
- **The tint and the alpha stay in the vertex color**, set from code; the intensity comes from the material. Alpha still dims the glow (piece life, ghost), because blending multiplies the boosted RGB by alpha.
- **With a threshold of 1, only the neon material blooms.** White text, buttons and the HUD stay below it. If they ever bleed, the rule is to raise the threshold a little (e.g. 1.1), never to darken the UI.
- **One material for all neon elements** keeps them in a single batch. Per-element intensity (for example, a stronger winning line) is the documented next step, not built: write the intensity into a vertex channel (`uv1`) with a mesh modifier, still one material.

### Art sources
- **The SVG files are the sources and the sprites**, in `Art/Neon/Source/`, imported by Unity's builtin SVG importer as Textured Sprites (512 px, 8× antialiasing, viewport preserved). The settings live in the `.meta` files, so a clone reproduces them with no tool and no package. The grid line is 9-sliced, with `pixelsPerUnitMultiplier` keeping its round caps round on a thinner line.

## Alternatives ruled out

- **Lower Bloom threshold (e.g. 0.7), no shader.** No code at all, but every bright element blooms: white text, buttons, the HUD. It can't be limited to the board, and since nothing goes above 1, the halo stays weak.
- **One material per color with an `[HDR]` color property, white sprites.** The shader is trivial, but the color moves from code to materials, against GDD §6 (color applied at runtime), and each color adds a material and breaks batching.
- **A separate Canvas only for the board** in Screen Space - Camera, leaving the rest in Overlay. More isolated, but the menu is going neon too, so it would be a temporary split that the next step undoes; it also complicates sorting between the two canvases.

## Consequences

**For:**
- The glow is selective and tunable without touching code: material intensity and Bloom settings.
- Color stays a runtime tint, as GDD §6 asks; new neon art is a white SVG plus the shared material.
- Validated on the target device at 60 FPS with Bloom on.
- No new package: the SVG importer and URP were already in the project.

**Against / risks:**
- **The shader is a copy of `UI/Default`.** If Unity changes the built-in UI shader (stencil, masking, clipping, new keywords), the copy doesn't follow. Symptoms would be masks or `RectMask2D` clipping failing on neon elements. When upgrading Unity, compare the copy against the new `UI/Default` and port the changes; the only intended difference is the `_Intensity` multiply.
- **All neon elements share one intensity** until the `uv1` approach is built.
- **The UI now depends on the camera:** without the Main Camera (or with it outside the canvas plane's clip range) nothing is drawn and taps don't land. `CanvasCameraTests` guards both.
- **Bloom costs GPU time.** 60 FPS on Cami's test phone; not measured on weaker devices or WebGL.
