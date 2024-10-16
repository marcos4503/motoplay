﻿using Avalonia;
using Avalonia.Threading;
using Coroutine;
using HarfBuzzSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
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
        public enum CommandsOfExtractionLoop
        {
            engineRpm,
            vehicleSpeed,
            engineLoad,
            coolantTemperature,
            batteryVoltage
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
        private bool abortObdAdapterInitializationThreadIfRunning = false;
        private bool abortObdDataExtractionThreadIfRunning = false;
        private List<double> lastPingMesurements = new List<double>();
        private long rpmInterpolationStartTime = DateTime.Now.Ticks;
        private long rpmInterpolationCurrentTime = DateTime.Now.Ticks;
        private long rpmInterpoltaionElpasedTime = 0;
        private int lastRpmSampleStoredForInterpolation = 0;
        private bool haveCalculedRpmSpeedRatioForGears = false;
        private float rpmSpeedRatioForGear1 = -1;
        private float rpmSpeedRatioForGear2 = -1;
        private float rpmSpeedRatioForGear3 = -1;
        private float rpmSpeedRatioForGear4 = -1;
        private float rpmSpeedRatioForGear5 = -1;
        private float rpmSpeedRatioForGear6 = -1;

        //Private variables
        private string rfcommSerialPortPath = "";
        private int pairedObdDeviceBaudRate = 0;
        private int channelToUseInRfcomm = -1;
        private string pairedObdDeviceName = "";
        private string pairedObdDeviceMac = "";
        private event ClassDelegates.OnReceiveAlertDialog onReceiveAlertDialog = null;
        private event ClassDelegates.OnReceiveLog onReceiveLog = null;
        private int rpmInterpolationSampleInterval = 0;
        private float rpmInterpolationAggressiveness = 0.0f;
        private int maxTransmissionGears = 0;
        private int minGear1RpmToChangeToGear2 = 0;
        private int minGear1SpeedToChangeToGear2 = 0;
        private int maxPossibleGear1Speed = 0;
        private int maxPossibleGear2Speed = 0;
        private int maxPossibleGear3Speed = 0;
        private int maxPossibleGear4Speed = 0;
        private int maxPossibleGear5Speed = 0;
        private int maxPossibleGear6Speed = 0;
        private int engineMaxRpm = 0;
        private event ClassDelegates.OnLostConnection onLostConnection = null;

        //Public variables
        public ConnectionStatus currentConnectionStatus = ConnectionStatus.None;
        public string disconnectionAdditionalInformation = "Additional Information. ";
        public bool isAdapterInitialized = false;
        public int adapterInitializationCurrentStep = 0;
        public int adapterInitializationMaxSteps = 14;
        public long commandResponseLosses = 0;
        public int sendAndReceivePing = 0;
        public int engineRpm = 0;
        public int engineRpmInterpolated = 0;
        public int vehicleSpeedKmh = 0;
        public int vehicleSpeedMph = 0;
        public float batteryVoltage = 0.0f;
        public int coolantTemperatureCelsius = 0;
        public int coolantTemperatureFarenheit = 0;
        public int engineLoad = 0;
        public int transmissionGear = 0;

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

        public void SetRpmInterpolationSamplesInterval(int intervalMs)
        {
            //Save the data
            this.rpmInterpolationSampleInterval = intervalMs;
        }

        public void SetRpmInterpolationAggressiveness(float aggressiveness)
        {
            //Save the data
            this.rpmInterpolationAggressiveness = aggressiveness;
        }

        public void SetMaxTransmissionGears(int gears)
        {
            //Save the data
            this.maxTransmissionGears = gears;
        }

        public void SetMinRpmToChangeFromGear1ToGear2(int rpm)
        {
            //Save the data
            this.minGear1RpmToChangeToGear2 = rpm;
        }

        public void SetMinSpeedToChangeFromGear1ToGear2(int speed)
        {
            //Save the data
            this.minGear1SpeedToChangeToGear2 = speed;
        }

        public void SetMaxPossibleSpeedAtGear(int gear, int speed)
        {
            //Save the data
            if (gear == 1)
                maxPossibleGear1Speed = speed;
            if (gear == 2)
                maxPossibleGear2Speed = speed;
            if (gear == 3)
                maxPossibleGear3Speed = speed;
            if (gear == 4)
                maxPossibleGear4Speed = speed;
            if (gear == 5)
                maxPossibleGear5Speed = speed;
            if (gear == 6)
                maxPossibleGear6Speed = speed;
        }

        public void SetEngineMaxRpm(int rpm)
        {
            //Save the data
            this.engineMaxRpm = rpm;
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

                //If the process continues running, but the Serial Port file in "/dev" not exists, means the process is still trying to connect, then, stops the attempt due to timeout
                if (rfcommCliProcess.HasExited == false && File.Exists(rfcommSerialPortPath) == false)
                {
                    AvaloniaDebug.WriteLine("The attempt to connect to Bluetooth Device \"" + pairedObdDeviceName + "\" was interrupted due to timeout.");
                    rfcommCliProcess.Kill();
                    rfcommReceivedOutputLines.Clear();
                    rfcommReceivedOutputLines.Add("Can't connect RFCOMM socket: Timeout");
                    AvaloniaDebug.WriteLine(("RFCOMM -> " + rfcommReceivedOutputLines[0]));
                }

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

                /* 
                 * If the process "rfcommCliProcess" finishes, means that the connection between the device and the Raspberry Pi has been broken, which
                 * automatically causes the "rfcommCliProcess" process to terminate.
                 * 
                 * This disconnection can occur if the "rfcommCliProcess" process is intentionally killed or if the device disconnects from the 
                 * Raspberry Pi. If the vehicle has its Ignition or ECU turned off, the OBD Adapter device also disconnects, ending the 
                 * "rfcommCliProcess" process.
                 */

                //If the "rfcommCliProcess" process was finished, while status is "connected", it means that the connection was lost. Run the lost connection code...
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

            //Send signal to stop the OBD Adapter Initialization thread, if running...
            abortObdAdapterInitializationThreadIfRunning = true;

            //Send signal to stop the OBD Data Extraction thread, if running...
            abortObdDataExtractionThreadIfRunning = true;

            //Try to release the Serial Port generated by the connection, after finished the OBD communication
            TryToReleaseTheSerialPort();
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
                SendAlertDialogMessage("Error", "Unable to open communication with Serial Port \""+ rfcommSerialPortPath +"\". Try restarting the Bluetooth OBD Adapter and Motoplay.\n\nE: \"" + e.Message + "\"");
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

                //Warn the user
                SendLogMessage("The Bluetooth OBD Adapter and communication with the ECU is being initialized...");

                //Send "AT D" command
                SendCommandToObdAdapterAndCatchResponse(CommandType.cmdAT, "AT D");
                adapterInitializationCurrentStep += 1;

                if (abortObdAdapterInitializationThreadIfRunning == true) { goto ThreadEndPoint; }   //<- If is required to abort, stop here!

                //Send "AT Z" command
                SendCommandToObdAdapterAndCatchResponse(CommandType.cmdAT, "AT Z");
                adapterInitializationCurrentStep += 1;

                if (abortObdAdapterInitializationThreadIfRunning == true) { goto ThreadEndPoint; }   //<- If is required to abort, stop here!

                //Send "AT E0" command
                SendCommandToObdAdapterAndCatchResponse(CommandType.cmdAT, "AT E0");
                adapterInitializationCurrentStep += 1;

                if (abortObdAdapterInitializationThreadIfRunning == true) { goto ThreadEndPoint; }   //<- If is required to abort, stop here!

                //Send "AT L0" command
                SendCommandToObdAdapterAndCatchResponse(CommandType.cmdAT, "AT L0");
                adapterInitializationCurrentStep += 1;

                if (abortObdAdapterInitializationThreadIfRunning == true) { goto ThreadEndPoint; }   //<- If is required to abort, stop here!

                //Send "AT S0" command
                SendCommandToObdAdapterAndCatchResponse(CommandType.cmdAT, "AT S0");
                adapterInitializationCurrentStep += 1;

                if (abortObdAdapterInitializationThreadIfRunning == true) { goto ThreadEndPoint; }   //<- If is required to abort, stop here!

                //Send "AT H0" command
                SendCommandToObdAdapterAndCatchResponse(CommandType.cmdAT, "AT H0");
                adapterInitializationCurrentStep += 1;

                if (abortObdAdapterInitializationThreadIfRunning == true) { goto ThreadEndPoint; }   //<- If is required to abort, stop here!

                //Send "AT ST FF" command
                SendCommandToObdAdapterAndCatchResponse(CommandType.cmdAT, "AT ST FF");
                adapterInitializationCurrentStep += 1;

                if (abortObdAdapterInitializationThreadIfRunning == true) { goto ThreadEndPoint; }   //<- If is required to abort, stop here!

                //Send "AT AT 1" command
                SendCommandToObdAdapterAndCatchResponse(CommandType.cmdAT, "AT AT 1");
                adapterInitializationCurrentStep += 1;

                if (abortObdAdapterInitializationThreadIfRunning == true) { goto ThreadEndPoint; }   //<- If is required to abort, stop here!

                //Send "AT SP 0" command
                SendCommandToObdAdapterAndCatchResponse(CommandType.cmdAT, "AT SP 0");
                adapterInitializationCurrentStep += 1;

                if (abortObdAdapterInitializationThreadIfRunning == true) { goto ThreadEndPoint; }   //<- If is required to abort, stop here!

                //Send "AT SS" command
                SendCommandToObdAdapterAndCatchResponse(CommandType.cmdAT, "AT SS");
                adapterInitializationCurrentStep += 1;

                if (abortObdAdapterInitializationThreadIfRunning == true) { goto ThreadEndPoint; }   //<- If is required to abort, stop here!

                //Send "AT RV" command
                SendCommandToObdAdapterAndCatchResponse(CommandType.cmdAT, "AT RV");
                adapterInitializationCurrentStep += 1;

                if (abortObdAdapterInitializationThreadIfRunning == true) { goto ThreadEndPoint; }   //<- If is required to abort, stop here!

                //Send "01 0C" command
                SendCommandToObdAdapterAndCatchResponse(CommandType.cmd01, "01 0C");
                adapterInitializationCurrentStep += 1;

                if (abortObdAdapterInitializationThreadIfRunning == true) { goto ThreadEndPoint; }   //<- If is required to abort, stop here!

                //Send "AT DP" command
                string response_atdp = SendCommandToObdAdapterAndCatchResponse(CommandType.cmdAT, "AT DP");
                adapterInitializationCurrentStep += 1;

                if (abortObdAdapterInitializationThreadIfRunning == true) { goto ThreadEndPoint; }   //<- If is required to abort, stop here!

                //Send "AT DPN" command
                string response_atdpn = SendCommandToObdAdapterAndCatchResponse(CommandType.cmdAT, "AT DPN");
                adapterInitializationCurrentStep += 1;

                //Show the founded procotol for ECU
                if (response_atdp != "" && response_atdpn != "")
                    SendLogMessage("The protocol \"" + response_atdp.Replace("AUTO, ", "") + "\", code \"" + response_atdpn.Replace("A", "") + "\" has been identified as the protocol to be used for the ECU.");

                //Warn the user
                SendLogMessage("ECU and Bluetooth OBD Adapter initialization completed successfully!");

                //Start the data extraction from Bluetooth OBD Adapter
                ExtractDataFromObdAdapter();

            //...



            //Prepare the end point of this Thread...
            ThreadEndPoint:
                abortObdAdapterInitializationThreadIfRunning = false;
                    AvaloniaDebug.WriteLine("OBD Adapter Initialization Thread aborted!");
            }).Start();
        }

        private void ExtractDataFromObdAdapter()
        {
            //Prepare the OBD Adapter Initialization Thread
            new Thread(() => {
                //Inform that is a background thread
                Thread.CurrentThread.IsBackground = true;

                //Warn the user
                SendLogMessage("The OBD Adapter Data Extraction Loop has started successfully!");

                //Prepare the queue of commands
                List<CommandsOfExtractionLoop> queueOfCommands = new List<CommandsOfExtractionLoop>();

                //Add a some initialization unique commands to queue
                queueOfCommands.AddRange(Enumerable.Repeat(CommandsOfExtractionLoop.engineLoad, 1));
                queueOfCommands.AddRange(Enumerable.Repeat(CommandsOfExtractionLoop.batteryVoltage, 1));
                queueOfCommands.AddRange(Enumerable.Repeat(CommandsOfExtractionLoop.coolantTemperature, 1));

            //Prepare the queue feed point
            QueueFeedPoint:

                //Feed the queue with commands
                queueOfCommands.AddRange(Enumerable.Repeat(CommandsOfExtractionLoop.vehicleSpeed, 1));        //Group 1
                queueOfCommands.AddRange(Enumerable.Repeat(CommandsOfExtractionLoop.engineRpm, 4));           //Group 1
                queueOfCommands.AddRange(Enumerable.Repeat(CommandsOfExtractionLoop.vehicleSpeed, 1));        //Group 1
                queueOfCommands.AddRange(Enumerable.Repeat(CommandsOfExtractionLoop.engineRpm, 4));           //Group 1
                queueOfCommands.AddRange(Enumerable.Repeat(CommandsOfExtractionLoop.batteryVoltage, 1));      //Group 1
                queueOfCommands.AddRange(Enumerable.Repeat(CommandsOfExtractionLoop.engineRpm, 4));           //Group 1
                queueOfCommands.AddRange(Enumerable.Repeat(CommandsOfExtractionLoop.vehicleSpeed, 1));          //Group 2
                queueOfCommands.AddRange(Enumerable.Repeat(CommandsOfExtractionLoop.engineRpm, 4));             //Group 2
                queueOfCommands.AddRange(Enumerable.Repeat(CommandsOfExtractionLoop.vehicleSpeed, 1));          //Group 2
                queueOfCommands.AddRange(Enumerable.Repeat(CommandsOfExtractionLoop.engineRpm, 4));             //Group 2
                queueOfCommands.AddRange(Enumerable.Repeat(CommandsOfExtractionLoop.coolantTemperature, 1));    //Group 2
                queueOfCommands.AddRange(Enumerable.Repeat(CommandsOfExtractionLoop.engineRpm, 4));             //Group 2
                queueOfCommands.AddRange(Enumerable.Repeat(CommandsOfExtractionLoop.vehicleSpeed, 1));        //Group 3
                queueOfCommands.AddRange(Enumerable.Repeat(CommandsOfExtractionLoop.engineRpm, 4));           //Group 3
                queueOfCommands.AddRange(Enumerable.Repeat(CommandsOfExtractionLoop.vehicleSpeed, 1));        //Group 3
                queueOfCommands.AddRange(Enumerable.Repeat(CommandsOfExtractionLoop.engineRpm, 4));           //Group 3
                queueOfCommands.AddRange(Enumerable.Repeat(CommandsOfExtractionLoop.engineLoad, 1));          //Group 3
                queueOfCommands.AddRange(Enumerable.Repeat(CommandsOfExtractionLoop.engineRpm, 4));           //Group 3

                //Start the loop to execute all commands of the queue
                while (queueOfCommands.Count > 0)
                {
                    //If have a signal to stop this thread, move to thread end point
                    if (abortObdDataExtractionThreadIfRunning == true)
                        goto ThreadEndPoint;

                    //Run the first command of queue
                    if (queueOfCommands[0] == CommandsOfExtractionLoop.engineRpm)
                    {
                        object rpm = GetInterpretedCommandResponse(SendCommandToObdAdapterAndCatchResponse(CommandType.cmd01, "01 0C"), "01 0C");
                        if (rpm != null)
                        {
                            engineRpm = (int)rpm;
                            UpdateEngineRpmInterpolated();
                        }
                    }
                    if (queueOfCommands[0] == CommandsOfExtractionLoop.vehicleSpeed)
                    {
                        object speed = GetInterpretedCommandResponse(SendCommandToObdAdapterAndCatchResponse(CommandType.cmd01, "01 0D"), "01 0D");
                        if (speed != null)
                        {
                            vehicleSpeedKmh = (int)speed;
                            vehicleSpeedMph = (int)((float)((int)speed) / 1.609344f);
                            UpdateTransmissionGear();
                        }
                    }
                    if (queueOfCommands[0] == CommandsOfExtractionLoop.batteryVoltage)
                    {
                        object voltage = GetInterpretedCommandResponse(SendCommandToObdAdapterAndCatchResponse(CommandType.cmdAT, "AT RV"), "AT RV");
                        if (voltage != null)
                            batteryVoltage = (float)voltage;
                    }
                    if (queueOfCommands[0] == CommandsOfExtractionLoop.coolantTemperature)
                    {
                        object temperature = GetInterpretedCommandResponse(SendCommandToObdAdapterAndCatchResponse(CommandType.cmd01, "01 05"), "01 05");
                        if (temperature != null)
                        {
                            coolantTemperatureCelsius = (int)temperature;
                            coolantTemperatureFarenheit = (int)(((float)((int)temperature) * 1.8f) + 32);
                        }
                    }
                    if (queueOfCommands[0] == CommandsOfExtractionLoop.engineLoad)
                    {
                        object load = GetInterpretedCommandResponse(SendCommandToObdAdapterAndCatchResponse(CommandType.cmd01, "01 04"), "01 04");
                        if (load != null)
                            engineLoad = (int)load;
                    }

                    //Remove the first command of queue
                    queueOfCommands.RemoveAt(0);
                }

                //If endend the loop, re-feed the queue of commands to start the loop again
                goto QueueFeedPoint;

            //...



            //Prepare the end point of this Thread...
            ThreadEndPoint:
                abortObdDataExtractionThreadIfRunning = false;
                AvaloniaDebug.WriteLine("OBD Data Extraction Thread aborted!");
            }).Start();
        }

        private void TryToReleaseTheSerialPort()
        {
            //Warn the user
            SendLogMessage("Closing communication with Serial Port \"" + rfcommSerialPortPath + "\"...");

            //Try to close the communication...
            try
            {
                /*
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
                */

                //If the Serial Port is open, close the communication. This is particularly important as it unlinks the Motoplay process from the port present in "/dev". So the system can get rid of it!
                if (rfcommActiveSerialPort.IsOpen == true)
                    rfcommActiveSerialPort.Close();
            }
            catch (Exception e)
            {
                //If have a problem, warn the user
                SendAlertDialogMessage("Error", "Unable to close communication with Serial Port \"" + rfcommSerialPortPath + "\". Try restarting the Bluetooth OBD Adapter and Motoplay.\n\nE: \"" + e.Message + "\"");
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

        private string SendCommandToObdAdapterAndCatchResponse(CommandType commandType, string command)
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
             * AT ST FF      -    Set responses timeout of 1000ms.                  -    "OK\r\r>"
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
             * "STOPPED"     -    It means that the AT command could not be executed or there was some other error. If this appears, it is best to disconnect the adapter, wait a few moments and try again.
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
             * "NODATA"      -    The ELM327 Adapter failed to initialize or is unable to communicate with the ECU. If this appears, it is best to disconnect the adapter, wait a few moments and try again.
             * "CAN ERROR"   -    The ELM327 Adapter failed to initialize or is unable to communicate with the ECU. If this appears, it is best to disconnect the adapter, wait a few moments and try again.
             * "?"           -    It means that the character "\r" at end, was missing in the command sent to the ELM327 Adapter.
            */

            //START OF "SEND COMMAND TO OBD ADAPTER" CODE...



            //Prepare the command to send
            string finalCommandToSend = "";

            //Pre-process the command
            if (commandType == CommandType.cmdAT)
                finalCommandToSend = (command + "\r").Replace(" ", "");
            if (commandType == CommandType.cmd01)
                finalCommandToSend = (command + " 1\r").Replace(" ", "");

            //Start ping messurement
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            //Send the command to connected Bluetooth OBD Adapter trough Serial Port
            try
            {
                //Clear the buffer
                rfcommActiveSerialPort.DiscardOutBuffer();
                rfcommActiveSerialPort.DiscardInBuffer();
                //Send the command
                rfcommActiveSerialPort.Write(finalCommandToSend);
            }
            catch (Exception e) {
                //Add to disconnection additional informations
                disconnectionAdditionalInformation += ("Fail on Send command to device through Serial Port: " + e.Message + ".");

                //Warn on debug
                AvaloniaDebug.WriteLine(("Fail on Send command to device through Serial Port: " + e.Message + " --- " + e.StackTrace));

                //If have some problem, it probably means the device is disconnected, so force disconnect and stop here
                ForceDisconnect();
                return "";
            }

            //Prepare the response to receive
            string finalCommandResponse = "";

            //Receive the command response from connected Bluetooth OBD Adapter trough Serial Port
            try
            {
                //Prepare the buffer and iteraction counter, to control the number of interactions of buffer reading
                long iteractionCounter = 0;
                string buffer = string.Empty;

                //Start the loop of buffer reading, until the end of response. The response ever ends on ">" character...
                while (new string[] { ">" }.Any(x => buffer.EndsWith(x)) == false)
                {
                    //Store the readed now
                    buffer += rfcommActiveSerialPort.ReadExisting();

                    //If is reached the max time of buffer reading, break the loop of buffer reading and register as responses loss
                    if (iteractionCounter >= 15000)
                    {
                        commandResponseLosses += 1;
                        buffer = string.Empty;
                        break;
                    }

                    //Wait 1ms and increase the iteraction counter
                    Thread.Sleep(1);
                    iteractionCounter += 1;
                }

                //Save the buffer into the final command response
                finalCommandResponse = buffer;
            }
            catch (Exception e)
            {
                //Add to disconnection additional informations
                disconnectionAdditionalInformation += ("Fail on Receive command from device through Serial Port: " + e.Message + ".");

                //Warn on debug
                AvaloniaDebug.WriteLine(("Fail on Receive command from device through Serial Port: " + e.Message + " --- " + e.StackTrace));

                //If have some problem, it probably means the device is disconnected, so force disconnect and stop here
                ForceDisconnect();
                return "";
            }

            //Stop the ping messurment
            stopwatch.Stop();

            //Add the result in the messurements list
            lastPingMesurements.Add(stopwatch.Elapsed.TotalMilliseconds);
            //If the list have more than 10 itens, remove the older one
            if (lastPingMesurements.Count > 10)
                lastPingMesurements.RemoveAt(0);
            //Calculate the average
            double pingsSum = 0;
            foreach (double item in lastPingMesurements)
                pingsSum += item;
            sendAndReceivePing = (int)(pingsSum / lastPingMesurements.Count);

            //Prepare the response to return
            string responseToReturn = "";

            //Detect if the response contains some error
            bool responseContainsError = false;
            string[] errorsStrings = new string[] { "?", "ERROR", "STOPPED", "BUS INIT...", "BUS INIT", "BUSINIT", "UNABLE TO CONNECT", "UNABLE", "NO DATA", "NODATA", "CAN ERROR", "CANERROR" };
            foreach (string errorString in errorsStrings)
                if (finalCommandResponse.Contains(errorString) == true)
                    responseContainsError = true;

            //If the response contains error...
            if (responseContainsError == true)
            {
                //Warn to user
                SendAlertDialogMessage("Error", 
                                      ("The connection with the Bluetooth OBD Adapter has been undone. Found a Error on Response obtained from Command \"" + 
                                       finalCommandToSend.Replace("\r", "").Replace(">", "") + "\": \"" + finalCommandResponse.Replace("\r", "").Replace(">", "") + "\"."));

                //Warn on debug
                AvaloniaDebug.WriteLine(("Found a Error on Response obtained from Command \"" + finalCommandToSend + "\": \"" + finalCommandResponse + "\". Disconnecting..."));

                //Force the disconnection
                ForceDisconnect();

                //Stop here
                return "";
            }

            //If the response not contains error...
            if (responseContainsError == false)
            {
                //If is a empty response, is a "command response loss"...
                if (finalCommandResponse.Length == 0)
                {
                    //Inform a empty string
                    responseToReturn = "";
                }

                //If not is a empty response...
                if (finalCommandResponse.Length != 0)
                {
                    //If is a response sended by the OBD Adapter...
                    if (finalCommandResponse[0] != '4' && finalCommandResponse[1] != '1' && finalCommandResponse[0] != '7' && finalCommandResponse[1] != 'F')
                    {
                        //Inform a post-processed string
                        responseToReturn = finalCommandResponse.Replace("\r", "").Replace(">", "");
                    }

                    //If is a response sended by the ECU, successfully processed by the ECU...
                    if (finalCommandResponse[0] == '4' && finalCommandResponse[1] == '1')
                    {
                        //Inform a post-processed string
                        //responseToReturn = finalCommandResponse.Remove(0, 4).Replace("\r", "").Replace(">", "");
                        responseToReturn = finalCommandResponse.Replace("\r", "").Replace(">", "");
                    }

                    //If is a response sended by the ECU, but not supported by the ECU...
                    if (finalCommandResponse[0] == '7' && finalCommandResponse[1] == 'F')
                    {
                        //Inform a empty string to return
                        responseToReturn = "";
                    }
                }
            }

            //Return the response
            return responseToReturn;
        }
    
        private object GetInterpretedCommandResponse(string commandResponse, string sourceCommand)
        {
            //Prepare the value to return
            object toReturn = null;

            //If the command response is empty (a command loss result or unsupported response sended by ECU) return null
            if (commandResponse == "")
            {
                SendLogMessage("CRI: Ununderstood response obtained from command \"" + sourceCommand + "\" sended to OBD: \"\". Does the connected ECU support source command?");
                return null;
            }
            //If the command don't contains at least 2 characters on start (eg: 41, 7F, etc) return null
            if (commandResponse.Length < 2)
            {
                SendLogMessage("CRI: Ununderstood response obtained from command \"" + sourceCommand + "\" sended to OBD: \"" + commandResponse + "\". No byte of information.");
                return null;
            }



            //If is a response sended by ECU...
            if (commandResponse[0] == '4' && commandResponse[1] == '1')
            {
                //If the response don't have at least 1 byte of information, 1 byte of command type, return null
                if (commandResponse.Length < 4)
                {
                    SendLogMessage("CRI: Ununderstood response obtained from command \"" + sourceCommand + "\" sended to OBD: \"" + commandResponse + "\". No byte of command origin.");
                    return null;
                }

                //Get the second byte of response, that indicates the command type
                string originCommand = commandResponse.Substring(2, 2);

                //If the origin command is a RPM command...
                if (originCommand == "0C")
                {
                    try
                    {
                        float rpmFloat = Convert.ToInt32(commandResponse.Replace(("410C"), ""), 16);
                        toReturn = (int)(rpmFloat / 4.0f);
                    }
                    catch (Exception e) 
                    {
                        SendLogMessage("CRI: Ununderstood response obtained from command \"" + sourceCommand + "\" sended to OBD: \"" + commandResponse + "\". Not parseable value.");
                        toReturn = null;
                    }
                }
                //If the origin command is a Coolant Temperature command...
                if (originCommand == "05")
                {
                    try 
                    {
                        int tempInt = Convert.ToInt32(commandResponse.Replace(("4105"), ""), 16);
                        toReturn = (int)(tempInt - 40);
                    }
                    catch (Exception e)
                    {
                        SendLogMessage("CRI: Ununderstood response obtained from command \"" + sourceCommand + "\" sended to OBD: \"" + commandResponse + "\". Not parseable value.");
                        toReturn = null;
                    }
                }
                //If the origin command is a Engine Load command...
                if (originCommand == "04")
                {
                    try 
                    {
                        float loadFloat = Convert.ToInt32(commandResponse.Replace(("4104"), ""), 16);
                        toReturn = (int)((loadFloat / 255.0f) * 100.0f);
                    }
                    catch (Exception e)
                    {
                        SendLogMessage("CRI: Ununderstood response obtained from command \"" + sourceCommand + "\" sended to OBD: \"" + commandResponse + "\". Not parseable value.");
                        toReturn = null;
                    }
                }
                //If the origin command is a Speed command...
                if (originCommand == "0D")
                {
                    try
                    {
                        int speedInt = Convert.ToInt32(commandResponse.Replace(("410D"), ""), 16);
                        toReturn = (int)speedInt;
                    }
                    catch (Exception e)
                    {
                        SendLogMessage("CRI: Ununderstood response obtained from command \"" + sourceCommand + "\" sended to OBD: \"" + commandResponse + "\". Not parseable value.");
                        toReturn = null;
                    }
                }

                //If the origin command could not be indentified, warn
                if (originCommand != "0C" && originCommand != "05" && originCommand != "04" && originCommand != "0D")
                {
                    SendLogMessage("CRI: Ununderstood response obtained from command \"" + sourceCommand + "\" sended to OBD: \"" + commandResponse + "\". Did not inform the origin command.");
                    toReturn = null;
                }
            }

            //If is a response sended by OBD Adapter
            if (commandResponse[0] != '4' && commandResponse[1] != '1')
            {
                //If is a voltage command response...
                if (commandResponse.ToUpper().Contains("V") == true)
                {
                    try
                    {
                        float voltageFloat = float.Parse(commandResponse.ToUpper().Replace("V", ""));
                        toReturn = (float)voltageFloat;
                    }
                    catch (Exception e)
                    {
                        SendLogMessage("CRI: Ununderstood response obtained from command \"" + sourceCommand + "\" sended to OBD: \"" + commandResponse + "\". Not parseable value.");
                        toReturn = null;
                    }
                }
            }

            //Return the value
            return toReturn;
        }
    
        private void UpdateEngineRpmInterpolated()
        {
            //Update current time
            rpmInterpolationCurrentTime = DateTime.Now.Ticks;

            //Get elapsed time in milliseconds
            rpmInterpoltaionElpasedTime += (long)(new TimeSpan(rpmInterpolationCurrentTime).TotalMilliseconds - new TimeSpan(rpmInterpolationStartTime).TotalMilliseconds);

            //Update start time
            rpmInterpolationStartTime = rpmInterpolationCurrentTime;

            //If elapsed more than the configured sample interval, store the current RPM and reset the elapsed time
            if (rpmInterpoltaionElpasedTime >= rpmInterpolationSampleInterval)
            {
                lastRpmSampleStoredForInterpolation = engineRpm;
                rpmInterpoltaionElpasedTime = 0;
            }
                
            //Generate the interpolated RPM
            int rpmDiffSinceLastSample = (engineRpm - lastRpmSampleStoredForInterpolation);
            int rpmInterpolationValue = (int)((float)rpmDiffSinceLastSample * rpmInterpolationAggressiveness);
            int finalInterpolatedRpm = (engineRpm + rpmInterpolationValue);
            engineRpmInterpolated = finalInterpolatedRpm;
        }
    
        private void UpdateTransmissionGear()
        {
            /*
             * The provision of the Gear by the ECUs was a new implementation in the OBD standard. For this reason, the vast majority of vehicles
             * do not emit the Current Gear signal through the OBD port. For this reason, it is more reliable to detect the current Gait through calculations.
             * 
             * After doing some tests, I developed my own method to make this calculation. By following the steps of these comments, it is possible to discover
             * the current March with very good accuracy.
             * 
             * First, we need to find the RPMxSpeed ​​Ratio of each Gear!
             * 
             * 1. First, learn the MAXIMUM SPEED (we'll call it "maxGearXSpeed") of each Gear in your Vehicle.
             * 2. Drive the Vehicle in Gear 1 and identify the MINIMUM RPM ("minGear1Rpm") and MINIMUM SPEED ("minGear1Speed") to shift from Gear 1 to Gear 2.
             * 3. Divide "maxGear1Speed" from Gear 1 by "minGear1Speed". We will call the result of this calculation "minMaxSpeedRatioGear1".
             * 4. Now, divide the "maxGearXSpeed" of each Gear by "minMaxSpeedRatioGear1". Now, you have the MINIMUM SPEED ("minGearXSpeed") of each Gear.
             * 5. Now to calculate the RPMxSpeed ​​Ratio of Gear 1, divide "minGear1Rpm" by "minGear1Speed" of Gear 1.
             * 6. Repeat Step 5 to calculate the RPMxSpeed ​​Ratio for each Gear in your vehicle.
             * 
             * Moving on to practice, let's use the Honda CB500F as an example.
             * 
             * 1. Step 1 Calculation...
             *    maxGear1Speed = 62km/h
             *    maxGear2Speed = 95km/h
             *    maxGear3Speed = 120km/h
             *    maxGear4Speed = 142km/h
             *    maxGear5Speed = 156km/h
             *    maxGear6Speed = 170km/h
             * 2. Step 2 Calculation...
             *    minGear1Rpm = 4000rpm
             *    minGear1Speed = 30km/h
             * 3. Step 3 Calculation...
             *    minMaxSpeedRatioGear1 = maxGear1Speed / minGear1Speed -> 2.07
             * 4. Step 4 Calculation...
             *    minGear1Speed = maxGear1Speed / minMaxSpeedRatioGear1 -> 62 / 2.07 ->  29.9km/h
             *    minGear2Speed = maxGear2Speed / minMaxSpeedRatioGear1 -> 95 / 2.07 ->  45.9km/h
             *    minGear3Speed = maxGear3Speed / minMaxSpeedRatioGear1 -> 120 / 2.07 -> 58.0km/h
             *    minGear4Speed = maxGear4Speed / minMaxSpeedRatioGear1 -> 142 / 2.07 -> 68.6km/h
             *    minGear5Speed = maxGear5Speed / minMaxSpeedRatioGear1 -> 156 / 2.07 -> 75.4km/h
             *    minGear6Speed = maxGear6Speed / minMaxSpeedRatioGear1 -> 170 / 2.07 -> 82.1km/h
             * 5. Step 5 and 6 Calculation...
             *    rpmXspeedRatioForGear1 = minGear1Rpm / minGear1Speed -> 4000 / 29.9 -> 133.8
             *    rpmXspeedRatioForGear2 = minGear1Rpm / minGear2Speed -> 4000 / 45.9 -> 87.2
             *    rpmXspeedRatioForGear3 = minGear1Rpm / minGear3Speed -> 4000 / 58.0 -> 69.0
             *    rpmXspeedRatioForGear4 = minGear1Rpm / minGear4Speed -> 4000 / 68.6 -> 58.3
             *    rpmXspeedRatioForGear5 = minGear1Rpm / minGear5Speed -> 4000 / 75.4 -> 53.0
             *    rpmXspeedRatioForGear6 = minGear1Rpm / minGear6Speed -> 4000 / 82.1 -> 48.7
             *    
             * Now, if we want to calculate to find out the Gear the Vehicle is in, we simply divide the CURRENT RPM ("currentRpm") by the CURRENT SPEED ("currentSpeed").
             * Then look at the RPMxSpeed ​​ratios to find out the Gear.
             * 
             * Example 1: The CB500F in the example is traveling at 45km/h at 5234 RPM.
             *            5234 / 45 = 116.3
             *            Since 116 is smaller than 133.8 and greather than 87.2, we have that the Vehicle is in Gear 2!
             * Example 2: The CB500F in the example is traveling at 143km/h at 4220 RPM.
             *            4220 / 143 = 29.5
             *            Since 29.5 is smaller than 48.7, we have that the Vehicle is in Gear 6!
             *            
             * Now you know how to use this "algorithm" to calculate the Vehicle's current Gears!
             * 
             * 
             * 
             * For example reference purposes, the example CB500F information table looks like this...
             * 
             * CB500F
             * Min RPM to change from Gear 1 to Gear 2:   4000rpm
             * Min Speed to change from Gear 1 to Gear 2: 30Km/h
             * Gear 1 Min and Max
             *   Min and Max Speed To Change To Next Gear:  30Km/h  - 62Km/h
             *   Min and Max RPM To Change To Next Gear:    4000rpm - 8500rpm
             * Gear 2 Min and Max
             *   Min and Max Speed To Change To Next Gear:  46Km/h  - 95Km/h
             *   Min and Max RPM To Change To Next Gear:    4000rpm - 8500rpm
             * Gear 3 Min and Max
             *   Min and Max Speed To Change To Next Gear:  58Km/h  - 120Km/h
             *   Min and Max RPM To Change To Next Gear:    4000rpm - 8500rpm
             * Gear 4 Min and Max
             *   Min and Max Speed To Change To Next Gear:  69Km/h  - 142Km/h
             *   Min and Max RPM To Change To Next Gear:    4000rpm - 8500rpm
             * Gear 5 Min and Max
             *   Min and Max Speed To Change To Next Gear:  75Km/h  - 156Km/h
             *   Min and Max RPM To Change To Next Gear:    4000rpm - 8500rpm
             * Gear 6 Min and Max
             *   Min and Max Speed To Change To Next Gear:  82Km/h  - 170Km/h
             *   Min and Max RPM To Change To Next Gear:    4000rpm - 8500rpm
            */

            //If not calculed the RPMxSpeed Ratio for gears yet, calcule
            if (haveCalculedRpmSpeedRatioForGears == false)
            {
                //Determine the "minMaxSpeedRatioGear1"
                float minMaxSpeedRatioGear1 = ((float)maxPossibleGear1Speed / (float)minGear1SpeedToChangeToGear2);

                //Determine the min speed for all gears
                float minSpeedForGear1 = ((float)maxPossibleGear1Speed / minMaxSpeedRatioGear1);
                float minSpeedForGear2 = ((float)maxPossibleGear2Speed / minMaxSpeedRatioGear1);
                float minSpeedForGear3 = ((float)maxPossibleGear3Speed / minMaxSpeedRatioGear1);
                float minSpeedForGear4 = ((float)maxPossibleGear4Speed / minMaxSpeedRatioGear1);
                float minSpeedForGear5 = ((float)maxPossibleGear5Speed / minMaxSpeedRatioGear1);
                float minSpeedForGear6 = ((float)maxPossibleGear6Speed / minMaxSpeedRatioGear1);

                //Determine the RPMxSpeed Ratio for each gear
                rpmSpeedRatioForGear1 = ((float)minGear1RpmToChangeToGear2 / minSpeedForGear1);
                rpmSpeedRatioForGear2 = ((float)minGear1RpmToChangeToGear2 / minSpeedForGear2);
                rpmSpeedRatioForGear3 = ((float)minGear1RpmToChangeToGear2 / minSpeedForGear3);
                rpmSpeedRatioForGear4 = ((float)minGear1RpmToChangeToGear2 / minSpeedForGear4);
                rpmSpeedRatioForGear5 = ((float)minGear1RpmToChangeToGear2 / minSpeedForGear5);
                rpmSpeedRatioForGear6 = ((float)minGear1RpmToChangeToGear2 / minSpeedForGear6);

                //Inform that is calculed
                haveCalculedRpmSpeedRatioForGears = true;
            }

            //Prepare the estiamted gear
            int estimatedGear = -1;

            //Determine the current RPM and Speed relation
            float currentRpmSpeedRelation = ((float) engineRpm / (float) vehicleSpeedKmh);

            //Determine the current estimated gear
            if (maxTransmissionGears <= 5)
            {
                if (currentRpmSpeedRelation > rpmSpeedRatioForGear1)
                    estimatedGear = 1;
                if (currentRpmSpeedRelation > rpmSpeedRatioForGear2 && currentRpmSpeedRelation <= rpmSpeedRatioForGear1)
                    estimatedGear = 2;
                if (currentRpmSpeedRelation > rpmSpeedRatioForGear3 && currentRpmSpeedRelation <= rpmSpeedRatioForGear2)
                    estimatedGear = 3;
                if (currentRpmSpeedRelation > rpmSpeedRatioForGear4 && currentRpmSpeedRelation <= rpmSpeedRatioForGear3)
                    estimatedGear = 4;
                if (currentRpmSpeedRelation <= rpmSpeedRatioForGear4)
                    estimatedGear = 5;
            }
            if (maxTransmissionGears == 6)
            {
                if (currentRpmSpeedRelation > rpmSpeedRatioForGear1)
                    estimatedGear = 1;
                if (currentRpmSpeedRelation > rpmSpeedRatioForGear2 && currentRpmSpeedRelation <= rpmSpeedRatioForGear1)
                    estimatedGear = 2;
                if (currentRpmSpeedRelation > rpmSpeedRatioForGear3 && currentRpmSpeedRelation <= rpmSpeedRatioForGear2)
                    estimatedGear = 3;
                if (currentRpmSpeedRelation > rpmSpeedRatioForGear4 && currentRpmSpeedRelation <= rpmSpeedRatioForGear3)
                    estimatedGear = 4;
                if (currentRpmSpeedRelation > rpmSpeedRatioForGear5 && currentRpmSpeedRelation <= rpmSpeedRatioForGear4)
                    estimatedGear = 5;
                if (currentRpmSpeedRelation <= rpmSpeedRatioForGear5)
                    estimatedGear = 6;
            }

            //If the vehicle is stopped, show gear 0 (neutral)
            if (vehicleSpeedKmh <= 10)
                estimatedGear = 0;
            //If the vehicle is not stopped, but the RPM is lower than 25%, show gear -1 (clutch pressed)
            if (vehicleSpeedKmh > 10 && engineRpm <= ((float)engineMaxRpm * 0.25f))
                estimatedGear = -1;

            //Update the gear
            transmissionGear = estimatedGear;
        }
    }
}