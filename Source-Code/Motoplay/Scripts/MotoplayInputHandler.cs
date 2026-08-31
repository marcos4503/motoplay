using Avalonia;
using Avalonia.Threading;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Threading;

namespace Motoplay.Scripts
{
    /*
     * This class manage the Motoplay Input , sending data, receiving data and managing the connection
    */

    public class MotoplayInputHandler
    {
        //Private constant variables
        private const int MOTOPLAY_INPUT_BAUDRATE = 115200;
        private const float MIN_TIME_TO_WARN_FEEDBACK_OF_HOLD = 300.0f;
        private const float MIN_TIME_TO_CONSIDER_LONG_TAP = 1200.0f;

        //Enums of script
        public enum ConnectionStatus
        {
            None,
            Connecting,
            Connected,
            Disconnected
        }
        public enum PollingRate
        {
            hz45,
            hz60,
            hz90,
            hz125
        }

        //Classes of script
        public class ClassDelegates
        {
            public delegate void OnUpdateConnectionState(ConnectionStatus connectionStatus);
            public delegate void OnLongTapProgressUp(float progress);
            public delegate void OnSimpleTapProgressUp();
            public delegate void OnLongTapProgressRight(float progress);
            public delegate void OnSimpleTapProgressRight();
            public delegate void OnLongTapProgressDown(float progress);
            public delegate void OnSimpleTapProgressDown();
            public delegate void OnLongTapProgressLeft(float progress);
            public delegate void OnSimpleTapProgressLeft();
            public delegate void OnLongTapProgressClick(float progress);
            public delegate void OnSimpleTapProgressClick();
            public delegate void OnLongTapUp_Start();
            public delegate void OnLongTapUp_Tick(float deltaTimeMs);
            public delegate void OnLongTapUp_End();
            public delegate void OnSimpleTapUp();
            public delegate void OnLongTapRight_Start();
            public delegate void OnLongTapRight_Tick(float deltaTimeMs);
            public delegate void OnLongTapRight_End();
            public delegate void OnSimpleTapRight();
            public delegate void OnLongTapDown_Start();
            public delegate void OnLongTapDown_Tick(float deltaTimeMs);
            public delegate void OnLongTapDown_End();
            public delegate void OnSimpleTapDown();
            public delegate void OnLongTapLeft_Start();
            public delegate void OnLongTapLeft_Tick(float deltaTimeMs);
            public delegate void OnLongTapLeft_End();
            public delegate void OnSimpleTapLeft();
            public delegate void OnLongTapClick_Start();
            public delegate void OnLongTapClick_Tick(float deltaTimeMs);
            public delegate void OnLongTapClick_End();
            public delegate void OnSimpleTapClick();
        }
        public class RingBuffer
        {
            //Cache variables
            private readonly byte[] rawBuffer = null;
            private int readPointer = 0;
            private int readPointerNextValidBytes = 0;
            private int writePointer = 0;

            //Core methods
            public RingBuffer(int bufferSize)
            {
                //Initialize this Buffer
                this.rawBuffer = new byte[bufferSize];
            }

            public int GetFreeBytesSize()
            {
                //Return count of bytes free to be writen
                return (rawBuffer.Length - readPointerNextValidBytes);
            }

            public void Write(byte[] source, int offset, int length)
            {
                //If don't have free space to write, stop here
                if (GetFreeBytesSize() < length)
                    throw new Exception("Not enough space on Ring Buffer. Needed " + length + " bytes, but the Ring Buffer only have " + GetFreeBytesSize() + " bytes.");
                //Write the souce bytes on raw buffer
                for (int i = 0; i < length; i++)
                {
                    rawBuffer[writePointer] = source[offset + i];
                    writePointer = ((writePointer + 1) % rawBuffer.Length);
                    readPointerNextValidBytes += 1;
                }
            }

            public int GetUnreadedBytesSize()
            {
                //Return count of bytes unreaded
                return (readPointerNextValidBytes);
            }

            public byte Read()
            {
                //If dont have bytes unreaded, stop here
                if (GetUnreadedBytesSize() < 1)
                    throw new Exception("No unreaded byte available on Ring Buffer.");

                //Prepare the value to return
                byte toReturn = 0x00;

                //Extract the first byte unreaded on raw buffer
                toReturn = rawBuffer[(readPointer % rawBuffer.Length)];
                //Consume the first byte unreaded, now, readed
                readPointer = ((readPointer + 1) % rawBuffer.Length);
                readPointerNextValidBytes -= 1;

                //Return the value
                return toReturn;
            }
        }

        //Cache variables
        private bool isInputBlockedNow = false;
        private long inputCooldownTimeMs = 0;
        private long inputCooldownTimeMsMax = 0;
        private DateTime lastPollingTime = DateTime.Now;
        private float lastAxisX = 0.0f;
        private float lastAxisY = 0.0f;
        private bool lastBtnDwn = false;
        private long axisUpHoldingTimeMs = 0;
        private bool axisUpHoldingNow = false;
        private long axisRightHoldingTimeMs = 0;
        private bool axisRightHoldingNow = false;
        private long axisDownHoldingTimeMs = 0;
        private bool axisDownHoldingNow = false;
        private long axisLeftHoldingTimeMs = 0;
        private bool axisLeftHoldingNow = false;
        private long axisClickHoldingTimeMs = 0;
        private bool axisClickHoldingNow = false;

