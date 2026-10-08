#include "radiolora.h"
#include <LoRa.h>
#include "framing.h"
#include "bt.h"   // btSendRx (lo llama dispatch acá)
#include "storage.h"  // storageSaveMsgId (lo llama sendLoraBroadcast)

// --- Forward declarations (orden de llamadas entre sí) ---
static bool seenBefore(uint8_t src, uint16_t msgId);
static void markSeen(uint8_t src, uint16_t msgId);
static void relayPacket(const ParsedPacket& p, uint16_t msgId);
static bool readLoraBytes(uint8_t* buf, size_t bufMax, uint8_t& gotOut, uint8_t& expectedOut);
static bool acceptRxLora(const ParsedPacket& p, const char*& note);
static void dispatchAcceptedMessage(const ParsedPacket& p, uint16_t msgId);
static bool sendLoraWithRepeat(uint8_t src, uint8_t seq,
                               const char* payload, uint8_t payloadLen);

// --- Estado global (definiciones) ---
uint8_t  txSeq = 0;
uint8_t  txRepeatCount = TX_REPEAT_COUNT_DEFAULT;
uint32_t txRepeatGapMs = TX_REPEAT_GAP_MS_DEFAULT;

uint8_t  getTxRepeatCount()  { return txRepeatCount; }
void     setTxRepeatCount(uint8_t n)  { txRepeatCount = n; }
uint32_t getTxRepeatGapMs()   { return txRepeatGapMs; }
void     setTxRepeatGapMs(uint32_t ms) { txRepeatGapMs = ms; }
uint16_t getMsgIdCounter()    { return msgIdCounter; }
void     setMsgIdCounter(uint16_t v)  { msgIdCounter = v; }
uint16_t msgIdCounter = 0;

SeenId seenCache[SEEN_CACHE_SIZE];
uint8_t seenHead = 0;

// --- Helpers de payload ---

// extractMsgId: lee los 2 primeros bytes del payload y devuelve el msgId.
//   Devuelve 0 si el payload es demasiado corto.
uint16_t extractMsgId(const ParsedPacket& p) {
  if (p.payloadLen < MSG_ID_SIZE) return 0;
  return (uint16_t)(((uint16_t)((uint8_t)p.payload[0]) << 8) |
                    (uint16_t)((uint8_t)p.payload[1]));
}

// userText: rellena `out` con la parte del payload tras el msgId, terminada en '\0'.
//   Devuelve la longitud del texto. out debe ser char[MAX_PAYLOAD_USER+1].
uint8_t userText(const ParsedPacket& p, char* out) {
  if (p.payloadLen < MSG_ID_SIZE) { out[0] = '\0'; return 0; }
  uint8_t len = (uint8_t)(p.payloadLen - MSG_ID_SIZE);
  memcpy(out, &p.payload[MSG_ID_SIZE], len);
  out[len] = '\0';
  return len;
}

// --- Anti-duplicado ---

// seenBefore: recorre la caché circular y devuelve true si (src, msgId) ya está.
static bool seenBefore(uint8_t src, uint16_t msgId) {
  for (uint8_t i = 0; i < SEEN_CACHE_SIZE; i++) {
    if (seenCache[i].src == src && seenCache[i].msgId == msgId) return true;
  }
  return false;
}

// markSeen: añade (src, msgId) en la posición de escritura y avanza el head.
static void markSeen(uint8_t src, uint16_t msgId) {
  seenCache[seenHead].src = src;
  seenCache[seenHead].msgId = msgId;
  seenHead = (uint8_t)((seenHead + 1) % SEEN_CACHE_SIZE);
}

// --- Logs ---

// logTx: log por Serial para TX LoRa.
//   Si reason != nullptr imprime solo la razón (caso build=FAIL).
//   Si reason == nullptr imprime formato completo msgId=.. seq=.. len=.. ok=.. repeat=..
void logTx(uint16_t msgId, uint8_t seq, uint8_t payloadLen, int ok,
          uint8_t repeat, const char* reason) {
  if (reason != nullptr) {
    Serial.print("[TX] ");
    Serial.println(reason);
    return;
  }
  Serial.print("[TX] msgId=");
  Serial.print(msgId);
  Serial.print(" seq=");
  Serial.print(seq);
  Serial.print(" len=");
  Serial.print(payloadLen);
  Serial.print(" ok=");
  Serial.print(ok);
  Serial.print(" repeat=");
  Serial.println(repeat);
}

