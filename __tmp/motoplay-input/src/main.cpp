//Include dependency files
#include <Arduino.h>
#include "Pins.h"
#include "CommandsIDs.h"

//Include optional test files
//#include "ComponentsTest.h"

//Initialize needed variables & libraries
const char* DEVICE_ID_MSG = "Motoplay Input - by marcos4503";
unsigned long lastPollingTimeMs = 0;
unsigned long pollingRateIntervalMs = 500;
bool canEmitOutput = false;

//Declare methods
void ReadSerialCommand();
void SendJoystickOutput();

//Initialize the firmware

void setup() {

    //Initialize the Pins
    pinMode(PIN_JOYSTICK_SW, INPUT_PULLUP);
    pinMode(PIN_JOYSTICK_VRX, INPUT);
    pinMode(PIN_JOYSTICK_VRY, INPUT);

    //---//

    //Initialize methods. Needed to some libraries receive the processing setup from the Nano.
    //...

    //---//

    //Wait more initialization time
    delay(25);

    //---//

    //Test method. Uncomment if you need to run some Component Test. Tests can return messages on Serial Monitor.
    //InitializeComponentTest();
    //return;

    //---//

    //Set the baud rate for Serial communication
    Serial.begin(115200);

    //---//

    //...
}

//Run the firmware on loop

void loop() {

    //Loop methods. Needed to some libraries receive the processing loop from the Nano.
    //...

    //---//

    //Tests methods. Uncomment if you need to run some Component Test. Tests can return messages on Serial Monitor.
    //VoltageSensorTest(pin_voltageSensor);

    //Required for tests methods. Uncomment if you need to run some Component Test. Tests can return messages on Serial Monitor.
    //return;

    //---//

    //Read a possible Serial command, if exists on Buffer
    ReadSerialCommand();

    //Get current milliseconds time
    unsigned long currentTimeMs = millis();
    //If was reached the minimum time of Polling Rate for next output...
    if ((currentTimeMs - lastPollingTimeMs) >= pollingRateIntervalMs) {
        //If can emit output now, process the Joystick and emit output
        if (canEmitOutput == true)
            SendJoystickOutput();

        //Update the time of last Polling
        lastPollingTimeMs = currentTimeMs;
    }
}

//Auxiliar methods

void ReadSerialCommand() {
    //If don't have a valid command bytes to read, stop here
    if (Serial.available() < 3)
        return;

    //Get the first byte available in the Serial Buffer of Nano
    byte firstByte = Serial.read();

    //If not is a valid Header byte, indicating that have a Command and Payload following, stop here and pauses to wait for the next execution of this method to get the next byte in Buffer
    if (firstByte != PKT_HEADER)
        return;

    //Get all at once, the Command and Payload from Buffer of Nano
    CommandID command = static_cast<CommandID>(Serial.read());
    byte payload = Serial.read();

    //Process the sended Command
    switch (command) {
    case CommandID::GET_IDENTIFICATION:
        //Outputs the name of this device
        Serial.write((uint8_t*)DEVICE_ID_MSG, strlen(DEVICE_ID_MSG));
        break;

    case CommandID::START_OUTPUT:
        //Enable the Output
        canEmitOutput = true;
        break;

    case CommandID::STOP_OUTPUT:
        //Disable the Output
        canEmitOutput = false;
        break;

    case CommandID::SET_POLLING_RATE:
        //Set a new Polling Rate
        if (payload == 0x00)
            pollingRateIntervalMs = 500; //<- 2hz
        if (payload == 0x01)
            pollingRateIntervalMs = 22; //<- 45hz
        if (payload == 0x02)
            pollingRateIntervalMs = 16; //<- 60hz
        if (payload == 0x03)
            pollingRateIntervalMs = 11; //<- 90hz
        if (payload == 0x04)
            pollingRateIntervalMs = 8;  //<- 125hz
        break;

    default:
        break;
    }
}

void SendJoystickOutput() {
    //Build the packet of Joystick Output to be sent
    byte header1 = 0xAA;
    byte header2 = 0xFF;
    byte axisX = constrain(((analogRead(PIN_JOYSTICK_VRX) >> 2) - 1), 0, 254);   //<- The ">> 2" is "Bitwise Right Shift" that converts 10 bits to 8 bits, basicly dividing the number by 4
    byte axisY = constrain(((analogRead(PIN_JOYSTICK_VRY) >> 2) - 1), 0, 254);   //<- The ">> 2" is "Bitwise Right Shift" that converts 10 bits to 8 bits, basicly dividing the number by 4
    byte btnSw = (digitalRead(PIN_JOYSTICK_SW) == LOW) ? 1 : 0;

    //Send the packet through Serial
    Serial.write(header1);
    Serial.write(header2);
    Serial.write(axisX);
    Serial.write(axisY);
    Serial.write(btnSw);
}