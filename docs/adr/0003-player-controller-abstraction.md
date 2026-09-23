# ADR 0003 — Abstracción de jugadores vía IPlayerController

**Estado:** Aceptada
**Fecha:** 2026-09-23

## Contexto

Hasta ahora `GameManager.OnCellClicked(int)` asumía dos cosas: que toda jugada viene de un toque de la UI, y que el jugador que la hace es siempre `CurrentState.CurrentPlayer` — construía el `Move` directamente ahí mismo. Eso funciona para el modo local (dos humanos en el mismo dispositivo), pero bloquea todo lo que necesita una fuente de jugadas distinta: el online (`docs/adr/0002-online-multiplayer-services.md`, donde las jugadas del rival llegan por red) y la IA del roadmap (GDD §8.2).

CLAUDE.md ya declaraba esta regla como no negociable (arquitectura, regla 5: "Players are interchangeable through `IPlayerController`... The match code must never branch on the player type"), pero no estaba implementada.

## Decisión

- `IPlayerController` (capa Game) es la única fuente de jugadas que `GameManager` conoce: expone `Player`, un evento `MoveChosen` y dos hooks, `NotifyTurnStarted(GameState)` (para que un controller autónomo pueda actuar sin intervención externa) y `NotifyCellSelected(int)` (para que la UI le pueda seguir hablando a `GameManager.OnCellClicked` exactamente igual que antes, sin saber qué tipo de controller hay del otro lado).
- `LocalHumanPlayer` es la primera implementación: no hace nada en `NotifyTurnStarted` (espera el toque), y traduce `NotifyCellSelected` en un `Move` disparando `MoveChosen`.
- `GameManager` recibe los dos controllers vía `Initialize(IPlayerController, IPlayerController)`, público y re-llamable (desuscribe los anteriores antes de suscribir los nuevos). Si nadie lo llamó antes de `Awake()`, `GameManager` crea el par `LocalHumanPlayer` por default — así el modo local sigue funcionando sin wiring extra, y un test (o a futuro un flujo de setup online/IA) puede inyectar otra cosa.
- Los controllers **no se wirean desde el scene builder**: un campo de tipo interfaz con un `event` no sobrevive la serialización de Unity (no es un `UnityEngine.Object`), así que intentar asignarlos por reflection al construir la escena se perdería en cuanto se guarda/recarga. Por eso viven enteramente en código C# (`Awake()`/`Initialize()`), no en el `.unity`.
- El avance de turnos (`GameManager.AdvanceTurns`) es un bucle con flag de re-entrada, no una llamada recursiva. La primera versión notificaba el turno siguiente llamando directo desde dentro del manejo de la jugada anterior — con dos controllers autónomos eso apila un frame de stack por jugada de toda la partida, y una jugada rechazada que no corta el ciclo produce recursión infinita (StackOverflow, cuelga el Editor sin excepción capturable). El bucle re-entrante hace que una partida completa entre dos controllers autónomos corra en un solo stack frame, y una jugada rechazada (`MoveRejectedEvent`) corta el bucle en vez de reintentar.

## Consecuencias

**A favor:**

- `GameManager` nunca hace `if (controller is LocalHumanPlayer)`: agregar `RemotePlayer`/`AIPlayer` no toca su código, solo implementa la interfaz.
- El modo local sigue andando sin cambios de configuración (default en `Awake()`).
- Se puede testear una partida completa sin UI ni escena real más que el `GameManager`: dos controllers fake alcanzan (ver `FakeAutoPlayer` en `Assets/_Project/Tests/PlayMode/`).
- La UI (`BoardView`, `GameHud`, `RestartButton`) no cambió una sola línea: siguen llamando `OnCellClicked`/`StartNewGame`/leyendo `CurrentState` exactamente igual.

**En contra:**

- El punto de inyección (`Initialize`) es manual: no hay todavía un mecanismo de UI/Editor para elegir "Local vs Local" / "Local vs IA" antes de tiempo de ejecución. Se resuelve cuando exista ese flujo (online o IA).
- `GameConfigAsset` (el wrapper ScriptableObject de `GameConfig`, ya decidido en ADR 0001) no valida rangos de sus campos en el Inspector — deuda anotada, no bloqueante para el MVP.