        //Private variables
        private string systemCurrentUsername = "";
        private ConnectionStatus connectionStatus = ConnectionStatus.None;
        private event ClassDelegates.OnUpdateConnectionState onUpdateConnectionState = null;
        private PollingRate pollingRate = PollingRate.hz60;
        private bool invertXbyY = false;
        private bool invertAxisX = false;
        private bool invertAxisY = false;
        private float deadZonePercent = 0.0f;
        private event ClassDelegates.OnLongTapProgressUp onLongTapProgressUp = null;
        private event ClassDelegates.OnSimpleTapProgressUp onSimpleTapProgressUp = null;
        private event ClassDelegates.OnLongTapProgressRight onLongTapProgressRight = null;
        private event ClassDelegates.OnSimpleTapProgressRight onSimpleTapProgressRight = null;
        private event ClassDelegates.OnLongTapProgressDown onLongTapProgressDown = null;
        private event ClassDelegates.OnSimpleTapProgressDown onSimpleTapProgressDown = null;
        private event ClassDelegates.OnLongTapProgressLeft onLongTapProgressLeft = null;
        private event ClassDelegates.OnSimpleTapProgressLeft onSimpleTapProgressLeft = null;
        private event ClassDelegates.OnLongTapProgressClick onLongTapProgressClick = null;
        private event ClassDelegates.OnSimpleTapProgressClick onSimpleTapProgressClick = null;
        private event ClassDelegates.OnLongTapUp_Start onLongTapUp_Start = null;
        private event ClassDelegates.OnLongTapUp_Tick onLongTapUp_Tick = null;
        private event ClassDelegates.OnLongTapUp_End onLongTapUp_End = null;
        private event ClassDelegates.OnSimpleTapUp onSimpleTapUp = null;
        private event ClassDelegates.OnLongTapRight_Start onLongTapRight_Start = null;
        private event ClassDelegates.OnLongTapRight_Tick onLongTapRight_Tick = null;
        private event ClassDelegates.OnLongTapRight_End onLongTapRight_End = null;
        private event ClassDelegates.OnSimpleTapRight onSimpleTapRight = null;
        private event ClassDelegates.OnLongTapDown_Start onLongTapDown_Start = null;
        private event ClassDelegates.OnLongTapDown_Tick onLongTapDown_Tick = null;
        private event ClassDelegates.OnLongTapDown_End onLongTapDown_End = null;
        private event ClassDelegates.OnSimpleTapDown onSimpleTapDown = null;
        private event ClassDelegates.OnLongTapLeft_Start onLongTapLeft_Start = null;
        private event ClassDelegates.OnLongTapLeft_Tick onLongTapLeft_Tick = null;
        private event ClassDelegates.OnLongTapLeft_End onLongTapLeft_End = null;
        private event ClassDelegates.OnSimpleTapLeft onSimpleTapLeft = null;
        private event ClassDelegates.OnLongTapClick_Start onLongTapClick_Start = null;
        private event ClassDelegates.OnLongTapClick_Tick onLongTapClick_Tick = null;
        private event ClassDelegates.OnLongTapClick_End onLongTapClick_End = null;
        private event ClassDelegates.OnSimpleTapClick onSimpleTapClick = null;

        //Core methods

        public MotoplayInputHandler()
        {
            //Warn to debug
            AvaloniaDebug.WriteLine("Creating a new Motoplay Input Handler!");
        }

        public void SetInputBlocked(bool blockedNow)
        {
            //Update the parameter
            this.isInputBlockedNow = blockedNow;
        }

        public void SetInputCooldown(long cooldownTimeMs)
        {
            //Update the value
            this.inputCooldownTimeMsMax = cooldownTimeMs;
        }

        public void ForceCallOfOnUpdateConnectionStateCallbackAsDisconnected()
        {
            //Send a fake callback of disconnection
            Dispatcher.UIThread.Invoke(() => { onUpdateConnectionState(ConnectionStatus.Disconnected); }, DispatcherPriority.MaxValue);
        }

        //Setup methods

        public void SetSystemCurrentUsername(string username)
        {
            //Store the username info
            this.systemCurrentUsername = username;
        }

        public void RegisterOnUpdateConnectionStateCallback(ClassDelegates.OnUpdateConnectionState onUpdateConnectionState)
        {
            //Register the callback
            this.onUpdateConnectionState = onUpdateConnectionState;
        }

        public void SetPollingRate(PollingRate pollingRate)
        {
            //Set the desired Polling Rate
            this.pollingRate = pollingRate;
        }

        public void SetAxisXbyY(bool invertXbyY)
        {
            //Set the axis inversion
            this.invertXbyY = invertXbyY;
        }

        public void SetInvertAxisX(bool invertAxisX)
        {
            //Set X Axis inversion
            this.invertAxisX = invertAxisX;
        }

        public void SetInvertAxisY(bool invertAxisY)
        {
            //Set Y Axis inversion
            this.invertAxisY = invertAxisY;
        }

        public void SetDeadZonePercent(float deadZonePercent)
        {
            //Set dead zone in percent
            this.deadZonePercent = deadZonePercent;
        }

        public void RegisterOnLongTapProgressUpCallback(ClassDelegates.OnLongTapProgressUp onLongTapProgressUp)
        {
            //Register the callback
            this.onLongTapProgressUp = onLongTapProgressUp;
        }

        public void RegisterOnSimpleTapProgressUpCallback(ClassDelegates.OnSimpleTapProgressUp onSimpleTapProgressUp)
        {
            //Register the callback
            this.onSimpleTapProgressUp = onSimpleTapProgressUp;
        }

        public void RegisterOnLongTapProgressRightCallback(ClassDelegates.OnLongTapProgressRight onLongTapProgressRight)
        {
            //Register the callback
            this.onLongTapProgressRight = onLongTapProgressRight;
        }

        public void RegisterOnSimpleTapProgressRightCallback(ClassDelegates.OnSimpleTapProgressRight onSimpleTapProgressRight)
        {
            //Register the callback
            this.onSimpleTapProgressRight = onSimpleTapProgressRight;
        }

        public void RegisterOnLongTapProgressDownCallback(ClassDelegates.OnLongTapProgressDown onLongTapProgressDown)
        {
            //Register the callback
            this.onLongTapProgressDown = onLongTapProgressDown;
        }

        public void RegisterOnSimpleTapProgressDownCallback(ClassDelegates.OnSimpleTapProgressDown onSimpleTapProgressDown)
        {
            //Register the callback
            this.onSimpleTapProgressDown = onSimpleTapProgressDown;
        }

        public void RegisterOnLongTapProgressLeftCallback(ClassDelegates.OnLongTapProgressLeft onLongTapProgressLeft)
        {
            //Register the callback
            this.onLongTapProgressLeft = onLongTapProgressLeft;
        }

