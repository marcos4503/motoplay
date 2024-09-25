using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Coroutine;
using HarfBuzzSharp;
using MarcosTomaz.ATS;
using Motoplay.Scripts;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Motoplay.Views;

/*
* This is the code responsible by the Motoplay App Window
*/

public partial class MainWindow : Window
{
    //Enums of script
    public enum ToastDuration
    {
        Short,
        Long
    }
    public enum ToastType
    {
        Normal,
        Problem
    }
    public enum ScrollDirection
    {
        Up,
        Down
    }
    public enum AppPage
    {
        VehiclePanel,
        GeneralMetrics,
        MusicPlayer,
        WebBrowser,
        UsbCamera,
        MirrorPhone,
        AppPreferences
    }
    public enum BluetoothDeviceAction
    {
        Unknown,
        Appeared,
        Disappeared
    }

    //Classes of script
    private class BluetoothDeviceInScanLogs()
    {
        //This class stores information about a bluetooth device registered in logs of scan

        //Public variables
        public BluetoothDeviceAction action = BluetoothDeviceAction.Unknown;
        public string deviceName = "";
        public string deviceMac = "";
    }

    //Cache variables
    private Process terminalCliProcess = null;
    private List<string> terminalReceivedOutputLines = new List<string>();
    private string currentTerminalCliRentKey = "";
    private Dictionary<string, string> runningTasks = new Dictionary<string, string>();
    private Process wvkbdProcess = null;
    private KeyFeedbackWindow wvkbdFeedbackWindow = null;
    private ActiveCoroutine wvkbdFeedbackRoutine = null;
    private ActiveCoroutine openKeyboardTipRoutine = null;
    private ActiveCoroutine showToastNotificationRoutine = null;
    private ActiveCoroutine hideToastNotificationRoutine = null;
    private Process bluetoothctlCliProcess = null;
    private List<string> bluetoothctlReceivedOutputLines = new List<string>();
    private ActiveCoroutine bluetoothSearchRoutine = null;
    private List<BluetoothDeviceItem> instantiatedBluetoothDevicesInUi = new List<BluetoothDeviceItem>();
    private ActiveCoroutine unpairThePairedObdDeviceRoutine = null;
    private int triesOfConnectionForBluetoothObdDevice = -1;
    private ActiveCoroutine bluetoothObdDeviceConnectionRoutine = null;
    private ActiveCoroutine postPreferencesSaveRoutine = null;
    private bool isVehiclePanelDrawerOpen = false;
    private ActiveCoroutine openCloseVehiclePanelDrawerRoutine = null;
    private List<PanelLogItem> instantiatedPanelLogsInUi = new List<PanelLogItem>();

    //Private variables
    private string[] receivedCliArgs = null;
    private string systemCurrentUsername = "";
    private string motoplayRootPath = "";
    private Preferences appPrefs = null;
    private string originalWindowTitle = "";
    private string applicationVersion = "";
    private ObdAdapterHandler activeObdConnection = null;

    //Core methods

    public MainWindow()
    {
        //Initialize the window normally on Avalonia side
        InitializeComponent();
    }

    public MainWindow(string[]? cliArgs) : this()
    {
        //Create a mutex to inform to system that is running a instance of the app
        bool created = false;
        Mutex mutex = new Mutex(true, "Motoplay.Desktop", out created);

        //If already have a instance of the app running, cancel
        if (created == false)
        {
            //Warn and stop here
            MessageBoxManager.GetMessageBoxStandard("Error", "The Motoplay App is already running!", ButtonEnum.Ok).ShowAsync();
            this.Close();
            return;
        }

        //Log the parameters found, to console
        StringBuilder paramsStr = new StringBuilder();
        foreach (string item in cliArgs)
            paramsStr.Append(" " + item);
        AvaloniaDebug.WriteLine("CLI Params Found: \"" + paramsStr.ToString() + "\".");

        //Save the CLI arguments
        this.receivedCliArgs = cliArgs;

        //Start the initialization of Window
        StartThisWindow();
    }

