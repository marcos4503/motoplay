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

        //Classes of script
        public class ClassDelegates
        {
            public delegate void OnLostConnection();
        }

        //Cache variables
        private Process rfcommCliProcess = null;
        private List<string> rfcommReceivedOutputLines = new List<string>();

        //Private variables
        private string rfcommSerialPortPath = "";
        private int channelToUseInRfcomm = -1;
        private string pairedObdDeviceName = "";
        private string pairedObdDeviceMac = "";
        private event ClassDelegates.OnLostConnection onLostConnection = null;

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
            Test();
        }

        private void OnConnectionFailAndNotStablishedSerialPort()
        {

        }

        private void OnLostConnection()
        {

        }
        
        //Internal methods

        private void Test()
        {
            //teste
            new Thread(() => 
            {
                AvaloniaDebug.WriteLine("TEST> INITIALIZING...");
                //Initialize
                SerialPort serialPort = new SerialPort();
                serialPort.PortName = "/dev/rfcomm14";
                serialPort.BaudRate = 4096;
                serialPort.Parity = Parity.None;
                serialPort.Handshake = Handshake.None;
                //serialPort.DataBits = 8;
                //serialPort.StopBits = StopBits.One;
                //serialPort.RtsEnable = true;
                //serialPort.DtrEnable = true;
                serialPort.WriteTimeout = 250;
                serialPort.ReadTimeout = 250;
                AvaloniaDebug.WriteLine("TEST> INITIALIZED!");

                //Open
                try
                {
                    serialPort.Open();
                    AvaloniaDebug.WriteLine("TEST> PORT OPENED!");
                }
                catch (TimeoutException e)
                {
                    if (serialPort.IsOpen == true)
                        serialPort.Close();
                    AvaloniaDebug.WriteLine(("TEST> PORT OPEN TIME OUT.\n" + e.Message + "\n" + e.StackTrace));
                    return;
                }
                catch (Exception e)
                {
                    if (serialPort.IsOpen == true)
                        serialPort.Close();
                    AvaloniaDebug.WriteLine(("TEST> PORT OPEN EXCEPTION GENERIC.\n" + e.Message + "\n" + e.StackTrace));
                    return;
                }

                //Wait
                Thread.Sleep(1000);

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

                //Wait
                Thread.Sleep(1000);

                //Close
                try
                {
                    if (serialPort.IsOpen == true)
                        serialPort.Close();
                    AvaloniaDebug.WriteLine("TEST> CLOSED!");
                }
                catch (Exception e) {
                    AvaloniaDebug.WriteLine(("TEST> CLOSE EXCEPTION GENERIC.\n" + e.Message + "\n" + e.StackTrace));
                }

            }).Start();
        }
    }
}