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

## Referencias

- https://docs.unity.com/ugs/en-us/manual/mps-sdk/manual/join-session
- https://docs.unity.com/mps-sdk/working-with-mps
- https://docs.unity.com/relay/relay-and-ngo.html