// logRx: log por Serial para RX LoRa.
//   p == nullptr → imprime "[RX] <note>" (eco, corto, parse=FAIL, duplicado).
//   p != nullptr → imprime formato completo src=.. msgId=.. seq=.. len=.. payload="..".
void logRx(const ParsedPacket* p, uint16_t msgId, const char* note) {
  if (p == nullptr) {
    Serial.print("[RX] ");
    Serial.println(note);
    return;
  }
  char srcBuf[3];
  addrToHex(p->src, srcBuf);
  Serial.print("[RX] src=");
  Serial.print(srcBuf);
  Serial.print(" msgId=");
  Serial.print(msgId);
  Serial.print(" seq=");
  Serial.print(p->seq);
  Serial.print(" len=");
  Serial.print(p->payloadLen);
  Serial.print(" payload=\"");
  char text[MAX_PAYLOAD_USER + 1];
  userText(*p, text);
  Serial.print(text);
  Serial.println("\"");
  (void)note;
}

// --- Capa LoRa ---

// loraBeginSafe: configura pines del SX1278 y arranca el radio.
//   Aplica todos los parámetros RF definidos en config.h.
//   true si LoRa.begin tuvo éxito, false si falla la inicialización.
bool loraBeginSafe() {
  LoRa.setPins(LORA_SS, LORA_RST, LORA_DIO0);
  if (!LoRa.begin(LORA_FREQ)) return false;
  LoRa.setTxPower(LORA_TX_POWER);
  LoRa.setSpreadingFactor(LORA_SPREADING_FACTOR);
  LoRa.setSignalBandwidth(LORA_BANDWIDTH);
  LoRa.setCodingRate4(LORA_CODING_RATE);
  LoRa.setPreambleLength(LORA_PREAMBLE_LENGTH);
  LoRa.setSyncWord(LORA_SYNC_WORD);
  if (LORA_LNA_GAIN > 0) LoRa.setGain(LORA_LNA_GAIN);
  return true;
}

// sendLoraWithRepeat: envía el mismo paquete N veces con gap entre cada envío.
//   Bloquea durante toda la ráfaga. Cada iteración reconstruye el paquete
//   para que el seq (en TX originada) sea el mismo en todos los envíos.
//   Imprime un log por cada envío individual con r=N/total.
//   Devuelve true si al menos un envío tuvo ok=1.
static bool sendLoraWithRepeat(uint8_t src, uint8_t seq,
                               const char* payload, uint8_t payloadLen) {
  uint8_t buf[HEADER_SIZE + PAYLOAD_MAX + CRC_SIZE];
  size_t wrote = buildPacket(src, seq, payload, payloadLen, buf, sizeof(buf));
  if (wrote == 0) return false;

  for (uint8_t r = 1; r <= txRepeatCount; r++) {
    LoRa.beginPacket();
    for (size_t i = 0; i < wrote; i++) LoRa.write(buf[i]);
    int ok = LoRa.endPacket(true);

    Serial.print("[TX r=");
    Serial.print(r);
    Serial.print("/");
    Serial.print(txRepeatCount);
    Serial.print(" seq=");
    Serial.print(seq);
    Serial.print(" ok=");
    Serial.println(ok);

    if (r < txRepeatCount && txRepeatGapMs > 0) {
      delay(txRepeatGapMs);
    }
  }
  return true;
}

