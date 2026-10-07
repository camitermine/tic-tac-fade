# ADR 0002 — Online con Unity Multiplayer Services (sesiones + código de sala)

**Estado:** Aceptada
**Fecha:** 2026-09-22

## Contexto

El MVP necesita partidas 1v1 online entre celulares, con un flujo de "creo sala, te paso un código, te unís". Dos celulares en redes móviles distintas no pueden conectarse en forma directa por NAT y firewalls, así que hace falta un intermediario.

Alternativas consideradas:

1. **Unity Multiplayer Services (Sessions) + Relay + Netcode for GameObjects.**
2. **Backend propio** (por ejemplo, WebSockets en un servidor propio).
3. **Servicios de terceros** tipo Photon.

## Decisión

Usar **Unity Multiplayer Services (paquete `com.unity.services.multiplayer`)** con sesiones y Relay.

- El host crea la sesión y el servicio genera un **join code** corto y fácil de dictar.
- El cliente entra con `JoinSessionByCodeAsync(joinCode)`.
- El SDK se encarga del lobby, la asignación de Relay y el establecimiento de la conexión de Netcode.
- Modelo host-cliente, con el host como autoridad de las reglas y del timer.

Por la red viajan **jugadas** (`Move`), no el estado del tablero. Cada cliente aplica las mismas reglas del Core y compara el hash de posición para detectar desincronización.

## Consecuencias

**A favor:**

- Es exactamente el flujo de "sala con código" que pide el GDD, sin backend propio.
- Relay resuelve NAT y firewalls: ambos clientes ven siempre la misma IP y puerto.
- Está integrado con Unity 6 y con Netcode for GameObjects.
- Soporta Web si se usa Unity Transport 2.0+ con WebSockets, lo que deja abierta la puerta al build de WebGL para portfolio.

**En contra / riesgos:**

- **Dependencia de Unity Gaming Services:** requiere cuenta, proyecto vinculado y autenticación anónima.
- **Costo por uso** más allá del tier gratuito. Hay que monitorearlo, aunque para un proyecto de portfolio debería alcanzar de sobra.
- **Si cae el host, cae la partida.** Aceptado en el MVP: el cliente vuelve al menú con un mensaje.
- La **reconexión** es *best effort* en el MVP. El modo ausente del GDD §3.5 da margen, pero no garantiza volver a la partida.

## Mitigación de acoplamiento

La capa `Net` se esconde detrás de una interfaz propia (`IMatchTransport` o similar) en `Game`. Si más adelante hay que cambiar de proveedor, el Core y la UI no se tocan.

## Implementación (actualizado 2026-09-23, online iteración 1: salas sin sincronización)

Se mantiene la decisión. Cómo quedó implementada y en qué se precisa lo de arriba:

