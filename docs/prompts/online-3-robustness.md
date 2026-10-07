Online, iteración 3: timer de turno, modo ausente, desconexiones y abandono (GDD §3.5 y §5).

Decisión de Cami: se confirma que 3 vencimientos consecutivos cuentan como derrota por abandono. Actualizá el GDD sacando el "a confirmar".

Timer de turno, solo en online:
El host es el dueño del timer. Al empezar cada turno le avisa al cliente la duración; el cliente muestra la cuenta regresiva localmente, pero la única verdad es la del host.
Duraciones desde GameConfig: 30 s normal, 10 s en modo ausente.
Al vencer, el host elige una jugada legal al azar para ese jugador y la difunde como cualquier jugada confirmada. La aleatoriedad vive en la capa Game con una fuente inyectable, nunca en el Core.
Modo ausente: después de un vencimiento, ese jugador pasa a 10 s por turno hasta que haga una jugada propia.
3 vencimientos consecutivos: derrota por abandono.
Abandono: "Salir" en medio de una partida online deja de ser solo cerrar la sesión y pasa a contar como derrota de quien se va. El que queda ve "Ganaste: el rival abandonó". El abandono es un resultado de partida, no una regla del tablero: si modelarlo requiere tocar el Core, pará y proponé antes.
Desconexiones:
Si se cae el cliente, la partida sigue en el host y los turnos del cliente vencen normalmente hasta el abandono. Sin reconexión en el MVP.
Si se cae el host, el cliente vuelve al menú con "Se perdió la conexión con el rival".
Pendiente de la iteración 2: si una propuesta del cliente no recibe respuesta en un tiempo razonable, se desbloquea el input y se trata como caída del host.
HUD: tiempo restante visible en online, con un estado distinto en modo ausente, e indicador de "ausente" sobre el jugador que corresponda.

Tests: inyectá la fuente de tiempo, para que ningún test espere 30 segundos reales. Con el transporte en memoria cubrí: vencimiento con jugada aleatoria legal, entrada y salida del modo ausente, abandono al tercer vencimiento, abandono por "Salir", caída del host, caída del cliente, y propuesta sin respuesta.

Verificación manual en dos dispositivos, uno en wifi y otro con datos móviles: dejar vencer un turno, volver a jugar y salir del modo ausente, abandonar con "Salir", y cerrar la app del cliente a mitad de partida para ver cómo el host termina ganando por abandono.

Documentación: ai-log, GDD §3.5 y §5, y ADR 0002 si cambia el modelo de red.