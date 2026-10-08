# ESP32 — Firmware LoRa mesh

Firmware Arduino para ESP32 + módulo LoRa SX1278 433 MHz. Hace de pasarela
entre Bluetooth SPP y una red LoRa en mesh/flooding. Pensado para correr
en Arduino IDE 2.x con la librería **Sandeep Mistry LoRa 0.8.0** y Core
ESP32 3.x.

## Qué cosas hace

- Empareja como `BT-POC` por Bluetooth SPP (sin PIN).
- Cada línea de texto recibida por BT se envía como broadcast por LoRa con
  un `msgId` local y un `seq` propio.
- Cada paquete LoRa aceptado de otro nodo se reenvía por broadcast, salvo
  que ya se haya visto (anti-duplicado por `(src, msgId)`) o sea del propio
  nodo (anti-eco).
- Si no hay cliente BT conectado, los mensajes aceptados se acumulan en un
  buffer circular de 32 entradas en RAM y se entregan al reconectarse
  (store-and-forward). Política FIFO: en overflow se descarta el más antiguo.
- Comandos serie por BT:
  - `/help` — ayuda.
  - `/id` — devuelve `OK id=XX`.
  - `/stat` — `OK id=XX seq=N mesh=ON buf=N/32 bt=ON tx=A/Bms`.
  - `/tx [N]` — cambia repeticiones por mensaje (1..10).
  - `/gap [ms]` — cambia el gap entre repeticiones (0..60000).

## Pines

Definidos en `firmware/config.h`:

| Señal   | GPIO |
|---------|------|
| LORA_SS | 23   |
| LORA_RST| 14   |
| LORA_DIO0| 2   |
| SCK/MISO/MOSI/CS | 18 / 5 / 19 / 23 |

SPI se inicializa con `SPI.begin(18, 5, 19, 23)` por los pines soldados.
Frecuencia 433 MHz, TX power 17 dBm, SF7 / BW125 kHz por defecto.

## Hardware

Las pruebas se han hecho sobre un **ESP32-WROOM-32** y un módulo LoRa
**SX1278 433 MHz** conectado por SPI.

## Cómo flashear

1. Abrir Arduino IDE 2.x con el Core ESP32 3.x instalado.
2. Instalar la librería **LoRa** de Sandeep Mistry versión **0.8.0**.
3. Abrir `firmware/program.ino` (el resto de `.h/.cpp` están en la misma
   carpeta; Arduino IDE los recoge automáticamente como sketch tabs).
4. Editar `firmware/config.h` y poner el `NODE_HEX` de este nodo (0x01..0xFE).
   Cada nodo de la red tiene que tener un id distinto.
5. Compilar y subir.

Tras el boot debería verse por Serial (115200):

```
[BOOT] NODE_HEX=XX selfAddr=01
[BOOT] BT=OK name=BT-POC
[BOOT] LoRa=OK sf=7 bw=125000 txp=17
```

## Estructura del código

```
firmware/
├── program.ino       # setup() + loop() (orquesta)
├── config.h          # constantes, structs, extern del estado, NODE_HEX
├── framing.h/.cpp    # CRC-16/CCITT, buildPacket, parsePacket, addrToHex
├── radiolora.h/.cpp  # capa LoRa: TX, RX, anti-duplicado, store-and-forward
├── bt.h/.cpp         # SPP, polling de líneas, comandos, store-and-forward
└── storage.h/.cpp    # persistencia NVS (Preferences): tx, gap, msgIdCounter
```

El `.ino` solo tiene `setup()` y `loop()`. Toda la lógica vive en los
`.cpp/.h` por capa. El estado global se declara con `extern` en `config.h`
y se define en el `.cpp` correspondiente.

## Persistencia (NVS)

Tres valores sobreviven a reinicios vía `Preferences`:
- `txRepeatCount` (uint8_t) — repeticiones por mensaje.
- `txRepeatGapMs` (uint32_t) — gap entre repeticiones.
- `msgIdCounter` (uint16_t) — contador local de IDs.

`NODE_HEX` **no** persiste: es `#define` por build. Cámbialo en `config.h`
antes de flashear cada nodo.

## Trama binaria

Cabecera de 5 bytes, payload hasta 200 bytes, CRC-16/CCITT de 2 bytes al
final. Enteros little-endian salvo el `msgId` (big-endian). Layout:

```
offset  campo     bytes  notas
0       magic       1   0xA5
1       version     1   0x01
2       src         1   0x01..0xFE
3       seq         1   contador de TX por nodo, wrap 256
4       len         1   0..200
5..     payload     len  en DATA empieza con msgId (2 bytes BE) + texto UTF-8
5+len   crc         2   CRC-16/CCITT sobre [0..5+len-1]
```

