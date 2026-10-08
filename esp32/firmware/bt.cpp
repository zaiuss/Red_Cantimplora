#include "bt.h"
#include "config.h"
#include "framing.h"
#include "radiolora.h"  // sendLoraBroadcast (la llama onBtRxLine)
#include "storage.h"   // storageSaveTx (lo llaman cmdTx/cmdGap)

// --- Forward declarations para resolver el orden de llamadas entre funciones static ---
static void btReply(const char* s);
static void btReplyf(const char* fmt, const char* arg);
static void sendRxToBt(uint8_t src, uint16_t msgId, uint8_t seq, const char* text, uint8_t textLen);
static void storeMessage(uint8_t src, uint16_t msgId, uint8_t seq, const char* text, uint8_t textLen);
static bool popMessage(StoredMsg& out);
static void drainMessageBuffer();
static bool handleCommand(const char* line);
static void onBtRxLine(char* line, size_t len);
static void cmdHelp();
static void cmdId();
static void cmdStat();
static void cmdTx(const char* args);
static void cmdGap(const char* args);
static bool matchCmd(const char* p, const char* cmd);

// --- Estado global (definiciones) ---
BluetoothSerial SerialBT;
bool btClientConnected = false;

StoredMsg msgBuffer[MSG_BUFFER_SIZE];
uint16_t   msgHead = 0;
uint16_t   msgCount = 0;

// --- Log BT ---

// logBt: log por Serial para eventos del enlace SPP.
void logBt(const char* state) {
  Serial.print("[BT] ");
  Serial.println(state);
}

// --- Respuestas BT (bajo nivel) ---

// btReply: envía una respuesta corta por BT al móvil.
//   Workaround del driver SPP: mete '\n' extra y un delay(10) para
//   empujar el buffer interno; sin esto, respuestas como /help no salen.
static void btReply(const char* s) {
  if (!btClientConnected) return;
  SerialBT.print(s);
  SerialBT.print('\n');
  SerialBT.write('\n');
  delay(10);
}

// btReplyf: btReply con formato printf (un solo argumento %s).
//   Buffer interno de 64 bytes. Si el formato crece, ajustar tamaño.
static void btReplyf(const char* fmt, const char* arg) {
  if (!btClientConnected) return;
  char buf[64];
  snprintf(buf, sizeof(buf), fmt, arg);
  btReply(buf);
}

// --- Sobre RX ---

// sendRxToBt: bajo nivel. Escribe un sobre "RX src=.. msgId=.. seq=.. len=.. payload=\"..\""
//   al socket SPP. No chequea btClientConnected ni el tipo. Usado por btSendRx
//   (camino directo) y por drainMessageBuffer (camino diferido).
static void sendRxToBt(uint8_t src, uint16_t msgId, uint8_t seq, const char* text, uint8_t textLen) {
  char srcBuf[3];
  addrToHex(src, srcBuf);

  SerialBT.print("RX src=");
  SerialBT.print(srcBuf);
  SerialBT.print(" msgId=");
  SerialBT.print(msgId);
  SerialBT.print(" seq=");
  SerialBT.print(seq);
  SerialBT.print(" len=");
  SerialBT.print(textLen);
  SerialBT.print(" payload=\"");
  SerialBT.print(text);
  SerialBT.println("\"");
}

// btSendRx: manda el sobre RX al móvil. Si no hay cliente BT, lo almacena
//   en el buffer circular para entregarlo cuando se conecte.
void btSendRx(const ParsedPacket& p, uint16_t msgId) {
  char text[MAX_PAYLOAD_USER + 1];
  uint8_t textLen = userText(p, text);

  if (btClientConnected) {
    sendRxToBt(p.src, msgId, p.seq, text, textLen);
  } else {
    storeMessage(p.src, msgId, p.seq, text, textLen);
  }
}

// --- Store-and-forward ---

