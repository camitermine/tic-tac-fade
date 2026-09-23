Online, iteración 2: partida jugable entre dos dispositivos.

Objetivo: cuando el rival se conecta a la sala, la partida arranca sola en ambos dispositivos y se juega completa por red hasta el resultado, con revancha. Timer de turno, desconexiones en medio de partida y abandono quedan para la iteración 3.

Modelo de autoridad (no negociable, ver ADR 0002):

El host es la autoridad. El cliente nunca aplica su propia jugada: la envía al host, el host la valida con RulesEngine.IsLegal y, si es legal, la difunde a ambos.
En los dos dispositivos, el GameManager solo recibe jugadas confirmadas por el host. Así los dos aplican exactamente la misma secuencia.
El rival es un IPlayerController más (RemotePlayer), que dispara MoveChosen cuando llega una jugada confirmada. El GameManager no sabe que hay red.
Después de cada jugada, ambos lados comparan el hash de posición del Core. Si no coinciden: log de error claro, se corta la partida y ambos vuelven al menú con un mensaje. Nunca seguir jugando desincronizados.

Reglas de juego online (GDD §3.1): el host es siempre X y el cliente siempre O. La primera partida arranca X; cada revancha alterna quién empieza, sin cambiar los símbolos.

Transporte de jugadas sobre Netcode for GameObjects, dentro de TicTacFade.Net. Evaluá entre RPCs sobre un NetworkObject y mensajes con nombre (CustomMessagingManager), que no requieren spawnear objetos ni registrar prefabs; elegí uno y justificalo en el plan. Por la red viajan jugadas (Move), nunca el tablero.
El tablero solo acepta input cuando es tu turno. El HUD pasa a decir "Tu turno" / "Turno del rival" en online (en local sigue como está). Mientras el host no confirma una jugada, la celda no puede tocarse dos veces.
Resultado online: la revancha requiere que ambos la pidan; quien la pidió ve "Esperando al rival...". Si uno elige "Menú", la sesión se cierra y el otro vuelve al menú con el mensaje "El rival salió".
El botón "Salir" en medio de una partida online, por ahora, hace lo mismo: cierra la sesión y ambos vuelven al menú. Que cuente como derrota es de la iteración 3.

Tests:

Un test sin red real: dos GameManager conectados por un transporte falso en memoria, uno como host y otro como cliente, jugando una partida completa. Verificar que al final ambos estados son idénticos, y que una jugada ilegal del cliente es rechazada por el host sin que ninguno de los dos cambie de estado.
Los tests existentes siguen en verde, incluido el modo local sin cambios.

Verificación manual: con dos dispositivos reales, partida completa hasta ganar, revancha (arranca O), empate por repetición si se puede forzar, y "Menú" desde el resultado en uno de los dos.

Documentación: ai-log, ADR 0002 con la decisión de transporte, y GDD §5 si algo del modelo de red cambia.