        public void RegisterOnSimpleTapProgressLeftCallback(ClassDelegates.OnSimpleTapProgressLeft onSimpleTapProgressLeft)
        {
            //Register the callback
            this.onSimpleTapProgressLeft = onSimpleTapProgressLeft;
        }

        public void RegisterOnLongTapProgressClickCallback(ClassDelegates.OnLongTapProgressClick onLongTapProgressClick)
        {
            //Register the callback
            this.onLongTapProgressClick = onLongTapProgressClick;
        }

        public void RegisterOnSimpleTapProgressClickCallback(ClassDelegates.OnSimpleTapProgressClick onSimpleTapProgressClick)
        {
            //Register the callback
            this.onSimpleTapProgressClick = onSimpleTapProgressClick;
        }

        public void RegisterNewInputReceiver(UpEvents up, RightEvents right, DownEvents down, LeftEvents left, ClickEvents click)
        {
            //Clear old events
            this.onLongTapUp_Start = null;
            this.onLongTapUp_Tick = null;
            this.onLongTapUp_End = null;
            this.onSimpleTapUp = null;
            this.onLongTapRight_Start = null;
            this.onLongTapRight_Tick = null;
            this.onLongTapRight_End = null;
            this.onSimpleTapRight = null;
            this.onLongTapDown_Start = null;
            this.onLongTapDown_Tick = null;
            this.onLongTapDown_End = null;
            this.onSimpleTapDown = null;
            this.onLongTapLeft_Start = null;
            this.onLongTapLeft_Tick = null;
            this.onLongTapLeft_End = null;
            this.onSimpleTapLeft = null;
            this.onLongTapClick_Start = null;
            this.onLongTapClick_Tick = null;
            this.onLongTapClick_End = null;
            this.onSimpleTapClick = null;

            //Regiser new events
            this.onLongTapUp_Start = up.onLongTapUp_Start;
            this.onLongTapUp_Tick = up.onLongTapUp_Tick;
            this.onLongTapUp_End = up.onLongTapUp_End;
            this.onSimpleTapUp = up.onSimpleTapUp;
            this.onLongTapRight_Start = right.onLongTapRight_Start;
            this.onLongTapRight_Tick = right.onLongTapRight_Tick;
            this.onLongTapRight_End = right.onLongTapRight_End;
            this.onSimpleTapRight = right.onSimpleTapRight;
            this.onLongTapDown_Start = down.onLongTapDown_Start;
            this.onLongTapDown_Tick = down.onLongTapDown_Tick;
            this.onLongTapDown_End = down.onLongTapDown_End;
            this.onSimpleTapDown = down.onSimpleTapDown;
            this.onLongTapLeft_Start = left.onLongTapLeft_Start;
            this.onLongTapLeft_Tick = left.onLongTapLeft_Tick;
            this.onLongTapLeft_End = left.onLongTapLeft_End;
            this.onSimpleTapLeft = left.onSimpleTapLeft;
            this.onLongTapClick_Start = click.onLongTapClick_Start;
            this.onLongTapClick_Tick = click.onLongTapClick_Tick;
            this.onLongTapClick_End = click.onLongTapClick_End;
            this.onSimpleTapClick = click.onSimpleTapClick;
        }

        //Connection methods

