#ifndef STORAGE_H
#define STORAGE_H

#include <Arduino.h>

// Carga los valores persistidos en NVS y los aplica a las variables globales
//   (txRepeatCount, txRepeatGapMs, msgIdCounter). Si NVS está vacía, usa los
//   defaults de config.h. Llamada desde setup() antes del primer uso.
void storageLoad();

// Guarda txRepeatCount y txRepeatGapMs en NVS. Llamada desde cmdTx/cmdGap.
void storageSaveTx();

// Guarda msgIdCounter en NVS. Llamada tras cada incremento en sendLoraBroadcast.
void storageSaveMsgId();

#endif