// storeMessage: añade un mensaje al buffer circular. Si está lleno,
//   descarta el más antiguo (FIFO circular).
static void storeMessage(uint8_t src, uint16_t msgId, uint8_t seq, const char* text, uint8_t textLen) {
  if (textLen > MAX_PAYLOAD_USER) textLen = MAX_PAYLOAD_USER;

  if (msgCount >= MSG_BUFFER_SIZE) {
    msgHead = (uint16_t)((msgHead + 1) % MSG_BUFFER_SIZE);
    msgCount = (uint16_t)(msgCount - 1);
  }

  uint16_t writeIdx = (uint16_t)((msgHead + msgCount) % MSG_BUFFER_SIZE);
  StoredMsg& m = msgBuffer[writeIdx];
  m.src = src;
  m.msgId = msgId;
  m.seq = seq;
  m.textLen = textLen;
  memcpy(m.text, text, textLen);
  m.text[textLen] = '\0';
  msgCount = (uint16_t)(msgCount + 1);
}

// popMessage: saca el mensaje más antiguo. Devuelve false si está vacío.
static bool popMessage(StoredMsg& out) {
  if (msgCount == 0) return false;
  out = msgBuffer[msgHead];
  msgHead = (uint16_t)((msgHead + 1) % MSG_BUFFER_SIZE);
  msgCount = (uint16_t)(msgCount - 1);
  return true;
}

// drainMessageBuffer: envía todos los mensajes acumulados por BT en orden
//   de llegada. Llamado desde BTEvent al conectarse un cliente.
static void drainMessageBuffer() {
  StoredMsg m;
  uint16_t sent = 0;
  while (popMessage(m)) {
    sendRxToBt(m.src, m.msgId, m.seq, m.text, m.textLen);
    sent++;
    delay(5);
  }
  if (sent > 0) {
    char buf[32];
    snprintf(buf, sizeof(buf), "OK drained=%u", sent);
    btReply(buf);
  }
}

// --- Comandos ---

// cmdHelp: imprime la lista de comandos + notas mesh/buffer.
static void cmdHelp() {
  btReply("OK commands:");
  btReply("  /help");
  btReply("  /id");
  btReply("  /stat");
  btReply("  /tx [N]");
  btReply("  /gap [ms]");
  btReply("");
  btReply("Mesh: cada mensaje se propaga a todos los nodos.");
  btReply("       Los duplicados se descartan por (src,msgId).");
  btReply("Buffer: si no hay cliente BT, los RX se acumulan");
  btReply("        (hasta 32) y se entregan al volver.");
  btReply("TX: por defecto se emiten N veces con gap ms entre");
  btReply("    cada envio. Configurable con /tx y /gap.");
}

// cmdId: devuelve la dirección propia en hex.
static void cmdId() {
  char selfBuf[3];
  addrToHex(selfAddr(), selfBuf);
  btReplyf("OK id=%s", selfBuf);
}

// cmdStat: imprime resumen del estado del nodo.
static void cmdStat() {
  char selfBuf[3];
  char statBuf[96];
  addrToHex(selfAddr(), selfBuf);
  snprintf(statBuf, sizeof(statBuf), "OK id=%s seq=%u mesh=ON buf=%u/%u bt=%s tx=%u/%ums",
           selfBuf, txSeq, msgCount, MSG_BUFFER_SIZE, btClientConnected ? "ON" : "OFF",
           getTxRepeatCount(), getTxRepeatGapMs());
  btReply(statBuf);
}

// cmdTx: /tx [N] ajusta el número de repeticiones por TX. Sin argumento, consulta.
//   Rango válido: 1..TX_REPEAT_COUNT_MAX (default 4).
static void cmdTx(const char* args) {
  char buf[80];
  if (*args == '\0') {
    snprintf(buf, sizeof(buf), "OK tx=%u (max %u, default %u)",
             getTxRepeatCount(), TX_REPEAT_COUNT_MAX, TX_REPEAT_COUNT_DEFAULT);
    btReply(buf);
    return;
  }
  int n = atoi(args);
  if (n < 1 || n > (int)TX_REPEAT_COUNT_MAX) {
    snprintf(buf, sizeof(buf), "ERR tx fuera de rango (1..%u)", TX_REPEAT_COUNT_MAX);
    btReply(buf);
    return;
  }
  setTxRepeatCount((uint8_t)n);
  storageSaveTx();
  snprintf(buf, sizeof(buf), "OK tx=%u", (uint8_t)n);
  btReply(buf);
}