        public void StartHandler()
        {
            //Start a new thread to start the connection and watch the maintenance of the connection, if have one
            new Thread(() =>
            {
                //Inform that is a background thread
                Thread.CurrentThread.IsBackground = true;

                //Prepare the connection start checkpoint
            TryConnectStart:

                //Reset the cache data
                lastPollingTime = DateTime.Now;
                //Process this polling input as empty, to reset remaing old inputs
                ProcessMotoplayInputPolling(false, 0, 0, false);

                //Change connection status to disconnected
                connectionStatus = ConnectionStatus.Disconnected;
                Dispatcher.UIThread.Invoke(() => { onUpdateConnectionState(connectionStatus); }, DispatcherPriority.MaxValue);

                //Wait some time
                Thread.Sleep(1000);

                //Warn to debug
                AvaloniaDebug.WriteLine("Fetching available Serial Ports on system...");
                //Get all Serial Port availables on system
                string[] availableSerialPorts = SerialPort.GetPortNames();
                //Warn Serial Ports available
                AvaloniaDebug.WriteLine("Found " + availableSerialPorts.Length + " Serial Ports.");
                foreach (string item in availableSerialPorts)
                    AvaloniaDebug.WriteLine("- " + item);
                //Prepare a reference for the Serial Port of the Motoplay Input device
                SerialPort motoplayInputSerial = null;
                string motoplayInputUsbHubId = "";

                //Inform that was connecting to Motoplay Input device
                connectionStatus = ConnectionStatus.Connecting;
                Dispatcher.UIThread.Invoke(() => { onUpdateConnectionState(connectionStatus); }, DispatcherPriority.MaxValue);
                //Interact with each Serial Port until find the Motoplay Input device
                foreach (string serialPortName in availableSerialPorts)
                {
                    //Warn about connection try
                    AvaloniaDebug.WriteLine("Analyzing: " + serialPortName);
                    //Create the Serial Port to current device and try to open connection to it
                    SerialPort tmpSerialPort = new SerialPort();
                    tmpSerialPort.PortName = serialPortName;
                    tmpSerialPort.BaudRate = MOTOPLAY_INPUT_BAUDRATE;
                    tmpSerialPort.Parity = Parity.None;
                    tmpSerialPort.Handshake = Handshake.None;
                    tmpSerialPort.DataBits = 8;
                    tmpSerialPort.DtrEnable = true;
                    tmpSerialPort.RtsEnable = false;
                    tmpSerialPort.WriteTimeout = 250;
                    tmpSerialPort.ReadTimeout = 250;
                    //Try to connect to current device...
                    try
                    {
                        //Try to open a connection with the current device
                        tmpSerialPort.Open();
                        //Wait a bit
                        Thread.Sleep(3000);  //<- Wait 3 seconds in case of the UART of the Motoplay Input be slow
                        //Send a command to stop any Output emission   (if is a Motoplay Input, it will stop)
                        tmpSerialPort.Write(new byte[] { 0xAA, 03, 00 }, 0, 3);
                        //Wait a bit
                        Thread.Sleep(100);
                        //Clear the Buffer of this device
                        tmpSerialPort.DiscardInBuffer();
                        //Wait a bit
                        Thread.Sleep(100);
                        //Send a command to Output identity            (if is a Motoplay Input, it will reply)
                        tmpSerialPort.Write(new byte[] { 0xAA, 01, 00 }, 0, 3);
                        //Wait a bit
                        Thread.Sleep(500);
                        //Get the possible output existing
                        if (tmpSerialPort.BytesToRead >= 64)
                            throw new Exception("The device is not a Motoplay Input. (Too long ID)");
                        string identityOutput = tmpSerialPort.ReadExisting();
                        //If the identity don't match...
                        if (identityOutput != "Motoplay Input - by marcos4503")
                            throw new Exception("The device is not a Motoplay Input. (ID: \"" + identityOutput + "\")");
                        //Detect the USB Hub ID of the Motoplay Input device
                        string tmpSerialPortHubId = GetSerialDeviceUSBHubID(serialPortName).Replace("\r", "").Replace("\n", "").Replace(" ", "").Split(".")[0];
                        //Set the Polling Rate
                        if (pollingRate == PollingRate.hz45)
                            tmpSerialPort.Write(new byte[] { 0xAA, 0x04, 0x01 }, 0, 3);
                        if (pollingRate == PollingRate.hz60)
                            tmpSerialPort.Write(new byte[] { 0xAA, 0x04, 0x02 }, 0, 3);
                        if (pollingRate == PollingRate.hz90)
                            tmpSerialPort.Write(new byte[] { 0xAA, 0x04, 0x03 }, 0, 3);
                        if (pollingRate == PollingRate.hz125)
                            tmpSerialPort.Write(new byte[] { 0xAA, 0x04, 0x04 }, 0, 3);
                        //Wait a bit
                        Thread.Sleep(100);
                        //Send a command to start Output emission
                        tmpSerialPort.Write(new byte[] { 0xAA, 02, 00 }, 0, 3);
                        //Warn that is connected
                        AvaloniaDebug.WriteLine("Connected to Motoplay Input device!");
                        //Store a reference for this Serial Port
                        motoplayInputSerial = tmpSerialPort;
                        motoplayInputUsbHubId = tmpSerialPortHubId;
                    }
                    catch (Exception ex)
                    {
                        //Warn about error
                        AvaloniaDebug.WriteLine("Error on analyze Serial Port: " + ex.Message);
                        //If the temp Serial Port is open, close it
                        if (tmpSerialPort != null && tmpSerialPort.IsOpen == true)
                            try { tmpSerialPort.Close(); } catch (Exception exCl) { } finally { tmpSerialPort.Dispose(); }
                    }
                    //If was found the Motoplay Input device, break this loop
                    if (motoplayInputSerial != null)
                        break;
                    //Wait before go to next iteraction
                    Thread.Sleep(500);
                }
                //If analyzed all Serial Ports found, but, not found a Motoplay Input device, go to connection start checkpoint, again
                if (motoplayInputSerial == null)
                    goto TryConnectStart;

                //Inform that was connected to Motoplay Input device
                connectionStatus = ConnectionStatus.Connected;
                Dispatcher.UIThread.Invoke(() => { onUpdateConnectionState(connectionStatus); }, DispatcherPriority.MaxValue);

                //Reset the last time of polling
                lastPollingTime = DateTime.Now;

                //Prepare a in app Work Buffer to read bytes without relying on "SerialPort.BytesToRead()" because of low reliability on Linux Kernel
                RingBuffer appWorkBuffer = new RingBuffer(256);
                byte[] itrctBytesOfKernelBuffer = new byte[256];
                //Prepare a interaction counter with no packets
                int interactionsWithNoPacketsCount = 0;
                //Prepare a byte array to store interaction data
                byte[] itrctPktHeaderBytes = new byte[2];
                byte[] itrctPktContentBytes = new byte[3];
                //Start the loop of Motoplay Input read
                while (motoplayInputSerial.IsOpen == true)
                {
                    //Try to continue listening the Motoplay Input device...
                    try
                    {
                        //First, flushes the Kernel Buffer, transferring all possible data, into this self-managed in app Work Buffer
                        if (appWorkBuffer.GetFreeBytesSize() > 0)
                            try
                            {
                                //Get the possible from Kernel Buffer, to fullfill the Work Buffer
                                int bytesReadedCount = motoplayInputSerial.Read(itrctBytesOfKernelBuffer, 0, appWorkBuffer.GetFreeBytesSize());
                                if (bytesReadedCount > 0)
                                    appWorkBuffer.Write(itrctBytesOfKernelBuffer, 0, bytesReadedCount);
                            }
                            catch (TimeoutException timeOutException)
                            {
                                //Warn about this Time Out
                                AvaloniaDebug.WriteLine("Motoplay Input device Time Out. Trying to restart the USB Port...");
                                //This is a Time Out while reading the Kernel Buffer. On Linux, this means that the Serial Port was probaly freezed, so, simulate a device re-connection on USB Port
                                ForceRestartOfUSBHubAndPorts(motoplayInputUsbHubId);
                            }

                        //If don't have a valid packet size on Work Buffer...
                        if (appWorkBuffer.GetUnreadedBytesSize() < 5)
                        {
                            //Wait a bit before next interaction to avoid Thread freeze and go to next interaction
                            Thread.Sleep(1);
                            //Increase the interactions counter with no complete packets
                            interactionsWithNoPacketsCount += 1;
                            //If the interactions with no packets was reached the limit, break this loop. This is a protection in case of fail of "SerialPort.IsOpen()" of .NET)
                            if (interactionsWithNoPacketsCount >= 128)
                                throw new Exception("Time out.");
                            //Continue to next interaction
                            continue;
                        }
                        //Reset the interactions counter with no complete packets
                        interactionsWithNoPacketsCount = 0;

                        //Read the in app Work Buffer to extract the input data...
                        if (appWorkBuffer.GetUnreadedBytesSize() >= 5)
                        {
                            //Get the first byte of the suposed packet, the "part 1 of the header"
                            itrctPktHeaderBytes[0] = appWorkBuffer.Read();
                            //If the suposed "part 1 of the header" is not valid, skip to next iteraction...
                            if (itrctPktHeaderBytes[0] != 0xAA)
                                continue;
                            //Get the second byte of the suposed packet, the "part 2 of the header"
                            itrctPktHeaderBytes[1] = appWorkBuffer.Read();
                            //If the suposed "part 2 of the header" is not valid, skip to next iteraction...
                            if (itrctPktHeaderBytes[1] != 0xFF)
                                continue;

                            //Get the rest of the packet, now 100% validated
                            itrctPktContentBytes[0] = appWorkBuffer.Read();
                            itrctPktContentBytes[1] = appWorkBuffer.Read();
                            itrctPktContentBytes[2] = appWorkBuffer.Read();

                            //Process this polling input
                            ProcessMotoplayInputPolling(true, (itrctPktContentBytes[0] - 127), (itrctPktContentBytes[1] - 127), ((itrctPktContentBytes[2] == 0x00) ? false : true));
                        }
                    }
                    catch (Exception ex)
                    {
                        //Warn about the error
                        AvaloniaDebug.WriteLine("Problem on listen Motoplay Input device: " + ex.Message);
                        //Process this polling input as empty, to reset remaing old inputs
                        ProcessMotoplayInputPolling(false, 0, 0, false);
                        //Break this loop
                        break;
                    }
                }

                //Warn that was diconnected
                AvaloniaDebug.WriteLine("Disconnected of Motoplay Input device!");
                //Now that the connection with Motoplay Input device was finished, clear the reference and go to start of connection
                try { motoplayInputSerial.Close(); } catch (Exception exCl) { } finally { motoplayInputSerial.Dispose(); }
                goto TryConnectStart;

            }).Start();
        }

