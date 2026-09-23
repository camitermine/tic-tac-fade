Refactor de la capa Game: abstraer quién juega cada turno.

Crear IPlayerController en la capa Game, con una implementación LocalHumanPlayer que es la que usa el input de pantalla. GameManager deja de asumir que ambas jugadas vienen de la UI: recibe dos controllers y no ramifica nunca por tipo de jugador.
Convertir GameConfig en un ScriptableObject editable desde el Editor, envolviendo el GameConfig de Core (que sigue siendo C# puro y no se toca). GameManager lo toma de ahí en vez de llamar a GameConfig.Mvp() hardcodeado.
Que el scene builder cree y wiree la configuración nueva, manteniéndose idempotente.

El Core no se toca. La UI solo se adapta a la firma nueva, sin cambios visuales.

Verificación: los tests EditMode siguen pasando, los PlayMode siguen pasando, y una partida local completa se juega igual que antes. Agregá un test que pruebe que una partida corre con un controller que no es el humano local (uno fake que juegue la primera celda libre) — es la prueba de que la abstracción sirve para lo que se hizo.

Documentación: entrada en ai-log y, si corresponde, un ADR nuevo sobre la abstracción de controllers.