using Avalonia;
using Avalonia.Threading;
using Coroutine;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        private void OnConnectionSuccessAndStablishedSerialPort()
        {

        }

        private void OnConnectionFailAndNotStablishedSerialPort()
        {

        }

        private void OnLostConnection()
        {

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

                //Wait time
                Thread.Sleep(250);

                //Wait until finish the connection try and detect the result
                bool isFinishedTry = false;
                bool isSuccessfully = false;
                while (isFinishedTry == false)
                {
                    //If found the log informing that can't connect, inform that is finished
                    foreach (string line in rfcommReceivedOutputLines)
                        if (line.Contains("Can't connect RFCOMM socket") == true)
                        {
                            isFinishedTry = true;
                            isSuccessfully = false;
                        }

                    //If found the log informing that is connected, inform that is finished
                    foreach (string line in rfcommReceivedOutputLines)
                        if (line.Contains("Connected ") == true && line.Contains(" on channel") == true)
                        {
                            isFinishedTry = true;
                            isSuccessfully = true;
                        }

                    //Wait time before continue
                    Thread.Sleep(500);
                }

                //If is not connected
                if (isSuccessfully == false)
                {
                    //Change connection status to disconnected
                    currentConnectionStatus = ConnectionStatus.Disconnected;

                    //Warn to debug
                    AvaloniaDebug.WriteLine("Failed to connect to Bluetooth Device \"" + pairedObdDeviceName + "\" and Stablish a Serial Port!");

                    //Run the internal hook
                    OnConnectionFailAndNotStablishedSerialPort();
                }

                //If is connected
                if (isSuccessfully == true)
                {
                    //Change connection status to connected
                    currentConnectionStatus = ConnectionStatus.Connected;

                    //Warn to debug
                    AvaloniaDebug.WriteLine("Connection success to Bluetooth Device \"" + pairedObdDeviceName + "\" and Stablished a Serial Port!");

                    //Run the internal hook
                    OnConnectionSuccessAndStablishedSerialPort();
                }

                //Warn to debug
                AvaloniaDebug.WriteLine("Connection try for Bluetooth Device \"" + pairedObdDeviceName + "\" was finished.");





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
                    AvaloniaDebug.WriteLine("Disconnected from Bluetooth Device \"" + pairedObdDeviceName + "\" and stablished Serial Port!");
                }

            }).Start();
        }

        //...
    }
}