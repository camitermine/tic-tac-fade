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

## Referencias

- https://docs.unity.com/ugs/en-us/manual/mps-sdk/manual/join-session
- https://docs.unity.com/mps-sdk/working-with-mps
- https://docs.unity.com/relay/relay-and-ngo.html
