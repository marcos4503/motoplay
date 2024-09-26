using Avalonia;
using Avalonia.Threading;
using Coroutine;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Motoplay.Scripts
{
    /*
     * This class manage the OBD Connection, sending data, receiving data and managing the connection
    */

    public class ObdAdapterHandler
    {
        //Enums of script
        public enum ConnectionStatus
        {
            None,
            Connecting,
            Connected,
            Disconnected
        }
        public enum CommandType
        {
            cmdAT,
            cmd01
        }

        //Classes of script
        public class ClassDelegates
        {
            public delegate void OnLostConnection();
            public delegate void OnReceiveAlertDialog(string title, string message);
            public delegate void OnReceiveLog(string message);
        }

        //Cache variables
        private Process rfcommCliProcess = null;
        private List<string> rfcommReceivedOutputLines = new List<string>();
        private SerialPort rfcommActiveSerialPort = null;

        //Private variables
        private string rfcommSerialPortPath = "";
        private int pairedObdDeviceBaudRate = 0;
        private int channelToUseInRfcomm = -1;
        private string pairedObdDeviceName = "";
        private string pairedObdDeviceMac = "";
        private event ClassDelegates.OnLostConnection onLostConnection = null;
        private event ClassDelegates.OnReceiveAlertDialog onReceiveAlertDialog = null;
        private event ClassDelegates.OnReceiveLog onReceiveLog = null;

        //Public variables
        public ConnectionStatus currentConnectionStatus = ConnectionStatus.None;

        //Core methods

        public ObdAdapterHandler()
        {
            //Warn to debug
            AvaloniaDebug.WriteLine("Creating a new OBD Adapter Handler!");
        }

        //Setup methods

        public void SetRfcommSerialPortPath(string portPath)
        {
            //Save the data
            this.rfcommSerialPortPath = portPath;
        }

        public void SetPairedObdDeviceBaudRate(int baudRate)
        {
            //Save the data
            this.pairedObdDeviceBaudRate = baudRate;
        }

        public void SetChannelToUseInRfcomm(int channel)
        {
            //Save the data
            this.channelToUseInRfcomm = channel;
        }

        public void SetPairedObdDeviceName(string deviceName)
        {
            //Save the data
            this.pairedObdDeviceName = deviceName;
        }

        public void SetPairedObdDeviceMac(string deviceMac)
        {
            //Save the data
            this.pairedObdDeviceMac = deviceMac;
        }

        public void RegisterOnReceiveAlertDialogCallback(ClassDelegates.OnReceiveAlertDialog onReceiveAlertDialog)
        {
            //Register the callback
            this.onReceiveAlertDialog = onReceiveAlertDialog;
        }

        public void RegisterOnReceiveLogCallback(ClassDelegates.OnReceiveLog onReceiveLog)
        {
            //Register the callback
            this.onReceiveLog = onReceiveLog;
        }

        public void RegisterOnLostConnectionCallback(ClassDelegates.OnLostConnection onLostConnection)
        {
            //Register the callback
            this.onLostConnection = onLostConnection;
        }

        //Connection methods

        public void TryConnect()
        {
            //Start a new thread to start the connection and watch the maintenance of the connection, if have one
            new Thread(() => 
            {
                //Inform that is a background thread
                Thread.CurrentThread.IsBackground = true;

                //Change connection status to connecting
                currentConnectionStatus = ConnectionStatus.Connecting;

                //Warn to debug
                AvaloniaDebug.WriteLine("Tryng to connect to Bluetooth Device \"" + pairedObdDeviceName + "\" to Stablish a Serial Port...");

                //Wait time
                Thread.Sleep(250);

                //Create a new process for rfcomm
                rfcommCliProcess = new Process();
                ProcessStartInfo processStartInfo = new ProcessStartInfo();
                processStartInfo.FileName = "/usr/bin/sudo";
                processStartInfo.Arguments = ("/usr/bin/rfcomm connect " + rfcommSerialPortPath + " " + pairedObdDeviceMac + " " + channelToUseInRfcomm.ToString());
                processStartInfo.UseShellExecute = false;
                processStartInfo.CreateNoWindow = true;
                processStartInfo.RedirectStandardInput = true;
                processStartInfo.RedirectStandardOutput = true;
                processStartInfo.RedirectStandardError = true;
                rfcommCliProcess.StartInfo = processStartInfo;

                //Register receivers of all outputs from rfcomm
                rfcommCliProcess.ErrorDataReceived += new DataReceivedEventHandler((s, e) =>
                {
                    //If don't have data, cancel
                    if (String.IsNullOrEmpty(e.Data) == true)
                        return;

                    //Store the lines and resend to debug
                    rfcommReceivedOutputLines.Add(e.Data);
                    AvaloniaDebug.WriteLine(("RFCOMM -> " + e.Data));
                });
                rfcommCliProcess.OutputDataReceived += new DataReceivedEventHandler((s, e) =>
                {
                    //If don't have data, cancel
                    if (String.IsNullOrEmpty(e.Data) == true)
                        return;

                    //Store the lines and resend to debug
                    rfcommReceivedOutputLines.Add(e.Data);
                    AvaloniaDebug.WriteLine(("RFCOMM -> " + e.Data));
                });

                //Start the process and the connection
                rfcommCliProcess.Start();
                rfcommCliProcess.BeginOutputReadLine();
                rfcommCliProcess.BeginErrorReadLine();

                //Warn to debug
                AvaloniaDebug.WriteLine("Trying to connect to Bluetooth Device \"" + pairedObdDeviceName + "\"...");

                //Wait time to ensure a minimum response time
                Thread.Sleep(8000);

                //Prepare the connection try result info
                bool isConnectionSuccessfully = false;
                if (rfcommCliProcess.HasExited == true)   //<- If the process was finished, means the connection was not maintained (device offline or other connection errors)
                    isConnectionSuccessfully = false;
                if (rfcommCliProcess.HasExited == false)  //<- If the process continues running, means the connection was successful and is still active
                    isConnectionSuccessfully = true;

                //If is not connected
                if (isConnectionSuccessfully == false)
                {
                    //Change connection status to disconnected
                    currentConnectionStatus = ConnectionStatus.Disconnected;

                    //Warn to debug
                    AvaloniaDebug.WriteLine("Failed to connect to Bluetooth Device \"" + pairedObdDeviceName + "\"!");

                    //Run the internal hook
                    OnConnectionFailAndNotStablishedSerialPort();
                }

                //If is connected
                if (isConnectionSuccessfully == true)
                {
                    //Change connection status to connected
                    currentConnectionStatus = ConnectionStatus.Connected;

                    //Warn to debug
                    AvaloniaDebug.WriteLine("Connection success to Bluetooth Device \"" + pairedObdDeviceName + "\"! Establishing Serial Port...");

                    //Run the internal hook
                    OnConnectionSuccessAndStablishedSerialPort();
                }

                //Warn to debug
                AvaloniaDebug.WriteLine(("Connection try for Bluetooth Device \"" + pairedObdDeviceName + "\" was finished, with status " + currentConnectionStatus.ToString().ToUpper() + "."));





                //Wait until the process was finishes...
                rfcommCliProcess.WaitForExit();

                //If the status is connected, it means that the connection was lost along with the end of the process. Run the lost connection code
                if (currentConnectionStatus == ConnectionStatus.Connected)
                {
                    //Change connection status to disconnected
                    currentConnectionStatus = ConnectionStatus.Disconnected;

                    //Run on main thread all code of disconnection
                    Dispatcher.UIThread.Invoke(() =>
                    {
                        //Run callback, if have a registered
                        if (onLostConnection != null)
                            onLostConnection();

                        //Run the internal hook
                        OnLostConnection();

                    }, DispatcherPriority.MaxValue);

                    //Warn to debug
                    AvaloniaDebug.WriteLine("Disconnected from Bluetooth Device \"" + pairedObdDeviceName + "\" and finished Serial Port!");
                }

            }).Start();
        }

        public string[] GetConnectionTryLogs()
        {
            //Rerturn all logs returned by the rfcomm process of the Serial Port
            return rfcommReceivedOutputLines.ToArray();
        }

        public void ForceDisconnect()
        {
            //Kill the rfcomm process that mantain the socket of Serial Port. This Disconnection is done as if the device were disconnecting.
            rfcommCliProcess.Kill();
        }

        //Events methods

        private void OnConnectionSuccessAndStablishedSerialPort()
        {
            /*
             * NOTE: This method is called when the Bluetooth OBD Adapter is connected and a Serial Port is successfully generated.
            */

            //Try to use the Serial Port generated by the connection, before starting the OBD communication
            TryToUseSerialPort();
        }

        private void OnConnectionFailAndNotStablishedSerialPort()
        {
            /*
             * NOTE: This method is called when it is not possible to connect to the requested Bluetooth OBD Adapter, and consequently a Serial Port is not generated.
            */

            //...
        }

        private void OnLostConnection()
        {
            /*
             * NOTE: Disconnection may occur if the Bluetooth OBD Adapter is no longer available for connection (turned off) or if the "ForceDisconnect()" method is called.
             * 
             * Bluetooth OBD Adapters also disconnect if the vehicle's Ignition or ECU is turned off.
             * 
             * This method is called in case the connected Bluetooth OBD Adapter is disconnected due to any reason.
            */

            //...
        }

        //Internal methods

        private void TryToUseSerialPort()
        {
            //Warn the user about Serial Port
            SendLogMessage("The Serial Port \"" + rfcommSerialPortPath + "\" on channel \"" + channelToUseInRfcomm + "\" was created for Bluetooth OBD Adapter \"" + pairedObdDeviceName + "\"!");

            //Warn the user
            SendLogMessage("Initializing communication with Serial Port \"" + rfcommSerialPortPath + "\" with Baud Rate of \"" + pairedObdDeviceBaudRate + "\" bps...");

            //Initialize the connection to OBD Adapter Serial Port
            rfcommActiveSerialPort = new SerialPort();
            rfcommActiveSerialPort.PortName = rfcommSerialPortPath;
            rfcommActiveSerialPort.BaudRate = pairedObdDeviceBaudRate;
            rfcommActiveSerialPort.Parity = Parity.None;
            rfcommActiveSerialPort.Handshake = Handshake.None;
            //rfcommActiveSerialPort.DataBits = 8;
            //rfcommActiveSerialPort.StopBits = StopBits.One;
            //rfcommActiveSerialPort.RtsEnable = true;
            //rfcommActiveSerialPort.DtrEnable = true;
            rfcommActiveSerialPort.WriteTimeout = 250;
            rfcommActiveSerialPort.ReadTimeout = 250;

            //Warn the user
            SendLogMessage("Opening communication with Serial Port \"" + rfcommSerialPortPath + "\"...");

            //Try to open the communication...
            try
            {
                //Open the communication with the Serial Port
                rfcommActiveSerialPort.Open();

                //Warn the user
                SendLogMessage("Communication has been opened with Serial Port \"" + rfcommSerialPortPath + "\", successfully!");

                //Initialize the Bluetooth OBD Adapter
                InitializeTheObdAdapter();
            }
            catch (Exception e)
            {
                //If have a problem, warn the user and force disconnection
                SendAlertDialogMessage("Error", "Unable to open communication with Serial Port \"" + rfcommSerialPortPath + "\". Try restarting the Bluetooth OBD Adapter and Motoplay.");
                ForceDisconnect();
            }
        }

        private void InitializeTheObdAdapter()
        {
            //Prepare the OBD Adapter Initialization Thread
            new Thread(() => 
            {
                //Inform that is a background thread
                Thread.CurrentThread.IsBackground = true;


            }).Start();
        }

        private void ExtractDataFromObdAdapter()
        {

        }

        private void TryToReleaseTheSerialPort()
        {
            /*
             * NOTE: At the moment, this method is useless, since the Bluetooth OBD Adapter is automatically disconnected when the vehicle's ECU or Ignition is turned off. Furthermore, 
             * even in cases where the Application calls "ForceDisconnect()", the disconnection is instantaneous, closing the Bluetooth connection with the Bluetooth OBD Adapter and
             * closing the Serial Port created for it in the system. 
             * 
             * Due to these reasons, this method is only here for future reference needs.
            */

            //Try to close the communication...
            try
            {
                //If the Serial Port stills open, send the "Close Protocol" command for OBD Adapter
                if (rfcommActiveSerialPort.IsOpen == true)
                {
                    //Send the command
                    rfcommActiveSerialPort.DiscardOutBuffer();
                    rfcommActiveSerialPort.DiscardInBuffer();
                    rfcommActiveSerialPort.Write(("AT PC" + "\r"));

                    //Wait to ensure receive of the OBD Adapter
                    Thread.Sleep(50);
                }

                //If the Serial Port stills open, close the communication now
                if (rfcommActiveSerialPort.IsOpen == true)
                    rfcommActiveSerialPort.Close();
            }
            catch (Exception e)
            {

            }
        }

        //Auxiliar methods

        private void SendAlertDialogMessage(string title, string message)
        {
            //Send the message as debug message too
            AvaloniaDebug.WriteLine(message);

            //Run on main thread the alert dialog callback
            Dispatcher.UIThread.Invoke(() => { onReceiveAlertDialog(title, message); }, DispatcherPriority.MaxValue);
        }

        private void SendLogMessage(string message)
        {
            //Send the message as debug message too
            AvaloniaDebug.WriteLine(message);

            //Run on main thread the log callback
            Dispatcher.UIThread.Invoke(() => { onReceiveLog(message); }, DispatcherPriority.MaxValue);
        }

        private void SendCommandToObdAdapterAndCatchResponse(CommandType commandType, string command)
        {
            /*
             * TABLE WITH AT COMMANDS USED FOR INITIALIZE ANY ELM327 OBD ADAPTER
             * 
             * The table below shows all the AT commands that must be used to initialize an ELM327 OBD Adapter. The commands below are in the correct order of execution
             * for initializing any ELM327 OBD Adapter. Once all commands in the table below have been successfully executed on the OBD Adapter, it will be ready to request
             * data from the ECU.
             * 
             * Command            Description                                            Raw Response Example     
             *
             * AT D          -    Set all ELM327 parameters to Default.             -    "AT D\r\rOK\r\r>"
             * AT Z          -    Reset the state of the device.                    -    "AT Z\r\r\rELM327 v2.2\r\r>"
             * AT E0         -    Disable the command echo in responses.            -    "AT E0\rOK\r\r>"
             * AT L0         -    Disable the linefeed in responses.                -    "OK\r\r>"
             * AT S0         -    Disable spaces in responses.                      -    "OK\r\r>"
             * AT H0         -    Disable headers in responses.                     -    "OK\r\r>"
             * AT ST FF      -    Set responses timeout of 255ms.                   -    "OK\r\r>"
             * AT AT 1       -    Set Adaptive Timing to Normal Mode.               -    "OK\r\r>"
             * AT SP 0       -    Set to automatic ECU protocol detection.          -    "OK\r\r>"
             * AT SS         -    Set to Standard Searching Protocol order.         -    "OK\r\r>"
             * AT RV         -    Read the voltage of OBD-II port.                  -    "14.2V\r\r>"
             * 01 0C 1       -    Read the RPM from ECU, opening the communication. -    "SEARCHING...\r410C128C\r\r>"
             * AT DP         -    Show the protocol detected for ECU communication. -    "AUTO, ISO 14230-4 (KWP FAST)\r\r>"
             * AT DPN        -    Show the protocol code, detected for ECU.         -    "A5\r\r>"
             * 
             * The list below shows all the responses that may appear if any of the above commands are used in the ELM327 OBD Adapter.
             * 
             * "OK"          -    It means that the command was successfully interpreted and executed by the ELM327 Adapter.
             * "ERROR"       -    It means that the AT command could not be executed or there was some other error. If this appears, it is best to disconnect the adapter, wait a few moments and try again.
             * "?"           -    It means that the character "\r" at end, was missing in the command sent to the ELM327 Adapter.
             * 
             * 
             * 
             * 
             * 
             * TABLE WITH COMMON OBD PIDs (PARAMETER IDs) USED TO REQUEST DATA FROM ECUs
             * 
             * The table below shows all the common, standardized PIDs used to request data from the ECU, along with the equation used to convert the raw response into a 
             * human-readable value. All "Raw Response Examples" in the table below were obtained after initializing the ELM327 OBD Adapter correctly, with all the commands
             * in the table above.
             * 
             * NOTE: If the first Byte of the ECU response is "41", it means that the request was correctly interpreted by the ECU, while if the first Byte of the ECU 
             *       response is "7F", it means that the ECU does not support the request.
             * NOTE: The second Byte contained in the ECU responses, just shows you again the command you sent (Example: 0C, 0D, etc).
             * NOTE: Including the Hexadecimal "1" at the end of "01 HH" requests causes the ECU to stop responding when the first line is completed, which improves request
             *       response times.
             * NOTE: Sending commands without spaces can help optimize response times!
             * 
             * Command            Description                                             Raw Response Example        Response Conversion
             * 
             * 01 0C 1       -    Request the current RPM of engine.                 -    "410C1244\r\r>"        -    "RpmHex -> RpmFloat -> ResultFloat = (RpmFloat / 4.0f) -> ResultInt"
             * 01 05 1       -    Request the coolant liquid temperature in Celsius. -    "410589\r\r>"          -    "TempHex -> TempInt -> ResultInt = (TempInt - 40)"
             * 01 04 1       -    Request the engine load in percent.                -    "410451\r\r>"          -    "LoadHex -> LoadFloat -> ResultFloat = ((LoadFloat / 255.0f) * 100.0f) -> ResultInt"
             * 01 0D 1       -    Request the current vehicle speed in Km/h.         -    "410D00\r\r>"          -    "SpeedHex -> SpeedInt"
             * 
             * The list below shows all the responses that may appear if any of the above commands are used in the ELM327 OBD Adapter.
             * 
             * "41"          -    It means that the command was successfully interpreted and returned by the ECU (appears in the first Byte of the response).
             * "7F"          -    It means that the command is not supported by the ECU (appears in the first Byte of the response).
             * "SEARCHING..."-    It appears on the first command of type "01" sent to the ECU, while the ELM327 Adapter detects the protocol compatible with the ECU.
             * "ERROR"       -    The ELM327 Adapter failed to initialize or is unable to communicate with the ECU. If this appears, it is best to disconnect the adapter, wait a few moments and try again.
             * "STOPPED"     -    The ELM327 Adapter failed to initialize or is unable to communicate with the ECU. If this appears, it is best to disconnect the adapter, wait a few moments and try again.
             * "BUS INIT..." -    The ELM327 Adapter failed to initialize or is unable to communicate with the ECU. If this appears, it is best to disconnect the adapter, wait a few moments and try again.
             * "UNABLE TO CONNECT"The ELM327 Adapter failed to initialize or is unable to communicate with the ECU. If this appears, it is best to disconnect the adapter, wait a few moments and try again.
             * "NO DATA"     -    The ELM327 Adapter failed to initialize or is unable to communicate with the ECU. If this appears, it is best to disconnect the adapter, wait a few moments and try again.
             * "CAN ERROR"   -    The ELM327 Adapter failed to initialize or is unable to communicate with the ECU. If this appears, it is best to disconnect the adapter, wait a few moments and try again.
             * "?"           -    It means that the character "\r" at end, was missing in the command sent to the ELM327 Adapter.
            */

            //...
        }






























        private void Test()
        {
            //teste
            new Thread(() => 
            {
                /*
                //Send
                try
                {
                    serialPort.DiscardOutBuffer();
                    serialPort.DiscardInBuffer();
                    serialPort.Write(("AT Z" + "\r"));
                    AvaloniaDebug.WriteLine("TEST> COMMAND \"AT Z\" SENDED!");
                }
                catch (Exception e) {
                    AvaloniaDebug.WriteLine(("TEST> COMMAND \"AT Z\" EXCEPTION GENERIC.\n" + e.Message + "\n" + e.StackTrace));
                }

                //Receive
                string buffer = "";
                try
                {
                    int iteractionCount = 0;

                    buffer = string.Empty;
                    while (new string[] { ">" }.Any(x => buffer.EndsWith(x)) == false)
                    {
                        string received = serialPort.ReadExisting();
                        buffer += received;

                        AvaloniaDebug.WriteLine(("TEST> RECEIVING. ITERACTION " + iteractionCount));

                        Thread.Sleep(5);

                        iteractionCount += 1;
                    }
                    AvaloniaDebug.WriteLine("TEST> RECEIVED \"" + buffer.Replace("\r", "r").Replace("\n", "n") + "\"");
                }
                catch (Exception e) 
                {
                    buffer = string.Empty;
                    AvaloniaDebug.WriteLine(("TEST> RECEIVE EXCEPTION GENERIC.\n" + e.Message + "\n" + e.StackTrace));
                }
                */
            }).Start();
        }
    }
}