CRC-16/CCITT: polinomio `0x1021`, init `0xFFFF`, sin reflect, sin xor out.
El payload de DATA siempre empieza por `[msgId_hi][msgId_lo][texto]`. Texto
máximo del usuario: 198 bytes.

## Mesh: anti-eco y anti-duplicado

| Concepto | Cuándo aplica | Acción |
|----------|---------------|--------|
| Anti-eco | `src == selfAddr()` | descartar |
| Anti-duplicado | `(src, msgId)` ya en `seenCache[32]` | descartar |

Caché circular de 32 entradas, ~96 bytes. Si entra un mensaje nuevo cuando
está llena, se olvida el más antiguo.

El nodo relay reenvía con `src = src_original` y `seq = txSeq++` local. El
id único del mensaje en toda la red es siempre `(src_original, msgId_original)`.

## Changelog

### v0.8.1 — 2026-10-06
Hotfix de v0.8.0. Faltaba `#include storage.h` en `bt.cpp`, por lo que
`cmdTx` y `cmdGap` no veían `storageSaveTx`. Añadido.

### v0.8.0 — 2026-10-06
Persistencia de configuración con NVS (`Preferences`). Tres valores
sobreviven a reinicios: `txRepeatCount`, `txRepeatGapMs`, `msgIdCounter`.
Nuevos `storage.h/.cpp`. `storageLoad()` en `setup()`, `storageSaveTx()`
desde `/tx` y `/gap`, `storageSaveMsgId()` tras cada `sendLoraBroadcast`.
Sin cambios de comportamiento en el primer boot. `NODE_HEX` sigue por
build.

### v0.7.4 — 2026-10-06
Log por envío individual en `sendLoraWithRepeat`. Antes solo se veía el
resumen al final de la ráfaga; ahora `[TX r=N/total seq=M ok=K]` por cada
envío.

### v0.7.3 — 2026-10-06
Hotfix de v0.7.2. Forward declaration de `sendLoraWithRepeat` tenía
retorno `void` pero la definición devolvía `bool`. C++ no permite cambiar
el tipo en una redeclaración. Corregida.

### v0.7.2 — 2026-10-06
Repetición configurable de TX LoRa. Por defecto cada mensaje se envía 4
veces con 1 segundo de gap. Constantes: `TX_REPEAT_COUNT_DEFAULT=4`,
`TX_REPEAT_GAP_MS_DEFAULT=1000`, `TX_REPEAT_COUNT_MAX=10`,
`TX_REPEAT_GAP_MS_MAX=60000`. Comandos `/tx [N]` y `/gap [ms]`. `sendLoraWithRepeat`
delega en `sendLoraBroadcast` y `relayPacket`.

### v0.7.1 — 2026-10-06
Bugfix crítico. `HEADER_SIZE` estaba definido como 4 pero la cabecera real
tiene 5 bytes. `writePacketPayload` empezaba a escribir en `out[4]`,
sobrescribiendo el `payloadLen`. El receptor leía `0x00` como longitud y
fallaba `parsePacket`. Corregido a 5.

### v0.7.0 — 2026-10-06
Bugfix crítico del parser. En v0.5.2 se simplificó la cabecera de 9 a 5
bytes, pero las lecturas en `checkFrameHeader` y `parsePacket` quedaron
con las posiciones antiguas. `payloadLen` se leía de `buf[3]` (donde está
`seq`) y `seq` se copiaba de `buf[4]` (donde está `payloadLen`). Intercambiadas.

### v0.6.9 — 2026-10-06
Hotfix. Forward declarations movidas al inicio de `bt.cpp`. Estaban en
línea 161, después de las definiciones, por lo que `btSendRx` (línea 68)
no las veía cuando llamaba a `storeMessage`. Movidas a la línea 6.

### v0.6.7 — 2026-10-06
Hotfix. Renombrados `radio.h/.cpp` -> `radiolora.h/.cpp` para evitar la
colisión case-insensitive con la librería Sandeep Mistry `LoRa.cpp/h` en
Windows. Arduino IDE confundía `radio.cpp` con `LoRa.cpp`.

