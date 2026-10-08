#ifndef RADIOLORA_H
#define RADIOLORA_H

#include <Arduino.h>
#include "config.h"

// Configura pines del SX1278 y arranca el radio. true si OK, false si falla.
bool loraBeginSafe();

// Envía un mensaje al aire con msgId nuevo (broadcast).
void sendLoraBroadcast(const char* text, uint8_t textLen);

// Lee un paquete LoRa, lo valida, aplica anti-duplicado y lo entrega + reenvía.
// Llamada desde loop() en cada vuelta.
void onLoraRx();

// Mensaje al usuario por Serial de eventos del header de mensajes LoRa.
void logTx(uint16_t msgId, uint8_t seq, uint8_t payloadLen, int ok,
          uint8_t repeat, const char* reason);
void logRx(const ParsedPacket* p, uint16_t msgId, const char* note);

// Auxiliares para extraer partes del payload aceptado.
uint16_t extractMsgId(const ParsedPacket& p);
uint8_t  userText(const ParsedPacket& p, char* out);

// Acceso a la repetición configurable por BT (/tx, /gap).
uint8_t  getTxRepeatCount();
void     setTxRepeatCount(uint8_t n);
uint32_t getTxRepeatGapMs();
void     setTxRepeatGapMs(uint32_t ms);

// Acceso al contador de mensajes (para persistencia).
uint16_t getMsgIdCounter();
void     setMsgIdCounter(uint16_t v);

#endif