- **Interfaz:** la del ciclo de vida de la sala se llama `ISessionService` (en `Game`): crear, unirse con código, salir, y eventos de rival conectado/desconectado y sala perdida. Las fallas vuelven como un enum propio (`SessionFailure`), nunca como excepciones del SDK. El envío de jugadas (lo que este ADR llamaba `IMatchTransport`) es de la iteración siguiente y puede ser otra interfaz.
- **Wiring sin singletons:** Unity no serializa campos de interfaz, así que la escena referencia una base abstracta `SessionServiceBehaviour : MonoBehaviour, ISessionService`. `TicTacFade.Net` aporta `UgsSessionService` y los tests un fake sin red.
- **Paquetes:** `com.unity.services.multiplayer` 2.3.3, `com.unity.netcode.gameobjects` 2.13.3 y `com.unity.multiplayer.playmode` 3.0.0 (builtin en Unity 6.6).
- **Sesiones:** privadas, `MaxPlayers = 2`, con `WithRelayNetwork()`. El SDK arranca el host/cliente de Netcode sobre el `NetworkManager` + `UnityTransport` de la escena, y falla si no existe. Al salir se usa `LeaveAsync` (el SDK pide no llamar a `NetworkManager.Shutdown()` a mano). El host usa `DeleteAsync`, para que el otro jugador vea la sala cerrada en vez de quedar como host migrado de una sala vacía. Antes de cada crear/unirse se espera a que el `NetworkManager` quede libre.
- **Inicialización perezosa:** Unity Services y la autenticación anónima se inicializan en el primer "Crear sala" o "Unirse", no al arrancar. El modo local nunca toca UGS (hay un test PlayMode que lo verifica).
- **Multiplayer Play Mode:** cada jugador virtual usa un perfil de autenticación propio, derivado de la ruta de su clon (`PlayModeAuthProfile`). El paquete de Authentication ya lo hace leyendo argumentos de línea de comandos, pero como comportamiento interno no documentado; acá queda explícito.
- **Limitación del SDK (verificada contra el servicio real):** `LobbyConverter.ToSessionException` descarta la excepción original y solo conserva "lobby not found" (→ `SessionNotFound`). Sala llena, código con caracteres inválidos, etc. llegan como `SessionError.Unknown` con solo un mensaje. Cómo se maneja:
  - **Formato del código, validado localmente** (`JoinCodeFormat`, en Net y detrás de `ISessionService.IsWellFormedCode`, porque el formato es del proveedor): alfabeto `6789BCDFGHJKLMNPQRTW`, 6 a 12 caracteres. El SDK lo documenta para los join codes de Relay; para los de Lobby no hay documentación, pero el servicio real es consistente con esa regla (ver ai-log [13]). Un código fuera de formato da "Código inválido" sin salir a la red.
  - Si el servicio igual devuelve un error de formato, se reconoce por el mensaje (única señal que deja el SDK) y también da "Código inválido".
  - Cualquier otro rechazo al unirse (p. ej. sala llena) da un mensaje genérico: "No se pudo unir a la sala. Revisá el código o pedí uno nuevo.".

## Implementación (actualizado 2026-09-23, online iteración 2: partida jugable)

### Transporte de jugadas: mensajes con nombre de Netcode, no RPCs

- **Decisión:** `NgoMatchTransport` (en `TicTacFade.Net`) usa `CustomMessagingManager` con un solo mensaje con nombre (`TicTacFade.Match`), identificado por un byte de tipo. Se descartaron los RPCs porque necesitan un `NetworkObject`: spawnearlo con un prefab registrado, o ponerlo en la escena. La escena la genera el builder por código, y un `NetworkObject` en escena necesita un `GlobalObjectIdHash` que el editor calcula al validarlo; generarlo por script es frágil y un error ahí rompe la conexión en silencio. Los mensajes con nombre viajan directo sobre el `NetworkManager`, sin objetos ni prefabs.
- **Entrega:** todos los mensajes van con `NetworkDelivery.ReliableSequenced`, explícito en el código (constante `Delivery`) aunque sea el default del método. Una jugada confirmada perdida o desordenada no puede quedar librada a que la detecte la comparación de hash.
- **Interfaz:** `IMatchTransport` (en Game), con una base serializable `MatchTransportBehaviour` para la escena, igual que `ISessionService`. Los tests usan un transporte en memoria con cola y bombeo explícito.
- **Mensajes:** `StartMatch`, `Propose`, `Confirmed` (jugada, número de jugada, `PositionKey.Value` después de aplicarla), `Rejected`, `Ack` (número de jugada y clave del cliente), `RematchRequest`, `Desync`. Viajan jugadas, nunca el tablero.
- **Buffer de entrada:** lo que llega antes de que el flujo del dispositivo abra el canal (`Open`) se guarda y se entrega al abrir. Cubre el caso de un `StartMatch` que el host manda apenas conecta el cliente, antes de que el join haya terminado del lado del cliente. (Desde 2026-10-06 el arranque ya no depende de este buffer: ver el handshake `Ready` más abajo.)

### Autoridad y sincronización

