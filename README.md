# Red Cantimplora

Red de mensajería LoRa mesh/flooding entre nodos ESP32, con una app Android
(.NET MAUI) que actúa como terminal Bluetooth SPP para hablar con cada nodo.

El repositorio está dividido en dos partes:

- `android/` — aplicación MAUI Android (terminal BT-SPP y chat).
- `esp32/` — firmware Arduino para el nodo (capa LoRa + BT-SPP).

## ¿Para qué sirve esto?

Red de nodos ESP32 que se comunican entre sí por radio LoRa. La app
Android es el terminal que el usuario usa para enviar y recibir
mensajes de texto a través del nodo al que esté enlazada por
Bluetooth, y que el ESP32 propaga al resto de la red.

```
[móvil Android]  --BT SPP-->  [ESP32 nodo A]  --LoRa broadcast-->  [ESP32 nodo B]  --BT SPP-->  [móvil Android]
```

- La app Android se conecta por Bluetooth SPP a un ESP32 emparejado.
- El texto que envías desde la app se empaqueta en una trama binaria y se
  transmite por LoRa (433 MHz) en broadcast.
- Cada nodo que la recibe la reenvía (mesh/flooding) salvo que ya la haya
  visto. Tiene anti-duplicado por `(src, msgId)` y anti-eco.
- Si el nodo receptor no tiene la app conectada por BT, guarda el mensaje
  en un buffer circular y lo entrega cuando se reconecta (store-and-forward).
- No hay ACK, no hay unicast, no hay retransmisiones. La red es best-effort.

Detalles completos del protocolo y del firmware en `esp32/README.md`.
Detalles de la app en `android/README.md`.

## Hardware necesario

- ESP32-WROOM-32. Las pruebas se han hecho con esta placa. Para otras
  variantes (S3, C3, WROVER) revisa los pines en  `esp32/firmware/config.h`.
- Módulo LoRa SX1278 433 MHz (SPI, pines en `esp32/firmware/config.h`).
- Móvil Android con Bluetooth clásico (Android 6.0 / API 21 mínimo).
- App "Bluetooth SPP" genérica del sistema o similar para pruebas rápidas
  (la app de este repo es la recomendada).

## Créditos

MIT. Ver [LICENSE](LICENSE).

LLM Minimax utilizado en el desarrollo (pa'lo bueno y pa'lo malo).
Si usas o adaptas este proyecto, una mención a Zaiuss en los créditos se agradece.
