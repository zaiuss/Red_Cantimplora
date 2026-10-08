#include "framing.h"
#include "config.h"   // para ParsedPacket, NODE_HEX, constantes

// --- Forward declarations ---
static uint16_t crc16(const uint8_t* data, size_t len);
static bool    buildPacketValidate(uint8_t src, uint8_t payloadLen);
static void    writePacketHeader(uint8_t* out, uint8_t src, uint8_t seq, uint8_t payloadLen);
static void    writePacketPayload(uint8_t* out, const char* payload, uint8_t payloadLen);
static void    writePacketCrc(uint8_t* out, uint8_t payloadLen);
static bool    checkFrameHeader(const uint8_t* buf, size_t len, uint8_t& payloadLen);
static bool    validateCrcAt(const uint8_t* buf, size_t crcOffset);
static void    copyPayload(const uint8_t* buf, uint8_t payloadLen, char* out);
static int     hexNibble(char c);

// selfAddr: dirección de este nodo, fija por '#define NODE_HEX' en build.
uint8_t selfAddr() {
  return NODE_HEX;
}

// crc16: CRC-16/CCITT (polinomio 0x1021, init 0xFFFF, sin reflect).
//   Función pura, sin estado. Mismos bytes -> mismo CRC.
static uint16_t crc16(const uint8_t* data, size_t len) {
  uint16_t crc = 0xFFFF;
  for (size_t i = 0; i < len; i++) {
    crc ^= (uint16_t)data[i] << 8;
    for (int b = 0; b < 8; b++) {
      if (crc & 0x8000) crc = (uint16_t)((crc << 1) ^ 0x1021);
      else              crc = (uint16_t)(crc << 1);
    }
  }
  return crc;
}

// buildPacketValidate: valida src y tamaño de payload.
//   Devuelve true si todos los parámetros son válidos.
static bool buildPacketValidate(uint8_t src, uint8_t payloadLen) {
  if (payloadLen > PAYLOAD_MAX) return false;
  if (src == 0x00 || src == 0xFF) return false;
  return true;
}

// writePacketHeader: escribe los 4 bytes fijos de cabecera.
static void writePacketHeader(uint8_t* out, uint8_t src, uint8_t seq, uint8_t payloadLen) {
  out[0] = PROTO_MAGIC;
  out[1] = PROTO_VERSION;
  out[2] = src;
  out[3] = seq;
  out[4] = payloadLen;
}

// writePacketPayload: copia payloadLen bytes a partir de out[HEADER_SIZE].
static void writePacketPayload(uint8_t* out, const char* payload, uint8_t payloadLen) {
  for (uint8_t i = 0; i < payloadLen; i++) out[HEADER_SIZE + i] = (uint8_t)payload[i];
}

// writePacketCrc: calcula el CRC de la cabecera+payload y lo escribe
//   en los 2 últimos bytes.
static void writePacketCrc(uint8_t* out, uint8_t payloadLen) {
  size_t crcOffset = (size_t)HEADER_SIZE + payloadLen;
  uint16_t c = crc16(out, crcOffset);
  out[crcOffset]     = (uint8_t)(c & 0xFF);
  out[crcOffset + 1] = (uint8_t)((c >> 8) & 0xFF);
}

// buildPacket: serializa un paquete binario (cabecera + payload + CRC)
//   en `out`. `src` se pasa explícitamente: selfAddr() para TX originada,
//   p.src para relay (preserva el emisor original).
//   Devuelve bytes escritos o 0 si falla alguna validación.
size_t buildPacket(uint8_t src, uint8_t seq,
                   const char* payload, uint8_t payloadLen,
                   uint8_t* out, size_t outMax) {
  if (!buildPacketValidate(src, payloadLen)) return 0;
  const size_t total = (size_t)HEADER_SIZE + payloadLen + CRC_SIZE;
  if (total > outMax) return 0;

  writePacketHeader(out, src, seq, payloadLen);
  writePacketPayload(out, payload, payloadLen);
  writePacketCrc(out, payloadLen);
  return total;
}

// checkFrameHeader: valida magic, version, longitud total y tamaño de payload.
static bool checkFrameHeader(const uint8_t* buf, size_t len, uint8_t& payloadLen) {
  if (len < (size_t)(HEADER_SIZE + CRC_SIZE)) return false;
  if (buf[0] != PROTO_MAGIC) return false;
  if (buf[1] != PROTO_VERSION) return false;
  payloadLen = buf[4];
  if ((size_t)(HEADER_SIZE + payloadLen + CRC_SIZE) != len) return false;
  if (payloadLen > PAYLOAD_MAX) return false;
  return true;
}

// validateCrcAt: compara el CRC almacenado en buf[crcOffset..crcOffset+1]
//   con el calculado sobre buf[0..crcOffset-1].
static bool validateCrcAt(const uint8_t* buf, size_t crcOffset) {
  uint16_t expected = (uint16_t)((uint16_t)buf[crcOffset]) |
                      (uint16_t)((uint16_t)buf[crcOffset + 1] << 8);
  uint16_t actual = crc16(buf, crcOffset);
  return expected == actual;
}

// copyPayload: copia bytes del payload al string C en `out` y termina en '\0'.
static void copyPayload(const uint8_t* buf, uint8_t payloadLen, char* out) {
  for (uint8_t i = 0; i < payloadLen; i++) out[i] = (char)buf[HEADER_SIZE + i];
  out[payloadLen] = '\0';
}

// parsePacket: deserializa bytes validando magic, version, longitud, src y CRC.
//   Rellena `out`. false si cualquier validación falla.
bool parsePacket(const uint8_t* buf, size_t len, ParsedPacket& out) {
  out.src = 0; out.seq = 0;
  out.payloadLen = 0; out.payload[0] = '\0';

  uint8_t payloadLen;
  if (!checkFrameHeader(buf, len, payloadLen)) return false;
  if (buf[2] == 0x00 || buf[2] == 0xFF) return false;

  const size_t crcOffset = (size_t)HEADER_SIZE + payloadLen;
  if (!validateCrcAt(buf, crcOffset)) return false;

  out.src = buf[2];
  out.seq = buf[3];
  out.payloadLen = payloadLen;
  copyPayload(buf, payloadLen, out.payload);
  return true;
}

// hexNibble: parsea un dígito hex ('0'-'9', 'A'-'F', 'a'-'f'). -1 si no.
//   Helper interno de addrToHex.
static int hexNibble(char c) {
  if (c >= '0' && c <= '9') return c - '0';
  if (c >= 'A' && c <= 'F') return c - 'A' + 10;
  if (c >= 'a' && c <= 'f') return c - 'a' + 10;
  return -1;
}

// addrToHex: convierte dirección numérica (0x01..0xFE) a 2 chars hex
//   mayúsculos ("01".."FE"). out debe ser char[3].
void addrToHex(uint8_t a, char* out) {
  static const char hex[] = "0123456789ABCDEF";
  out[0] = hex[(a >> 4) & 0x0F];
  out[1] = hex[a & 0x0F];
  out[2] = '\0';
}