// cmdGap: /gap [ms] ajusta el tiempo entre repeticiones. Sin argumento, consulta.
//   Rango válido: 0..TX_REPEAT_GAP_MS_MAX (default 1000).
static void cmdGap(const char* args) {
  char buf[80];
  if (*args == '\0') {
    snprintf(buf, sizeof(buf), "OK gap=%ums (max %u, default %u)",
             getTxRepeatGapMs(), TX_REPEAT_GAP_MS_MAX, TX_REPEAT_GAP_MS_DEFAULT);
    btReply(buf);
    return;
  }
  long n = atol(args);
  if (n < 0 || n > (long)TX_REPEAT_GAP_MS_MAX) {
    snprintf(buf, sizeof(buf), "ERR gap fuera de rango (0..%u)", TX_REPEAT_GAP_MS_MAX);
    btReply(buf);
    return;
  }
  setTxRepeatGapMs((uint32_t)n);
  storageSaveTx();
  snprintf(buf, sizeof(buf), "OK gap=%ums", (uint32_t)n);
  btReply(buf);
}

// matchCmd: true si `p` empieza por `cmd` seguido de fin o espacio.
static bool matchCmd(const char* p, const char* cmd) {
  size_t n = strlen(cmd);
  if (strncmp(p, cmd, n) != 0) return false;
  char c = p[n];
  return c == '\0' || c == ' ';
}

// handleCommand: parsea líneas que empiezan por '/'. Responde por BT.
//   Comandos: /help, /id, /stat.
static bool handleCommand(const char* line) {
  if (line[0] != '/') return false;

  const char* p = line + 1;
  if (matchCmd(p, "help")) { cmdHelp(); return true; }
  if (matchCmd(p, "id"))   { cmdId();   return true; }
  if (matchCmd(p, "stat")) { cmdStat(); return true; }
  if (matchCmd(p, "tx"))   { cmdTx(p + 3);   return true; }
  if (matchCmd(p, "gap"))  { cmdGap(p + 4);  return true; }

  btReply("ERR comando no reconocido. /help");
  return true;
}

// --- SPP polling y eventos ---

// onBtRxLine: recibe una línea completa del BT y decide:
//   '/' → comando (handleCommand)
//   otro → texto que se envía por LoRa como broadcast.
static void onBtRxLine(char* line, size_t len) {
  if (len == 0) return;

  if (line[0] == '/') {
    line[len] = '\0';
    handleCommand(line);
    return;
  }

  if (len > MAX_PAYLOAD_USER) {
    btReply("ERR linea demasiado larga");
    return;
  }

  for (uint8_t i = 0; i < len; i++) {
    if (line[i] == '\n' || line[i] == '\r') line[i] = ' ';
  }
  line[len] = '\0';

  sendLoraBroadcast(line, len);
}

// pollBt: lee bytes del SPP hasta encontrar '\n'. Acumula en buffer
//   estático (no reentrante). '\r' se ignora. Llama a onBtRxLine al cerrar.
//   Si el buffer se llena, los bytes siguientes se descartan sin avisar.
void pollBt() {
  static char line[BT_RX_BUF_MAX + 1];
  static uint16_t idx = 0;

  while (SerialBT.available()) {
    char c = (char)SerialBT.read();
    if (c == '\n') {
      line[idx] = '\0';
      onBtRxLine(line, idx);
      idx = 0;
    } else if (c != '\r') {
      if (idx < BT_RX_BUF_MAX) line[idx++] = c;
    }
  }
}

// BTEvent: callback del driver SPP. Se invoca cuando un cliente abre
//   o cierra el socket. Actualiza btClientConnected y, al abrir, drena
//   el buffer de mensajes pendientes.
void BTEvent(esp_spp_cb_event_t event, esp_spp_cb_param_t* param) {
  if (event == ESP_SPP_SRV_OPEN_EVT) {
    btClientConnected = true;
    logBt("cliente conectado");
    drainMessageBuffer();
  } else if (event == ESP_SPP_CLOSE_EVT) {
    btClientConnected = false;
    logBt("cliente desconectado");
  }
}