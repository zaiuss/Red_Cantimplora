#ifndef CONFIG_H
#define CONFIG_H

#include <Arduino.h>
#include <SPI.h>
#include <LoRa.h>
#include "BluetoothSerial.h"

// --- Pines ---
#define LORA_SS        23
#define LORA_RST       14
#define LORA_DIO0      2

// --- Frecuencia portadora (Hz) ---
// Driver: 410..525 MHz. España SRD IR-266 (BOE 12-03-2021):
//   433.050-434.040 MHz -> 10 mW PIRE, duty cycle <= 10%
//   434.040-434.790 MHz -> 10 mW PIRE + LBT/AFA obligatorio
// 433.92 MHz = canal 35 LPD, centro de la sub-banda inferior, mejor
// comportamiento de antena y filtros SAW del módulo.
#define LORA_FREQ              433920000UL

// --- Potencia TX en dBm (PIRE a la antena) ---
// Driver PA_BOOST: 2..17 dBm. (Rango RFO: 0..14 dBm, no usado aquí).
// Techo legal España 433 MHz: 10 dBm (10 mW). El valor previo de 17 era ilegal.
// NO subir por encima de 10. Mejorar alcance = antena y SF, no potencia.
#define LORA_TX_POWER          10

// --- Spreading Factor (SF) ---
// Driver: 6..12. Mayor SF = más sensibilidad pero más tiempo en aire.
//   SF7  ~-123 dBm / 0.3 s payload max
//   SF9  ~-128 dBm / 0.7 s payload max   <-- valor propuesto
//   SF10 ~-130 dBm / 1.4 s payload max
//   SF12 ~-137 dBm / 5.6 s payload max
// SF9 = sweet spot mesh: +5 dB vs SF7, tiempo en aire aún manejable
// (4 reps x 1s gap = ~6s bloqueando loop con payload 200B).
#define LORA_SPREADING_FACTOR  9

// --- Bandwidth (Hz) ---
// Driver: 7800, 10400, 15600, 20800, 31250, 41700, 62500, 125000, 250000, 500000.
// Cada vez que doblas BW pierdes ~3 dB de sensibilidad.
// 125000 = default equilibrado. 62500 = +3 dB sensibilidad (2x tiempo en aire).
// 41700/31250 = aún más sensibilidad pero más tiempo.
#define LORA_BANDWIDTH         125E3

// --- Coding Rate (FEC) ---
// Driver: 4/5..4/8 (denominador 5..8). Más alto = más corrección, más overhead.
//   4/5 -> 0% overhead (default)
//   4/6 -> +8% overhead, +1-2 dB robustez
//   4/7 -> +12.5% overhead, +2-3 dB robustez   <-- valor propuesto
//   4/8 -> +20% overhead, +3-4 dB robustez
#define LORA_CODING_RATE       7

// --- Preámbulo (bytes) ---
// Driver: 1..65535. Default librería: 8.
// Subir a 10-12 puede ayudar en enlaces muy débiles donde el RX no detecta
// el inicio del paquete. Bajar a 6 reduce tiempo en aire pero es arriesgado.
#define LORA_PREAMBLE_LENGTH   8

// --- Sync Word (0..255) ---
// Filtro hardware: dos nodos con sync word distinto NO se ven.
// Default Sandeep Mistry: 0x12. NO afecta a la sensibilidad ni al alcance.
// Cambiarlo solo sirve para aislarse de otras redes 433 MHz con sync 0x12.
// Si alguna vez sospechas paquetes no deseados, cámbialo en TODOS los nodos.
#define LORA_SYNC_WORD         0x12

// --- LNA Gain (0..6) ---
// 0 = AGC automático (recomendado para mesh).
// 1..6 = ganancia fija, ~+1-2 dB extra en RX pero sin adaptarse al entorno.
// Solo fijar si tienes entorno muy homogéneo y quieres exprimir el último dB.
#define LORA_LNA_GAIN          0

// --- Dirección del nodo (fija por build; cambiar antes de cada flasheo) ---
#define NODE_HEX 0x08

// --- BT ---
#define BT_NAME "BT-POC"

// --- Protocolo ---
#define PROTO_MAGIC        0xA5
#define PROTO_VERSION      0x01
#define PAYLOAD_MAX        200

// --- Buffers / tamaños ---
#define BT_RX_BUF_MAX      (PAYLOAD_MAX + 16)
#define MSG_ID_SIZE        2
#define MAX_PAYLOAD_USER   (PAYLOAD_MAX - MSG_ID_SIZE)
#define SEEN_CACHE_SIZE    32
#define MSG_BUFFER_SIZE    32

// --- Repetición de TX (mesh robusto) ---
#define TX_REPEAT_COUNT_DEFAULT   4
#define TX_REPEAT_GAP_MS_DEFAULT  1000
#define TX_REPEAT_COUNT_MAX       10
#define TX_REPEAT_GAP_MS_MAX      60000

// --- Cabecera / CRC ---
#define HEADER_SIZE  5
#define CRC_SIZE     2

// --- Tipos ---
struct ParsedPacket {
  uint8_t src;
  uint8_t seq;
  uint8_t payloadLen;
  char    payload[PAYLOAD_MAX + 1];
};

struct SeenId {
  uint8_t src;
  uint16_t msgId;
};

struct StoredMsg {
  uint8_t src;
  uint16_t msgId;
  uint8_t seq;
  uint16_t textLen;
  char    text[MAX_PAYLOAD_USER + 1];
};

// --- Estado global (declaraciones; las definiciones están en bt.cpp / radiolora.cpp) ---
extern BluetoothSerial SerialBT;
extern bool btClientConnected;

extern uint8_t  txSeq;
extern uint16_t msgIdCounter;

extern SeenId seenCache[SEEN_CACHE_SIZE];
extern uint8_t seenHead;

extern StoredMsg msgBuffer[MSG_BUFFER_SIZE];
extern uint16_t   msgHead;
extern uint16_t   msgCount;

#endif