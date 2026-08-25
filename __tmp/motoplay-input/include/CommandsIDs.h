//If not was included this File yet, start the include code...
#ifndef SERIAL_COMMANDS_H
#define SERIAL_COMMANDS_H

//Do the necessary includes
#include <Arduino.h>



//Create the Packet Header byte
constexpr uint8_t PKT_HEADER = 0xAA;

//Create the Commands IDs enum
enum class CommandID : uint8_t {
    SET_POLLING_RATE = 0x01,
};

//Finish the include code...
#endif