- **El host es la autoridad** (`OnlineMatch`, en Game). Toda propuesta, sea un toque en el host o un mensaje del cliente, pasa por la misma validación: `RulesEngine.IsLegal` más "cada dispositivo solo mueve su símbolo". Una propuesta ilegal se rechaza y ningún estado cambia.
- **En ambos dispositivos `GameManager` solo recibe jugadas confirmadas**, a través del controller de cada lado: `OnlineLocalPlayer` (el humano de este dispositivo, que propone y espera) o `RemotePlayer`. `GameManager` no sabe que hay red.
- **Comparación de hash:** el host manda su `PositionKey.Value` con cada confirmación. El cliente aplica, compara y devuelve un `Ack` con su clave, y el host también compara. Ante cualquier diferencia: `Debug.LogError`, `Desync` al otro lado, se cierra la sesión y ambos vuelven al menú. Nunca se sigue jugando desincronizados.
- ~~**Arranque:** lo dispara la conexión de Netcode (`OnClientConnectedCallback` en el host), no el join de lobby, porque antes de eso no se pueden mandar mensajes.~~ Reemplazado por el handshake `Ready` (ver "Handshake de arranque y versión de protocolo").
- **Símbolos:** el host es siempre X y el cliente siempre O (GDD §3.1). La revancha requiere el pedido de los dos; el host la anuncia con `StartMatch` invirtiendo quién empieza.
- ~~Pendiente (iteración 3): si una propuesta del cliente no recibe `Confirmed` ni `Rejected` (host caído), `OnlineLocalPlayer` queda esperando con el input bloqueado.~~ Resuelto en la iteración 3 (ver abajo).

## Implementación (actualizado 2026-09-23, online iteración 3: timer, abandono y desconexiones)

- **Timer autoritativo del host:** al empezar cada turno el host fija el vencimiento y lo anuncia con `TurnTimer` (jugador, duración en ms, quién está ausente). El cliente solo muestra la cuenta regresiva desde que recibe el aviso, sin compensar latencia; nunca decide un vencimiento. Al vencer, el host elige la jugada automática y la difunde como un `Confirmed` más, así que el cliente no necesita lógica aparte para aplicarla.
- **Tiempo y azar inyectados** (`IClock`, `IRandomSource`, en Game): `OnlineMatch` no lee el reloj de Unity ni `System.Random` directamente. `MatchFlow` lo avanza con `Tick()` una vez por frame. Los tests usan un reloj manual.
- **Parámetros:** los del timer (`OnlineTimerConfig`: 30 s, 10 s, 3 vencimientos) son de juego, viven en Game y se editan en `GameConfigAsset`. El Core no los tiene, porque el `RulesEngine` nunca los lee (CLAUDE.md regla 6, reescrita en esta iteración). Las esperas de red (`OnlineNetworkSettings`: 10 s sin respuesta a una propuesta, 2 s para el ack de "Salir", 5 s de gracia tras una desconexión) son `SerializeField` de `MatchFlow`.
- **Abandono como resultado de partida, no regla del tablero:** `MatchResult` (Game) envuelve el `GameEndedEvent` del Core o un abandono con su causa (`Timeouts`, `Quit`, `Disconnected`). `GameManager.Halt()` congela la partida sin tocar el `GameState`. El Core no cambió.
- **Mensajes nuevos** (mismo mensaje con nombre, `ReliableSequenced`): `TurnTimer`, `Abandoned` (perdedor y causa, host → cliente), `Forfeit` y `ForfeitAck` (en ambos sentidos). `MatchMessage` suma `DurationMs`, `AbsentFlags` y `Cause`.
- **"Salir" con confirmación:** quien sale manda `Forfeit` y espera `ForfeitAck` (hasta 2 s) antes de cerrar la sesión. Si la cerrara enseguida, el apagado de la red podría descartar el aviso. Sin ack, cierra igual y el otro lado cae en el caso de desconexión.
- **Detección de desconexión:** `IMatchTransport.PeerDisconnected`, que en `NgoMatchTransport` sale de `OnClientDisconnectCallback`. También cuentan los eventos de sesión (`PlayerHasLeft` en el host, `Deleted`/`RemovedFromSession` en el cliente). Los dos caminos terminan en `OnlineMatch.NotifyPeerGone()`, que es idempotente.
  - Cliente caído en partida: el host espera una gracia de 5 s y después el cliente pierde por abandono. Los 3 vencimientos quedan para el rival conectado que no juega.
  - Host caído en partida, o propuesta sin respuesta en 10 s: el cliente vuelve al menú con "Se perdió la conexión con el rival".

## Implementación (actualizado 2026-10-06: handshake de arranque y versión de protocolo)

