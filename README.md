# Tic-Tac-Fade

> **Language note:** this project was developed in Spanish. The documentation was translated to English on 2026-10-29; the Spanish originals are preserved in the git history. The game's UI text is still in Spanish.

Tactical tic-tac-toe in Unity 6: each player has at most **3 pieces** on the board. When the 4th is placed, the oldest one disappears. The board never fills up and there is no draw by blocking.

Personal project by [Camila Termine](https://github.com/camitermine). Besides being a game, it is a documented experiment in **iterative AI-assisted development** with Claude Code and the Unity MCP, under human supervision.

## Status

MVP in development. See the scope in [`docs/GDD.md`](docs/GDD.md) §7.

## Documentation

| Document | What for |
| :-- | :-- |
| [`docs/GDD.md`](docs/GDD.md) | **Source of truth** for rules, scope and parameters. |
| [`docs/adr/`](docs/adr/) | Architecture decisions, with their context and their consequences. |
| [`docs/ai-log.md`](docs/ai-log.md) | Iteration log: what the AI was asked, what it produced and what was corrected. |
| [`CLAUDE.md`](CLAUDE.md) | Standing instructions for the agent. |

## Architecture

```
Assets/_Project/Scripts/
  Core/   TicTacFade.Core    Pure C#, no Unity. All the rules.
  Game/   TicTacFade.Game    Match flow, player controllers.
  Net/    TicTacFade.Net     Online sessions and move sync.
  UI/     TicTacFade.UI      Views, input and HUD.
Assets/_Project/Tests/EditMode/   TicTacFade.Core.Tests
```

The rules live in an assembly with no engine references, so they are tested without opening a scene and reused the same way in local mode, online and the future AI. See [ADR 0001](docs/adr/0001-pure-csharp-core.md).

## Requirements

- Unity 6
- A Unity Gaming Services account for online (see [ADR 0002](docs/adr/0002-online-multiplayer-services.md))

## Tests

```powershell
& "<UnityEditorPath>\Unity.exe" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults ./TestResults/editmode.xml
```
