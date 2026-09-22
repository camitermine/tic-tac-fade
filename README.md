# Tic-Tac-Fade

Tateti táctico en Unity 6: cada jugador tiene como máximo **3 fichas** en el tablero. Al colocar la 4ª, desaparece la más antigua. El tablero nunca se llena y no existe el empate por bloqueo.

Proyecto personal de [Camila Termine](https://github.com/camitermine). Además de ser un juego, es un experimento documentado de **desarrollo iterativo asistido por IA** con Claude Code y el MCP de Unity, bajo supervisión humana.

## Estado

MVP en desarrollo. Ver el alcance en [`docs/GDD.md`](docs/GDD.md) §7.

## Documentación

| Documento | Para qué |
| :-- | :-- |
| [`docs/GDD.md`](docs/GDD.md) | **Fuente de verdad** de reglas, alcance y parámetros. |
| [`docs/adr/`](docs/adr/) | Decisiones de arquitectura, con su contexto y sus consecuencias. |
| [`docs/ai-log.md`](docs/ai-log.md) | Bitácora de iteraciones: qué se le pidió a la IA, qué produjo y qué se corrigió. |
| [`CLAUDE.md`](CLAUDE.md) | Instrucciones permanentes para el agente. |

## Arquitectura

```
Assets/_Project/Scripts/
  Core/   TicTacFade.Core    C# puro, sin Unity. Todas las reglas.
  Game/   TicTacFade.Game    Flujo de partida, controladores de jugador.
  Net/    TicTacFade.Net     Sesiones online y sincronización de jugadas.
  UI/     TicTacFade.UI      Vistas, input y HUD.
Assets/_Project/Tests/EditMode/   TicTacFade.Core.Tests
```

Las reglas viven en un assembly sin referencias al engine, así que se testean sin abrir una escena y se reutilizan igual en el modo local, el online y la IA futura. Ver [ADR 0001](docs/adr/0001-core-puro-csharp.md).

## Requisitos

- Unity 6
- Una cuenta de Unity Gaming Services para el online (ver [ADR 0002](docs/adr/0002-online-multiplayer-services.md))

## Tests

```powershell
& "<UnityEditorPath>\Unity.exe" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults ./TestResults/editmode.xml
```
