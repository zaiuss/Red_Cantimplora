#include "config.h"
#include "framing.h"
#include "radiolora.h"
#include "bt.h"
#include "storage.h"

void setup() {
  Serial.begin(115200);
  delay(200);
  Serial.println();
  Serial.print("[BOOT] NODE_HEX=");
  char bootBuf[3];
  addrToHex(NODE_HEX, bootBuf);
  Serial.print(bootBuf);
  Serial.print(" selfAddr=");
  Serial.println(bootBuf);

  storageLoad();

  SerialBT.register_callback(BTEvent);
  if (!SerialBT.begin(BT_NAME)) {
    Serial.println("[BOOT] BT=FAIL");
    while (true) delay(1000);
  }
  Serial.print("[BOOT] BT=OK name=");
  Serial.println(BT_NAME);

  SPI.end();
  SPI.begin(18, 5, 19, 23);

  if (!loraBeginSafe()) {
    Serial.println("[BOOT] LoRa=FAIL");
    while (true) delay(1000);
  }
  Serial.print("[BOOT] LoRa=OK sf=");
  Serial.print(LORA_SPREADING_FACTOR);
  Serial.print(" bw=");
  Serial.print((long)LORA_BANDWIDTH);
  Serial.print(" txp=");
  Serial.print(LORA_TX_POWER);
  Serial.print(" cr=4/");
  Serial.print(LORA_CODING_RATE);
  Serial.print(" pre=");
  Serial.print(LORA_PREAMBLE_LENGTH);
  Serial.print(" sw=0x");
  Serial.println(LORA_SYNC_WORD, HEX);
}

void loop() {
  pollBt();
  onLoraRx();
}