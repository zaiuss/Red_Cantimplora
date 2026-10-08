#include "storage.h"
#include <Preferences.h>
#include "config.h"
#include "radiolora.h"

static const char* NVS_NAMESPACE = "lora_cfg";
static const char* KEY_TX_COUNT   = "txCount";
static const char* KEY_TX_GAP_MS  = "txGapMs";
static const char* KEY_MSG_ID     = "msgId";

void storageLoad() {
  Preferences prefs;
  prefs.begin(NVS_NAMESPACE, true);  // read-only
  uint8_t  savedCount = prefs.getUChar(KEY_TX_COUNT, TX_REPEAT_COUNT_DEFAULT);
  uint32_t savedGap   = prefs.getULong(KEY_TX_GAP_MS, TX_REPEAT_GAP_MS_DEFAULT);
  uint16_t savedMsgId = (uint16_t)prefs.getUShort(KEY_MSG_ID, 0);
  prefs.end();

  setTxRepeatCount(savedCount);
  setTxRepeatGapMs(savedGap);
  setMsgIdCounter(savedMsgId);
}

void storageSaveTx() {
  Preferences prefs;
  prefs.begin(NVS_NAMESPACE, false);
  prefs.putUChar(KEY_TX_COUNT, getTxRepeatCount());
  prefs.putULong(KEY_TX_GAP_MS, getTxRepeatGapMs());
  prefs.end();
}

void storageSaveMsgId() {
  Preferences prefs;
  prefs.begin(NVS_NAMESPACE, false);
  prefs.putUShort(KEY_MSG_ID, getMsgIdCounter());
  prefs.end();
}