#ifndef FRAMING_H
#define FRAMING_H

#include <Arduino.h>
#include "config.h"

uint8_t selfAddr();

// Serializa un paquete (cabecera + payload + CRC) en `out`.
// Devuelve bytes escritos o 0 si falla validación.
size_t buildPacket(uint8_t src, uint8_t seq,
                   const char* payload, uint8_t payloadLen,
                   uint8_t* out, size_t outMax);

// Deserializa bytes validando magic, version, longitud, src y CRC.
bool parsePacket(const uint8_t* buf, size_t len, ParsedPacket& out);

// Convierte dirección numérica (0x01..0xFE) a 2 chars hex mayúsculos ("01".."FE").
void addrToHex(uint8_t a, char* out);

#endif