### v0.6.6 — 2026-10-06
Hotfix. Borradas las cachés del perfil de Arduino IDE 2.x
(`%APPDATA%\Roaming\arduino-ide\` y `%APPDATA%\Roaming\Arduino IDE\`).
El IDE mantiene una caché persistente de los `.cpp/.h` del sketch en
IndexedDB que no se borra cerrando/reabriendo ni renombrando la carpeta
`program/`.

### v0.6.5 — 2026-10-06
Hotfix. Reemplazada la carpeta `program/` por una copia limpia en
`program_new/` renombrada a `program/`. Esto fuerza al IDE 2.x a
reindexar desde cero.

### v0.6.4 — 2026-10-06
Hotfix. Añadido `#include <LoRa.h>` explícito a `radio.cpp` e `#include
"config.h"` explícito a `framing.cpp` y `bt.cpp`.

### v0.6.3 — 2026-10-06
Hotfix. Añadidas forward declarations en `bt.cpp` para resolver el orden
de llamadas entre funciones `static`.

### v0.6.2 — 2026-10-06
Hotfix. `lora.h` colisionaba con `LoRa.h` en sistemas case-insensitive
(Windows). Renombrados `lora.h` -> `radio.h` y `lora.cpp` -> `radio.cpp`.
`#include` actualizados. `#define NODE_HEX` movido de `program.ino` a
`config.h`.

### v0.6.1 — 2026-10-06
Hotfix. Funciones `static` marcadas donde correspondía y sus declaraciones
quitadas de los `.h`. Forward declarations añadidas a `framing.cpp` y
`lora.cpp`.

### v0.6.0 — 2026-10-06
Reorganización mayor. El sketch monolítico `program.ino` se divide en
5 ficheros por capa: `config.h`, `framing.h/.cpp`, `lora.h/.cpp`,
`bt.h/.cpp` y `program.ino` reducido a `setup()`/`loop()`. Sin cambios
de comportamiento.

### v0.5.2 — 2026-10-06
Simplificación de la cabecera del protocolo. Quitados `dst` y `type` de
la trama (siempre 0xFF y 0x01). Cabecera pasa de 9 a 5 bytes.
`ParsedPacket` ya no tiene `dst`/`type`. `addrToHex` ya no trata broadcast
como `*`. Eliminado el comando `/dst`.

### v0.5.1 — 2026-10-06
Refactor amplio sin cambio de comportamiento. `onLoraRx` se divide en
`readLoraBytes`, `acceptRxLora`, `dispatchAcceptedMessage`.
`parsePacket` en `checkFrameHeader`, `isValidSrc`, `validateCrcAt`,
`copyPayload`. `buildPacket` en `buildPacketValidate`, `writePacketHeader`,
`writePacketPayload`, `writePacketCrc`. `handleCommand` en `cmdHelp`,
`cmdId`, `cmdDst`, `cmdStat` y un helper `matchCmd`.

### v0.5.0 — 2026-10-06
Store-and-forward. Si no hay cliente BT conectado, los mensajes RX
aceptados se acumulan en `msgBuffer[32]` (RAM, ~6.5 KB) con política
FIFO. Al conectarse el cliente BT, `drainMessageBuffer()` envía los
pendientes en orden cronológico y responde con `OK drained=N`.

### v0.4.1 — 2026-10-06
`src` preservado en el reenvío. `relayPacket` transmite con `src=p.src`
(emisor original). `buildPacket` gana un parámetro `src` explícito. CRC
sobre el `src` real. Eliminado `markSeen(selfAddr(), msgId)` redundante
en `relayPacket`.

### v0.4.0 — 2026-10-06
Mesh/flooding. Eliminada toda la lógica de ACK previa. Añadidas
`seenCache[32]` con `seenBefore/markSeen`, `relayPacket`, `extractMsgId`,
`userText`. Payload de DATA ahora empieza con `msgId` (2 bytes big-endian).
`sendLoraData` -> `sendLoraBroadcast`. `onLoraRx` aplica anti-duplicado
y reenvía. `/dst` deprecado.

### v0.3.1 — 2026-10-06
Hardcoded de la dirección del nodo. `#define NODE_ID 'A'` reemplazado por
`#define NODE_HEX 0x01`. `selfAddr()` devuelve directamente `NODE_HEX`.

### v0.3.0 — 2026-10-06
Direccionamiento hex corto (`01..FE`) en lugar del mapa `A..Z` + `1..9`.

### v0.2.0 — 2026-10-06
Hito 2. BT-SPP `BT-POC` como entrada única. Muleta Serial retirada.

### v0.1.0 — 2026-10-05
Hito 1. Capa LoRa con trama binaria v1 + CRC-16/CCITT.

### v0.0.1 — 2026-10-05
Smoke test Core 3.x (solo TX, 5 paquetes `hola mundo`).
