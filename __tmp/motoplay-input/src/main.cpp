//Include dependency files
#include <Arduino.h>
#include "Pins.h"
#include "CommandsIDs.h"

//Include optional test files
//#include "ComponentsTest.h"

//Initialize needed variables & libraries
unsigned long pollingRateInterval = 500;
unsigned long pollingRateLast = 0;

//Declare methods
void ReadSerialCommand();
void SendJoystickCommand();

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

    //Get current milliseconds time
    unsigned long currentTime = millis();

    //If was reached the minimum time of Polling Rate for next communication...
    if ((currentTime - pollingRateLast) >= pollingRateInterval) {
        //Send the current Joystick command...
        SendJoystickCommand();

        //Update the reference of time of last Polling
        pollingRateLast = currentTime;
    }

    //Read a possible Serial command, if exists
    ReadSerialCommand();
}

//Auxiliar methods

void ReadSerialCommand() {
    //If don't have a valid command bytes to read, stop here
    if (Serial.available() < 3)
        return;

    //Get the first byte available in the Serial Buffer
    byte firstByte = Serial.read();

    //If not is a valid Header byte, indicating that have a Command and Payload following, stop here and pauses to wait for the next execution of this method to get the next byte in Buffer
    if (firstByte != PKT_HEADER)
        return;

    //Get the Command and Payload from Buffer
    CommandID command = static_cast<CommandID>(Serial.read());
    byte payload = Serial.read();

    //Process the sended Command
    switch (command) {
    case CommandID::SET_POLLING_RATE:
        if (payload == 0x01)
            pollingRateInterval = 16; //<- 60hz
        if (payload == 0x02)
            pollingRateInterval = 11; //<- 90hz
        if (payload == 0x03)
            pollingRateInterval = 8;  //<- 125hz
        break;
    default:
        break;
    }
}

void SendJoystickCommand() {
    //Build the packet of Joystick Command to be sent
    byte header1 = 0xAA;
    byte header2 = 0xFF;
    byte axisX = analogRead(PIN_JOYSTICK_VRX) >> 2;             //<- The ">> 2" is "Bitwise Right Shift" that converts 10 bits to 8 bits, basicly dividing the number by 4
    byte axisY = analogRead(PIN_JOYSTICK_VRY) >> 2;             //<- The ">> 2" is "Bitwise Right Shift" that converts 10 bits to 8 bits, basicly dividing the number by 4
    byte btnSw = (digitalRead(PIN_JOYSTICK_SW) == LOW) ? 1 : 0;

    //Send the packet through Serial
    Serial.write(header1);
    Serial.write(header2);
    Serial.write(axisX);
    Serial.write(axisY);
    Serial.write(btnSw);
}