        private string GetSerialDeviceUSBHubID(string serialPortName)
        {
            //Prepare the value to return
            string toReturn = "";

            //If is running on Linux...
            if (OperatingSystem.IsLinux() == true)
            {
                //Get the Serial Port file name
                string serialPortFileName = Path.GetFileName(serialPortName);

                //Setup a Terminal and send the command
                Process terminalProcess = new Process();
                terminalProcess.StartInfo.FileName = "/bin/bash";
                terminalProcess.StartInfo.Arguments = ("-c \"basename $(readlink -f /sys/class/tty/" + serialPortFileName + "/device/../..)\"");
                terminalProcess.StartInfo.WorkingDirectory = (@"/home/" + systemCurrentUsername);
                terminalProcess.StartInfo.UseShellExecute = false;
                terminalProcess.StartInfo.CreateNoWindow = true;
                terminalProcess.StartInfo.RedirectStandardInput = true;
                terminalProcess.StartInfo.RedirectStandardOutput = true;
                terminalProcess.StartInfo.RedirectStandardError = true;
                terminalProcess.Start();
                string output = terminalProcess.StandardOutput.ReadToEnd();
                string error = terminalProcess.StandardError.ReadToEnd();
                terminalProcess.WaitForExit();
                //Get the device USB Hub ID
                toReturn = output;
            }

            //Return the value
            return toReturn;
        }

        private void ForceRestartOfUSBHubAndPorts(string usbHubId)
        {
            //If is not running on Linux, stop here
            if (OperatingSystem.IsLinux() == false)
                return;

            //If the USB Hub ID is empty, stop here
            if (string.IsNullOrEmpty(usbHubId) == true)
                return;

            //Setup a Terminal and send the command
            Process terminalProcess = new Process();
            terminalProcess.StartInfo.FileName = "/bin/bash";
            terminalProcess.StartInfo.Arguments = ("-c \"sudo uhubctl -l " + usbHubId + " -a cycle\"");
            terminalProcess.StartInfo.WorkingDirectory = (@"/home/" + systemCurrentUsername);
            terminalProcess.StartInfo.UseShellExecute = false;
            terminalProcess.StartInfo.CreateNoWindow = true;
            terminalProcess.StartInfo.RedirectStandardInput = true;
            terminalProcess.StartInfo.RedirectStandardOutput = true;
            terminalProcess.StartInfo.RedirectStandardError = true;
            terminalProcess.Start();
            string output = terminalProcess.StandardOutput.ReadToEnd();
            string error = terminalProcess.StandardError.ReadToEnd();
            terminalProcess.WaitForExit();
        }

        //Auxiliar methods

