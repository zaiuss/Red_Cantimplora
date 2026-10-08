#ifndef CONFIG_H
#define CONFIG_H

#include <Arduino.h>
#include <SPI.h>
#include <LoRa.h>
#include "BluetoothSerial.h"

// --- Pines / RF ---
#define LORA_SS        23
#define LORA_RST       14
#define LORA_DIO0      2
#define LORA_FREQ      433E6
#define LORA_TX_POWER  17

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