    private void StartThisWindow()
    {
        //Adjust this window, if is running on Windows PC, for testing purposes
        if (OperatingSystem.IsWindows() == true)
        {
            //Adjust this window settings
            this.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            this.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
            this.SystemDecorations = SystemDecorations.None;
            this.CanResize = false;
            this.SizeToContent = SizeToContent.WidthAndHeight;
            this.Topmost = false;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.WindowState = WindowState.Normal;
            this.IsEnabled = true;
            this.Background = new SolidColorBrush(new Color(0, 255, 255, 255), 0.0f);
            windowRoot.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            windowRoot.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
            windowRoot.Width = 800;
            windowRoot.Height = 480;
        }

        //Update the tasks running display
        UpdateTasksDisplay();

        //If is Linux...
        if (OperatingSystem.IsLinux() == true)
        {
            //Find the active user name
            systemCurrentUsername = Directory.GetCurrentDirectory().Replace("/home/", "").Split("/")[0];
            //Build the root path for motoplay
            motoplayRootPath = (@"/home/" + systemCurrentUsername + "/Motoplay");
            //Create the root folder if not exists
            if (Directory.Exists(motoplayRootPath) == false)
                Directory.CreateDirectory(motoplayRootPath);
        }
        //If is Windows...
        if (OperatingSystem.IsWindows() == true)
        {
            //Find the active user name
            systemCurrentUsername = System.Security.Principal.WindowsIdentity.GetCurrent().Name.Split("\\")[1];
            //Build the root path for motoplay
            motoplayRootPath = (@"C:\Users\" + systemCurrentUsername + @"\Desktop\Motoplay");
            //Create the root folder if not exists
            if (Directory.Exists(motoplayRootPath) == false)
                Directory.CreateDirectory(motoplayRootPath);
        }

        //Load the preferences
        appPrefs = new Preferences((motoplayRootPath + "/PersistentData/preferences.json"));
        //Update the preferences on UI
        UpdatePreferencesOnUI();

        //Get the original title of this window
        originalWindowTitle = this.Title;

        //Recover the version of the application
        System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
        FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
        applicationVersion = fvi.FileVersion;

        //Load the correct language resource file for the app
        this.Resources.MergedDictionaries.Clear();
        string resourceDictionaryUri = "";
        switch (appPrefs.loadedData.appLang)
        {
            case "en-us":
                resourceDictionaryUri = "avares://Motoplay/Assets/Languages/LangStrings.axaml";
                break;
            case "pt-br":
                resourceDictionaryUri = "avares://Motoplay/Assets/Languages/LangStrings-PT-BR.axaml";
                break;
        }
        ResourceInclude loadedLangResourceFile = new ResourceInclude(new Uri(resourceDictionaryUri)) { Source = new Uri(resourceDictionaryUri) };
        this.Resources.MergedDictionaries.Add(loadedLangResourceFile);

        //Prepare and start the UI
        PrepareTheUI();
    }

    private void PrepareTheUI()
    {
        //Prepare the UI
        toggleKeyboardButton.IsVisible = false;
        toggleKeyboardButton.Click += (s, e) => { ToggleVirtualKeyboard(); };
        toastNotificationRoot.IsVisible = false;
        toastNotificationDismissButton.IsEnabled = false;
        toastNotificationDismissButton.Click += (s, e) => { HideToastNow(); };
        updateAppButton.IsVisible = false;
        updateAppButton.Click += (s, e) => { InstallUpdateForApp(); };
        rollMenuUp.Click += (s, e) => { ScrollMenuTo(ScrollDirection.Up); };
        rollMenuDown.Click += (s, e) => { ScrollMenuTo(ScrollDirection.Down); };
        menuQuitButton.Click += (s, e) => { QuitApplication(); };
        menuPanelButton.Click += (s, e) => { SwitchAppPage(AppPage.VehiclePanel); };
        menuMetricsButton.Click += (s, e) => { SwitchAppPage(AppPage.GeneralMetrics); };
        menuPlayerButton.Click += (s, e) => { SwitchAppPage(AppPage.MusicPlayer); };
        menuBrowserButton.Click += (s, e) => { SwitchAppPage(AppPage.WebBrowser); };
        menuCameraButton.Click += (s, e) => { SwitchAppPage(AppPage.UsbCamera); };
        menuPhoneButton.Click += (s, e) => { SwitchAppPage(AppPage.MirrorPhone); };
        menuPreferencesButton.Click += (s, e) => { SwitchAppPage(AppPage.AppPreferences); };
        bindedCliButton.IsVisible = false;
        bindedbluetoothCtlButton.IsVisible = false;
        tryingConnectToObdButton.IsVisible = false;
        connectedToObdButton.IsVisible = false;
        tempInitializationLogo.IsVisible = true;

        //Switch to Panel page
        SwitchAppPage(AppPage.VehiclePanel);

        //Start a terminal for CLI process
        StartBindedCliTerminalProcess();
    }

    private void StartBindedCliTerminalProcess()
    {
        //Start a new thread to start the process
        AsyncTaskSimplified asyncTask = new AsyncTaskSimplified(null, new string[] { systemCurrentUsername });
        asyncTask.onStartTask_RunMainThread += (callerWindow, startParams) => { };
        asyncTask.onExecuteTask_RunBackground += (callerWindow, startParams, threadTools) =>
        {
            //Get the start params
            string user = startParams[0];

            //Warn about the start of the binded CLI terminal
            AvaloniaDebug.WriteLine("Starting Binded CLI Terminal...");

            //Prepare the Terminal process
            Process terminalProcess = new Process();
            if (OperatingSystem.IsLinux() == true)
            {
                terminalProcess.StartInfo.FileName = "/bin/bash";
                terminalProcess.StartInfo.Arguments = "";
                terminalProcess.StartInfo.WorkingDirectory = (@"/home/" + user);
            }
            if (OperatingSystem.IsWindows() == true)
            {
                terminalProcess.StartInfo.FileName = "cmd.exe";
                terminalProcess.StartInfo.Arguments = "/k";
                terminalProcess.StartInfo.WorkingDirectory = (@"C:\Users\" + user);
            }
            terminalProcess.StartInfo.UseShellExecute = false;
            terminalProcess.StartInfo.CreateNoWindow = true;
            terminalProcess.StartInfo.RedirectStandardInput = true;
            terminalProcess.StartInfo.RedirectStandardOutput = true;
            terminalProcess.StartInfo.RedirectStandardError = true;

            //Register receivers for all outputs from terminal
            terminalProcess.ErrorDataReceived += new DataReceivedEventHandler((s, e) =>
            {
                //If don't have data, cancel
                if (String.IsNullOrEmpty(e.Data) == true)
                    return;

                //Get the string output for this line
                string currentLineOutput = e.Data;

                //Add this new line to list
                terminalReceivedOutputLines.Add(currentLineOutput);
                //Repass this new line to for debugging too
                AvaloniaDebug.WriteLine(currentLineOutput);
            });
            terminalProcess.OutputDataReceived += new DataReceivedEventHandler((s, e) =>
            {
                //If don't have data, cancel
                if (String.IsNullOrEmpty(e.Data) == true)
                    return;

                //Get the string output for this line
                string currentLineOutput = e.Data;

                //Add this new line to list
                terminalReceivedOutputLines.Add(currentLineOutput);
                //Repass this new line to for debugging too
                AvaloniaDebug.WriteLine(currentLineOutput);
            });

            //Start the process, and store a reference for the process
            terminalProcess.Start();
            terminalProcess.BeginOutputReadLine();
            terminalProcess.BeginErrorReadLine();
            terminalCliProcess = terminalProcess;

            //Wait time
            threadTools.MakeThreadSleep(500);

            //Rent the Binded CLI Process
            string rKey = RentTheBindedCliTerminal();
            //Send started successfully message
            SendCommandToTerminalAndClearCurrentOutputLines(rKey, "echo \"> Terminal Opened!\"");
            //Wait until the end of command execution
            while (isLastCommandFinishedExecution(rKey) == false)
                threadTools.MakeThreadSleep(100);
            //Release the Binded CLI Process
            ReleaseTheBindedCliTerminal(rKey);

            //Wait time
            threadTools.MakeThreadSleep(500);

            //Finish the thread...
            return new string[] { "none" };
        };
        asyncTask.onDoneTask_RunMainThread += (callerWindow, backgroundResult) =>
        {
            //Run tasks after successfull started the binded CLI terminal process
            OnDoneStartOfBindedCliTerminalProcess();
        };
        asyncTask.Execute(AsyncTaskSimplified.ExecutionMode.NewDefaultThread);
    }

    private void OnDoneStartOfBindedCliTerminalProcess()
    {
        //Kill possible already processes of virtual keyboard
        CoroutineHandler.Start(KillPossibleExistingVirtualKeyboardProcess());

        //Start the process of setup for "unclutter"
        CoroutineHandler.Start(SetupTheUnclutterCursorHider());

        //Check if have updates for Motoplay App
        CoroutineHandler.Start(CheckIfHaveUpdatesForApp());



        //Hide the temporary background logo
        tempInitializationLogo.IsVisible = false;



        //Prepare the UI for Vehicle Panel
        PrepareTheVehiclePanel();

        //Prepare the UI for Preferences
        PrepareThePreferences();
    }

    private IEnumerator<Wait> KillPossibleExistingVirtualKeyboardProcess()
    {
        //Add this task running
        AddTask("killVirtualKeyboardProcesses", "Kill possible already existing Virtual Keyboard processes.");

        //If the Binded CLI Process is already rented by another task, wait until release
        while (isBindedCliTerminalRented() == true)
            yield return new Wait(0.5f);
        //Rent the Binded CLI Process
        string rKey = RentTheBindedCliTerminal();



        //Wait time
        yield return new Wait(1.0f);

        //Send command to kill possible instances of "wvkbd-mobintl", already running
        SendCommandToTerminalAndClearCurrentOutputLines(rKey, "sudo pkill -f wvkbd");
        //Wait the end of command execution
        while (isLastCommandFinishedExecution(rKey) == false)
            yield return new Wait(0.1f);

        //Make the toggle virtual keyboard button visible
        toggleKeyboardButton.IsVisible = true;



        //Release the Binded CLI Process
        ReleaseTheBindedCliTerminal(rKey);

        //Remove the task running
        RemoveTask("killVirtualKeyboardProcesses");
    }

    private IEnumerator<Wait> SetupTheUnclutterCursorHider()
    {
        //Add this task running
        AddTask("unclutter_setup", "Setup the Unclutter to hide the Cursor inside the Motoplay.");

        //If the Binded CLI Process is already rented by another task, wait until release
        while (isBindedCliTerminalRented() == true)
            yield return new Wait(0.5f);
        //Rent the Binded CLI Process
        string rKey = RentTheBindedCliTerminal();



        //Wait time
        yield return new Wait(1.0f);

        //If Linux, continue...
        if (OperatingSystem.IsLinux() == true)
        {
            //Send a command to check if the "unclutter" is installed
            SendCommandToTerminalAndClearCurrentOutputLines(rKey, "sudo dpkg -s unclutter");
            //Wait the end of command execution
            while (isLastCommandFinishedExecution(rKey) == false)
                yield return new Wait(0.1f);

            //If not installed, start installation
            if (isThermFoundInTerminalOutputLines(rKey, "is not installed") == true)
            {
                //Wait time
                yield return new Wait(1.0f);

                //Send a command to install the "unclutter"
                SendCommandToTerminalAndClearCurrentOutputLines(rKey, "sudo apt-get install unclutter -y");
                //Wait the end of command execution
                while (isLastCommandFinishedExecution(rKey) == false)
                    yield return new Wait(0.1f);

                //Wait time
                yield return new Wait(1.0f);

                //Send a command to confirm that the "unclutter" is installed
                SendCommandToTerminalAndClearCurrentOutputLines(rKey, "sudo dpkg -s unclutter");
                //Wait the end of command execution
                while (isLastCommandFinishedExecution(rKey) == false)
                    yield return new Wait(0.1f);

                //If not installed, stop the program
                if (isThermFoundInTerminalOutputLines(rKey, "is not installed") == true)
                {
                    var diag = MessageBoxManager.GetMessageBoxStandard("Error", "There was a problem, when installing a required package. Check your Internet connection!", ButtonEnum.Ok).ShowAsync();
                    while (diag.IsCompleted == false)
                        yield return new Wait(0.1f);
                    this.Close();
                }
            }

            //Now, that is ensured that "unclutter" is already installed, send command to kill possible instances of "unclutter", already running
            SendCommandToTerminalAndClearCurrentOutputLines(rKey, "sudo pkill -f unclutter");
            //Wait the end of command execution
            while (isLastCommandFinishedExecution(rKey) == false)
                yield return new Wait(0.1f);

            //Now, that is ensured that "unclutter" is already installed, send command to hide the cursor
            SendCommandToTerminalAndClearCurrentOutputLines(rKey, "unclutter -idle 0.2 -root & echo \"> ContinueInOtherThread\"");
            //Wait the end of command execution
            while (isLastCommandFinishedExecution(rKey) == false)
                yield return new Wait(0.1f);
        }

        //If Windows, just continue...
        if (OperatingSystem.IsWindows() == true)
            AvaloniaDebug.WriteLine("The tool \"unclutter\" is not necessary on Windows.");



        //Release the Binded CLI Process
        ReleaseTheBindedCliTerminal(rKey);

        //Remove the task running
        RemoveTask("unclutter_setup");
    }

    private IEnumerator<Wait> CheckIfHaveUpdatesForApp()
    {
        //Add this task running
        AddTask("updateCheck", "Check if have update available for Motoplay App.");

        //Wait time
        yield return new Wait(1.0f);

        //Start a new thread to check if have updates for app
        AsyncTaskSimplified asyncTask = new AsyncTaskSimplified(null, new string[] { motoplayRootPath });
        asyncTask.onStartTask_RunMainThread += (callerWindow, startParams) => { };
        asyncTask.onExecuteTask_RunBackground += (callerWindow, startParams, threadTools) =>
        {
            //Wait some time
            threadTools.MakeThreadSleep(1000);

            //Get the needed params
            string rootPath = startParams[0];

            //Try to do the task
            try
            {
                //------------- START -------------//

                //-------------------------- REPOSITORY INFO DOWNLOAD --------------------------//

                //Wait some time
                threadTools.MakeThreadSleep(1000);

                //If the folder of persistent data don't exists, create it
                if (Directory.Exists((rootPath + "/PersistentData")) == false)
                    Directory.CreateDirectory((rootPath + "/PersistentData"));

                //If the file already exists, delete it
                if (File.Exists((rootPath + "/PersistentData/app-repository-info.json")) == true)
                    File.Delete((rootPath + "/PersistentData/app-repository-info.json"));

                //Prepare the target download URL
                string downloadUrl = @"https://marcos4503.github.io/motoplay/Repository-Pages/motoplay-data-info.json";
                string saveAsPath = (rootPath + "/PersistentData/app-repository-info.json");
                //Download the data sync
                HttpClient httpClient = new HttpClient();
                HttpResponseMessage httpRequestResult = httpClient.GetAsync(downloadUrl).Result;
                httpRequestResult.EnsureSuccessStatusCode();
                Stream downloadStream = httpRequestResult.Content.ReadAsStreamAsync().Result;
                FileStream fileStream = new FileStream(saveAsPath, FileMode.Create, FileAccess.Write, FileShare.None);
                downloadStream.CopyTo(fileStream);
                httpClient.Dispose();
                fileStream.Dispose();
                fileStream.Close();
                downloadStream.Dispose();
                downloadStream.Close();

                //-------------------------- DATA INTERPRETATION --------------------------//

                //Load the repository info
                AppRepositoryInfo appInfo = new AppRepositoryInfo((rootPath + "/PersistentData/app-repository-info.json"));

                //Recover the server version of the app
                string serverVersion = appInfo.loadedData.version;

                //-------------- END --------------//

                //Wait some time
                threadTools.MakeThreadSleep(1000);

                //Return a success response
                return new string[] { "success", serverVersion };
            }

            catch (Exception ex)
            {
                //Return a error response
                return new string[] { "error", "" };
            }

            //Finish the thread...
            return new string[] { "none", "" };
        };
        asyncTask.onDoneTask_RunMainThread += (callerWindow, backgroundResult) =>
        {
            //Remove the task running
            RemoveTask("updateCheck");

            //If have a error, cancel here
            if (backgroundResult[0] != "success")
                return;

            //If the app version and server version are differents, notify the user about the update
            if (applicationVersion != backgroundResult[1])
            {
                ShowToast(GetStringApplicationResource("statusBar_updateAvailable"), ToastDuration.Long, ToastType.Normal);
                updateAppButton.IsVisible = true;
            }
        };
        asyncTask.Execute(AsyncTaskSimplified.ExecutionMode.NewDefaultThread);
    }

    //Vehicle Panel methods

    private void PrepareTheVehiclePanel()
    {
        //Prepare the UI of Vehicle Panel
        vehiclePanel_obdSetup_startButton.Click += (s, e) => { StartTheBluetoothctlBindedCliTerminal(); };
        vehiclePanel_obdSetup_backToSearch.Click += (s, e) => { StartBluetoothDevicesSearch(); };
        vehiclePanel_obdSetup_goPair.Click += (s, e) => { StartPairWithTargetBluetoothDevice(); };
        vehiclePanel_obdConnect_unpairButton.Click += (s, e) => { UnpairTheCurrentlyPairedBluetoothObdDevice(); };
        vehiclePanel_drawerHandler.PointerPressed += (s, e) => { ToggleVehiclePanelDrawer(); };
        vehiclePanel_drawerBackground.PointerPressed += (s, e) => { ToggleVehiclePanelDrawer(); };
        vehiclePanel_drawer_adapterTab_unpairButton.Click += (s, e) => { UnpairTheCurrentlyPairedBluetoothObdDevice(); };

        //Start the vehicle panel
        StartVehiclePanel();
    }

    private void StartVehiclePanel()
    {
        //If don't have a configured OBD adapter, start the setup...
        if (appPrefs.loadedData.configuredObdBtAdapter.haveConfigured == false)
        {
            StartBluetoothObdAdapterSetup();
            return;
        }

        //Reset the connect try count
        triesOfConnectionForBluetoothObdDevice = 1;

        //Connect to the paired Bluetooth OBD adapter
        ConnectToPairedBluetoothObdDeviceAndStablishSerialPort();
    }

    //Vehicle Panel methods: Setup Flow

    private void StartBluetoothObdAdapterSetup()
    {
        //Change to background of setup and connect
        backgroundForPanelSetupAndConnect.IsVisible = true;
        backgroundForPanelContent.IsVisible = false;

        //Change to screen of setup
        vehiclePanel_obdSetupScreen.IsVisible = true;
        vehiclePanel_obdConnectScreen.IsVisible = false;
        vehiclePanel_panelScreen.IsVisible = false;

        //Change to initial step of setup
        vehiclePanel_obdSetup_InitialStep.IsVisible = true;
        vehiclePanel_obdSetup_Search.IsVisible = false;
        vehiclePanel_obdSetup_Password.IsVisible = false;
        vehiclePanel_obdSetup_Pair.IsVisible = false;

        //Wait until user click in "Start Setup" button...
    }

    private void StartTheBluetoothctlBindedCliTerminal()
    {
        //Start coroutine to start the Bluetoothctl Binded CLI Terminal
        CoroutineHandler.Start(StartTheBluetoothctlBindedCliTerminalRoutine());
    }

    private IEnumerator<Wait> StartTheBluetoothctlBindedCliTerminalRoutine()
    {
        //Add this task running
        AddTask("startBluetoothctl", "Start the Bluetoothctl binded CLI Process.");

        //Disable the "Start Setup" button
        vehiclePanel_obdSetup_startButton.IsEnabled = false;



        //Wait time
        yield return new Wait(1.0f);

        //If the Binded CLI Process is already rented by another task, wait until release
        while (isBindedCliTerminalRented() == true)
            yield return new Wait(0.5f);
        //Rent the Binded CLI Process
        string rKey = RentTheBindedCliTerminal();

        //Send command to stop the bluetooth service
        SendCommandToTerminalAndClearCurrentOutputLines(rKey, "sudo systemctl stop bluetooth.service");
        //Wait the end of command execution
        while (isLastCommandFinishedExecution(rKey) == false)
            yield return new Wait(0.1f);

        //Wait time
        yield return new Wait(0.5f);

        //Send command to start the bluetooth service
        SendCommandToTerminalAndClearCurrentOutputLines(rKey, "sudo systemctl start bluetooth.service");
        //Wait the end of command execution
        while (isLastCommandFinishedExecution(rKey) == false)
            yield return new Wait(0.1f);

        //Wait time
        yield return new Wait(0.5f);

        //Warn
        AvaloniaDebug.WriteLine("Starting BluetoothCTL Binded Cli Terminal...");

        //Release the Binded CLI Process
        ReleaseTheBindedCliTerminal(rKey);

        //Reset the received output lines
        bluetoothctlReceivedOutputLines.Clear();

        //Start the "bluetoothctl" direct terminal process
        Process bluetoothctlProcess = new Process() { StartInfo = new ProcessStartInfo() { FileName = "/usr/bin/bluetoothctl", Arguments = "" } };
        bluetoothctlProcess.StartInfo.UseShellExecute = false;
        bluetoothctlProcess.StartInfo.CreateNoWindow = true;
        bluetoothctlProcess.StartInfo.RedirectStandardInput = true;
        bluetoothctlProcess.StartInfo.RedirectStandardOutput = true;
        bluetoothctlProcess.StartInfo.RedirectStandardError = true;

        //Register receivers of all outputs from terminal
        bluetoothctlProcess.ErrorDataReceived += new DataReceivedEventHandler((s, e) =>
        {
            //If don't have data, cancel
            if (String.IsNullOrEmpty(e.Data) == true)
                return;

            //Store the lines and resend to debug
            bluetoothctlReceivedOutputLines.Add(e.Data);
            AvaloniaDebug.WriteLine(("BindedBluetoothCTL -> " + e.Data));
        });
        bluetoothctlProcess.OutputDataReceived += new DataReceivedEventHandler((s, e) =>
        {
            //If don't have data, cancel
            if (String.IsNullOrEmpty(e.Data) == true)
                return;

            //Store the lines and resend to debug
            bluetoothctlReceivedOutputLines.Add(e.Data);
            AvaloniaDebug.WriteLine(("BindedBluetoothCTL -> " + e.Data));
        });

        //Start the process
        bluetoothctlProcess.Start();
        bluetoothctlProcess.BeginOutputReadLine();
        bluetoothctlProcess.BeginErrorReadLine();
        //Store the process reference
        bluetoothctlCliProcess = bluetoothctlProcess;

        //Wait time
        yield return new Wait(0.5f);

        //Send command to enable the agent
        bluetoothctlCliProcess.StandardInput.WriteLine("agent on");

        //Wait time
        yield return new Wait(0.5f);

        //Send command to power on
        bluetoothctlCliProcess.StandardInput.WriteLine("power on");

        //Wait time
        yield return new Wait(3.0f);

        //Check if the bluetoothctl was started successfully
        bool wasStartedSuccessfully = false;
        foreach (string line in bluetoothctlReceivedOutputLines)
            if (line.Contains("Changing power on succeeded") == true)
                wasStartedSuccessfully = true;
        
        //If was not started successfully
        if (wasStartedSuccessfully == false)
        {
            //Notify the user
            ShowToast(GetStringApplicationResource("vehiclePanel_setupEnableBluetoothError"), ToastDuration.Short, ToastType.Problem);

            //Stop the Bluetoothctl binded terminal
            StopTheBluetoothctlBindedCliTerminal();
        }

        //If was started successfully...
        if (wasStartedSuccessfully == true)
        {
            //Show the use in status bar
            bindedbluetoothCtlButton.IsVisible = true;

            //Start the bluetooth devices search
            StartBluetoothDevicesSearch();
        }
        


        //Enable the "Start Setup" button
        vehiclePanel_obdSetup_startButton.IsEnabled = true;

        //Add this task running
        RemoveTask("startBluetoothctl");
    }

    private void StopTheBluetoothctlBindedCliTerminal()
    {
        //Warn
        AvaloniaDebug.WriteLine("Stopping BluetoothCTL Binded Cli Terminal...");

        //Hide the use in status bar
        bindedbluetoothCtlButton.IsVisible = false;

        //Send comand to exit
        bluetoothctlCliProcess.StandardInput.WriteLine("exit");

        //Clear the received output lines
        bluetoothctlReceivedOutputLines.Clear();

        //Clear the reference for the process
        bluetoothctlCliProcess = null;
    }

    private void StartBluetoothDevicesSearch()
    {
        //Change to background of setup and connect
        backgroundForPanelSetupAndConnect.IsVisible = true;
        backgroundForPanelContent.IsVisible = false;

        //Change to screen of setup
        vehiclePanel_obdSetupScreen.IsVisible = true;
        vehiclePanel_obdConnectScreen.IsVisible = false;
        vehiclePanel_panelScreen.IsVisible = false;

        //Change to search step
        vehiclePanel_obdSetup_InitialStep.IsVisible = false;
        vehiclePanel_obdSetup_Search.IsVisible = true;
        vehiclePanel_obdSetup_Password.IsVisible = false;
        vehiclePanel_obdSetup_Pair.IsVisible = false;

        //Start the bluetooth devices search routine, if is not running
        if (bluetoothSearchRoutine == null)
            bluetoothSearchRoutine = CoroutineHandler.Start(BluetoothDevicesSearchRoutine());
    }

    private IEnumerator<Wait> BluetoothDevicesSearchRoutine()
    {
        //Add this task running
        AddTask("bluetoothDevicesSearch", "Search for nearby Bluetooth Devices.");

        //Warn to debug
        AvaloniaDebug.WriteLine("Starting Bluetooth Devices Search...");

        //Enable the no devices found warn
        vehiclePanel_obdSetup_noDevice.IsVisible = true;
        //Clear the devices found in the UI
        foreach (BluetoothDeviceItem item in instantiatedBluetoothDevicesInUi)
            vehiclePanel_obdSetup_devicesList.Children.Remove(item);
        instantiatedBluetoothDevicesInUi.Clear();

        //Wait time
        yield return new Wait(1.0f);

        //Send command to bluetoothctl start scanning devices
        bluetoothctlCliProcess.StandardInput.WriteLine("scan on");

        //Wait time
        yield return new Wait(3.0f);

        //Prepare the data
        int lastNumberOfBtCtlLogsSinceLastUiUpdate = -1;
        //Start the bluetooth devices UI update loop
        while (true)
        {
            //If the number of logs of bluetoothctl was changed since last UI update, update the UI again
            if(bluetoothctlReceivedOutputLines.Count != lastNumberOfBtCtlLogsSinceLastUiUpdate)
            {
                //Warn to debug
                AvaloniaDebug.WriteLine("Updating found Bluetooth Devices to UI!");

                //Clear the devices found in the UI
                foreach (BluetoothDeviceItem item in instantiatedBluetoothDevicesInUi)
                    vehiclePanel_obdSetup_devicesList.Children.Remove(item);
                instantiatedBluetoothDevicesInUi.Clear();

                //Prepare a dictionary of nearby found devices
                Dictionary<string, string> nearbyDevices = new Dictionary<string, string>();

                //Analyze each log of bluetoothctl output
                foreach (string log in bluetoothctlReceivedOutputLines)
                {
                    //Analyze the log and get the bluetooth device, if have
                    BluetoothDeviceInScanLogs potentialDevice = AnalyzeLogAndGetPossibleBluetoothDeviceInfo(log);

                    //If is not a device, skip to next log
                    if (potentialDevice.action == BluetoothDeviceAction.Unknown)
                        continue;

                    //If is to add...
                    if (potentialDevice.action == BluetoothDeviceAction.Appeared && nearbyDevices.ContainsKey(potentialDevice.deviceMac) == false)
                        nearbyDevices.Add(potentialDevice.deviceMac, potentialDevice.deviceName);
                    //If is to remove...
                    if (potentialDevice.action == BluetoothDeviceAction.Disappeared && nearbyDevices.ContainsKey(potentialDevice.deviceMac) == true)
                        nearbyDevices.Remove(potentialDevice.deviceMac);
                }

                //Render all found devices in the UI
                foreach (var key in nearbyDevices)
                {
                    //Instantiate and store reference for it
                    BluetoothDeviceItem item = new BluetoothDeviceItem(this);
                    instantiatedBluetoothDevicesInUi.Add(item);
                    vehiclePanel_obdSetup_devicesList.Children.Add(item);
                    //Set it up
                    item.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
                    item.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
                    item.Width = double.NaN;
                    item.Height = double.NaN;
                    //Fill this item
                    item.SetDeviceName(key.Value);
                    item.SetDeviceMAC(key.Key);
                    item.RegisterOnClickCallback((btDeviceInfo) => 
                    {
                        //Stop the Bluetooth Devices search routine
                        StopBluetoothDevicesSearch();

                        //Start the pre pairing with the target device
                        GoToPrePairingWithTargetBluetoothDevice(btDeviceInfo.btDeviceName.Text, btDeviceInfo.btDeviceMac.Text);
                    });
                }

                //If not found devices, show warn
                if (nearbyDevices.Keys.Count == 0)
                    vehiclePanel_obdSetup_noDevice.IsVisible = true;
                //If found devices, hide warn
                if (nearbyDevices.Keys.Count > 0)
                    vehiclePanel_obdSetup_noDevice.IsVisible = false;

                //Update the last number of logs
                lastNumberOfBtCtlLogsSinceLastUiUpdate = bluetoothctlReceivedOutputLines.Count;
            }

            //Wait time
            yield return new Wait(0.5f);
        }

        //...
    }

    private void StopBluetoothDevicesSearch()
    {
        //Warn to debug
        AvaloniaDebug.WriteLine("Stopping Bluetooth Devices Search...");

        //Stop the search routine
        if (bluetoothSearchRoutine != null)
            bluetoothSearchRoutine.Cancel();

        //Reset the routine reference
        bluetoothSearchRoutine = null;

        //Enable the no devices found warn
        vehiclePanel_obdSetup_noDevice.IsVisible = true;
        //Clear the devices found in the UI
        foreach (BluetoothDeviceItem item in instantiatedBluetoothDevicesInUi)
            vehiclePanel_obdSetup_devicesList.Children.Remove(item);
        instantiatedBluetoothDevicesInUi.Clear();

        //Send command to bluetoothctl stop scanning devices
        bluetoothctlCliProcess.StandardInput.WriteLine("scan off");

        //Remove the task
        RemoveTask("bluetoothDevicesSearch");
    }

    private void GoToPrePairingWithTargetBluetoothDevice(string deviceName, string deviceMac)
    {
        //Change to background of setup and connect
        backgroundForPanelSetupAndConnect.IsVisible = true;
        backgroundForPanelContent.IsVisible = false;

        //Change to screen of setup
        vehiclePanel_obdSetupScreen.IsVisible = true;
        vehiclePanel_obdConnectScreen.IsVisible = false;
        vehiclePanel_panelScreen.IsVisible = false;

        //Change to password step
        vehiclePanel_obdSetup_InitialStep.IsVisible = false;
        vehiclePanel_obdSetup_Search.IsVisible = false;
        vehiclePanel_obdSetup_Password.IsVisible = true;
        vehiclePanel_obdSetup_Pair.IsVisible = false;

        //Show the device info in the UI
        vehiclePanel_obdSetup_inputName.Text = deviceName;
        vehiclePanel_obdSetup_inputMac.Text = deviceMac;
        vehiclePanel_obdSetup_inputPin.Text = "";
    }

    private void StartPairWithTargetBluetoothDevice()
    {
        //Change to background of setup and connect
        backgroundForPanelSetupAndConnect.IsVisible = true;
        backgroundForPanelContent.IsVisible = false;

        //Change to screen of setup
        vehiclePanel_obdSetupScreen.IsVisible = true;
        vehiclePanel_obdConnectScreen.IsVisible = false;
        vehiclePanel_panelScreen.IsVisible = false;

        //Change to pair step
        vehiclePanel_obdSetup_InitialStep.IsVisible = false;
        vehiclePanel_obdSetup_Search.IsVisible = false;
        vehiclePanel_obdSetup_Password.IsVisible = false;
        vehiclePanel_obdSetup_Pair.IsVisible = true;

        //Start the bluetooth pair routine
        CoroutineHandler.Start(PairWithTargetBluetoothDeviceRoutine());
    }

    private IEnumerator<Wait> PairWithTargetBluetoothDeviceRoutine()
    {
        //Add this task running
        AddTask("bluetoothObdPair", "Pair with the Bluetooth Device.");

        //Wait time
        yield return new Wait(1.0f);

        //Get device information
        string targetDeviceName = vehiclePanel_obdSetup_inputName.Text;
        string targetDeviceMac = vehiclePanel_obdSetup_inputMac.Text;
        string targetDevicePin = vehiclePanel_obdSetup_inputPin.Text;

        //Clear the output received from the bluetoothctl binded
        bluetoothctlReceivedOutputLines.Clear();

        //Wait time
        yield return new Wait(1.0f);

        //Send command to bluetooothctl remove the target device, for the case of is already paired
        //bluetoothctlCliProcess.StandardInput.WriteLine(("remove " + targetDeviceMac));

        //Wait time
        //yield return new Wait(1.0f);

        //Send command to bluetoothctl pair with the target device
        bluetoothctlCliProcess.StandardInput.WriteLine(("pair " + targetDeviceMac));

        //Wait time to ensure a good response time
        yield return new Wait(8.0f);

        //Send PIN code if necessary
        foreach (string line in bluetoothctlReceivedOutputLines)
            if (line.Contains("Request PIN code") == true)
            {
                //Send command to inform the PIN code
                bluetoothctlCliProcess.StandardInput.WriteLine(targetDevicePin);

                //Wait time to ensure a good response time
                yield return new Wait(8.0f);

                //Stop here the loop
                break;
            }

        //Send YES to confirm, if necessary
        foreach (string line in bluetoothctlReceivedOutputLines)
            if (line.Contains("Request confirmation") == true)
            {
                //Send command to confirm
                bluetoothctlCliProcess.StandardInput.WriteLine("yes");

                //Wait time to ensure a good response time
                yield return new Wait(8.0f);

                //Stop here the loop
                break;
            }

        //Wait until finish the pairing and detect the pairing result
        bool isFinishedPairing = false;
        bool isPairingSuccessfull = false;
        while (isFinishedPairing == false)
        {
            //If found the log informing fail on pair, inform that is finished
            foreach (string line in bluetoothctlReceivedOutputLines)
                if (line.Contains("Failed to pair") == true)
                {
                    isFinishedPairing = true;
                    isPairingSuccessfull = false;
                }

            //If found the log informing device not available, inform that is finished
            foreach (string line in bluetoothctlReceivedOutputLines)
                if (line.Contains("Device ") == true && line.Contains(" not available") == true)
                {
                    isFinishedPairing = true;
                    isPairingSuccessfull = false;
                }

            //If found the log informing success on pair, inform that is finished
            foreach (string line in bluetoothctlReceivedOutputLines)
                if (line.Contains("Pairing successful") == true)
                {
                    isFinishedPairing = true;
                    isPairingSuccessfull = true;
                }

            //Wait time before continue
            yield return new Wait(0.5f);
        }

        //If not paired, go back to search screen
        if (isPairingSuccessfull == false)
        {
            //Go back to search screen
            StartBluetoothDevicesSearch();

            //Get the error string
            string errorString = "notFoundErrorCodeOrUnvailableDevice";
            foreach (string line in bluetoothctlReceivedOutputLines)
                if (line.Contains("org.bluez.Error") == true)
                {
                    //Inform the error string
                    errorString = line.Replace("Failed to pair: org.bluez.Error.", "");

                    //Break the loop
                    break;
                }

            //Notify the user
            ShowToast(GetStringApplicationResource("vehiclePanel_setupPairingError").Replace("%d", targetDeviceName).Replace("%e", errorString), ToastDuration.Long, ToastType.Problem);
        }

        //If paired successfull, continues...
        if (isPairingSuccessfull == true)
        {
            //Send command to bluetoothctl trust the target device
            bluetoothctlCliProcess.StandardInput.WriteLine(("trust " + targetDeviceMac));

            //Wait time
            yield return new Wait(3.0f);

            //Save the info about the device
            appPrefs.loadedData.configuredObdBtAdapter.haveConfigured = true;
            appPrefs.loadedData.configuredObdBtAdapter.deviceName = targetDeviceName;
            appPrefs.loadedData.configuredObdBtAdapter.deviceMac = targetDeviceMac;
            appPrefs.loadedData.configuredObdBtAdapter.devicePassword = targetDevicePin;
            SaveAllPreferences();

            //Notify the user
            ShowToast(GetStringApplicationResource("vehiclePanel_setupPairingSuccess").Replace("%d", targetDeviceName), ToastDuration.Long, ToastType.Normal);

            //Stop the binded bluetoothctl CLI terminal
            StopTheBluetoothctlBindedCliTerminal();

            //Restart the vehicle panel
            StartVehiclePanel();
        }

        //Remove this task running
        RemoveTask("bluetoothObdPair");
    }

    //Vehicle Panel methods: Unpair Flow

    private void UnpairTheCurrentlyPairedBluetoothObdDevice()
    {
        //Reset the data saved
        appPrefs.loadedData.configuredObdBtAdapter.haveConfigured = false;
        appPrefs.loadedData.configuredObdBtAdapter.deviceName = "";
        appPrefs.loadedData.configuredObdBtAdapter.deviceMac = "";
        appPrefs.loadedData.configuredObdBtAdapter.devicePassword = "";
        SaveAllPreferences();

        //Start the unpair routine, if is not running
        if (unpairThePairedObdDeviceRoutine == null)
            unpairThePairedObdDeviceRoutine = CoroutineHandler.Start(UnpairTheCurrentlyPairedBluetoothObdDeviceRoutine());
    }

    private IEnumerator<Wait> UnpairTheCurrentlyPairedBluetoothObdDeviceRoutine()
    {
        //Add this task running
        AddTask("unpairPairedObdDevice", "Unpair the currently saved and paired Bluetooth Obd Device.");

        //Show the interaction blocker
        SetActiveInteractionBlocker(true);

        //If the Binded CLI Process is already rented by another task, wait until release
        while (isBindedCliTerminalRented() == true)
            yield return new Wait(0.5f);
        //Rent the Binded CLI Process
        string rKey = RentTheBindedCliTerminal();



        //Wait time
        yield return new Wait(1.0f);



        //Stop the routine of connection, if is trying to connect to the device, now
        if (bluetoothObdDeviceConnectionRoutine != null)
        {
            bluetoothObdDeviceConnectionRoutine.Cancel();
            bluetoothObdDeviceConnectionRoutine = null;
            RemoveTask("bluetoothObdConnect");
            AvaloniaDebug.WriteLine("Aborting Bluetooth OBD Device to Serial Port connection attempter...");
        }

        //Force disconnect the active connection OBD Adapter Handler, if have
        if (activeObdConnection != null)
        {
            activeObdConnection.ForceDisconnect();
            AvaloniaDebug.WriteLine("Forcing Disconnection of OBD Adapter Handler...");
            activeObdConnection = null;
        }



        //Send command to unpair the paired device
        SendCommandToTerminalAndClearCurrentOutputLines(rKey, ("bluetoothctl remove " + appPrefs.loadedData.configuredObdBtAdapter.deviceMac));
        //Wait the end of command execution
        while (isLastCommandFinishedExecution(rKey) == false)
            yield return new Wait(0.1f);

        //...

        //Restart the panel
        StartVehiclePanel();

        //Hide the interaction blocker
        SetActiveInteractionBlocker(false);

        //Inform that was finished
        unpairThePairedObdDeviceRoutine = null;



        //Release the Binded CLI Process
        ReleaseTheBindedCliTerminal(rKey);

        //Remove the task
        RemoveTask("unpairPairedObdDevice");
    }

    //Vehicle Panel methods: Connection Flow

    private void ConnectToPairedBluetoothObdDeviceAndStablishSerialPort()
    {
        //Change to background of setup and connect
        backgroundForPanelSetupAndConnect.IsVisible = true;
        backgroundForPanelContent.IsVisible = false;

        //Change to screen of connect
        vehiclePanel_obdSetupScreen.IsVisible = false;
        vehiclePanel_obdConnectScreen.IsVisible = true;
        vehiclePanel_panelScreen.IsVisible = false;

        //Set initial connect status
        vehiclePanel_odbConnect_statusText.Text = GetStringApplicationResource("vehiclePanel_odbConnectInitialStatus");

        //Start the bluetooth connection routine, if is not running
        if (bluetoothObdDeviceConnectionRoutine == null)
            bluetoothObdDeviceConnectionRoutine = CoroutineHandler.Start(ConnectToPairedBluetoothObdDeviceAndStablishSerialPortRoutine());
    }

    private IEnumerator<Wait> ConnectToPairedBluetoothObdDeviceAndStablishSerialPortRoutine()
    {
        //Add this task running
        AddTask("bluetoothObdConnect", "Connect with the Paired Bluetooth OBD Adapter device and stablish a Serial Port.");

        //Disable the unpair button
        vehiclePanel_obdConnect_unpairButton.IsVisible = false;

        //If the Binded CLI Process is already rented by another task, wait until release
        while (isBindedCliTerminalRented() == true)
            yield return new Wait(0.5f);
        //Rent the Binded CLI Process
        string rKey = RentTheBindedCliTerminal();



        //Wait time
        yield return new Wait(1.0f);

        //Send command to stop the bluetooth service
        SendCommandToTerminalAndClearCurrentOutputLines(rKey, "sudo systemctl stop bluetooth.service");
        //Wait the end of command execution
        while (isLastCommandFinishedExecution(rKey) == false)
            yield return new Wait(0.1f);

        //Wait time
        yield return new Wait(0.5f);

        //Send command to start the bluetooth service
        SendCommandToTerminalAndClearCurrentOutputLines(rKey, "sudo systemctl start bluetooth.service");
        //Wait the end of command execution
        while (isLastCommandFinishedExecution(rKey) == false)
            yield return new Wait(0.1f);

        //Wait time
        yield return new Wait(0.5f);

        //Send command to kill all rfcomm ports
        SendCommandToTerminalAndClearCurrentOutputLines(rKey, "sudo killall rfcomm");
        //Wait the end of command execution
        while (isLastCommandFinishedExecution(rKey) == false)
            yield return new Wait(0.1f);

        //Wait time
        yield return new Wait(0.5f);

        //Send command to release a possible already existing rfcomm port
        SendCommandToTerminalAndClearCurrentOutputLines(rKey, ("sudo rfcomm release " + appPrefs.loadedData.bluetoothSerialPortToUse));
        //Wait the end of command execution
        while (isLastCommandFinishedExecution(rKey) == false)
            yield return new Wait(0.1f);

        //Wait time
        yield return new Wait(0.5f);

        //Release the Binded CLI Process
        ReleaseTheBindedCliTerminal(rKey);

        //Warn that is starting connection attempter
        AvaloniaDebug.WriteLine("Starting Bluetooth OBD Device to Serial Port connection attempter...");

        //Wait time
        yield return new Wait(0.5f);

        //Start the connection attempter
        while (activeObdConnection == null)
        {
            //Show the atempt number on debug
            AvaloniaDebug.WriteLine(("Bluetooth OBD Device to Serial Port connection attempt: #" + triesOfConnectionForBluetoothObdDevice.ToString()));

            //Show the attempt on status
            vehiclePanel_odbConnect_statusText.Text = GetStringApplicationResource("vehiclePanel_odbConnectTryStatus").Replace("%d", appPrefs.loadedData.configuredObdBtAdapter.deviceName)
                                                                                                                      .Replace("%n", triesOfConnectionForBluetoothObdDevice.ToString());

            //Show the icon of trying to connect in status bar
            tryingConnectToObdButton.IsVisible = true;

            //Create a new OBD Adapter Handler
            ObdAdapterHandler newObdAdapterHandlerConnection = new ObdAdapterHandler();

            //Setup the OBD Adapter Handler
            newObdAdapterHandlerConnection.SetRfcommSerialPortPath(appPrefs.loadedData.bluetoothSerialPortToUse);
            newObdAdapterHandlerConnection.SetChannelToUseInRfcomm(appPrefs.loadedData.bluetoothSerialPortChannelToUse);
            newObdAdapterHandlerConnection.SetPairedObdDeviceName(appPrefs.loadedData.configuredObdBtAdapter.deviceName);
            newObdAdapterHandlerConnection.SetPairedObdDeviceMac(appPrefs.loadedData.configuredObdBtAdapter.deviceMac);

            //Try to connect to OBD Adapter and Stablish a Serial Port
            newObdAdapterHandlerConnection.TryConnect();

            //Wait time
            yield return new Wait(0.5f);

            //Wait until exit from status of "Connecting"
            while (newObdAdapterHandlerConnection.currentConnectionStatus == ObdAdapterHandler.ConnectionStatus.Connecting)
                yield return new Wait(0.1f);

            //Hide the icon of trying to connect in status bar
            tryingConnectToObdButton.IsVisible = false;

            //If was failed to connect...
            if (newObdAdapterHandlerConnection.currentConnectionStatus == ObdAdapterHandler.ConnectionStatus.Disconnected)
            {
                //Reset the attempt status
                vehiclePanel_odbConnect_statusText.Text = GetStringApplicationResource("vehiclePanel_odbConnectInitialStatus");

                //Enable the unpair button
                vehiclePanel_obdConnect_unpairButton.IsVisible = true;

                //Analyse the logs of the connection try, and notify the user, if found the error string
                foreach (string line in newObdAdapterHandlerConnection.GetConnectionTryLogs())
                    if (line.Contains("Can't connect") == true)
                        ShowToast(GetStringApplicationResource("vehiclePanel_odbConnectErrorMsg").Replace("%e", line.Split(": ")[1].ToUpper()), ToastDuration.Short, ToastType.Problem);

                //Start a loop to wait time before continue
                int elapsedSeconds = 0;
                int secondsToWait = appPrefs.loadedData.invervalOfObdConnectionRetry;
                while (elapsedSeconds < secondsToWait)
                {
                    //Warn to debug
                    AvaloniaDebug.WriteLine(("A new connection attempt to Bluetooth OBD Device to Serial Port will be done in " + (secondsToWait - elapsedSeconds) + " seconds."));

                    //Wait one seconds
                    yield return new Wait(1.0f);

                    //Increase the seconds
                    elapsedSeconds += 1;
                }

                //Increase the connection try counter
                triesOfConnectionForBluetoothObdDevice += 1;

                //If excedeed the limite of attempts, stop this loop and continues...
                if (triesOfConnectionForBluetoothObdDevice > appPrefs.loadedData.maxOfObdConnectionRetry)
                {
                    //Run the callback of excedeed connection attempts
                    OnExceedLimitOfConnectionAttemptsToBluetoothObdDevice();

                    //Break this loop
                    break;
                }
            }

            //If was successfull connect
            if (newObdAdapterHandlerConnection.currentConnectionStatus == ObdAdapterHandler.ConnectionStatus.Connected)
            {
                //Register on lost connection callback
                newObdAdapterHandlerConnection.RegisterOnLostConnectionCallback(() =>
                {
                    //Clear the reference for the active connection for OBD Adapter Handler
                    activeObdConnection = null;

                    //Call the callback for run needed code
                    OnActiveConnectionForObdHandlerFinished();
                });

                //Store the reference and active connection for OBD Adapter Handler
                activeObdConnection = newObdAdapterHandlerConnection;

                //Show the connection symbol in status bar
                connectedToObdButton.IsVisible = true;

                //Start the final Panel
                StartReadyVehiclePanel();
            }
        }

        //Wait time
        yield return new Wait(0.5f);

        //Warn that is starting connection attempter
        AvaloniaDebug.WriteLine("Stopping Bluetooth OBD Device to Serial Port connection attempter...");

        //Inform that the routine was finished
        bluetoothObdDeviceConnectionRoutine = null;



        //Remove the task
        RemoveTask("bluetoothObdConnect");
    }

    private void OnExceedLimitOfConnectionAttemptsToBluetoothObdDevice()
    {
        //Notify the user
        ShowToast(GetStringApplicationResource("vehiclePanel_odbConnectExcedeedTries").Replace("%d", appPrefs.loadedData.configuredObdBtAdapter.deviceName), ToastDuration.Long, ToastType.Problem);

        //Disable all screens of the panel

        //Change to background of none
        backgroundForPanelSetupAndConnect.IsVisible = false;
        backgroundForPanelContent.IsVisible = false;

        //Change to screen of none
        vehiclePanel_obdSetupScreen.IsVisible = false;
        vehiclePanel_obdConnectScreen.IsVisible = false;
        vehiclePanel_panelScreen.IsVisible = false;
    }

    private void OnActiveConnectionForObdHandlerFinished()
    {
        //Clear the reference for the active connection for OBD Adapter Handler
        activeObdConnection = null;

        //Hide the connection symbol in status bar
        connectedToObdButton.IsVisible = false;

        //Clear the panel logs
        ClearAllVehiclePanelLogs();

        //Warn the user
        ShowToast(GetStringApplicationResource("vehiclePanel_odbConnectLostConnection").Replace("%d", appPrefs.loadedData.configuredObdBtAdapter.deviceName), ToastDuration.Short, ToastType.Problem);

        //Restart the vehicle panel
        StartVehiclePanel();
    }

    //Vehicle Panel methods: Final Panel methods

    private void StartReadyVehiclePanel()
    {
        //Change to background of panel
        backgroundForPanelSetupAndConnect.IsVisible = false;
        backgroundForPanelContent.IsVisible = true;

        //Change to screen of panel
        vehiclePanel_obdSetupScreen.IsVisible = false;
        vehiclePanel_obdConnectScreen.IsVisible = false;
        vehiclePanel_panelScreen.IsVisible = true;

        //Show the information of adapter in the place
        vehiclePanel_drawer_adapterTab_deviceName.Text = appPrefs.loadedData.configuredObdBtAdapter.deviceName;
        vehiclePanel_drawer_adapterTab_deviceMac.Text = appPrefs.loadedData.configuredObdBtAdapter.deviceMac;
        vehiclePanel_drawer_adapterTab_devicePin.Text = appPrefs.loadedData.configuredObdBtAdapter.devicePassword;

        //...
    }

    private void ToggleVehiclePanelDrawer()
    {
        //If the Drawer is opened
        if (isVehiclePanelDrawerOpen == true)
        {
            //If the routine is not running, start it
            if (openCloseVehiclePanelDrawerRoutine == null)
                openCloseVehiclePanelDrawerRoutine = CoroutineHandler.Start(CloseVehiclePanelDrawerRoutine());

            //Inform that is closed
            isVehiclePanelDrawerOpen = false;

            //Cancel
            return;
        }

        //If the Drawer is closed
        if (isVehiclePanelDrawerOpen == false)
        {
            //If the routine is not running, start it
            if (openCloseVehiclePanelDrawerRoutine == null)
                openCloseVehiclePanelDrawerRoutine = CoroutineHandler.Start(OpenVehiclePanelDrawerRoutine());

            //Inform that is opened
            isVehiclePanelDrawerOpen = true;

            //Cancel
            return;
        }
    }

    private IEnumerator<Wait> OpenVehiclePanelDrawerRoutine()
    {
        //Enable the background
        vehiclePanel_drawerBackground.IsVisible = true;
        vehiclePanel_drawerBackground.Opacity = 0.7;

        //Wait time
        yield return new Wait(0.2f);

        //Open the drawer
        vehiclePanel_drawer.Margin = new Thickness(0.0f, 0.0f, 0.0f, 0.0f);

        //Wait until animation finishes
        yield return new Wait(0.3f);

        //Change the drawer handler icon
        vehiclePanel_drawerHandlerIcon.Source = new Bitmap(AssetLoader.Open(new Uri("avares://Motoplay/Assets/drawer-handler-foreground-close.png")));

        //Inform that is finished
        openCloseVehiclePanelDrawerRoutine = null;
    }

    private IEnumerator<Wait> CloseVehiclePanelDrawerRoutine()
    {
        //Close the drawer
        vehiclePanel_drawer.Margin = new Thickness(0.0f, 0.0f, -352.0f, 0.0f);

        //Wait until animation finishes
        yield return new Wait(0.3f);

        //Disable the background
        vehiclePanel_drawerBackground.Opacity = 0.0;

        //Wait until animation finishes
        yield return new Wait(0.3f);

        //Disable the background completely
        vehiclePanel_drawerBackground.IsVisible = false;

        //Change the drawer handler icon
        vehiclePanel_drawerHandlerIcon.Source = new Bitmap(AssetLoader.Open(new Uri("avares://Motoplay/Assets/drawer-handler-foreground-open.png")));

        //Inform that is finished
        openCloseVehiclePanelDrawerRoutine = null;
    }

    private void SendVehiclePanelLog(string message)
    {
        //Detect if the scroll view is scrolled to end
        bool isScrollInEnd = false;
        if (vehiclePanel_drawer_logsTab_logScroll.Offset.Y >= vehiclePanel_drawer_logsTab_logScroll.ScrollBarMaximum.Y)
            isScrollInEnd = true;

        //Instantiate and store reference for it
        PanelLogItem item = new PanelLogItem();
        instantiatedPanelLogsInUi.Add(item);
        vehiclePanel_drawer_logsTab_logList.Children.Add(item);
        //Set it up
        item.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        item.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
        item.Width = double.NaN;
        item.Height = double.NaN;
        //Fill this item
        item.Time = DateTime.Now.ToString("HH:mm:ss");
        item.Message = message;

        //If the scroll view was at end, move it to end again
        vehiclePanel_drawer_logsTab_logScroll.ScrollToEnd();
    }

    private void ClearAllVehiclePanelLogs()
    {
        //Clear all existing logs
        foreach (PanelLogItem item in instantiatedPanelLogsInUi)
            vehiclePanel_drawer_logsTab_logList.Children.Remove(item);
        instantiatedPanelLogsInUi.Clear();
    }

    //Pages Manager

    private void SwitchAppPage(AppPage targetPage)
    {
        //Build a list of page menu buttons
        List<Button> pageButtons = new List<Button>();
        pageButtons.Add(menuPanelButton);
        pageButtons.Add(menuMetricsButton);
        pageButtons.Add(menuPlayerButton);
        pageButtons.Add(menuBrowserButton);
        pageButtons.Add(menuCameraButton);
        pageButtons.Add(menuPhoneButton);
        pageButtons.Add(menuPreferencesButton);

        //Build a list of page backgrounds
        List<Grid> pageBackgrounds = new List<Grid>();
        pageBackgrounds.Add(pageBgForPanel);
        pageBackgrounds.Add(pageBgForMetrics);
        pageBackgrounds.Add(pageBgForPlayer);
        pageBackgrounds.Add(pageBgForBrowser);
        pageBackgrounds.Add(pageBgForCamera);
        pageBackgrounds.Add(pageBgForPhone);
        pageBackgrounds.Add(pageBgForPreferences);

        //Build a list of page contents
        List<Grid> pageContents = new List<Grid>();
        pageContents.Add(pageContentForPanel);
        pageContents.Add(pageContentForMetrics);
        pageContents.Add(pageContentForPlayer);
        pageContents.Add(pageContentForBrowser);
        pageContents.Add(pageContentForCamera);
        pageContents.Add(pageContentForPhone);
        pageContents.Add(pageContentForPreferences);

        //Set all menu buttons as default
        foreach (Button item in pageButtons)
        {
            item.BorderThickness = new Thickness(0.0f, 0.0f, 0.0f, 0.0f);
            item.BorderBrush = new SolidColorBrush(new Color(255, 255, 255, 255));
        }
        //Disable all page backgrounds
        foreach (Grid item in pageBackgrounds)
            item.IsVisible = false;
        //Disable all page contents
        foreach (Grid item in pageContents)
            item.IsVisible = false;

        //Prepare the target page int ID
        int targetPageIndex = -1;
        //Translate the target page from enum to int ID
        switch (targetPage)
        {
            case AppPage.VehiclePanel: targetPageIndex = 0; break;
            case AppPage.GeneralMetrics: targetPageIndex = 1; break;
            case AppPage.MusicPlayer: targetPageIndex = 2; break;
            case AppPage.WebBrowser: targetPageIndex = 3; break;
            case AppPage.UsbCamera: targetPageIndex = 4; break;
            case AppPage.MirrorPhone: targetPageIndex = 5; break;
            case AppPage.AppPreferences: targetPageIndex = 6; break;
        }

        //Set color for the selected page menu item
        pageButtons[targetPageIndex].BorderThickness = new Thickness(4.0f, 4.0f, 4.0f, 4.0f);
        pageButtons[targetPageIndex].BorderBrush = new SolidColorBrush(new Color(255, 38, 197, 255));
        pageBackgrounds[targetPageIndex].IsVisible = true;
        pageContents[targetPageIndex].IsVisible = true;
    }

    //Preferences manager

    private void PrepareThePreferences()
    {
        //Prepare the UI of Preferences
        preferences_saveButton.Click += (s, e) => { SaveAllPreferences(); };
    }

    private void UpdatePreferencesOnUI()
    {
        //Show all current settings of save file in the UI

        //Keyboard Tab
        //*** pref_keyboard_heightScreenPercent
        if (appPrefs.loadedData.keyboardHeightScreenPercent == 0.2f)
            pref_keyboard_heightScreenPercent.SelectedIndex = 0;
        if (appPrefs.loadedData.keyboardHeightScreenPercent == 0.25f)
            pref_keyboard_heightScreenPercent.SelectedIndex = 1;
        if (appPrefs.loadedData.keyboardHeightScreenPercent == 0.35f)
            pref_keyboard_heightScreenPercent.SelectedIndex = 2;
        if (appPrefs.loadedData.keyboardHeightScreenPercent == 0.45f)
            pref_keyboard_heightScreenPercent.SelectedIndex = 3;

        //Panel Tab
        //*** preferences_panel_serialPortToUse
        if (appPrefs.loadedData.bluetoothSerialPortToUse == "/dev/rfcomm0")
            pref_panel_serialPortToUse.SelectedIndex = 0;
        if (appPrefs.loadedData.bluetoothSerialPortToUse == "/dev/rfcomm5")
            pref_panel_serialPortToUse.SelectedIndex = 1;
        if (appPrefs.loadedData.bluetoothSerialPortToUse == "/dev/rfcomm10")
            pref_panel_serialPortToUse.SelectedIndex = 2;
        if (appPrefs.loadedData.bluetoothSerialPortToUse == "/dev/rfcomm14")
            pref_panel_serialPortToUse.SelectedIndex = 3;
        if (appPrefs.loadedData.bluetoothSerialPortToUse == "/dev/rfcomm15")
            pref_panel_serialPortToUse.SelectedIndex = 4;
        if (appPrefs.loadedData.bluetoothSerialPortToUse == "/dev/rfcomm20")
            pref_panel_serialPortToUse.SelectedIndex = 5;
        if (appPrefs.loadedData.bluetoothSerialPortToUse == "/dev/rfcomm25")
            pref_panel_serialPortToUse.SelectedIndex = 6;
        if (appPrefs.loadedData.bluetoothSerialPortToUse == "/dev/rfcomm30")
            pref_panel_serialPortToUse.SelectedIndex = 7;
        if (appPrefs.loadedData.bluetoothSerialPortToUse == "/dev/rfcomm40")
            pref_panel_serialPortToUse.SelectedIndex = 8;
        if (appPrefs.loadedData.bluetoothSerialPortToUse == "/dev/rfcomm50")
            pref_panel_serialPortToUse.SelectedIndex = 9;
        //*** pref_panel_serialPortChannelToUse
        if (appPrefs.loadedData.bluetoothSerialPortChannelToUse == 1)
            pref_panel_serialPortChannelToUse.SelectedIndex = 0;
        if (appPrefs.loadedData.bluetoothSerialPortChannelToUse == 2)
            pref_panel_serialPortChannelToUse.SelectedIndex = 1;
        if (appPrefs.loadedData.bluetoothSerialPortChannelToUse == 3)
            pref_panel_serialPortChannelToUse.SelectedIndex = 2;
        if (appPrefs.loadedData.bluetoothSerialPortChannelToUse == 4)
            pref_panel_serialPortChannelToUse.SelectedIndex = 3;
        if (appPrefs.loadedData.bluetoothSerialPortChannelToUse == 5)
            pref_panel_serialPortChannelToUse.SelectedIndex = 4;
        //*** pref_panel_intervalObdConnectTries
        if (appPrefs.loadedData.invervalOfObdConnectionRetry == 30)
            pref_panel_intervalObdConnectTries.SelectedIndex = 0;
        if (appPrefs.loadedData.invervalOfObdConnectionRetry == 60)
            pref_panel_intervalObdConnectTries.SelectedIndex = 1;
        if (appPrefs.loadedData.invervalOfObdConnectionRetry == 300)
            pref_panel_intervalObdConnectTries.SelectedIndex = 2;
        //*** pref_panel_maxObdConnectTries
        if (appPrefs.loadedData.maxOfObdConnectionRetry == 5)
            pref_panel_maxObdConnectTries.SelectedIndex = 0;
        if (appPrefs.loadedData.maxOfObdConnectionRetry == 15)
            pref_panel_maxObdConnectTries.SelectedIndex = 1;
        if (appPrefs.loadedData.maxOfObdConnectionRetry == 999999999)
            pref_panel_maxObdConnectTries.SelectedIndex = 2;
    }

    private void SaveAllPreferences()
    {
        //Save all the settings of the UI to the save file

        //Keyboard Tab
        //*** pref_keyboard_heightScreenPercent
        if (pref_keyboard_heightScreenPercent.SelectedIndex == 0)
            appPrefs.loadedData.keyboardHeightScreenPercent = 0.2f;
        if (pref_keyboard_heightScreenPercent.SelectedIndex == 1)
            appPrefs.loadedData.keyboardHeightScreenPercent = 0.25f;
        if (pref_keyboard_heightScreenPercent.SelectedIndex == 2)
            appPrefs.loadedData.keyboardHeightScreenPercent = 0.35f;
        if (pref_keyboard_heightScreenPercent.SelectedIndex == 3)
            appPrefs.loadedData.keyboardHeightScreenPercent = 0.45f;

        //Panel Tab
        //*** preferences_panel_serialPortToUse
        if (pref_panel_serialPortToUse.SelectedIndex == 0)
            appPrefs.loadedData.bluetoothSerialPortToUse = "/dev/rfcomm0";
        if (pref_panel_serialPortToUse.SelectedIndex == 1)
            appPrefs.loadedData.bluetoothSerialPortToUse = "/dev/rfcomm5";
        if (pref_panel_serialPortToUse.SelectedIndex == 2)
            appPrefs.loadedData.bluetoothSerialPortToUse = "/dev/rfcomm10";
        if (pref_panel_serialPortToUse.SelectedIndex == 3)
            appPrefs.loadedData.bluetoothSerialPortToUse = "/dev/rfcomm14";
        if (pref_panel_serialPortToUse.SelectedIndex == 4)
            appPrefs.loadedData.bluetoothSerialPortToUse = "/dev/rfcomm15";
        if (pref_panel_serialPortToUse.SelectedIndex == 5)
            appPrefs.loadedData.bluetoothSerialPortToUse = "/dev/rfcomm20";
        if (pref_panel_serialPortToUse.SelectedIndex == 6)
            appPrefs.loadedData.bluetoothSerialPortToUse = "/dev/rfcomm25";
        if (pref_panel_serialPortToUse.SelectedIndex == 7)
            appPrefs.loadedData.bluetoothSerialPortToUse = "/dev/rfcomm30";
        if (pref_panel_serialPortToUse.SelectedIndex == 8)
            appPrefs.loadedData.bluetoothSerialPortToUse = "/dev/rfcomm40";
        if (pref_panel_serialPortToUse.SelectedIndex == 9)
            appPrefs.loadedData.bluetoothSerialPortToUse = "/dev/rfcomm50";
        //*** pref_panel_serialPortChannelToUse
        if (pref_panel_serialPortChannelToUse.SelectedIndex == 0)
            appPrefs.loadedData.bluetoothSerialPortChannelToUse = 1;
        if (pref_panel_serialPortChannelToUse.SelectedIndex == 1)
            appPrefs.loadedData.bluetoothSerialPortChannelToUse = 2;
        if (pref_panel_serialPortChannelToUse.SelectedIndex == 2)
            appPrefs.loadedData.bluetoothSerialPortChannelToUse = 3;
        if (pref_panel_serialPortChannelToUse.SelectedIndex == 3)
            appPrefs.loadedData.bluetoothSerialPortChannelToUse = 4;
        if (pref_panel_serialPortChannelToUse.SelectedIndex == 4)
            appPrefs.loadedData.bluetoothSerialPortChannelToUse = 5;
        //*** pref_panel_intervalObdConnectTries
        if (pref_panel_intervalObdConnectTries.SelectedIndex == 0)
            appPrefs.loadedData.invervalOfObdConnectionRetry = 30;
        if (pref_panel_intervalObdConnectTries.SelectedIndex == 1)
            appPrefs.loadedData.invervalOfObdConnectionRetry = 60;
        if (pref_panel_intervalObdConnectTries.SelectedIndex == 2)
            appPrefs.loadedData.invervalOfObdConnectionRetry = 300;
        //*** pref_panel_maxObdConnectTries
        if (pref_panel_maxObdConnectTries.SelectedIndex == 0)
            appPrefs.loadedData.maxOfObdConnectionRetry = 5;
        if (pref_panel_maxObdConnectTries.SelectedIndex == 1)
            appPrefs.loadedData.maxOfObdConnectionRetry = 15;
        if (pref_panel_maxObdConnectTries.SelectedIndex == 2)
            appPrefs.loadedData.maxOfObdConnectionRetry = 999999999;

        //Save the preferences to file
        appPrefs.Save();

        //If the post save routine is not running, run it
        if (postPreferencesSaveRoutine == null)
            postPreferencesSaveRoutine = CoroutineHandler.Start(DoPostPreferencesSaveRoutine());
    }

    private IEnumerator<Wait> DoPostPreferencesSaveRoutine()
    {
        //Add this task running
        AddTask("postSavePreferences", "Post-save preferences task.");

        //Disable the save preferences button
        preferences_saveButton.IsEnabled = false;

        //Wait time
        yield return new Wait(1.0f);

        //Renable the save preferences button
        preferences_saveButton.IsEnabled = true;

        //If the preferences page is enabled, notify the user
        if (pageContentForPreferences.IsVisible == true)
            ShowToast(GetStringApplicationResource("appPreferences_saveNotification"), ToastDuration.Long, ToastType.Normal);

        //Inform that the routine was finished
        postPreferencesSaveRoutine = null;

        //Remove the task running
        RemoveTask("postSavePreferences");
    }

    //Interaction Blocker Manager

    public void SetActiveInteractionBlocker(bool enabled)
    {
        //If is enabled...
        if (enabled == true)
            appInteractionBlocker.IsVisible = true;

        //If is disabled
        if (enabled == false)
            appInteractionBlocker.IsVisible = false;
    }

    //Toast manager

    public void ShowToast(string message, ToastDuration duration, ToastType tType)
    {
        //If is already running a toast notification, stop the routine
        if (showToastNotificationRoutine != null)
        {
            showToastNotificationRoutine.Cancel();
            showToastNotificationRoutine = null;
        }
        //If is already running a toas exit, stop the routine
        if (hideToastNotificationRoutine != null)
        {
            hideToastNotificationRoutine.Cancel();
            hideToastNotificationRoutine = null;
        }

        //Start the toast notification
        showToastNotificationRoutine = CoroutineHandler.Start(ShowToastRoutine(message, duration, tType));
    }

    public void HideToastNow()
    {
        //If is already running a toast notification, stop the routine
        if (showToastNotificationRoutine != null)
        {
            showToastNotificationRoutine.Cancel();
            showToastNotificationRoutine = null;
        }
        //If is already running a toas exit, stop the routine
        if (hideToastNotificationRoutine != null)
        {
            hideToastNotificationRoutine.Cancel();
            hideToastNotificationRoutine = null;
        }

        //Stop the toast notification now
        hideToastNotificationRoutine = CoroutineHandler.Start(HideToastRoutine());
    }

    private IEnumerator<Wait> ShowToastRoutine(string message, ToastDuration duration, ToastType type)
    {
        //Enable the dismiss button
        toastNotificationDismissButton.IsEnabled = true;

        //Reset the toast notification
        toastNotificationRoot.Margin = new Thickness(0, -256.0f, 0, 0);
        toastNotificationTimeBar.Value = 100.0f;
        toastNotificationRoot.IsVisible = true;

        //Set the color for the notification
        if (type == ToastType.Normal)
        {
            toastNotificationBg.Background = new SolidColorBrush(new Color(255, 66, 158, 189));
            toastNotificationBg.BorderBrush = new SolidColorBrush(new Color(255, 50, 119, 143));
            toastNotificationTimeBar.Foreground = new SolidColorBrush(new Color(255, 0, 206, 252));
        }
        if (type == ToastType.Problem)
        {
            toastNotificationBg.Background = new SolidColorBrush(new Color(255, 138, 11, 11));
            toastNotificationBg.BorderBrush = new SolidColorBrush(new Color(255, 89, 1, 1));
            toastNotificationTimeBar.Foreground = new SolidColorBrush(new Color(255, 255, 0, 0));
        }

        //Set the message
        toastNotificationText.Text = message;

        //Call the entry animation
        ((Animation)this.Resources["toastNotificationEntry"]).RunAsync(toastNotificationRoot);

        //Wait until the end of the animation
        yield return new Wait(0.5f);

        //Prepare the timer data
        long startTime = DateTime.Now.Ticks;
        long currentTime = startTime;
        float durationTime = ((duration == ToastDuration.Long) ? 10.0f : 5.0f);
        float elapsedTime = 0;
        int lastFilledBarsCount = -1;
        //Start a loop of regressive count
        while (elapsedTime < durationTime)
        {
            //Update the current time
            currentTime = DateTime.Now.Ticks;

            //Update elapsed time
            elapsedTime += (float)((new TimeSpan((currentTime - startTime))).TotalMilliseconds / 1000.0f);

            //Update starttime
            startTime = currentTime;

            //Update the progressbar
            toastNotificationTimeBar.Value = (float)(100.0f - (elapsedTime / durationTime) * 100.0f);

            //Update the window title
            string buildedTitle = "";
            int countOfTitleBarChars = 12;
            int filledBarChars = (int)((float)(1.0f - (elapsedTime / durationTime)) * (float)countOfTitleBarChars);
            if (filledBarChars != lastFilledBarsCount)
            {
                while (buildedTitle.Length < filledBarChars)
                    buildedTitle += "▮";
                while ((buildedTitle.Length - filledBarChars) < (countOfTitleBarChars - filledBarChars))
                    buildedTitle += "▯";
                this.Title = buildedTitle;

                //Update the last filled bars count
                lastFilledBarsCount = filledBarChars;
            }

            //Wait some time
            yield return new Wait(0.033f);
        }

        //Start the coroutine to hide the toast notification
        hideToastNotificationRoutine = CoroutineHandler.Start(HideToastRoutine());

        //Inform that the routine was finished
        showToastNotificationRoutine = null;
    }

    private IEnumerator<Wait> HideToastRoutine()
    {
        //Disable the dismiss button
        toastNotificationDismissButton.IsEnabled = false;

        //Restore the window title
        this.Title = originalWindowTitle;

        //Call the exit animation
        ((Animation)this.Resources["toastNotificationExit"]).RunAsync(toastNotificationRoot);

        //Wait until the end of the animation
        yield return new Wait(0.5f);

        //Reset the toast notification
        toastNotificationRoot.Margin = new Thickness(0, -256.0f, 0, 0);
        toastNotificationTimeBar.Value = 100.0f;
        toastNotificationRoot.IsVisible = false;

        //Inform that the routine was finished
        hideToastNotificationRoutine = null;
    }

    //Tasks manager

    public void AddTask(string id, string description)
    {
        //Add the task for the queue
        runningTasks.Add(id, description);

        //Update the tasks display
        UpdateTasksDisplay();
    }

    public void RemoveTask(string id)
    {
        //Remove the task from the queue
        runningTasks.Remove(id);

        //Update the tasks display
        UpdateTasksDisplay();
    }

    private void UpdateTasksDisplay()
    {
        //Count tasks quantity
        int tasksQuantity = GetRunningTasksCount();

        //If don't have more tasks
        if (tasksQuantity == 0)
            runningTasksIndicator.IsVisible = false;

        //If is doing tasks
        if (tasksQuantity >= 1)
            runningTasksIndicator.IsVisible = true;
    }

    public int GetRunningTasksCount()
    {
        //Return the running tasks count
        return runningTasks.Keys.Count;
    }

    //Keyboard methods

    private void ToggleVirtualKeyboard()
    {
        //If is keyboard not open
        if (wvkbdProcess == null)
        {
            //Prepare the Window of Feedback for virtual keyboard
            wvkbdFeedbackWindow = new KeyFeedbackWindow();
            wvkbdFeedbackWindow.Opened += (s, e) => { this.Focus(); };
            wvkbdFeedbackWindow.Position = new PixelPoint((int)(((float)Screens.Primary.Bounds.Width / 2.0f) - ((float)wvkbdFeedbackWindow.GetWindowWidth() / 2.0f)), 0);

            //Build the string of keyboard parameters
            StringBuilder keyboardParams = new StringBuilder();
            keyboardParams.Append(("-L " + (int)((float)Screens.Primary.Bounds.Height * appPrefs.loadedData.keyboardHeightScreenPercent)));
            keyboardParams.Append(" --fn \"Segoe UI 16\"");
            keyboardParams.Append(" --bg 000000AA");
            keyboardParams.Append(" --fg 323333F0");
            keyboardParams.Append(" --fg-sp 141414F0");
            keyboardParams.Append(" --press 00AEFFFF");
            keyboardParams.Append(" --press-sp 00AEFFFF");
            keyboardParams.Append(" --swipe 00AEFFFF");
            keyboardParams.Append(" --swipe-sp 00AEFFFF");
            keyboardParams.Append(" --text FFFFFFFF");
            keyboardParams.Append(" --text-sp FFFFFFFF");
            keyboardParams.Append(" --text-sp FFFFFFFF");
            keyboardParams.Append(" -O");

            //Start the wvkbd process
            wvkbdProcess = new Process() { StartInfo = new ProcessStartInfo() { FileName = (motoplayRootPath + "/Keyboard/wvkbd-mobintl"), Arguments = keyboardParams.ToString(), RedirectStandardOutput = true } };
            wvkbdProcess.OutputDataReceived += new DataReceivedEventHandler((s, e) => { ShowOnVirtualKeyboardKeyPressFeedback((string) (e.Data)); });
            wvkbdProcess.Start();
            wvkbdProcess.BeginOutputReadLine();
            wvkbdFeedbackWindow.Show();

            //Set the icon
            toggleKeyboardButtonImg.Source = new Bitmap(AssetLoader.Open(new Uri("avares://Motoplay/Assets/keyboard-off.png")));

            //If already is running keyboard tip routine, stop it
            if (openKeyboardTipRoutine != null)
            {
                openKeyboardTipRoutine.Cancel();
                openKeyboardTipRoutine = null;
            }
            openKeyboardTipRoutine = CoroutineHandler.Start(ShowOnOpenKeyboardTip());

            //Cancel here
            return;
        }

        //If is keyboard open
        if (wvkbdProcess != null)
        {
            //Close the Window of Feedback for virtual keyboard
            wvkbdFeedbackWindow.Close();
            wvkbdFeedbackWindow = null;

            //Stop the wvkbd process
            wvkbdProcess.Kill();
            wvkbdProcess = null;

            //Set the icon
            toggleKeyboardButtonImg.Source = new Bitmap(AssetLoader.Open(new Uri("avares://Motoplay/Assets/keyboard-on.png")));

            //Cancel here
            return;
        }
    }

    private void ShowOnVirtualKeyboardKeyPressFeedback(string key)
    {
        //If don't have content, cancel
        if (String.IsNullOrEmpty(key) == true)
            return;

        //If already is running feedback routine, cancel
        if (wvkbdFeedbackRoutine != null)
        {
            wvkbdFeedbackRoutine.Cancel();
            wvkbdFeedbackRoutine = null;
        }

        //Start a feedback routine for this key press
        wvkbdFeedbackRoutine = CoroutineHandler.Start(ShowVirtualKeyboardFeedbackRoutine(key));
    }

    private IEnumerator<Wait> ShowVirtualKeyboardFeedbackRoutine(string key)
    {
        //Show the pressed letter
        if (wvkbdFeedbackWindow != null)
            Dispatcher.UIThread.Invoke(() => { wvkbdFeedbackWindow.SetLetter(key); });

        //Wait time before continue
        yield return new Wait(1.0f);

        //Hide the feedback again
        if (wvkbdFeedbackWindow != null)
            Dispatcher.UIThread.Invoke(() => { wvkbdFeedbackWindow.RemoveLetter(); });

        //Inform that this feedback routine was finished
        wvkbdFeedbackRoutine = null;
    }

    private IEnumerator<Wait> ShowOnOpenKeyboardTip()
    {
        //Prepare the original width of the tip
        float ORIGINAL_TIP_WIDTH = 256.0f;

        //Prepare the tip
        toggleKeyboardTip.Width = 0;

        //Wait time before continue
        yield return new Wait(0.35f);

        //Inform the tip
        toggleKeyboardTip.IsVisible = true;
        toggleKeyboardTip.Width = ORIGINAL_TIP_WIDTH;

        //Wait time before continue
        yield return new Wait(5.0f);

        toggleKeyboardTip.Width = 0;

        //Wait time before continue
        yield return new Wait(0.35f);

        toggleKeyboardTip.IsVisible = false;
        toggleKeyboardTip.Width = ORIGINAL_TIP_WIDTH;

        //Inform that this routine is ended
        openKeyboardTipRoutine = null;
    }

    //Binded CLI Terminal methods

    private bool isBindedCliTerminalRented()
    {
        //Prepare to return response
        bool toReturn = false;

        //If is rented, inform
        if (currentTerminalCliRentKey != "")
            toReturn = true;

        //Return the response
        return toReturn;
    }

    private string RentTheBindedCliTerminal()
    {
        //If the binded CLI process is rented, cancel
        if (isBindedCliTerminalRented() == true)
        {
            AvaloniaDebug.WriteLine("The Binded CLI Process is already rented! Is not possible to rent it again, until he be released!");
            return null;
        }



        //Prepare to return the rent key
        string rentKey = "";

        //Prepare a array of allowed chars for rent keys
        string[] allowedCharsForRentKey = new string[] { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "a", "b", "c", "d", "e", "f" };

        //Generate a rent key
        while (rentKey.Length < 32)
            rentKey += allowedCharsForRentKey[(new Random()).Next(0, allowedCharsForRentKey.Length)];

        //Register the current key
        currentTerminalCliRentKey = rentKey;

        //Show the rent icon in status bar
        Dispatcher.UIThread.Invoke(() => { bindedCliButton.IsVisible = true; });

        //Warn
        AvaloniaDebug.WriteLine("Renting the Binded CLI Process for Task of Key \"" + rentKey + "\"...");

        //Return the rent key
        return rentKey;
    }

    private void ReleaseTheBindedCliTerminal(string currentRentKey)
    {
        //If the binded CLI process is not rented, cancel
        if (isBindedCliTerminalRented() == false)
        {
            AvaloniaDebug.WriteLine("The Binded CLI Process is not rented to be released!");
            return;
        }
        //If the provided key is empty, cancel
        if (string.IsNullOrEmpty(currentRentKey) == true)
        {
            AvaloniaDebug.WriteLine("Error on releasing a Rent of Binded CLI Process. The provided Ren Key is empty.");
            return;
        }
        //If the current rent key don't match with the registered one, cancel
        if (currentRentKey != currentTerminalCliRentKey)
        {
            AvaloniaDebug.WriteLine("Error on releasing a Rent of Binded CLI Process. The informed Rent Key don't match with the current Real Rent Key.");
            return;
        }



        //Hide the rent icon in status bar
        Dispatcher.UIThread.Invoke(() => { bindedCliButton.IsVisible = false; });

        //Warn
        AvaloniaDebug.WriteLine("Releasing the Binded CLI Process for Task of Key \"" + currentRentKey + "\"!");

        //Release the current key
        currentTerminalCliRentKey = "";
    }

    private void SendCommandToTerminalAndClearCurrentOutputLines(string bindedCliRentKey, string command)
    {
        //If this interaction is illegal, throw exception
        if (isBindedCliTerminalRented() == false || string.IsNullOrEmpty(bindedCliRentKey) == true || bindedCliRentKey != currentTerminalCliRentKey)
        {
            throw new Exception("Error interacting with Binded CLI Terminal. Maybe the Binded CLI is not Rented or the Rent Key provided is not the one being used by the Rental, or it is Invalid!");
            return;
        }



        //Clear the previous terminal output lines
        terminalReceivedOutputLines.Clear();

        //Send a command to terminal, with a embedded message of success, that will be sended at end of target command
        if (OperatingSystem.IsLinux() == true)
            terminalCliProcess.StandardInput.WriteLine((command + ";echo \"> Done Command! ForRentKey:" + currentTerminalCliRentKey + "\""));
        if (OperatingSystem.IsWindows() == true)
            terminalCliProcess.StandardInput.WriteLine((command + "&echo \"> Done Command! ForRentKey:" + currentTerminalCliRentKey + "\""));
    }

    private bool isLastCommandFinishedExecution(string bindedCliRentKey)
    {
        //If this interaction is illegal, throw exception
        if (isBindedCliTerminalRented() == false || string.IsNullOrEmpty(bindedCliRentKey) == true || bindedCliRentKey != currentTerminalCliRentKey)
        {
            throw new Exception("Error interacting with Binded CLI Terminal. Maybe the Binded CLI is not Rented or the Rent Key provided is not the one being used by the Rental, or it is Invalid!");
            return false;
        }



        //Prepare to return
        bool toReturn = false;

        //Check each line to get info
        foreach (string line in terminalReceivedOutputLines)
            if (line.Contains(("> Done Command! ForRentKey:" + currentTerminalCliRentKey)) == true)
            {
                toReturn = true;
                break;
            }

        //Return the result
        return toReturn;
    }

    private bool isThermFoundInTerminalOutputLines(string bindedCliRentKey, string therm)
    {
        //If this interaction is illegal, throw exception
        if (isBindedCliTerminalRented() == false || string.IsNullOrEmpty(bindedCliRentKey) == true || bindedCliRentKey != currentTerminalCliRentKey)
        {
            throw new Exception("Error interacting with Binded CLI Terminal. Maybe the Binded CLI is not Rented or the Rent Key provided is not the one being used by the Rental, or it is Invalid!");
            return false;
        }



        //Prepare to return
        bool toReturn = false;

        //Search the therm in terminal output lines
        foreach (string item in terminalReceivedOutputLines)
            if (item.Contains(therm) == true)
            {
                toReturn = true;
                break;
            }

        //Return the result
        return toReturn;
    }

    //Auxiliar methods

    public string GetStringApplicationResource(string resourceKey)
    {
        //Prepare the string to return
        string toReturn = "###";

        //Get the resource
        bool foundResource = (this.Resources.MergedDictionaries[0].TryGetResource(resourceKey, this.ActualThemeVariant, out object? resourceGetted));
        if (foundResource == true)
            if (resourceGetted != null)
                toReturn = (string)resourceGetted;

        //Return the string
        return toReturn;
    }

    private void InstallUpdateForApp()
    {
        //Block interactions
        SetActiveInteractionBlocker(true);

        //Disable the button
        updateAppButton.IsEnabled = false;

        //Start the process of Motoplay Installer
        Process updaterProcess = new Process() { StartInfo = new ProcessStartInfo() { FileName = (motoplayRootPath + "/Installer/InstallerMotoplay.Desktop"), Arguments = "online" }};
        updaterProcess.Start();
    }
    
    private void ScrollMenuTo(ScrollDirection direction)
    {
        //Start the routine of scroll
        CoroutineHandler.Start(ScrollMenuToRoutine(direction));
    }

    private IEnumerator<Wait> ScrollMenuToRoutine(ScrollDirection direction)
    {
        //Disable the buttons
        rollMenuUp.IsEnabled = false;
        rollMenuUp.Opacity = 0.35;
        rollMenuDown.IsEnabled = false;
        rollMenuDown.Opacity = 0.35;

        //Prepare the data
        float incrementScrollValue = 200.0f;
        float originScrollValue = (float) menuItensScroll.Offset.Y;

        //Prepare the timer data
        long startTime = DateTime.Now.Ticks;
        long currentTime = startTime;
        float durationTime = 0.25f;
        float elapsedTime = 0;
        //Start a loop of regressive count
        while (elapsedTime < durationTime)
        {
            //Update the current time
            currentTime = DateTime.Now.Ticks;

            //Update elapsed time
            elapsedTime += (float)((new TimeSpan((currentTime - startTime))).TotalMilliseconds / 1000.0f);

            //Update starttime
            startTime = currentTime;

            //If is to scroll to up...
            if (direction == ScrollDirection.Up)
                menuItensScroll.Offset = new Vector(0, (originScrollValue - (incrementScrollValue * (elapsedTime / durationTime))));
            //If is to scroll to down...
            if (direction == ScrollDirection.Down)
                menuItensScroll.Offset = new Vector(0, (originScrollValue + (incrementScrollValue * (elapsedTime / durationTime))));

            //Wait some time
            yield return new Wait(0.01f);
        }

        //Fix the scroll values
        if (menuItensScroll.Offset.Y < 0.0f)
            menuItensScroll.Offset = new Vector(0, 0);
        if (menuItensScroll.Offset.Y > menuItensScroll.ScrollBarMaximum.Y)
            menuItensScroll.Offset = new Vector(0, menuItensScroll.ScrollBarMaximum.Y);

        //Enable the buttons
        rollMenuUp.IsEnabled = true;
        rollMenuUp.Opacity = 1.0;
        rollMenuDown.IsEnabled = true;
        rollMenuDown.Opacity = 1.0;
    }
    
    private BluetoothDeviceInScanLogs AnalyzeLogAndGetPossibleBluetoothDeviceInfo(string logToAnalyze)
    {
        //Prepare the data to return
        BluetoothDeviceInScanLogs toReturn = new BluetoothDeviceInScanLogs();

        //If is Bluetooth Device log
        if (logToAnalyze.Contains(@"NEW") || logToAnalyze.Contains(@"DEL"))
        {
            //Discover the MAC of the device
            toReturn.deviceMac = logToAnalyze.Split(" ")[2];

            //Discover the Name of the device
            toReturn.deviceName = logToAnalyze.Replace((" Device " + toReturn.deviceMac + " "), "").Split("]")[1];

            //Discover the action of the device
            if (logToAnalyze.Contains("NEW") == true)
                toReturn.action = BluetoothDeviceAction.Appeared;
            if (logToAnalyze.Contains("DEL") == true)
                toReturn.action = BluetoothDeviceAction.Disappeared;
        }

        //Return the data
        return toReturn;
    }

    //Quit methods

    private void QuitApplication()
    {
        //Enable the iteraction blocker
        SetActiveInteractionBlocker(true);

        //Show the confirmation dialog
        var dialogResult = MessageBoxManager.GetMessageBoxStandard(GetStringApplicationResource("quitApplication_dialogTitle"),
                                                                   GetStringApplicationResource("quitApplication_dialogText"),
                                                                   ButtonEnum.YesNo, MsBox.Avalonia.Enums.Icon.Question).ShowAsync();

        //Register on finish dialog event
        dialogResult.GetAwaiter().OnCompleted(() =>
        {
            //Process the result
            if (dialogResult.Result == ButtonResult.Yes)
            {
                OnAboutToQuitApplication();
                this.Close();
            }
            if (dialogResult.Result != ButtonResult.Yes)
                SetActiveInteractionBlocker(false);
        });
    }

    private void OnAboutToQuitApplication()
    {
        //Warn that is quiting the application
        AvaloniaDebug.WriteLine("Closing Motoplay App...");

        //Force to disconnect the active OBD Adapter Handler, if have
        if(activeObdConnection != null)
        {
            activeObdConnection.ForceDisconnect();
            activeObdConnection = null;
        }
    }
}