        private void ProcessMotoplayInputPolling(bool isInputConnected, float joystickAxisX, float joystickAxisY, bool joystickButtonDown)
        {
            //Allocate the fixed axis input
            float fixedAxisX = 0.0f;
            float fixedAxisY = 0.0f;

            //Pre-process the Input
            if (invertXbyY == false)
            {
                fixedAxisX = ((invertAxisX == false) ? joystickAxisX : (joystickAxisX * -1.0f));
                fixedAxisY = ((invertAxisY == false) ? joystickAxisY : (joystickAxisY * -1.0f));
            }
            if (invertXbyY == true)
            {
                fixedAxisY = ((invertAxisX == false) ? joystickAxisX : (joystickAxisX * -1.0f));
                fixedAxisX = ((invertAxisY == false) ? joystickAxisY : (joystickAxisY * -1.0f));
            }
            //Apply dead zone rule
            if (Math.Abs(fixedAxisX) < (128.0f * deadZonePercent))
                fixedAxisX = 0.0f;
            if (Math.Abs(fixedAxisY) < (128.0f * deadZonePercent))
                fixedAxisY = 0.0f;

            //Avoid the received Input, if is not allowed now
            if (isInputBlockedNow == true || inputCooldownTimeMs < inputCooldownTimeMsMax)
            {
                fixedAxisX = 0.0f;
                fixedAxisY = 0.0f;
                joystickButtonDown = false;
            }

            //----- Input Processment -----//

            //Get the current time info
            DateTime currentTime = DateTime.Now;
            long deltaTimeMs = (long)((currentTime - lastPollingTime).TotalMilliseconds);
            if (deltaTimeMs < 0)
                deltaTimeMs = 0;



            //Axis Up: Simple Tap Detection
            if (lastAxisX > 0.0f && fixedAxisX <= 0.0f)
                if (axisUpHoldingNow == false)
                {
                    if (onSimpleTapUp != null)
                        Dispatcher.UIThread.Invoke(() => { onSimpleTapUp(); });
                    Dispatcher.UIThread.Invoke(() => { onSimpleTapProgressUp(); });
                    inputCooldownTimeMs = 0;
                }
            //Axis Up: Long Tap Detection
            if (lastAxisX > 0.0f && fixedAxisX > 0.0f)
            {
                axisUpHoldingTimeMs += deltaTimeMs;
                if (axisUpHoldingTimeMs >= MIN_TIME_TO_WARN_FEEDBACK_OF_HOLD)
                    Dispatcher.UIThread.Invoke(() => { onLongTapProgressUp(Math.Clamp(((axisUpHoldingTimeMs - MIN_TIME_TO_WARN_FEEDBACK_OF_HOLD) / MIN_TIME_TO_CONSIDER_LONG_TAP), 0.0f, 1.0f)); }, DispatcherPriority.MaxValue);
                if (axisUpHoldingTimeMs >= (MIN_TIME_TO_WARN_FEEDBACK_OF_HOLD + MIN_TIME_TO_CONSIDER_LONG_TAP))
                {
                    if (axisUpHoldingNow == false)
                    {
                        if (onLongTapUp_Start != null)
                            Dispatcher.UIThread.Invoke(() => { onLongTapUp_Start(); });
                        axisUpHoldingNow = true;
                    }
                    if (onLongTapUp_Tick != null)
                        Dispatcher.UIThread.Invoke(() => { onLongTapUp_Tick(deltaTimeMs); });
                }
            }
            if (lastAxisX > 0.0f && fixedAxisX <= 0.0f)
            {
                axisUpHoldingTimeMs = 0;
                Dispatcher.UIThread.Invoke(() => { onLongTapProgressUp(-1.0f); }, DispatcherPriority.MaxValue);
                if (axisUpHoldingNow == true)
                {
                    if (onLongTapUp_End != null)
                        Dispatcher.UIThread.Invoke(() => { onLongTapUp_End(); });
                    inputCooldownTimeMs = 0;
                    axisUpHoldingNow = false;
                }
            }



            //Axis Down: Simple Tap Detection
            if (lastAxisX < 0.0f && fixedAxisX >= 0.0f)
                if (axisDownHoldingNow == false)
                {
                    if (onSimpleTapDown != null)
                        Dispatcher.UIThread.Invoke(() => { onSimpleTapDown(); });
                    Dispatcher.UIThread.Invoke(() => { onSimpleTapProgressDown(); });
                    inputCooldownTimeMs = 0;
                }
            //Axis Down: Long Tap Detection
            if (lastAxisX < 0.0f && fixedAxisX < 0.0f)
            {
                axisDownHoldingTimeMs += deltaTimeMs;
                if (axisDownHoldingTimeMs >= MIN_TIME_TO_WARN_FEEDBACK_OF_HOLD)
                    Dispatcher.UIThread.Invoke(() => { onLongTapProgressDown(Math.Clamp(((axisDownHoldingTimeMs - MIN_TIME_TO_WARN_FEEDBACK_OF_HOLD) / MIN_TIME_TO_CONSIDER_LONG_TAP), 0.0f, 1.0f)); }, DispatcherPriority.MaxValue);
                if (axisDownHoldingTimeMs >= (MIN_TIME_TO_WARN_FEEDBACK_OF_HOLD + MIN_TIME_TO_CONSIDER_LONG_TAP))
                {
                    if (axisDownHoldingNow == false)
                    {
                        if (onLongTapDown_Start != null)
                            Dispatcher.UIThread.Invoke(() => { onLongTapDown_Start(); });
                        axisDownHoldingNow = true;
                    }
                    if (onLongTapDown_Tick != null)
                        Dispatcher.UIThread.Invoke(() => { onLongTapDown_Tick(deltaTimeMs); });
                }
            }
            if (lastAxisX < 0.0f && fixedAxisX >= 0.0f)
            {
                axisDownHoldingTimeMs = 0;
                Dispatcher.UIThread.Invoke(() => { onLongTapProgressDown(-1.0f); }, DispatcherPriority.MaxValue);
                if (axisDownHoldingNow == true)
                {
                    if (onLongTapDown_End != null)
                        Dispatcher.UIThread.Invoke(() => { onLongTapDown_End(); });
                    inputCooldownTimeMs = 0;
                    axisDownHoldingNow = false;
                }
            }



            //Axis Left: Simple Tap Detection
            if (lastAxisY > 0.0f && fixedAxisY <= 0.0f)
                if (axisLeftHoldingNow == false)
                {
                    if (onSimpleTapLeft != null)
                        Dispatcher.UIThread.Invoke(() => { onSimpleTapLeft(); });
                    Dispatcher.UIThread.Invoke(() => { onSimpleTapProgressLeft(); });
                    inputCooldownTimeMs = 0;
                }
            //Axis Left: Long Tap Detection
            if (lastAxisY > 0.0f && fixedAxisY > 0.0f)
            {
                axisLeftHoldingTimeMs += deltaTimeMs;
                if (axisLeftHoldingTimeMs >= MIN_TIME_TO_WARN_FEEDBACK_OF_HOLD)
                    Dispatcher.UIThread.Invoke(() => { onLongTapProgressLeft(Math.Clamp(((axisLeftHoldingTimeMs - MIN_TIME_TO_WARN_FEEDBACK_OF_HOLD) / MIN_TIME_TO_CONSIDER_LONG_TAP), 0.0f, 1.0f)); }, DispatcherPriority.MaxValue);
                if (axisLeftHoldingTimeMs >= (MIN_TIME_TO_WARN_FEEDBACK_OF_HOLD + MIN_TIME_TO_CONSIDER_LONG_TAP))
                {
                    if (axisLeftHoldingNow == false)
                    {
                        if (onLongTapLeft_Start != null)
                            Dispatcher.UIThread.Invoke(() => { onLongTapLeft_Start(); });
                        axisLeftHoldingNow = true;
                    }
                    if (onLongTapLeft_Tick != null)
                        Dispatcher.UIThread.Invoke(() => { onLongTapLeft_Tick(deltaTimeMs); });
                }
            }
            if (lastAxisY > 0.0f && fixedAxisY <= 0.0f)
            {
                axisLeftHoldingTimeMs = 0;
                Dispatcher.UIThread.Invoke(() => { onLongTapProgressLeft(-1.0f); }, DispatcherPriority.MaxValue);
                if (axisLeftHoldingNow == true)
                {
                    if (onLongTapLeft_End != null)
                        Dispatcher.UIThread.Invoke(() => { onLongTapLeft_End(); });
                    inputCooldownTimeMs = 0;
                    axisLeftHoldingNow = false;
                }
            }



            //Axis Right: Simple Tap Detection
            if (lastAxisY < 0.0f && fixedAxisY >= 0.0f)
                if (axisRightHoldingNow == false)
                {
                    if (onSimpleTapRight != null)
                        Dispatcher.UIThread.Invoke(() => { onSimpleTapRight(); });
                    Dispatcher.UIThread.Invoke(() => { onSimpleTapProgressRight(); });
                    inputCooldownTimeMs = 0;
                }
            //Axis Right: Long Tap Detection
            if (lastAxisY < 0.0f && fixedAxisY < 0.0f)
            {
                axisRightHoldingTimeMs += deltaTimeMs;
                if (axisRightHoldingTimeMs >= MIN_TIME_TO_WARN_FEEDBACK_OF_HOLD)
                    Dispatcher.UIThread.Invoke(() => { onLongTapProgressRight(Math.Clamp(((axisRightHoldingTimeMs - MIN_TIME_TO_WARN_FEEDBACK_OF_HOLD) / MIN_TIME_TO_CONSIDER_LONG_TAP), 0.0f, 1.0f)); }, DispatcherPriority.MaxValue);
                if (axisRightHoldingTimeMs >= (MIN_TIME_TO_WARN_FEEDBACK_OF_HOLD + MIN_TIME_TO_CONSIDER_LONG_TAP))
                {
                    if (axisRightHoldingNow == false)
                    {
                        if (onLongTapRight_Start != null)
                            Dispatcher.UIThread.Invoke(() => { onLongTapRight_Start(); });
                        axisRightHoldingNow = true;
                    }
                    if (onLongTapRight_Tick != null)
                        Dispatcher.UIThread.Invoke(() => { onLongTapRight_Tick(deltaTimeMs); });
                }
            }
            if (lastAxisY < 0.0f && fixedAxisY >= 0.0f)
            {
                axisRightHoldingTimeMs = 0;
                Dispatcher.UIThread.Invoke(() => { onLongTapProgressRight(-1.0f); }, DispatcherPriority.MaxValue);
                if (axisRightHoldingNow == true)
                {
                    if (onLongTapRight_End != null)
                        Dispatcher.UIThread.Invoke(() => { onLongTapRight_End(); });
                    inputCooldownTimeMs = 0;
                    axisRightHoldingNow = false;
                }
            }



            //Axis Click: Simple Tap Detection
            if (lastBtnDwn == true && joystickButtonDown == false)
                if (axisClickHoldingNow == false)
                {
                    if (onSimpleTapClick != null)
                        Dispatcher.UIThread.Invoke(() => { onSimpleTapClick(); });
                    Dispatcher.UIThread.Invoke(() => { onSimpleTapProgressClick(); });
                    inputCooldownTimeMs = 0;
                }
            //Axis Click: Long Tap Detection
            if (lastBtnDwn == true && joystickButtonDown == true)
            {
                axisClickHoldingTimeMs += deltaTimeMs;
                if (axisClickHoldingTimeMs >= MIN_TIME_TO_WARN_FEEDBACK_OF_HOLD)
                    Dispatcher.UIThread.Invoke(() => { onLongTapProgressClick(Math.Clamp(((axisClickHoldingTimeMs - MIN_TIME_TO_WARN_FEEDBACK_OF_HOLD) / MIN_TIME_TO_CONSIDER_LONG_TAP), 0.0f, 1.0f)); }, DispatcherPriority.MaxValue);
                if (axisClickHoldingTimeMs >= (MIN_TIME_TO_WARN_FEEDBACK_OF_HOLD + MIN_TIME_TO_CONSIDER_LONG_TAP))
                {
                    if (axisClickHoldingNow == false)
                    {
                        if (onLongTapClick_Start != null)
                            Dispatcher.UIThread.Invoke(() => { onLongTapClick_Start(); });
                        axisClickHoldingNow = true;
                    }
                    if (onLongTapClick_Tick != null)
                        Dispatcher.UIThread.Invoke(() => { onLongTapClick_Tick(deltaTimeMs); });
                }
            }
            if (lastBtnDwn == true && joystickButtonDown == false)
            {
                axisClickHoldingTimeMs = 0;
                Dispatcher.UIThread.Invoke(() => { onLongTapProgressClick(-1.0f); }, DispatcherPriority.MaxValue);
                if (axisClickHoldingNow == true)
                {
                    if (onLongTapClick_End != null)
                        Dispatcher.UIThread.Invoke(() => { onLongTapClick_End(); });
                    inputCooldownTimeMs = 0;
                    axisClickHoldingNow = false;
                }
            }



            //Increase the Input Cooldown, if is needed
            if (inputCooldownTimeMs <= inputCooldownTimeMsMax)
                inputCooldownTimeMs += deltaTimeMs;
            //Store the current input as last input
            lastPollingTime = currentTime;
            lastAxisX = fixedAxisX;
            lastAxisY = fixedAxisY;
            lastBtnDwn = joystickButtonDown;
        }
    }

