Refactor of the Game layer: abstract who plays each turn.

Create IPlayerController in the Game layer, with a LocalHumanPlayer implementation that is the one using the screen input. GameManager stops assuming that both moves come from the UI: it receives two controllers and never branches on the player type.
Turn GameConfig into a ScriptableObject editable from the Editor, wrapping Core's GameConfig (which stays pure C# and is not touched). GameManager takes it from there instead of calling a hardcoded GameConfig.Mvp().
Make the scene builder create and wire the new configuration, staying idempotent.

The Core is not touched. The UI only adapts to the new signature, with no visual changes.

Verification: the EditMode tests keep passing, the PlayMode tests keep passing, and a complete local match plays the same as before. Add a test proving that a match runs with a controller that is not the local human (a fake one that plays the first free cell) — it is the proof that the abstraction serves what it was made for.

Documentation: ai-log entry and, if appropriate, a new ADR about the controller abstraction.