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
  Serial.println("[BOOT] LoRa=OK sf=7 bw=125000 txp=17");
}

void loop() {
  pollBt();
  onLoraRx();
}