    public class UpEvents
    {
        //Public variables
        public MotoplayInputHandler.ClassDelegates.OnLongTapUp_Start onLongTapUp_Start { get; } = null;
        public MotoplayInputHandler.ClassDelegates.OnLongTapUp_Tick onLongTapUp_Tick { get; } = null;
        public MotoplayInputHandler.ClassDelegates.OnLongTapUp_End onLongTapUp_End { get; } = null;
        public MotoplayInputHandler.ClassDelegates.OnSimpleTapUp onSimpleTapUp { get; } = null;

        //Core methods
        public UpEvents(MotoplayInputHandler.ClassDelegates.OnLongTapUp_Start onLongTapUp_Start,
                        MotoplayInputHandler.ClassDelegates.OnLongTapUp_Tick onLongTapUp_Tick,
                        MotoplayInputHandler.ClassDelegates.OnLongTapUp_End onLongTapUp_End,
                        MotoplayInputHandler.ClassDelegates.OnSimpleTapUp onSimpleTapUp)
        {
            this.onLongTapUp_Start = onLongTapUp_Start;
            this.onLongTapUp_Tick = onLongTapUp_Tick;
            this.onLongTapUp_End = onLongTapUp_End;
            this.onSimpleTapUp = onSimpleTapUp;
        }
    }