// sendLoraBroadcast: arma un paquete con msgId nuevo y lo transmite N veces.
void sendLoraBroadcast(const char* text, uint8_t textLen) {
  if (textLen > MAX_PAYLOAD_USER) return;
  uint16_t msgId = msgIdCounter++;
  uint8_t seq = txSeq++;
  uint8_t payloadLen = (uint8_t)(MSG_ID_SIZE + textLen);
  uint8_t payload[PAYLOAD_MAX];
  payload[0] = (uint8_t)((msgId >> 8) & 0xFF);
  payload[1] = (uint8_t)(msgId & 0xFF);
  memcpy(&payload[MSG_ID_SIZE], text, textLen);

  if (!sendLoraWithRepeat(selfAddr(), seq, (const char*)payload, payloadLen)) {
    logTx(msgId, seq, payloadLen, 0, txRepeatCount, "build=FAIL");
    return;
  }
  storageSaveMsgId();
  logTx(msgId, seq, payloadLen, 1, txRepeatCount, nullptr);
}

// relayPacket: reenvía un paquete preservando el src del emisor original.
//   seq nuevo local. Mismo payload (incluido msgId original).
//   Se retransmite N veces con el gap configurado.
//   La caché ya tiene (p.src, msgId) marcada por onLoraRx; cuando el reenvío
//   vuelve a este nodo por el aire, anti-duplicado lo descarta.
static void relayPacket(const ParsedPacket& p, uint16_t msgId) {
  if (p.payloadLen < MSG_ID_SIZE) return;
  sendLoraWithRepeat(p.src, txSeq++, p.payload, p.payloadLen);
}

// readLoraBytes: lee bytes del driver LoRa hasta consumir todo el paquete.
//   Devuelve false si no había paquete o si se leyó menos de lo esperado.
//   buf debe ser al menos HEADER_SIZE+PAYLOAD_MAX.
//   gotOut = bytes leídos; expectedOut = bytes que decía parsePacket.
static bool readLoraBytes(uint8_t* buf, size_t bufMax, uint8_t& gotOut, uint8_t& expectedOut) {
  int pktLen = LoRa.parsePacket();
  if (pktLen <= 0) { gotOut = 0; expectedOut = 0; return false; }
  expectedOut = (uint8_t)pktLen;
  gotOut = 0;
  while (gotOut < expectedOut && gotOut < bufMax && LoRa.available()) {
    buf[gotOut++] = (uint8_t)LoRa.read();
  }
  return gotOut == expectedOut;
}

// acceptRxLora: decide si el paquete parseado debe procesarse (no eco,
//   payload con tamaño mínimo para el msgId). Devuelve true y deja note=nullptr
//   si es aceptable; false y rellena note con el motivo del descarte.
static bool acceptRxLora(const ParsedPacket& p, const char*& note) {
  if (p.src == selfAddr()) { note = "eco descartado"; return false; }
  if (p.payloadLen < MSG_ID_SIZE) { note = "payload corto"; return false; }
  note = nullptr;
  return true;
}

// dispatchAcceptedMessage: loggea el mensaje, lo entrega a la app por BT
//   (directo o bufferizado) y lo reenvía por broadcast.
static void dispatchAcceptedMessage(const ParsedPacket& p, uint16_t msgId) {
  logRx(&p, msgId, nullptr);
  btSendRx(p, msgId);
  relayPacket(p, msgId);
}

// onLoraRx: orquestador de la RX LoRa. Lee bytes, parsea, valida,
//   aplica anti-duplicado y despacha. Llamada desde loop() en cada vuelta.
void onLoraRx() {
  uint8_t buf[HEADER_SIZE + PAYLOAD_MAX];
  uint8_t got = 0, expected = 0;
  if (!readLoraBytes(buf, sizeof(buf), got, expected)) {
    if (expected > 0 && got != expected) {
      char note[32];
      snprintf(note, sizeof(note), "corto got=%u expected=%u", got, expected);
      logRx(nullptr, 0, note);
    }
    return;
  }

  ParsedPacket p;
  if (!parsePacket(buf, got, p)) {
    char note[32];
    snprintf(note, sizeof(note), "parse=FAIL len=%u", got);
    logRx(nullptr, 0, note);
    return;
  }

  const char* note = nullptr;
  if (!acceptRxLora(p, note)) { logRx(nullptr, 0, note); return; }

  uint16_t msgId = extractMsgId(p);
  if (seenBefore(p.src, msgId)) {
    logRx(nullptr, 0, "duplicado descartado");
    return;
  }
  markSeen(p.src, msgId);
  dispatchAcceptedMessage(p, msgId);
}