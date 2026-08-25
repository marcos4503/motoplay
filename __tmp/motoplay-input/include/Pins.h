//If not was included this File yet, start the include code...
#ifndef PINS_SETUP_H
#define PINS_SETUP_H

//Do the necessary includes
#include <Arduino.h>

/*
 * This file contains references for each Pin (or component) of Arduino Nano, regarding the components required
 * for the Motoplay Input mini-computer. All Motoplay Input firmware uses this file to get these references.
 *
 * If in the mini-computer that you have assembled, have some component linked to a different Pin than expected,
 * you can change the Pin ID here.
*/

// ------------------------------------ "ARDUINO NANO ATMEGA328P" EXPECTED GPIO PINS TABLE ----------------------------------- //
//Pin D13 - ID 13 - Input/Output - None
//Pin 3V3 - ID XX - None         - None                    ! > Provides 3.3v/50mA as Output
//Pin REF - ID XX - None         - None                    ! > Sets the max. reference voltage to be on perform "analogRead()".
//Pin A0  - ID A0 - Input/Output - None
//Pin A1  - ID A1 - Input/Output - None
//Pin A2  - ID A2 - Input/Output - None
//Pin A3  - ID A3 - Input/Output - None
//Pin A4  - ID A4 - Input/Output - None
//Pin A5  - ID A5 - Input/Output - None
//Pin A6  - ID A6 - Input/Output - Joystick VRX
//Pin A7  - ID A7 - Input/Output - Joystick VRY
//Pin 5V  - ID XX - None         - None                    ! > Can dir. power the Nano with 5v OR provides 5v/450mA as Output
//Pin RST - ID XX - None         - None                    ! > Reboot the Nano if receive a GND pulse.
//Pin GND - ID XX - None         - None                    ! > Used for complete circuits. Is the Ground connection, a.k.a as (-)
//Pin VIN - ID XX - None         - None                    ! > Can be used to direct power the Nano with 5v, a.k.a Voltage Input
//Pin D12 - ID 12 - Input/Output - None
//Pin D11 - ID 11 - Input/Output - None
//Pin D10 - ID 10 - Input/Output - None
//Pin D9  - ID 9  - Input/Output - None
//Pin D8  - ID 8  - Input/Output - None
//Pin D7  - ID 7  - Input/Output - None
//Pin D6  - ID 6  - Input/Output - None
//Pin D5  - ID 5  - Input/Output - None
//Pin D4  - ID 4  - Input/Output - None
//Pin D3  - ID 3  - Input/Output - Joystick SW
//Pin D2  - ID 2  - Input/Output - None
//Pin GND - ID XX - None         - None                    ! > Used for complete circuits. Is the Ground connection, a.k.a as (-)
//Pin RST - ID XX - None         - None                    ! > Reboot the Nano if receive a GND pulse.
//Pin RX0 - ID 0  - Input/Output - None                    ! > Used by UART Chip. Don't use "Serial.begin()" if using this Pin
//Pin TX1 - ID 1  - Input/Output - None                    ! > Used by UART Chip. Don't use "Serial.begin()" if using this Pin
// ---------------------------------------------------------------------------------------------------------------------------- //

//Pins references

constexpr uint8_t PIN_JOYSTICK_VRX = A6;
constexpr uint8_t PIN_JOYSTICK_VRY = A7;
constexpr uint8_t PIN_JOYSTICK_SW = 3;

//Finish the include code...
#endif