    public class RightEvents
    {
        //Public variables
        public MotoplayInputHandler.ClassDelegates.OnLongTapRight_Start onLongTapRight_Start { get; } = null;
        public MotoplayInputHandler.ClassDelegates.OnLongTapRight_Tick onLongTapRight_Tick { get; } = null;
        public MotoplayInputHandler.ClassDelegates.OnLongTapRight_End onLongTapRight_End { get; } = null;
        public MotoplayInputHandler.ClassDelegates.OnSimpleTapRight onSimpleTapRight { get; } = null;

        //Core methods
        public RightEvents(MotoplayInputHandler.ClassDelegates.OnLongTapRight_Start onLongTapRight_Start,
                           MotoplayInputHandler.ClassDelegates.OnLongTapRight_Tick onLongTapRight_Tick,
                           MotoplayInputHandler.ClassDelegates.OnLongTapRight_End onLongTapRight_End,
                           MotoplayInputHandler.ClassDelegates.OnSimpleTapRight onSimpleTapRight)
        {
            this.onLongTapRight_Start = onLongTapRight_Start;
            this.onLongTapRight_Tick = onLongTapRight_Tick;
            this.onLongTapRight_End = onLongTapRight_End;
            this.onSimpleTapRight = onSimpleTapRight;
        }
    }

    public class DownEvents
    {
        //Public variables
        public MotoplayInputHandler.ClassDelegates.OnLongTapDown_Start onLongTapDown_Start { get; } = null;
        public MotoplayInputHandler.ClassDelegates.OnLongTapDown_Tick onLongTapDown_Tick { get; } = null;
        public MotoplayInputHandler.ClassDelegates.OnLongTapDown_End onLongTapDown_End { get; } = null;
        public MotoplayInputHandler.ClassDelegates.OnSimpleTapDown onSimpleTapDown { get; } = null;

        //Core methods
        public DownEvents(MotoplayInputHandler.ClassDelegates.OnLongTapDown_Start onLongTapDown_Start,
                          MotoplayInputHandler.ClassDelegates.OnLongTapDown_Tick onLongTapDown_Tick,
                          MotoplayInputHandler.ClassDelegates.OnLongTapDown_End onLongTapDown_End,
                          MotoplayInputHandler.ClassDelegates.OnSimpleTapDown onSimpleTapDown)
        {
            this.onLongTapDown_Start = onLongTapDown_Start;
            this.onLongTapDown_Tick = onLongTapDown_Tick;
            this.onLongTapDown_End = onLongTapDown_End;
            this.onSimpleTapDown = onSimpleTapDown;
        }
    }

    public class LeftEvents
    {
        //Public variables
        public MotoplayInputHandler.ClassDelegates.OnLongTapLeft_Start onLongTapLeft_Start { get; } = null;
        public MotoplayInputHandler.ClassDelegates.OnLongTapLeft_Tick onLongTapLeft_Tick { get; } = null;
        public MotoplayInputHandler.ClassDelegates.OnLongTapLeft_End onLongTapLeft_End { get; } = null;
        public MotoplayInputHandler.ClassDelegates.OnSimpleTapLeft onSimpleTapLeft { get; } = null;

        //Core methods
        public LeftEvents(MotoplayInputHandler.ClassDelegates.OnLongTapLeft_Start onLongTapLeft_Start,
                          MotoplayInputHandler.ClassDelegates.OnLongTapLeft_Tick onLongTapLeft_Tick,
                          MotoplayInputHandler.ClassDelegates.OnLongTapLeft_End onLongTapLeft_End,
                          MotoplayInputHandler.ClassDelegates.OnSimpleTapLeft onSimpleTapLeft)
        {
            this.onLongTapLeft_Start = onLongTapLeft_Start;
            this.onLongTapLeft_Tick = onLongTapLeft_Tick;
            this.onLongTapLeft_End = onLongTapLeft_End;
            this.onSimpleTapLeft = onSimpleTapLeft;
        }
    }

    public class ClickEvents
    {
        //Public variables
        public MotoplayInputHandler.ClassDelegates.OnLongTapClick_Start onLongTapClick_Start { get; } = null;
        public MotoplayInputHandler.ClassDelegates.OnLongTapClick_Tick onLongTapClick_Tick { get; } = null;
        public MotoplayInputHandler.ClassDelegates.OnLongTapClick_End onLongTapClick_End { get; } = null;
        public MotoplayInputHandler.ClassDelegates.OnSimpleTapClick onSimpleTapClick { get; } = null;

        //Core methods
        public ClickEvents(MotoplayInputHandler.ClassDelegates.OnLongTapClick_Start onLongTapClick_Start,
                           MotoplayInputHandler.ClassDelegates.OnLongTapClick_Tick onLongTapClick_Tick,
                           MotoplayInputHandler.ClassDelegates.OnLongTapClick_End onLongTapClick_End,
                           MotoplayInputHandler.ClassDelegates.OnSimpleTapClick onSimpleTapClick)
        {
            this.onLongTapClick_Start = onLongTapClick_Start;
            this.onLongTapClick_Tick = onLongTapClick_Tick;
            this.onLongTapClick_End = onLongTapClick_End;
            this.onSimpleTapClick = onSimpleTapClick;
        }
    }
}