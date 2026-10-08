#ifndef BT_H
#define BT_H

#include <Arduino.h>
#include "config.h"

// Manda el sobre RX al móvil. Si no hay cliente BT, lo almacena
//   en el buffer circular para entregarlo cuando se conecte.
void btSendRx(const ParsedPacket& p, uint16_t msgId);

// Callback del driver SPP. Lo instala SerialBT.register_callback.
void BTEvent(esp_spp_cb_event_t event, esp_spp_cb_param_t* param);

// Lee bytes del SPP hasta encontrar '\n'. Acumula en buffer estático.
void pollBt();

// Log por Serial de eventos del enlace SPP.
void logBt(const char* state);

#endif