**Contexto:** en dos pruebas con celulares, el cliente se quedó en la sala de espera mientras al host le arrancaba la partida (ai-log [15]). Después no se reprodujo y la causa no se confirmó. En vez de seguir buscándola, se cambió el arranque para que esa clase de fallo no pueda dejar a un lado esperando para siempre (ai-log [16]).

- **Handshake `Ready`:** el host ya no arranca con la conexión de Netcode.
  - El cliente manda `Ready` cuando su flujo está listo (`OnlineMatch.Start`) y lo reenvía cada 1 s (`OnlineNetworkSettings.ReadyResendIntervalSeconds`, `SerializeField` de `MatchFlow`) hasta aceptar el primer `StartMatch`.
  - El host arranca la primera partida con el primer `Ready`. A cada `Ready` posterior le contesta reenviando el `StartMatch` actual, y nunca arranca otra partida por eso. Así, un `StartMatch` perdido se recupera con el siguiente `Ready`.
  - Se quitaron `IMatchTransport.PeerConnected` e `IsPeerConnected`, que quedaron sin uso.
- **Número de partida:** `StartMatch` lleva `MatchNumber`, un campo propio que vale 1 en la primera partida y suma 1 en cada revancha. El cliente acepta solo el número que espera y descarta cualquier `StartMatch` repetido o anterior. Una sola regla cubre el `Ready` duplicado, el reenvío y un `StartMatch` viejo que llega tarde después de una revancha, sin depender de cuándo se limpia un flag.
- **Timer en el reenvío:** si la partida está en curso, el reenvío incluye un `TurnTimer` con el **tiempo restante según el reloj del host**, no la duración completa. Si no, el cliente vería más tiempo del que tiene y el host le cortaría el turno antes de que su cuenta llegue a cero.
- **`Ready` sin conexión:** el cliente puede mandar `Ready` antes de que Netcode haya conectado, sobre todo en un arranque en frío. `CustomMessagingManager.SendNamedMessage` no chequea la conexión (NGO 2.13.3), así que `NgoMatchTransport.Send` no lo llama mientras `!IsConnectedClient`. Descarta el `Ready` en silencio, con un log de diagnóstico apagado por defecto, y el reenvío se ocupa del resto. Los demás mensajes mantienen el warning de siempre.
- **Formato de trama** (`MatchMessageCodec`, en Game, compartido por `NgoMatchTransport` y el transporte en memoria de los tests):
  - empieza con una cabecera fija que no cambia nunca: `kind`(1) + versión de protocolo(2), seguida de los campos;
  - mide 31 bytes en total;
  - los valores de `MatchMessageKind` son parte del protocolo y no se renumeran.
- **Versión de protocolo:** `MatchMessageCodec.ProtocolVersion = 1` (es la primera versión con número).
  - Toda trama la lleva en la cabecera. En la práctica, la primera que se chequea es el `Ready` del cliente.
  - Si no coincide, se decodifica solo la cabecera, sin importar el tamaño, y los dos lados vuelven al menú con "El rival tiene otra versión del juego".
  - El host manda `VersionMismatch` y espera la respuesta del cliente (o su caída, o a lo sumo 2 s) antes de cerrar, para que el cliente vea ese mensaje y no "La sala se cerró". El cliente contesta y se va enseguida.
- **Lectura tolerante:** el decodificador nunca lanza. Descarta con un warning ("dropped malformed match message") una trama más corta que la cabecera, un `kind` desconocido o una trama de la misma versión con tamaño inesperado. Reemplaza la lectura campo por campo que tiraba `OverflowException` con mensajes de otro tamaño.
- **Compatibilidad:** los builds anteriores (sin versión ni `Ready`) **no** son compatibles. Un host nuevo con un cliente viejo se queda esperando en la sala, porque nunca le llega un `Ready`. Desde la versión 1, cualquier cambio de formato o de significado sube `ProtocolVersion`.

## Referencias

- https://docs.unity.com/ugs/en-us/manual/mps-sdk/manual/join-session
- https://docs.unity.com/mps-sdk/working-with-mps
- https://docs.unity.com/relay/relay-and-ngo.html
