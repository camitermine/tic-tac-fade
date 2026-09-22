using System.Runtime.CompilerServices;

// Lets the EditMode tests (a separate assembly, TicTacFade.Core.Tests) inspect
// Core's `internal` members (e.g. GameState.PositionCounts) without
// exposing them as part of the game's public API.
[assembly: InternalsVisibleTo("TicTacFade.Core.Tests")]
