using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Coroutine;
using MarcosTomaz.ATS;
using Motoplay.Scripts;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using static Motoplay.Scripts.MusicPlayer;

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
    public enum SpeedArc
    {
        kmh40Arc,
        kmh70Arc,
        kmh100Arc
    }
    public enum LightShiftColorState
    {
        Blue,
        Green,
        Red
    }
    public enum LightShiftRotationState
    {
        Up,
        Down
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
    private ActiveCoroutine panelEntryAnimationPhase1Routine = null;
    private ActiveCoroutine panelEntryAnimationPhase2Routine = null;
    private bool isPanelEntryAnimationFinished = false;
    private ActiveCoroutine panelSpeedArc40kmhEntryRoutine = null;
    private bool isSpeedArc40kmhEnabled = true;
    private ActiveCoroutine panelSpeedArc70kmhEntryRoutine = null;
    private bool isSpeedArc70kmhEnabled = true;
    private ActiveCoroutine panelSpeedArc100kmhEntryRoutine = null;
    private bool isSpeedArc100kmhEnabled = true;
    private ActiveCoroutine panelCommandLossUpdateRoutine = null;
    private ActiveCoroutine panelCommandPingUpdateRoutine = null;
    private ActiveCoroutine panelRpmUpdateRoutine = null;
    private ActiveCoroutine panelRpmTextUpdateRoutine = null;
    private ActiveCoroutine panelSpeedUpdateRoutine = null;
    private ActiveCoroutine panelCoolantTemperatureUpdateRoutine = null;
    private ActiveCoroutine panelEngineLoadUpdateRoutine = null;
    private ActiveCoroutine panelBatteryVoltageUpdateRoutine = null;
    private ActiveCoroutine panelGearUpdateRoutine = null;
    private ActiveCoroutine panelSpeedArcsUpdateRoutine = null;
    private ActiveCoroutine panelLightShiftBlinkRoutine = null;
    private ActiveCoroutine panelLightShiftUpdateRoutine = null;
    private ActiveCoroutine panelShutdownShortcutUpdateRoutine = null;
    private int disconnectionsCounterWithObdAdapter = 0;
    private Grid[] arrayOfMetricsContents = null;
    private List<string> musicPlayerDependenciesToInstall = new List<string>();
    private MusicPlayer musicPlayerHandler = null;
    private List<string> musicPlayerFileList = new List<string>();
    private int musicPlayerCurrentPlayingIndex = 0;
    private ActiveCoroutine musicPlayerSetSystemVolumeRoutine = null;
    private ActiveCoroutine musicPlayerShowVolumeRoutine = null;
    private bool isMusicPlayerDrawerOpen = false;
    private ActiveCoroutine openCloseMusicPlayerDrawerRoutine = null;
    private List<LibraryMusicItem> instantiatedLibraryMusicsInUi = new List<LibraryMusicItem>();
    private List<DriveMusicItem> instantiatedDriveMusicsInUi = new List<DriveMusicItem>();
    private ActiveCoroutine musicPlayerUpdatePairedBluetoothSoundList = null;
    private List<BluetoothSoundItem> instantiatedSoundDevicesInUi = new List<BluetoothSoundItem>();

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

        //Prepare the UI for General Metrics
        PrepareTheGeneralMetrics();

        //Prepare the UI for Music Player
        PrepareTheMusicPlayer();

        //Prepare the UI for Web Browser
        PrepareTheWebBrowser();

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
        vehiclePanel_shutdownShortcut_root.Margin = new Thickness(8.0f, -80.0f, 0.0f, 0.0f);
        vehiclePanel_shutdownShortcutButton.Click += (s, e) => { CoroutineHandler.Start(PanelShutdownShortcutRoutine()); };
        vehiclePanel_rebootButton.Click += (s, e) => { CoroutineHandler.Start(PanelRebootShortcutRoutine()); };

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
        yield return new Wait(0.5f);

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

        //Send command to restart the daemon
        SendCommandToTerminalAndClearCurrentOutputLines(rKey, "sudo systemctl daemon-reload");
        //Wait the end of command execution
        while (isLastCommandFinishedExecution(rKey) == false)
            yield return new Wait(0.1f);

        //Wait time
        yield return new Wait(8.0f);

        //Send command to disconnect, if is connected
        SendCommandToTerminalAndClearCurrentOutputLines(rKey, "sudo bluetoothctl disconnect " + appPrefs.loadedData.configuredObdBtAdapter.deviceMac);
        //Wait the end of command execution
        while (isLastCommandFinishedExecution(rKey) == false)
            yield return new Wait(0.1f);

        //Wait time
        yield return new Wait(0.5f);

        //Send command to kill the rfcomm port, if have
        SendCommandToTerminalAndClearCurrentOutputLines(rKey, "sudo fuser -k " + appPrefs.loadedData.bluetoothSerialPortToUse);
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
            newObdAdapterHandlerConnection.SetPairedObdDeviceBaudRate(appPrefs.loadedData.bluetoothBaudRate);
            newObdAdapterHandlerConnection.SetChannelToUseInRfcomm(appPrefs.loadedData.bluetoothSerialPortChannelToUse);
            newObdAdapterHandlerConnection.SetPairedObdDeviceName(appPrefs.loadedData.configuredObdBtAdapter.deviceName);
            newObdAdapterHandlerConnection.SetPairedObdDeviceMac(appPrefs.loadedData.configuredObdBtAdapter.deviceMac);
            newObdAdapterHandlerConnection.RegisterOnReceiveAlertDialogCallback((title, message) => { MessageBoxManager.GetMessageBoxStandard(title, message, ButtonEnum.Ok).ShowAsync(); });
            newObdAdapterHandlerConnection.RegisterOnReceiveLogCallback((message) => { SendVehiclePanelLog(message); });
            newObdAdapterHandlerConnection.SetRpmInterpolationSamplesInterval(appPrefs.loadedData.rpmInterpolationSampleIntervalMs);
            newObdAdapterHandlerConnection.SetRpmInterpolationAggressiveness(appPrefs.loadedData.rpmInterpolationAggressiveness);
            newObdAdapterHandlerConnection.SetMaxTransmissionGears(appPrefs.loadedData.maxTransmissionGears);
            newObdAdapterHandlerConnection.SetMinRpmToChangeFromGear1ToGear2(appPrefs.loadedData.minGear1RpmToChangeToGear2);
            newObdAdapterHandlerConnection.SetMinSpeedToChangeFromGear1ToGear2(appPrefs.loadedData.minGear1SpeedToChangeToGear2);
            newObdAdapterHandlerConnection.SetMaxPossibleSpeedAtGear(1, appPrefs.loadedData.maxPossibleGear1Speed);
            newObdAdapterHandlerConnection.SetMaxPossibleSpeedAtGear(2, appPrefs.loadedData.maxPossibleGear2Speed);
            newObdAdapterHandlerConnection.SetMaxPossibleSpeedAtGear(3, appPrefs.loadedData.maxPossibleGear3Speed);
            newObdAdapterHandlerConnection.SetMaxPossibleSpeedAtGear(4, appPrefs.loadedData.maxPossibleGear4Speed);
            newObdAdapterHandlerConnection.SetMaxPossibleSpeedAtGear(5, appPrefs.loadedData.maxPossibleGear5Speed);
            newObdAdapterHandlerConnection.SetMaxPossibleSpeedAtGear(6, appPrefs.loadedData.maxPossibleGear6Speed);
            newObdAdapterHandlerConnection.SetEngineMaxRpm(appPrefs.loadedData.vehicleMaxRpm);

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
                        ShowToast(GetStringApplicationResource("vehiclePanel_odbConnectErrorMsg").Replace("%e", line.Split(": ")[1].ToUpper()), ToastDuration.Long, ToastType.Problem);

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
                newObdAdapterHandlerConnection.RegisterOnLostConnectionCallback(() => { OnActiveConnectionForObdHandlerFinished(); });

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

        //Enable the maximum connection tries excedeed UI
        vehiclePanel_connectTriesExcedeed.IsVisible = true;
    }

    private void OnActiveConnectionForObdHandlerFinished()
    {
        //Warn the user about the disconnection
        ShowToast((GetStringApplicationResource("vehiclePanel_odbConnectLostConnection").Replace("%d", appPrefs.loadedData.configuredObdBtAdapter.deviceName) + 
                   "\n\n" + activeObdConnection.disconnectionAdditionalInformation),
                   ToastDuration.Long, ToastType.Problem);

        //Hide the connection symbol in status bar
        connectedToObdButton.IsVisible = false;

        //Clear the panel logs
        ClearAllVehiclePanelLogs();

        //Stop all panel entry animations, if is running
        if (panelEntryAnimationPhase1Routine != null)
        {
            panelEntryAnimationPhase1Routine.Cancel();
            panelEntryAnimationPhase1Routine = null;
        }
        if (panelEntryAnimationPhase2Routine != null)
        {
            panelEntryAnimationPhase2Routine.Cancel();
            panelEntryAnimationPhase2Routine = null;
        }

        //Stop any animation of speed arc, if is running
        if (panelSpeedArc40kmhEntryRoutine != null)
        {
            panelSpeedArc40kmhEntryRoutine.Cancel();
            panelSpeedArc40kmhEntryRoutine = null;
        }
        if (panelSpeedArc70kmhEntryRoutine != null)
        {
            panelSpeedArc70kmhEntryRoutine.Cancel();
            panelSpeedArc70kmhEntryRoutine = null;
        }
        if (panelSpeedArc100kmhEntryRoutine != null)
        {
            panelSpeedArc100kmhEntryRoutine.Cancel();
            panelSpeedArc100kmhEntryRoutine = null;
        }

        //Stop panel display updaters
        if (panelCommandLossUpdateRoutine != null)
        {
            panelCommandLossUpdateRoutine.Cancel();
            panelCommandLossUpdateRoutine = null;
        }
        if (panelCommandPingUpdateRoutine != null)
        {
            panelCommandPingUpdateRoutine.Cancel();
            panelCommandPingUpdateRoutine = null;
        }
        if (panelRpmUpdateRoutine != null)
        {
            panelRpmUpdateRoutine.Cancel();
            panelRpmUpdateRoutine = null;
        }
        if (panelSpeedUpdateRoutine != null)
        {
            panelSpeedUpdateRoutine.Cancel();
            panelSpeedUpdateRoutine = null;
        }
        if (panelRpmTextUpdateRoutine != null)
        {
            panelRpmTextUpdateRoutine.Cancel();
            panelRpmTextUpdateRoutine = null;
        }
        if (panelCoolantTemperatureUpdateRoutine != null)
        {
            panelCoolantTemperatureUpdateRoutine.Cancel();
            panelCoolantTemperatureUpdateRoutine = null;
        }
        if (panelEngineLoadUpdateRoutine != null)
        {
            panelEngineLoadUpdateRoutine.Cancel();
            panelEngineLoadUpdateRoutine = null;
        }
        if (panelBatteryVoltageUpdateRoutine != null)
        {
            panelBatteryVoltageUpdateRoutine.Cancel();
            panelBatteryVoltageUpdateRoutine = null;
        }
        if (panelGearUpdateRoutine != null)
        {
            panelGearUpdateRoutine.Cancel();
            panelGearUpdateRoutine = null;
        }
        if (panelSpeedArcsUpdateRoutine != null)
        {
            panelSpeedArcsUpdateRoutine.Cancel();
            panelSpeedArcsUpdateRoutine = null;
        }
        if (panelLightShiftBlinkRoutine != null)
        {
            panelLightShiftBlinkRoutine.Cancel();
            panelLightShiftBlinkRoutine = null;
        }
        if (panelLightShiftUpdateRoutine != null)
        {
            panelLightShiftUpdateRoutine.Cancel();
            panelLightShiftUpdateRoutine = null;
        }
        if (panelShutdownShortcutUpdateRoutine != null)
        {
            panelShutdownShortcutUpdateRoutine.Cancel();
            panelShutdownShortcutUpdateRoutine = null;
        }

        //Clear the reference for the active connection for OBD Adapter Handler
        activeObdConnection = null;

        //Increase the disconnections counter
        disconnectionsCounterWithObdAdapter += 1;

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

        //Set to Dark mode, if necessary
        SetPanelDarkModeIfNecessary();

        //Show the information of adapter in the place
        vehiclePanel_drawer_adapterTab_deviceName.Text = appPrefs.loadedData.configuredObdBtAdapter.deviceName;
        vehiclePanel_drawer_adapterTab_deviceMac.Text = appPrefs.loadedData.configuredObdBtAdapter.deviceMac;
        vehiclePanel_drawer_adapterTab_devicePin.Text = appPrefs.loadedData.configuredObdBtAdapter.devicePassword;

        //Initialize each element of panel
        vehiclePanel_fadeCover.IsVisible = true;
        vehiclePanel_rpmGauge.PrimaryPointerAngle = 0;
        vehiclePanel_rpmGauge.SecondaryPointerAngle = 0;
        vehiclePanel_rpmGauge.SecondayPointerVisible = true;
        vehiclePanel_rpmGauge.RpmValueAt0Percent = "0";
        vehiclePanel_rpmGauge.RpmValueAt0Visible = false;
        vehiclePanel_rpmGauge.RpmValueAt12Visible = false;
        vehiclePanel_rpmGauge.RpmValueAt25Percent = "4";
        vehiclePanel_rpmGauge.RpmValueAt25Visible = false;
        vehiclePanel_rpmGauge.RpmValueAt38Visible = false;
        vehiclePanel_rpmGauge.RpmValueAt50Percent = "7";
        vehiclePanel_rpmGauge.RpmValueAt50Visible = false;
        vehiclePanel_rpmGauge.RpmValueAt62Visible = false;
        vehiclePanel_rpmGauge.RpmValueAt75Percent = "10";
        vehiclePanel_rpmGauge.RpmValueAt75Visible = false;
        vehiclePanel_rpmGauge.RpmValueAt88Visible = false;
        vehiclePanel_rpmGauge.RpmValueAt100Percent = "14";
        vehiclePanel_rpmGauge.RpmValueAt100Visible = false;
        vehiclePanel_shiftLight_root.IsVisible = false;
        vehiclePanel_gearIndicatorText.Text = "-";
        vehiclePanel_speedometerText.Text = "0";
        vehiclePanel_speedometerUnitText.Text = GetStringApplicationResource("vehiclePanel_speedGauge_titleKmh");
        DisableSpeedArc(SpeedArc.kmh40Arc);
        DisableSpeedArc(SpeedArc.kmh70Arc);
        DisableSpeedArc(SpeedArc.kmh100Arc);
        vehiclePanel_rpmText.Text = "- rpm";
        vehiclePanel_coolTemperatureText.Text = "- °C";
        vehiclePanel_engineLoadText.Text = "-%";
        vehiclePanel_batterVoltageText.Text = "-.-v";
        vehiclePanel_adapterPingText.Text = "- ms";
        vehiclePanel_adapterLossText.Text = "- cl";

        //Run Panel entry animation
        RunPanelEntryAnimation();

        //Start panel display updates
        if (panelCommandLossUpdateRoutine == null)
            panelCommandLossUpdateRoutine = CoroutineHandler.Start(PanelCommandLossUpdateRoutine());
        if (panelCommandPingUpdateRoutine == null)
            panelCommandPingUpdateRoutine = CoroutineHandler.Start(PanelCommandSendAndReceivePingUpdateRoutine());
        if (panelRpmUpdateRoutine == null)
            panelRpmUpdateRoutine = CoroutineHandler.Start(PanelRpmUpdateRoutine());
        if (panelSpeedUpdateRoutine == null)
            panelSpeedUpdateRoutine = CoroutineHandler.Start(PanelSpeedUpdateRoutine());
        if (panelRpmTextUpdateRoutine == null)
            panelRpmTextUpdateRoutine = CoroutineHandler.Start(PanelRpmTextUpdateRoutine());
        if (panelCoolantTemperatureUpdateRoutine == null)
            panelCoolantTemperatureUpdateRoutine = CoroutineHandler.Start(PanelCoolantTemperatureUpdateRoutine());
        if (panelEngineLoadUpdateRoutine == null)
            panelEngineLoadUpdateRoutine = CoroutineHandler.Start(PanelEngineLoadUpdateRoutine());
        if (panelBatteryVoltageUpdateRoutine == null)
            panelBatteryVoltageUpdateRoutine = CoroutineHandler.Start(PanelBatteryVoltageUpdateRoutine());
        if (panelGearUpdateRoutine == null)
            panelGearUpdateRoutine = CoroutineHandler.Start(PanelGearUpdateRoutine());
        if (panelSpeedArcsUpdateRoutine == null)
            panelSpeedArcsUpdateRoutine = CoroutineHandler.Start(PanelSpeedArcsUpdateRoutine());
        if (panelLightShiftBlinkRoutine == null)
            panelLightShiftBlinkRoutine = CoroutineHandler.Start(PanelLightShiftBlinkRoutine());
        if (panelLightShiftUpdateRoutine == null)
            panelLightShiftUpdateRoutine = CoroutineHandler.Start(PanelLightShiftUpdateRoutine());
        if (panelShutdownShortcutUpdateRoutine == null)
            panelShutdownShortcutUpdateRoutine = CoroutineHandler.Start(PanelShutdownShortcutUpdateRoutine());
    }

    private IEnumerator<Wait> PanelCommandLossUpdateRoutine()
    {
        //Prepare the interval time
        Wait intervalTime = new Wait(0.5f);

        //Start the update loop
        while (true)
        {
            //If the panel is not currently active, just continues
            if (pageContentForPanel.IsVisible == false)
            {
                yield return intervalTime;
                continue;
            }

            //Update the command loss gauge
            vehiclePanel_adapterLossText.Text = (activeObdConnection.commandResponseLosses + " cl");

            //Wait time
            yield return intervalTime;
        }
    }

    private IEnumerator<Wait> PanelCommandSendAndReceivePingUpdateRoutine()
    {
        //Prepare the interval time
        Wait intervalTime = new Wait(0.2f);

        //Start the update loop
        while (true)
        {
            //If the panel is not currently active, just continues
            if (pageContentForPanel.IsVisible == false)
            {
                yield return intervalTime;
                continue;
            }

            //Update the ping gauge
            vehiclePanel_adapterPingText.Text = (activeObdConnection.sendAndReceivePing + " ms");

            //Wait time
            yield return intervalTime;
        }
    }

    private IEnumerator<Wait> PanelRpmUpdateRoutine()
    {
        //Show the RPM numbers
        int rpmAt25percent = (int)Math.Ceiling((((float)appPrefs.loadedData.vehicleMaxRpm * 0.25f) / 1000.0f));
        int rpmAt50percent = (int)Math.Ceiling((((float)appPrefs.loadedData.vehicleMaxRpm * 0.50f) / 1000.0f));
        int rpmAt75percent = (int)Math.Ceiling((((float)appPrefs.loadedData.vehicleMaxRpm * 0.75f) / 1000.0f));
        int rpmAt100percent = (int)Math.Ceiling((((float)appPrefs.loadedData.vehicleMaxRpm * 1.0f) / 1000.0f));
        vehiclePanel_rpmGauge.RpmValueAt0Percent = "00";
        if (rpmAt25percent < 10)
            vehiclePanel_rpmGauge.RpmValueAt25Percent = ("0" + rpmAt25percent);
        if (rpmAt25percent >= 10)
            vehiclePanel_rpmGauge.RpmValueAt25Percent = rpmAt25percent.ToString();
        if (rpmAt50percent < 10)
            vehiclePanel_rpmGauge.RpmValueAt50Percent = ("0" + rpmAt50percent);
        if (rpmAt50percent >= 10)
            vehiclePanel_rpmGauge.RpmValueAt50Percent = rpmAt50percent.ToString();
        if (rpmAt75percent < 10)
            vehiclePanel_rpmGauge.RpmValueAt75Percent = ("0" + rpmAt75percent);
        if (rpmAt75percent >= 10)
            vehiclePanel_rpmGauge.RpmValueAt75Percent = rpmAt75percent.ToString();
        if (rpmAt100percent < 10)
            vehiclePanel_rpmGauge.RpmValueAt100Percent = ("0" + rpmAt100percent);
        if (rpmAt100percent >= 10)
            vehiclePanel_rpmGauge.RpmValueAt100Percent = rpmAt100percent.ToString();

        //If the secondary pointer is not necessary, hide it
        if (appPrefs.loadedData.rpmDisplayType != 2)
            vehiclePanel_rpmGauge.SecondayPointerVisible = false;
        //If the secondary pointer is necessary, show it
        if (appPrefs.loadedData.rpmDisplayType == 2)
            vehiclePanel_rpmGauge.SecondayPointerVisible = true;

        //Prepare the RPM pointer suavization multiplier
        float RPM_POINTER_SMOOTH_MULTIPLIER = 6.0f;

        //Prepare the interval time
        Wait intervalTime = new Wait(0.022f);

        //Initialize the timer data
        long startTime = DateTime.Now.Ticks;
        long currentTime = startTime;

        //If the initialization is not finished, show the initialization steps in the RPM gauge
        while (vehiclePanel_rpmGauge.PrimaryPointerAngle < 248.0f)
        {
            //Update the current time
            currentTime = DateTime.Now.Ticks;

            //Get deltatime
            float deltaTime = (float)(new TimeSpan(currentTime).TotalSeconds - new TimeSpan(startTime).TotalSeconds);

            //Update the start time
            startTime = currentTime;

            //Get the current and the target angle for the pointer of RPM gauge
            float currentAngle = (float)vehiclePanel_rpmGauge.PrimaryPointerAngle;
            float targetAngle = (float)(((float)activeObdConnection.adapterInitializationCurrentStep / (float)activeObdConnection.adapterInitializationMaxSteps) * 250.0f);

            //Do a linear interpolation suavized animation
            vehiclePanel_rpmGauge.PrimaryPointerAngle = (((targetAngle - currentAngle) * (deltaTime * RPM_POINTER_SMOOTH_MULTIPLIER)) + currentAngle);

            //Fix the angle if passed
            if (vehiclePanel_rpmGauge.PrimaryPointerAngle < 0.0f)
                vehiclePanel_rpmGauge.PrimaryPointerAngle = 0.0f;
            if (vehiclePanel_rpmGauge.PrimaryPointerAngle > 250.0f)
                vehiclePanel_rpmGauge.PrimaryPointerAngle = 250.0f;

            //Wait time
            yield return intervalTime;
        }

        //Reset the timer data
        startTime = DateTime.Now.Ticks;
        currentTime = startTime;

        //Animate goin back to zero rpm
        while (vehiclePanel_rpmGauge.PrimaryPointerAngle > 10)
        {
            //Update the current time
            currentTime = DateTime.Now.Ticks;

            //Get deltatime
            float deltaTime = (float)(new TimeSpan(currentTime).TotalSeconds - new TimeSpan(startTime).TotalSeconds);

            //Update the start time
            startTime = currentTime;

            //Get the current and the target angle for the pointer of RPM gauge
            float currentAngle = (float)vehiclePanel_rpmGauge.PrimaryPointerAngle;
            float targetAngle = 0.0f;

            //Do a linear interpolation suavized animation
            vehiclePanel_rpmGauge.PrimaryPointerAngle = (((targetAngle - currentAngle) * (deltaTime * RPM_POINTER_SMOOTH_MULTIPLIER)) + currentAngle);

            //Fix the angle if passed
            if (vehiclePanel_rpmGauge.PrimaryPointerAngle < 0.0f)
                vehiclePanel_rpmGauge.PrimaryPointerAngle = 0.0f;
            if (vehiclePanel_rpmGauge.PrimaryPointerAngle > 250.0f)
                vehiclePanel_rpmGauge.PrimaryPointerAngle = 250.0f;

            //Wait time
            yield return intervalTime;
        }

        //Reset the timer data
        startTime = DateTime.Now.Ticks;
        currentTime = startTime;

        //Get the preferences max RPM of vehicle
        int maxRpm = appPrefs.loadedData.vehicleMaxRpm;

        //Start the rpm update loop
        while (true)
        {
            //If the panel is not currently active, just continues
            if (pageContentForPanel.IsVisible == false)
            {
                yield return intervalTime;
                continue;
            }

            //Update the current time
            currentTime = DateTime.Now.Ticks;

            //Get deltatime
            float deltaTime = (float)(new TimeSpan(currentTime).TotalSeconds - new TimeSpan(startTime).TotalSeconds);

            //Update the start time
            startTime = currentTime;

            //Prepare the RPM source
            int rpmSource = 0;
            if (appPrefs.loadedData.rpmDisplayType == 0)   //<- If is desired RAW RPM
                rpmSource = activeObdConnection.engineRpm;
            if (appPrefs.loadedData.rpmDisplayType == 1 || appPrefs.loadedData.rpmDisplayType == 2)   //<- If is desired Interpolated RPM
                rpmSource = activeObdConnection.engineRpmInterpolated;

            //Get the current and the target angle for the pointer of RPM gauge
            float currentAngle = (float)vehiclePanel_rpmGauge.PrimaryPointerAngle;
            float targetAngle = (((float)rpmSource / (float)maxRpm) * 250.0f);

            //Do a linear interpolation suavized animation
            vehiclePanel_rpmGauge.PrimaryPointerAngle = (((targetAngle - currentAngle) * (deltaTime * RPM_POINTER_SMOOTH_MULTIPLIER)) + currentAngle);

            //Fix the angle if passed
            if (vehiclePanel_rpmGauge.PrimaryPointerAngle < 0.0f)
                vehiclePanel_rpmGauge.PrimaryPointerAngle = 0.0f;
            if (vehiclePanel_rpmGauge.PrimaryPointerAngle > 250.0f)
                vehiclePanel_rpmGauge.PrimaryPointerAngle = 250.0f;

            //If is desired to show the RAW RPM on secondary pointer
            if (appPrefs.loadedData.rpmDisplayType == 2)
            {
                //Get the current and the target angle for the secondary pointer of RPM gauge
                float sCurrentAngle = (float)vehiclePanel_rpmGauge.SecondaryPointerAngle;
                float sTargetAngle = (((float)activeObdConnection.engineRpm / (float)maxRpm) * 250.0f);

                //Do a linear interpolation suavized animation
                vehiclePanel_rpmGauge.SecondaryPointerAngle = (((sTargetAngle - sCurrentAngle) * (deltaTime * RPM_POINTER_SMOOTH_MULTIPLIER)) + sCurrentAngle);

                //Fix the angle if passed
                if (vehiclePanel_rpmGauge.SecondaryPointerAngle < 0.0f)
                    vehiclePanel_rpmGauge.SecondaryPointerAngle = 0.0f;
                if (vehiclePanel_rpmGauge.SecondaryPointerAngle > 250.0f)
                    vehiclePanel_rpmGauge.SecondaryPointerAngle = 250.0f;
            }

            //Wait time
            yield return intervalTime;
        }
    }

    private IEnumerator<Wait> PanelRpmTextUpdateRoutine()
    {
        //Prepare the interval time
        Wait intervalTime = new Wait(0.15f);

        //Start the update loop
        while (true)
        {
            //If the panel is not currently active, just continues
            if (pageContentForPanel.IsVisible == false)
            {
                yield return intervalTime;
                continue;
            }

            //Update the RPM text
            if (appPrefs.loadedData.rpmTextDisplayType == 0)
                vehiclePanel_rpmText.Text = (activeObdConnection.engineRpm + " rpm");
            if (appPrefs.loadedData.rpmTextDisplayType == 1)
                vehiclePanel_rpmText.Text = (activeObdConnection.engineRpmInterpolated + " rpm");

            //Wait time
            yield return intervalTime;
        }
    }

    private IEnumerator<Wait> PanelSpeedUpdateRoutine()
    {
        //Show the speed unit
        if (appPrefs.loadedData.speedDisplayUnit == 0)
            vehiclePanel_speedometerUnitText.Text = GetStringApplicationResource("vehiclePanel_speedGauge_titleMph");
        if (appPrefs.loadedData.speedDisplayUnit == 1)
            vehiclePanel_speedometerUnitText.Text = GetStringApplicationResource("vehiclePanel_speedGauge_titleKmh");

        //Prepare the interval time
        Wait intervalTime = new Wait(0.25f);

        //Start the update loop
        while (true)
        {
            //If the panel is not currently active, just continues
            if (pageContentForPanel.IsVisible == false)
            {
                yield return intervalTime;
                continue;
            }

            //Update the speed gauge
            if (appPrefs.loadedData.speedDisplayUnit == 0)
                vehiclePanel_speedometerText.Text = activeObdConnection.vehicleSpeedMph.ToString();
            if (appPrefs.loadedData.speedDisplayUnit == 1)
                vehiclePanel_speedometerText.Text = activeObdConnection.vehicleSpeedKmh.ToString();

            //Wait time
            yield return intervalTime;
        }
    }

    private IEnumerator<Wait> PanelCoolantTemperatureUpdateRoutine()
    {
        //Prepare the interval time
        Wait intervalTime = new Wait(0.5f);

        //Start the update loop
        while (true)
        {
            //If the panel is not currently active, just continues
            if (pageContentForPanel.IsVisible == false)
            {
                yield return intervalTime;
                continue;
            }

            //Update the coolant temperature gauge
            if (appPrefs.loadedData.temperatureUnit == 0)
                vehiclePanel_coolTemperatureText.Text = (activeObdConnection.coolantTemperatureCelsius + " °C");
            if (appPrefs.loadedData.temperatureUnit == 1)
                vehiclePanel_coolTemperatureText.Text = (activeObdConnection.coolantTemperatureFarenheit + " °F");

            //Wait time
            yield return intervalTime;
        }
    }

    private IEnumerator<Wait> PanelEngineLoadUpdateRoutine()
    {
        //Prepare the interval time
        Wait intervalTime = new Wait(0.25f);

        //Start the update loop
        while (true)
        {
            //If the panel is not currently active, just continues
            if (pageContentForPanel.IsVisible == false)
            {
                yield return intervalTime;
                continue;
            }

            //Update the engine load gauge
            vehiclePanel_engineLoadText.Text = (activeObdConnection.engineLoad + "%");

            //Wait time
            yield return intervalTime;
        }
    }

    private IEnumerator<Wait> PanelBatteryVoltageUpdateRoutine()
    {
        //Prepare the interval time
        Wait intervalTime = new Wait(0.5f);

        //Start the update loop
        while (true)
        {
            //If the panel is not currently active, just continues
            if (pageContentForPanel.IsVisible == false)
            {
                yield return intervalTime;
                continue;
            }

            //Update the battery voltage gauge
            vehiclePanel_batterVoltageText.Text = (activeObdConnection.batteryVoltage + "v");

            //Wait time
            yield return intervalTime;
        }
    }

    private IEnumerator<Wait> PanelGearUpdateRoutine()
    {
        //Prepare the interval time
        Wait intervalTime = new Wait(0.25f);

        //Start the update loop
        while (true)
        {
            //If the panel is not currently active, just continues
            if (pageContentForPanel.IsVisible == false)
            {
                yield return intervalTime;
                continue;
            }

            //Update the gear gauge
            if (activeObdConnection.transmissionGear == -1)
                vehiclePanel_gearIndicatorText.Text = appPrefs.loadedData.letterToUseAsClutchPressed;
            if (activeObdConnection.transmissionGear == 0)
                vehiclePanel_gearIndicatorText.Text = appPrefs.loadedData.letterToUseAsGearStopped;
            if (activeObdConnection.transmissionGear > 0)
                vehiclePanel_gearIndicatorText.Text = activeObdConnection.transmissionGear.ToString();

            //Wait time
            yield return intervalTime;
        }
    }

    private IEnumerator<Wait> PanelSpeedArcsUpdateRoutine()
    {
        //Wait the panel entry animation finish
        while (isPanelEntryAnimationFinished == false)
            yield return new Wait(0.5f);

        //Prepare the interval time
        Wait intervalTime = new Wait(0.2f);

        //Start the update loop
        while (true)
        {
            //If the panel is not currently active, just continues
            if (pageContentForPanel.IsVisible == false)
            {
                yield return intervalTime;
                continue;
            }

            //Control the speed arc of 40km/h
            if (activeObdConnection.vehicleSpeedKmh >= 40)
                EnableSpeedArc(SpeedArc.kmh40Arc);
            if (activeObdConnection.vehicleSpeedKmh < 40)
                DisableSpeedArc(SpeedArc.kmh40Arc);

            //Control the speed arc of 70km/h
            if (activeObdConnection.vehicleSpeedKmh >= 70)
                EnableSpeedArc(SpeedArc.kmh70Arc);
            if (activeObdConnection.vehicleSpeedKmh < 70)
                DisableSpeedArc(SpeedArc.kmh70Arc);

            //Control the speed arc of 100km/h
            if (activeObdConnection.vehicleSpeedKmh >= 100)
                EnableSpeedArc(SpeedArc.kmh100Arc);
            if (activeObdConnection.vehicleSpeedKmh < 100)
                DisableSpeedArc(SpeedArc.kmh100Arc);

            //Wait time
            yield return intervalTime;
        }
    }

    private IEnumerator<Wait> PanelLightShiftBlinkRoutine()
    {
        //Reset the state of the light shift
        vehiclePanel_shiftLight_root.IsVisible = false;

        //Prepare the interval time
        Wait intervalTime = new Wait(0.25f);

        //Start the update loop
        while (true)
        {
            //If the panel is not currently active, just continues
            if (pageContentForPanel.IsVisible == false)
            {
                yield return intervalTime;
                continue;
            }

            //Blink the light shift
            vehiclePanel_shiftLight_root.IsVisible = !vehiclePanel_shiftLight_root.IsVisible;

            //Wait time
            yield return intervalTime;
        }
    }

    private IEnumerator<Wait> PanelLightShiftUpdateRoutine()
    {
        //Prepare possible colors for lightshift
        SolidColorBrush blueColor = new SolidColorBrush(new Color(255, 0, 170, 225));
        SolidColorBrush greenColor = new SolidColorBrush(new Color(255, 14, 227, 78));
        SolidColorBrush redColor = new SolidColorBrush(new Color(255, 255, 0, 0));

        //Prepare possible rotations for lightshift
        RotateTransform toUp = new RotateTransform(0);
        RotateTransform toDown = new RotateTransform(180);

        //Reset the state of the light shift
        vehiclePanel_shiftLight_light.Opacity = 0.0f;
        vehiclePanel_shiftLight_light.Fill = blueColor;
        vehiclePanel_shiftLight_light.RenderTransform = toUp;

        //Prepare the states of light shift
        LightShiftColorState currentColorState = LightShiftColorState.Blue;
        LightShiftRotationState currentRotationState = LightShiftRotationState.Up;

        //Prepare the interval time
        Wait intervalTime = new Wait(0.2f);

        //Start the update loop
        while (true)
        {
            //If the panel is not currently active, just continues
            if (pageContentForPanel.IsVisible == false)
            {
                yield return intervalTime;
                continue;
            }

            //Prepare the target state
            float targetOpacity = 0.0f;
            LightShiftColorState targetColorState = LightShiftColorState.Blue;
            LightShiftRotationState targetRotationState = LightShiftRotationState.Up;

            //Detect the new state for light shift
            targetOpacity = 0.0f;
            //Low
            if (activeObdConnection.engineRpm <= ((float)appPrefs.loadedData.vehicleMaxRpm * 0.47f))
            {
                targetOpacity = 1.0f;
                targetColorState = LightShiftColorState.Blue;
                targetRotationState = LightShiftRotationState.Down;
            }
            if (activeObdConnection.engineRpm <= ((float)appPrefs.loadedData.vehicleMaxRpm * 0.39f))
            {
                targetOpacity = 1.0f;
                targetColorState = LightShiftColorState.Green;
                targetRotationState = LightShiftRotationState.Down;
            }
            if (activeObdConnection.engineRpm <= ((float)appPrefs.loadedData.vehicleMaxRpm * 0.3f))
            {
                targetOpacity = 1.0f;
                targetColorState = LightShiftColorState.Red;
                targetRotationState = LightShiftRotationState.Down;
            }
            //Up
            if (activeObdConnection.engineRpm >= ((float)appPrefs.loadedData.vehicleMaxRpm * 0.6f))
            {
                targetOpacity = 1.0f;
                targetColorState = LightShiftColorState.Blue;
                targetRotationState = LightShiftRotationState.Up;
            }
            if (activeObdConnection.engineRpm >= ((float)appPrefs.loadedData.vehicleMaxRpm * 0.7f))
            {
                targetOpacity = 1.0f;
                targetColorState = LightShiftColorState.Green;
                targetRotationState = LightShiftRotationState.Up;
            }
            if (activeObdConnection.engineRpm >= ((float)appPrefs.loadedData.vehicleMaxRpm * 0.8f))
            {
                targetOpacity = 1.0f;
                targetColorState = LightShiftColorState.Red;
                targetRotationState = LightShiftRotationState.Up;
            }
            //If the vehicle is stopped, force light shift disable
            if (activeObdConnection.transmissionGear <= 0)
                targetOpacity = 0.0f;

            //Update the state of the light shift
            if (vehiclePanel_shiftLight_light.Opacity != targetOpacity)
                vehiclePanel_shiftLight_light.Opacity = targetOpacity;
            if (currentColorState != targetColorState)
            {
                if (targetColorState == LightShiftColorState.Blue)
                    vehiclePanel_shiftLight_light.Fill = blueColor;
                if (targetColorState == LightShiftColorState.Green)
                    vehiclePanel_shiftLight_light.Fill = greenColor;
                if (targetColorState == LightShiftColorState.Red)
                    vehiclePanel_shiftLight_light.Fill = redColor;
                currentColorState = targetColorState;
            }
            if (currentRotationState != targetRotationState)
            {
                if (targetRotationState == LightShiftRotationState.Up)
                    vehiclePanel_shiftLight_light.RenderTransform = toUp;
                if (targetRotationState == LightShiftRotationState.Down)
                    vehiclePanel_shiftLight_light.RenderTransform = toDown;
                currentRotationState = targetRotationState;
            }

            //Wait time
            yield return intervalTime;
        }
    }

    private IEnumerator<Wait> PanelShutdownShortcutUpdateRoutine()
    {
        //Wait the panel entry animation finish
        while (isPanelEntryAnimationFinished == false)
            yield return new Wait(0.5f);

        //Prepare the margins for the button
        Thickness openedMargin = new Thickness(8.0f, 8.0f, 0.0f, 0.0f);
        Thickness closedMargin = new Thickness(8.0f, -80.0f, 0.0f, 0.0f);

        //Reset the state of 
        vehiclePanel_shutdownShortcut_root.Margin = closedMargin;
        bool isButtonClosed = true;

        //Prepare the interval time
        Wait intervalTime = new Wait(1.0f);

        //Start the update loop
        while (true)
        {
            //If the panel is not currently active, just continues
            if (pageContentForPanel.IsVisible == false)
            {
                yield return intervalTime;
                continue;
            }

            //Prepare the target state
            bool shouldDisplayButton = false;

            //Detect the new target state
            if (activeObdConnection.transmissionGear <= 0)
                shouldDisplayButton = true;

            //Sync the state
            if (shouldDisplayButton == true)
                if (isButtonClosed == true)
                {
                    vehiclePanel_shutdownShortcut_root.Margin = openedMargin;
                    isButtonClosed = false;
                }
            if (shouldDisplayButton == false)
                if (isButtonClosed == false)
                {
                    vehiclePanel_shutdownShortcut_root.Margin = closedMargin;
                    isButtonClosed = true;
                }

            //Wait time
            yield return intervalTime;
        }
    }

    private IEnumerator<Wait> PanelShutdownShortcutRoutine()
    {
        //Disable the shutdown button
        vehiclePanel_shutdownShortcutButton.IsEnabled = false;


        //Add this task running
        AddTask("shutdownNow", "Shut down the Raspberry Pi");

        //If the Binded CLI Process is already rented by another task, wait until release
        while (isBindedCliTerminalRented() == true)
            yield return new Wait(0.5f);
        //Rent the Binded CLI Process
        string rKey = RentTheBindedCliTerminal();



        //Wait time
        yield return new Wait(0.1f);

        //Run on pre-quit event code
        OnAboutToQuitApplication();

        //Send command to kill Motoplay and shutdown the device
        SendCommandToTerminalAndClearCurrentOutputLines(rKey, "sudo pkill -f App/Motoplay.Desktop;shutdown now");
        //Wait the end of command execution
        while (isLastCommandFinishedExecution(rKey) == false)
            yield return new Wait(0.1f);



        //Release the Binded CLI Process
        ReleaseTheBindedCliTerminal(rKey);

        //Remove the task running
        RemoveTask("shutdownNow");
    }

    private IEnumerator<Wait> PanelRebootShortcutRoutine()
    {
        //Disable the reboot button
        vehiclePanel_rebootButton.IsEnabled = false;


        //Add this task running
        AddTask("rebootNow", "Reboot the Raspberry Pi");

        //If the Binded CLI Process is already rented by another task, wait until release
        while (isBindedCliTerminalRented() == true)
            yield return new Wait(0.5f);
        //Rent the Binded CLI Process
        string rKey = RentTheBindedCliTerminal();



        //Wait time
        yield return new Wait(0.1f);

        //Send command to kill Motoplay and reboot the device
        SendCommandToTerminalAndClearCurrentOutputLines(rKey, "sudo pkill -f App/Motoplay.Desktop;sudo reboot");
        //Wait the end of command execution
        while (isLastCommandFinishedExecution(rKey) == false)
            yield return new Wait(0.1f);



        //Release the Binded CLI Process
        ReleaseTheBindedCliTerminal(rKey);

        //Remove the task running
        RemoveTask("rebootNow");
    }

    private void RunPanelEntryAnimation()
    {
        //Reset the entry animation status
        isPanelEntryAnimationFinished = false;

        //If is already running the entry animation, stop coroutines
        if (panelEntryAnimationPhase1Routine != null)
        {
            panelEntryAnimationPhase1Routine.Cancel();
            panelEntryAnimationPhase1Routine = null;
        }
        if (panelEntryAnimationPhase2Routine != null)
        {
            panelEntryAnimationPhase2Routine.Cancel();
            panelEntryAnimationPhase2Routine = null;
        }

        //Start the panel entry animation
        panelEntryAnimationPhase1Routine = CoroutineHandler.Start(PanelEntryAnimationPhase1Routine());
    }

    private IEnumerator<Wait> PanelEntryAnimationPhase1Routine()
    {
        //Enable the fade cover
        vehiclePanel_fadeCover.IsVisible = true;

        //Wait a delay
        yield return new Wait(0.5f);

        //Enable the panel fade cover and run the entry animation
        vehiclePanel_fadeCover.IsVisible = true;
        ((Animation)this.Resources["panelFadeInCover"]).RunAsync(vehiclePanel_fadeCover);

        //Wait until the end of the animation
        yield return new Wait(1.0f);

        //Disable the panel fade cover
        vehiclePanel_fadeCover.IsVisible = false;

        //Inform that was finished
        panelEntryAnimationPhase1Routine = null;

        //Start the coroutine of phase 2
        panelEntryAnimationPhase2Routine = CoroutineHandler.Start(PanelEntryAnimationPhase2Routine());
    }

    private IEnumerator<Wait> PanelEntryAnimationPhase2Routine()
    {
        //Wait a delay
        yield return new Wait(0.5f);

        //Enable the 40km/h arc
        EnableSpeedArc(SpeedArc.kmh40Arc);

        //Wait a delay
        yield return new Wait(0.5f);

        //Enable the 70km/h arc
        EnableSpeedArc(SpeedArc.kmh70Arc);

        //Wait a delay
        yield return new Wait(0.5f);

        //Enable the 100km/h arc
        EnableSpeedArc(SpeedArc.kmh100Arc);

        //Wait a delay
        yield return new Wait(0.5f);

        //Enable the rpm value
        vehiclePanel_rpmGauge.RpmValueAt0Visible = true;

        //Wait a delay
        yield return new Wait(0.25f);

        //Enable the rpm value
        vehiclePanel_rpmGauge.RpmValueAt12Visible = true;

        //Wait a delay
        yield return new Wait(0.25f);

        //Enable the rpm value
        vehiclePanel_rpmGauge.RpmValueAt25Visible = true;

        //Wait a delay
        yield return new Wait(0.25f);

        //Enable the rpm value
        vehiclePanel_rpmGauge.RpmValueAt38Visible = true;

        //Wait a delay
        yield return new Wait(0.25f);

        //Enable the rpm value
        vehiclePanel_rpmGauge.RpmValueAt50Visible = true;

        //Wait a delay
        yield return new Wait(0.25f);

        //Enable the rpm value
        vehiclePanel_rpmGauge.RpmValueAt62Visible = true;

        //Wait a delay
        yield return new Wait(0.25f);

        //Enable the rpm value
        vehiclePanel_rpmGauge.RpmValueAt75Visible = true;

        //Wait a delay
        yield return new Wait(0.25f);

        //Enable the rpm value
        vehiclePanel_rpmGauge.RpmValueAt88Visible = true;

        //Wait a delay
        yield return new Wait(0.25f);

        //Enable the rpm value
        vehiclePanel_rpmGauge.RpmValueAt100Visible = true;

        //Wait a delay
        yield return new Wait(0.5f);

        //Disable the 40km/h arc
        DisableSpeedArc(SpeedArc.kmh40Arc);

        //Wait a delay
        yield return new Wait(0.15f);

        //Disable the 70km/h arc
        DisableSpeedArc(SpeedArc.kmh70Arc);

        //Wait a delay
        yield return new Wait(0.15f);

        //Disable the 100km/h arc
        DisableSpeedArc(SpeedArc.kmh100Arc);

        //Inform that the entry animation was finished
        isPanelEntryAnimationFinished = true;

        //Inform that was finished
        panelEntryAnimationPhase2Routine = null;
    }

    private void EnableSpeedArc(SpeedArc targetArc)
    {
        //Enable the desired arc
        if (targetArc == SpeedArc.kmh40Arc)
        {
            //If is already active, cancel
            if (isSpeedArc40kmhEnabled == true)
                return;
            //Stop if is already running
            if (panelSpeedArc40kmhEntryRoutine != null)
            {
                panelSpeedArc40kmhEntryRoutine.Cancel();
                panelSpeedArc40kmhEntryRoutine = null;
            }
            //Start the entry animation
            panelSpeedArc40kmhEntryRoutine = CoroutineHandler.Start(Kmh40SpeedArcEntryRoutine());
            //Inform that is active
            isSpeedArc40kmhEnabled = true;
        }
        if (targetArc == SpeedArc.kmh70Arc)
        {
            //If is already active, cancel
            if (isSpeedArc70kmhEnabled == true)
                return;
            //Stop if is already running
            if (panelSpeedArc70kmhEntryRoutine != null)
            {
                panelSpeedArc70kmhEntryRoutine.Cancel();
                panelSpeedArc70kmhEntryRoutine = null;
            }
            //Start the entry animation
            panelSpeedArc70kmhEntryRoutine = CoroutineHandler.Start(Kmh70SpeedArcEntryRoutine());
            //Inform that is active
            isSpeedArc70kmhEnabled = true;
        }
        if (targetArc == SpeedArc.kmh100Arc)
        {
            //If is already active, cancel
            if (isSpeedArc100kmhEnabled == true)
                return;
            //Stop if is already running
            if (panelSpeedArc100kmhEntryRoutine != null)
            {
                panelSpeedArc100kmhEntryRoutine.Cancel();
                panelSpeedArc100kmhEntryRoutine = null;
            }
            //Start the entry animation
            panelSpeedArc100kmhEntryRoutine = CoroutineHandler.Start(Kmh100SpeedArcEntryRoutine());
            //Inform that is active
            isSpeedArc100kmhEnabled = true;
        }
    }

    private IEnumerator<Wait> Kmh40SpeedArcEntryRoutine()
    {
        //Enable
        vehiclePanel_background_speedArc40Kmh.IsVisible = true;
        vehiclePanel_background_speedArc40Kmh_reflection.IsVisible = true;

        //Wait to flash
        yield return new Wait(0.1f);

        //Disable
        vehiclePanel_background_speedArc40Kmh.IsVisible = false;
        vehiclePanel_background_speedArc40Kmh_reflection.IsVisible = false;

        //Wait to flash
        yield return new Wait(0.1f);

        //Enable
        vehiclePanel_background_speedArc40Kmh.IsVisible = true;
        vehiclePanel_background_speedArc40Kmh_reflection.IsVisible = true;

        //Wait to flash
        yield return new Wait(0.1f);

        //Disable
        vehiclePanel_background_speedArc40Kmh.IsVisible = false;
        vehiclePanel_background_speedArc40Kmh_reflection.IsVisible = false;

        //Wait to flash
        yield return new Wait(0.1f);

        //Enable
        vehiclePanel_background_speedArc40Kmh.IsVisible = true;
        vehiclePanel_background_speedArc40Kmh_reflection.IsVisible = true;

        //Inform that was finished
        panelSpeedArc40kmhEntryRoutine = null;
    }

    private IEnumerator<Wait> Kmh70SpeedArcEntryRoutine()
    {
        //Enable
        vehiclePanel_background_speedArc70Kmh.IsVisible = true;
        vehiclePanel_background_speedArc70Kmh_reflection.IsVisible = true;

        //Wait to flash
        yield return new Wait(0.1f);

        //Disable
        vehiclePanel_background_speedArc70Kmh.IsVisible = false;
        vehiclePanel_background_speedArc70Kmh_reflection.IsVisible = false;

        //Wait to flash
        yield return new Wait(0.1f);

        //Enable
        vehiclePanel_background_speedArc70Kmh.IsVisible = true;
        vehiclePanel_background_speedArc70Kmh_reflection.IsVisible = true;

        //Wait to flash
        yield return new Wait(0.1f);

        //Disable
        vehiclePanel_background_speedArc70Kmh.IsVisible = false;
        vehiclePanel_background_speedArc70Kmh_reflection.IsVisible = false;

        //Wait to flash
        yield return new Wait(0.1f);

        //Enable
        vehiclePanel_background_speedArc70Kmh.IsVisible = true;
        vehiclePanel_background_speedArc70Kmh_reflection.IsVisible = true;

        //Inform that was finished
        panelSpeedArc70kmhEntryRoutine = null;
    }

    private IEnumerator<Wait> Kmh100SpeedArcEntryRoutine()
    {
        //Enable
        vehiclePanel_background_speedArc100Kmh.IsVisible = true;
        vehiclePanel_background_speedArc100Kmh_reflection.IsVisible = true;

        //Wait to flash
        yield return new Wait(0.1f);

        //Disable
        vehiclePanel_background_speedArc100Kmh.IsVisible = false;
        vehiclePanel_background_speedArc100Kmh_reflection.IsVisible = false;

        //Wait to flash
        yield return new Wait(0.1f);

        //Enable
        vehiclePanel_background_speedArc100Kmh.IsVisible = true;
        vehiclePanel_background_speedArc100Kmh_reflection.IsVisible = true;

        //Wait to flash
        yield return new Wait(0.1f);

        //Disable
        vehiclePanel_background_speedArc100Kmh.IsVisible = false;
        vehiclePanel_background_speedArc100Kmh_reflection.IsVisible = false;

        //Wait to flash
        yield return new Wait(0.1f);

        //Enable
        vehiclePanel_background_speedArc100Kmh.IsVisible = true;
        vehiclePanel_background_speedArc100Kmh_reflection.IsVisible = true;

        //Inform that was finished
        panelSpeedArc100kmhEntryRoutine = null;
    }

    private void DisableSpeedArc(SpeedArc targetArc)
    {
        //Disable the desired arc
        if (targetArc == SpeedArc.kmh40Arc)
        {
            //If is already disabled, cancel
            if (isSpeedArc40kmhEnabled == false)
                return;
            if (panelSpeedArc40kmhEntryRoutine != null)
            {
                panelSpeedArc40kmhEntryRoutine.Cancel();
                panelSpeedArc40kmhEntryRoutine = null;
            }
            vehiclePanel_background_speedArc40Kmh.IsVisible = false;
            vehiclePanel_background_speedArc40Kmh_reflection.IsVisible = false;
            //Inform that is disabled
            isSpeedArc40kmhEnabled = false;
        }
        if (targetArc == SpeedArc.kmh70Arc)
        {
            //If is already disabled, cancel
            if (isSpeedArc70kmhEnabled == false)
                return;
            if (panelSpeedArc70kmhEntryRoutine != null)
            {
                panelSpeedArc70kmhEntryRoutine.Cancel();
                panelSpeedArc70kmhEntryRoutine = null;
            }
            vehiclePanel_background_speedArc70Kmh.IsVisible = false;
            vehiclePanel_background_speedArc70Kmh_reflection.IsVisible = false;
            //Inform that is disabled
            isSpeedArc70kmhEnabled = false;
        }
        if (targetArc == SpeedArc.kmh100Arc)
        {
            //If is already disabled, cancel
            if (isSpeedArc100kmhEnabled == false)
                return;
            if (panelSpeedArc100kmhEntryRoutine != null)
            {
                panelSpeedArc100kmhEntryRoutine.Cancel();
                panelSpeedArc100kmhEntryRoutine = null;
            }
            vehiclePanel_background_speedArc100Kmh.IsVisible = false;
            vehiclePanel_background_speedArc100Kmh_reflection.IsVisible = false;
            //Inform that is disabled
            isSpeedArc100kmhEnabled = false;
        }
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

    private void SetPanelDarkModeIfNecessary()
    {
        //Prepare the result
        bool isDarkModeNecessary = false;

        //If the automatic mode is enabled, detect the dark mode using time
        if (appPrefs.loadedData.panelColorScheme == 0)
        {
            int currentHour = DateTime.Now.Hour;
            if (currentHour >= 19 || currentHour <= 7)
                isDarkModeNecessary = true;
        }

        //If the dark mode is enabled, force dark mode
        if (appPrefs.loadedData.panelColorScheme == 1)
            isDarkModeNecessary = true;

        //If the light mode is enabled, force to not use dark mode
        if (appPrefs.loadedData.panelColorScheme == 2)
            isDarkModeNecessary = false;

        //If not necessary, cancel here
        if (isDarkModeNecessary == false)
            return;

        //Do the changes to UI
        vehiclePanel_background_wallDarkOverlay.IsVisible = true;
        vehiclePanel_background_groundImage.Source = new Bitmap(AssetLoader.Open(new Uri("avares://Motoplay/Assets/panel-ground-dark-mode.png")));
        vehiclePanel_rpmGauge.RpmValuesColor = "#adadad";
        LinearGradientBrush brightRingGradient = new LinearGradientBrush();
        brightRingGradient.StartPoint = new RelativePoint(0.2f, 0.1f, RelativeUnit.Relative);
        brightRingGradient.EndPoint = new RelativePoint(0.5f, 1.0f, RelativeUnit.Relative);
        brightRingGradient.GradientStops.Add(new GradientStop(new Color(255, 0, 212, 255), 0.0f));
        brightRingGradient.GradientStops.Add(new GradientStop(new Color(255, 189, 244, 255), 1.0f));
        vehiclePanel_rpmGauge.brightRing.Stroke = brightRingGradient;
        vehiclePanel_gearIndicatorText.Foreground = new SolidColorBrush(new Color(255, 252, 252, 252));
        vehiclePanel_gearIndicatorTitle.Foreground = new SolidColorBrush(new Color(255, 227, 227, 227));
        vehiclePanel_speedometerText.Foreground = new SolidColorBrush(new Color(255, 224, 224, 224));
        vehiclePanel_speedometerUnitText.Foreground = new SolidColorBrush(new Color(255, 227, 227, 227));
        vehiclePanel_background_speedArc40Kmh.Stroke = new SolidColorBrush(new Color(255, 84, 184, 250));
        vehiclePanel_background_speedArc40Kmh_reflection_arc.Stroke = new SolidColorBrush(new Color(255, 84, 184, 250));
        vehiclePanel_background_speedArc70Kmh.Stroke = new SolidColorBrush(new Color(255, 111, 143, 165));
        vehiclePanel_background_speedArc70Kmh_reflection_arc.Stroke = new SolidColorBrush(new Color(255, 111, 143, 165));
        vehiclePanel_background_speedArc100Kmh.Stroke = new SolidColorBrush(new Color(255, 250, 215, 85));
        vehiclePanel_background_speedArc100Kmh_reflection_arc.Stroke = new SolidColorBrush(new Color(255, 250, 215, 85));
    }

    //General Metrics

    private void PrepareTheGeneralMetrics()
    {
        //Initialize the array of contents
        arrayOfMetricsContents = new Grid[3];
        arrayOfMetricsContents[0] = generalMetrics_content_disconnectionCount;
        arrayOfMetricsContents[1] = generalMetrics_content_obdResponseTime;
        arrayOfMetricsContents[2] = generalMetrics_content_timeSpentInGears;

        //Disable all contents
        foreach (Grid item in arrayOfMetricsContents)
            item.IsVisible = false;

        //Enable the default content
        arrayOfMetricsContents[0].IsVisible = true;

        //Detect the content change
        generalMetrics_contentSelector.SelectionChanged += (s, e) =>
        {
            //Disable all contents
            foreach (Grid item in arrayOfMetricsContents)
                item.IsVisible = false;

            //Enable the required content
            arrayOfMetricsContents[generalMetrics_contentSelector.SelectedIndex].IsVisible = true;
        };

        //Start the routine of general metrics
        CoroutineHandler.Start(GeneralMetricsRoutine());
    }

    private IEnumerator<Wait> GeneralMetricsRoutine()
    {
        //Prepare the cache data
        int secondsElapsedSinceLastUpdateOnDisconnectionsCounter = 0;
        int secondsElapsedSinceLastUpdateOfTimeSpendInEachGear = 0;
        TimeSpan timeElapsedOnGearStopped = new TimeSpan(0);
        TimeSpan timeElapsedOnGearClutch = new TimeSpan(0);
        TimeSpan timeElapsedOnGear1 = new TimeSpan(0);
        TimeSpan timeElapsedOnGear2 = new TimeSpan(0);
        TimeSpan timeElapsedOnGear3 = new TimeSpan(0);
        TimeSpan timeElapsedOnGear4 = new TimeSpan(0);
        TimeSpan timeElapsedOnGear5 = new TimeSpan(0);
        TimeSpan timeElapsedOnGear6 = new TimeSpan(0);

        //Initialize the "Time Spent In Each Gear" chart
        generalMetrics_content_timeSpentInGears_chart.InitializeForUse();
        generalMetrics_content_timeSpentInGears_chart.AddBar(GetStringApplicationResource("generalMetrics_timeSpentInEachGear_stopped"), 0.0f);
        generalMetrics_content_timeSpentInGears_chart.AddBar(GetStringApplicationResource("generalMetrics_timeSpentInEachGear_clutch"), 0.0f);
        generalMetrics_content_timeSpentInGears_chart.AddBar(GetStringApplicationResource("generalMetrics_timeSpentInEachGear_gear1"), 0.0f);
        generalMetrics_content_timeSpentInGears_chart.AddBar(GetStringApplicationResource("generalMetrics_timeSpentInEachGear_gear2"), 0.0f);
        generalMetrics_content_timeSpentInGears_chart.AddBar(GetStringApplicationResource("generalMetrics_timeSpentInEachGear_gear3"), 0.0f);
        generalMetrics_content_timeSpentInGears_chart.AddBar(GetStringApplicationResource("generalMetrics_timeSpentInEachGear_gear4"), 0.0f);
        generalMetrics_content_timeSpentInGears_chart.AddBar(GetStringApplicationResource("generalMetrics_timeSpentInEachGear_gear5"), 0.0f);
        generalMetrics_content_timeSpentInGears_chart.AddBar(GetStringApplicationResource("generalMetrics_timeSpentInEachGear_gear6"), 0.0f);
        generalMetrics_content_timeSpentInGears_chart.BuildChart();

        //Prepare the interval time
        Wait intervalTime = new Wait(1.0f);

        //Start the data collector loop
        while (true)
        {
            //START: ================= Number of Disconnections with OBD Adapter =================

            //Update the timer on UI
            if (pageContentForMetrics.IsVisible == true)
                generalMetrics_content_disconnectionCount_textUpdate.Text = GetStringApplicationResource("generalMetrics_metricsUpdate").
                                                                            Replace("%d", secondsElapsedSinceLastUpdateOnDisconnectionsCounter.ToString());

            //Decrese the time
            secondsElapsedSinceLastUpdateOnDisconnectionsCounter -= 1;

            //Update the counter if elpased minimum time
            if (secondsElapsedSinceLastUpdateOnDisconnectionsCounter <= 0)
            {
                //Do the update
                UpdateMetricsForNumberOfDisconnectionsOfObdAdapter(disconnectionsCounterWithObdAdapter);

                //Set a new timer
                secondsElapsedSinceLastUpdateOnDisconnectionsCounter = 5;
            }

            //END: ================= Number of Disconnections with OBD Adapter =================





            //START: ================= OBD Adapter Response Time (Ping) =================

            //Update the chart
            if (activeObdConnection != null)
                UpdateMetricsForObdAdapterResponseTime(activeObdConnection.sendAndReceivePing);
            if (activeObdConnection == null)
                UpdateMetricsForObdAdapterResponseTime(0);

            //END: ================= OBD Adapter Response Time (Ping) =================





            //START: ================= Time Spent In Each Gear =================

            //Update the timer on UI
            if (pageContentForMetrics.IsVisible == true)
                generalMetrics_content_timeSpentInGears_textUpdate.Text = GetStringApplicationResource("generalMetrics_metricsUpdate").
                                                                          Replace("%d", secondsElapsedSinceLastUpdateOfTimeSpendInEachGear.ToString());

            //Decrese the time
            secondsElapsedSinceLastUpdateOfTimeSpendInEachGear -= 1;

            //Update the chart if elpased minimum time
            if (secondsElapsedSinceLastUpdateOfTimeSpendInEachGear <= 0)
            {
                //Do the update
                UpdateMetricsForTimeSpentInEachGear(timeElapsedOnGearStopped.TotalMinutes, timeElapsedOnGearClutch.TotalMinutes, timeElapsedOnGear1.TotalMinutes,
                                                    timeElapsedOnGear2.TotalMinutes, timeElapsedOnGear3.TotalMinutes, timeElapsedOnGear4.TotalMinutes, timeElapsedOnGear5.TotalMinutes,
                                                    timeElapsedOnGear6.TotalMinutes);

                //Set a new timer
                secondsElapsedSinceLastUpdateOfTimeSpendInEachGear = 5;
            }

            //Collect the data
            if (activeObdConnection != null && activeObdConnection.currentConnectionStatus == ObdAdapterHandler.ConnectionStatus.Connected)
            {
                if (activeObdConnection.transmissionGear == -1)
                    timeElapsedOnGearClutch += TimeSpan.FromSeconds(1.0f);
                if (activeObdConnection.transmissionGear == 0)
                    timeElapsedOnGearStopped += TimeSpan.FromSeconds(1.0f);
                if (activeObdConnection.transmissionGear == 1)
                    timeElapsedOnGear1 += TimeSpan.FromSeconds(1.0f);
                if (activeObdConnection.transmissionGear == 2)
                    timeElapsedOnGear2 += TimeSpan.FromSeconds(1.0f);
                if (activeObdConnection.transmissionGear == 3)
                    timeElapsedOnGear3 += TimeSpan.FromSeconds(1.0f);
                if (activeObdConnection.transmissionGear == 4)
                    timeElapsedOnGear4 += TimeSpan.FromSeconds(1.0f);
                if (activeObdConnection.transmissionGear == 5)
                    timeElapsedOnGear5 += TimeSpan.FromSeconds(1.0f);
                if (activeObdConnection.transmissionGear == 6)
                    timeElapsedOnGear6 += TimeSpan.FromSeconds(1.0f);
            }

            //END: ================= Time Spent In Each Gear =================

            //Wait the interval
            yield return intervalTime;
        }
    }

    private void UpdateMetricsForNumberOfDisconnectionsOfObdAdapter(int number)
    {
        //If the metrics page is not enabled, don't update
        if (pageContentForMetrics.IsVisible == false)
            return;

        //Update metric in UI
        generalMetrics_content_disconnectionCount_text.Text = number.ToString();
    }

    private void UpdateMetricsForTimeSpentInEachGear(double stoppedMins, double clutchMins, double gear1Mins, double gear2Mins, double gear3Mins, double gear4Mins, double gear5Mins, double gear6Mins)
    {
        //If the metrics page is not enabled, don't update
        if (pageContentForMetrics.IsVisible == false)
            return;

        //Update metric in UI
        generalMetrics_content_timeSpentInGears_chart.UpdateBar(0, stoppedMins);
        generalMetrics_content_timeSpentInGears_chart.UpdateBar(1, clutchMins);
        generalMetrics_content_timeSpentInGears_chart.UpdateBar(2, gear1Mins);
        generalMetrics_content_timeSpentInGears_chart.UpdateBar(3, gear2Mins);
        generalMetrics_content_timeSpentInGears_chart.UpdateBar(4, gear3Mins);
        generalMetrics_content_timeSpentInGears_chart.UpdateBar(5, gear4Mins);
        generalMetrics_content_timeSpentInGears_chart.UpdateBar(6, gear5Mins);
        generalMetrics_content_timeSpentInGears_chart.UpdateBar(7, gear6Mins);
    }

    private void UpdateMetricsForObdAdapterResponseTime(int ping)
    {
        //If the metrics page is not enabled, don't update
        if (pageContentForMetrics.IsVisible == false)
            return;

        //Update metric in UI
        generalMetrics_content_obdResponseTime_chart.AddValue(ping);
    }

    //Music Player

    private void PrepareTheMusicPlayer()
    {
        //If the musics folder, not exists, create it
        if (Directory.Exists((motoplayRootPath + "/Musics")) == false)
            Directory.CreateDirectory((motoplayRootPath + "/Musics"));

        //Prepare the base UI
        musicPlayer_dependenciesScreen_installButton.Click += (s, e) => { CoroutineHandler.Start(InstallMusicPlayerDependencies()); };
        musicPlayer_skipPreviousButton.Click += (s, e) => { MusicPlayerSkipToPrevious(); };
        musicPlayer_playPauseButton.Click += (s, e) => { MusicPlayerPlayPause(); };
        musicPlayer_skipNextButton.Click += (s, e) => { MusicPlayerSkipToNext(); };
        musicPlayer_volumeUpButton.Click += (s, e) => { MusicPlayerVolumeUp(); };
        musicPlayer_volumeDownButton.Click += (s, e) => { MusicPlayerVolumeDown(); };
        musicPlayer_drawerHandler.PointerPressed += (s, e) => { ToggleMusicPlayerDrawer(); };
        musicPlayer_drawerBackground.PointerPressed += (s, e) => { ToggleMusicPlayerDrawer(); };
        musicPlayer_openOutputSelectorButton.Click += (s, e) => { CoroutineHandler.Start(EmulateMouseMoveToSpeakerIconAndRightClick()); };

        //Change to background of loading
        musicPlayer_background_loading.IsVisible = true;
        musicPlayer_background_player.IsVisible = false;

        //Change to screen of loading
        musicPlayer_loadScreen.IsVisible = true;
        musicPlayer_dependenciesScreen.IsVisible = false;
        musicPlayer_playerScreen.IsVisible = false;

        //Start the dependencies checking
        CoroutineHandler.Start(CheckMusicPlayerDependencies());
    }

    private IEnumerator<Wait> CheckMusicPlayerDependencies()
    {
        //Change to background of loading
        musicPlayer_background_loading.IsVisible = true;
        musicPlayer_background_player.IsVisible = false;

        //Change to screen of loading
        musicPlayer_loadScreen.IsVisible = true;
        musicPlayer_dependenciesScreen.IsVisible = false;
        musicPlayer_playerScreen.IsVisible = false;

        //Add this task running
        AddTask("musicPlayerDependenciesCheck", "Checks dependencies for Motoplay Music Player functionality.");

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
            //Send a command to check if the "libvlc-dev" is installed
            SendCommandToTerminalAndClearCurrentOutputLines(rKey, "sudo dpkg -s libvlc-dev");
            //Wait the end of command execution
            while (isLastCommandFinishedExecution(rKey) == false)
                yield return new Wait(0.1f);

            //If not installed, add to list of install
            if (isThermFoundInTerminalOutputLines(rKey, "is not installed") == true)
                musicPlayerDependenciesToInstall.Add("libvlc-dev");

            //Send a command to check if the "vlc" is installed
            SendCommandToTerminalAndClearCurrentOutputLines(rKey, "sudo dpkg -s vlc");
            //Wait the end of command execution
            while (isLastCommandFinishedExecution(rKey) == false)
                yield return new Wait(0.1f);

            //If not installed, add to list of install
            if (isThermFoundInTerminalOutputLines(rKey, "is not installed") == true)
                musicPlayerDependenciesToInstall.Add("vlc");
        }

        //If Windows, just continue...
        if (OperatingSystem.IsWindows() == true)
            AvaloniaDebug.WriteLine("None additional dependencie is required in Windows.");

        //If have dependencies to install, change to install screen
        if (musicPlayerDependenciesToInstall.Count > 0)
        {
            //Change to background of loading
            musicPlayer_background_loading.IsVisible = true;
            musicPlayer_background_player.IsVisible = false;

            //Change to screen of dependencies install request
            musicPlayer_loadScreen.IsVisible = false;
            musicPlayer_dependenciesScreen.IsVisible = true;
            musicPlayer_playerScreen.IsVisible = false;
        }

        //If not have dependencies to install, initialize the Music Player
        if (musicPlayerDependenciesToInstall.Count == 0)
            InitializeTheMusicPlayer();



        //Release the Binded CLI Process
        ReleaseTheBindedCliTerminal(rKey);

        //Remove the task running
        RemoveTask("musicPlayerDependenciesCheck");
    }

    private IEnumerator<Wait> InstallMusicPlayerDependencies()
    {
        //Disable the install dependencies button
        musicPlayer_dependenciesScreen_installButton.IsEnabled = false;

        //Change to background of loading
        musicPlayer_background_loading.IsVisible = true;
        musicPlayer_background_player.IsVisible = false;

        //Change to screen of loading
        musicPlayer_loadScreen.IsVisible = true;
        musicPlayer_dependenciesScreen.IsVisible = false;
        musicPlayer_playerScreen.IsVisible = false;

        //Add this task running
        AddTask("musicPlayerDependenciesInstall", "Install dependencies for Motoplay Music Player functionality.");

        //If the Binded CLI Process is already rented by another task, wait until release
        while (isBindedCliTerminalRented() == true)
            yield return new Wait(0.5f);
        //Rent the Binded CLI Process
        string rKey = RentTheBindedCliTerminal();



        //Wait time
        yield return new Wait(1.0f);

        //While have dependencies to install
        while (musicPlayerDependenciesToInstall.Count > 0)
        {
            //Wait time
            yield return new Wait(1.0f);

            //Send a command to install the first package of list
            SendCommandToTerminalAndClearCurrentOutputLines(rKey, ("sudo apt-get install " + musicPlayerDependenciesToInstall[0] + " -y"));
            //Wait the end of command execution
            while (isLastCommandFinishedExecution(rKey) == false)
                yield return new Wait(0.1f);

            //Wait time
            yield return new Wait(1.0f);

            //Send a command to confirm that the first package is installed
            SendCommandToTerminalAndClearCurrentOutputLines(rKey, ("sudo dpkg -s " + musicPlayerDependenciesToInstall[0]));
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

            //Remove the first installed package from the list
            musicPlayerDependenciesToInstall.RemoveAt(0);
        }

        //Go back to dependencies check
        CoroutineHandler.Start(CheckMusicPlayerDependencies());



        //Release the Binded CLI Process
        ReleaseTheBindedCliTerminal(rKey);

        //Remove the task running
        RemoveTask("musicPlayerDependenciesInstall");
    }

    private void InitializeTheMusicPlayer()
    {
        //Change to background of player
        musicPlayer_background_loading.IsVisible = false;
        musicPlayer_background_player.IsVisible = true;

        //Change to screen of dependencies install request
        musicPlayer_loadScreen.IsVisible = false;
        musicPlayer_dependenciesScreen.IsVisible = false;
        musicPlayer_playerScreen.IsVisible = true;

        //Initialize the music player instance, only one time
        if (musicPlayerHandler == null)
        {
            //Create a new instance
            musicPlayerHandler = new MusicPlayer(appPrefs.loadedData.playerVolume);

            //Register the callback of start loading new music
            musicPlayerHandler.RegisterOnStartLoadingNewMusicCallback(() =>
            {
                //Reset the track name
                musicPlayer_musicName.Text = "-";
                musicPlayer_musicAuthor.Text = "-";

                //Show the time
                musicPlayer_musicCurrentTime.Text = "00:00";
                musicPlayer_musicProgress.Value = 0.0f;
                musicPlayer_musicTotalTime.Text = "00:00";

                //Change covers to default
                musicPlayer_cover1.Source = new Bitmap(AssetLoader.Open(new Uri("avares://Motoplay/Assets/no-album-cover.png")));
                musicPlayer_cover2.Source = new Bitmap(AssetLoader.Open(new Uri("avares://Motoplay/Assets/no-album-cover.png")));
                musicPlayer_cover3.Source = new Bitmap(AssetLoader.Open(new Uri("avares://Motoplay/Assets/no-album-cover.png")));
                musicPlayer_cover4.Source = new Bitmap(AssetLoader.Open(new Uri("avares://Motoplay/Assets/no-album-cover.png")));
                musicPlayer_cover5.Source = new Bitmap(AssetLoader.Open(new Uri("avares://Motoplay/Assets/no-album-cover.png")));
                musicPlayer_musicsCount.Text = "-/-";

                //Disable skip buttons
                musicPlayer_skipPreviousButton.IsVisible = false;
                musicPlayer_skipNextButton.IsVisible = false;
            });

            //Register the callback of metadata loaded
            musicPlayerHandler.RegisterOnLoadMusicMetadataCallback((MusicMetadata music, Bitmap coverBitmap, string musicName, string musicAuthor, string musicExtension) =>
            {
                //If is current music
                if (music == MusicMetadata.Current)
                {
                    //Render the text metadata
                    musicPlayer_musicName.Text = musicName;
                    musicPlayer_musicAuthor.Text = musicAuthor;

                    //Render the cover
                    musicPlayer_cover1.Source = coverBitmap;
                    musicPlayer_background_album.Source = coverBitmap;
                }

                //If is for nexts
                if (music == MusicMetadata.Next2)
                    musicPlayer_cover2.Source = coverBitmap;
                if (music == MusicMetadata.Next3)
                    musicPlayer_cover3.Source = coverBitmap;
                if (music == MusicMetadata.Next4)
                    musicPlayer_cover4.Source = coverBitmap;

                //If is the last cover loaded
                if (music == MusicMetadata.Next5)
                {
                    musicPlayer_cover5.Source = coverBitmap;

                    //Renable the skip buttons
                    musicPlayer_skipPreviousButton.IsVisible = true;
                    musicPlayer_skipNextButton.IsVisible = true;
                }
            });

            //Register the callback of pause
            musicPlayerHandler.RegisterOnPausedCallback(() => 
            {
                //Change the UI
                musicPlayer_musicsCount.Text = ((musicPlayerCurrentPlayingIndex + 1) + "/" + musicPlayerFileList.Count);
                musicPlayer_skipPreviousButton.IsEnabled = true;
                musicPlayer_playPauseButton.IsEnabled = true;
                musicPlayer_playPauseImage.Source = new Bitmap(AssetLoader.Open(new Uri("avares://Motoplay/Assets/play-icon.png")));
                musicPlayer_skipNextButton.IsEnabled = true;
            });

            //Register the callback of play
            musicPlayerHandler.RegisterOnPlayedCallback(() =>
            {
                //Change the UI
                musicPlayer_musicsCount.Text = ((musicPlayerCurrentPlayingIndex + 1) + "/" + musicPlayerFileList.Count);
                musicPlayer_skipPreviousButton.IsEnabled = true;
                musicPlayer_playPauseButton.IsEnabled = true;
                musicPlayer_playPauseImage.Source = new Bitmap(AssetLoader.Open(new Uri("avares://Motoplay/Assets/pause-icon.png")));
                musicPlayer_skipNextButton.IsEnabled = true;

                //Reset the volume of system, if is enabled
                if (appPrefs.loadedData.resetSystemVolumeOnPlaySong == true)
                    if (musicPlayerSetSystemVolumeRoutine == null)
                        musicPlayerSetSystemVolumeRoutine = CoroutineHandler.Start(SetSystemVolumeRoutine(100));
            });

            //Register the callback of stop
            musicPlayerHandler.RegisterOnStoppedCallback(() =>
            {
                //Change the UI
                musicPlayer_skipPreviousButton.IsEnabled = false;
                musicPlayer_playPauseButton.IsEnabled = false;
                musicPlayer_playPauseImage.Source = new Bitmap(AssetLoader.Open(new Uri("avares://Motoplay/Assets/play-icon.png")));
                musicPlayer_skipNextButton.IsEnabled = false;
            });

            //Register the callback of time changed
            musicPlayerHandler.RegisterOnUpdateTimeCallback((curTime, maxTime, progress) => 
            {
                //Change the UI
                musicPlayer_musicCurrentTime.Text = curTime;
                musicPlayer_musicProgress.Value = progress;
                musicPlayer_musicTotalTime.Text = maxTime;
            });

            //Register the callback of finished
            musicPlayerHandler.RegisterOnFinishedTimeCallback(() => 
            {
                //Go to next music
                MusicPlayerSkipToNext();
            });

            //Setup the equalization
            if (appPrefs.loadedData.equalizerProfile == 2)
            {
                musicPlayerHandler.SetEqualization(appPrefs.loadedData.equalizerAmplifierValue,
                                                   appPrefs.loadedData.equalizerBand31hz,
                                                   appPrefs.loadedData.equalizerBand62hz,
                                                   appPrefs.loadedData.equalizerBand125hz,
                                                   appPrefs.loadedData.equalizerBand250hz,
                                                   appPrefs.loadedData.equalizerBand500hz,
                                                   appPrefs.loadedData.equalizerBand1khz,
                                                   appPrefs.loadedData.equalizerBand2khz,
                                                   appPrefs.loadedData.equalizerBand4khz,
                                                   appPrefs.loadedData.equalizerBand8khz,
                                                   appPrefs.loadedData.equalizerBand16khz);
            }
            if (appPrefs.loadedData.equalizerProfile == 1)
                musicPlayerHandler.SetEqualization(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            if (appPrefs.loadedData.equalizerProfile == 0)
                musicPlayerHandler.SetEqualizationDisabled();

            //If is enabled the auto play or auto resume
            if (appPrefs.loadedData.autoPauseOnStopVehicle == true || appPrefs.loadedData.autoPlayOnVehicleMove == true || appPrefs.loadedData.automaticVolume == true)
                CoroutineHandler.Start(MusicPlayerVehicleMoveMonitor());

            //Render all musics of library
            RenderAllMusicsOfLibrary();
        }

        //Reset the file list
        musicPlayerFileList.Clear();
        //Reset the playing index
        musicPlayerCurrentPlayingIndex = 0;

        //Get the list of musics
        foreach (FileInfo file in (new DirectoryInfo((motoplayRootPath + "/Musics")).GetFiles()))
            if (Path.GetExtension(file.FullName).ToLower() == ".mp3")
                musicPlayerFileList.Add(file.FullName);
        foreach (FileInfo file in (new DirectoryInfo((motoplayRootPath + "/Musics")).GetFiles()))
            if (Path.GetExtension(file.FullName).ToLower() == ".m4a")
                musicPlayerFileList.Add(file.FullName);
        foreach (FileInfo file in (new DirectoryInfo((motoplayRootPath + "/Musics")).GetFiles()))
            if (Path.GetExtension(file.FullName).ToLower() == ".ogg")
                musicPlayerFileList.Add(file.FullName);
        foreach (FileInfo file in (new DirectoryInfo((motoplayRootPath + "/Musics")).GetFiles()))
            if (Path.GetExtension(file.FullName).ToLower() == ".wmv")
                musicPlayerFileList.Add(file.FullName);
        foreach (FileInfo file in (new DirectoryInfo((motoplayRootPath + "/Musics")).GetFiles()))
            if (Path.GetExtension(file.FullName).ToLower() == ".wav")
                musicPlayerFileList.Add(file.FullName);

        //Show or hide the volume buttons control
        if (appPrefs.loadedData.automaticVolume == true)
        {
            musicPlayer_volumeUpButton.IsEnabled = false;
            musicPlayer_volumeDownButton.IsEnabled = false;
        }
        if (appPrefs.loadedData.automaticVolume == false)
        {
            musicPlayer_volumeUpButton.IsEnabled = true;
            musicPlayer_volumeDownButton.IsEnabled = true;
        }

        //If don't have musics
        if (musicPlayerFileList.Count == 0)
        {
            //Set default background
            musicPlayer_background_album.Source = new Bitmap(AssetLoader.Open(new Uri("avares://Motoplay/Assets/music-player-background.png")));

            //Disable additional covers
            musicPlayer_cover5.IsVisible = false;
            musicPlayer_cover5.Source = new Bitmap(AssetLoader.Open(new Uri("avares://Motoplay/Assets/no-album-cover.png")));
            musicPlayer_cover4.IsVisible = false;
            musicPlayer_cover4.Source = new Bitmap(AssetLoader.Open(new Uri("avares://Motoplay/Assets/no-album-cover.png")));
            musicPlayer_cover3.IsVisible = false;
            musicPlayer_cover3.Source = new Bitmap(AssetLoader.Open(new Uri("avares://Motoplay/Assets/no-album-cover.png")));
            musicPlayer_cover2.IsVisible = false;
            musicPlayer_cover2.Source = new Bitmap(AssetLoader.Open(new Uri("avares://Motoplay/Assets/no-album-cover.png")));
            musicPlayer_cover1.IsVisible = true;
            musicPlayer_cover1.Source = new Bitmap(AssetLoader.Open(new Uri("avares://Motoplay/Assets/no-album-cover.png")));
            musicPlayer_musicsCount.Text = "0/0";
            musicPlayer_coverSeparator1.IsVisible = false;
            musicPlayer_coverSeparator2.IsVisible = false;
            musicPlayer_coverSeparator3.IsVisible = false;
            musicPlayer_coverSeparator4.IsVisible = false;

            //Show empty name
            musicPlayer_musicName.Text = GetStringApplicationResource("musicPlayer_noMusicsName");
            musicPlayer_musicAuthor.Text = GetStringApplicationResource("musicPlayer_noMusicsArtist");

            //Show the time
            musicPlayer_musicCurrentTime.Text = "--:--";
            musicPlayer_musicProgress.Value = 0.0f;
            musicPlayer_musicTotalTime.Text = "--:--";

            //Hide buttons
            musicPlayer_skipPreviousButton.IsEnabled = false;
            musicPlayer_playPauseButton.IsEnabled = false;
            musicPlayer_playPauseImage.Source = new Bitmap(AssetLoader.Open(new Uri("avares://Motoplay/Assets/play-icon.png")));
            musicPlayer_skipNextButton.IsEnabled = false;

            //Disable the volume text
            musicPlayer_volumeText.IsVisible = false;
        }

        //If have musics
        if (musicPlayerFileList.Count > 0)
        {
            //Set default background
            musicPlayer_background_album.Source = new Bitmap(AssetLoader.Open(new Uri("avares://Motoplay/Assets/music-player-background.png")));

            //Disable additional covers
            musicPlayer_cover5.IsVisible = true;
            musicPlayer_cover5.Source = new Bitmap(AssetLoader.Open(new Uri("avares://Motoplay/Assets/no-album-cover.png")));
            musicPlayer_cover4.IsVisible = true;
            musicPlayer_cover4.Source = new Bitmap(AssetLoader.Open(new Uri("avares://Motoplay/Assets/no-album-cover.png")));
            musicPlayer_cover3.IsVisible = true;
            musicPlayer_cover3.Source = new Bitmap(AssetLoader.Open(new Uri("avares://Motoplay/Assets/no-album-cover.png")));
            musicPlayer_cover2.IsVisible = true;
            musicPlayer_cover2.Source = new Bitmap(AssetLoader.Open(new Uri("avares://Motoplay/Assets/no-album-cover.png")));
            musicPlayer_cover1.IsVisible = true;
            musicPlayer_cover1.Source = new Bitmap(AssetLoader.Open(new Uri("avares://Motoplay/Assets/no-album-cover.png")));
            musicPlayer_musicsCount.Text = "-/-";
            musicPlayer_coverSeparator1.IsVisible = true;
            musicPlayer_coverSeparator2.IsVisible = true;
            musicPlayer_coverSeparator3.IsVisible = true;
            musicPlayer_coverSeparator4.IsVisible = true;

            //Show temp name
            musicPlayer_musicName.Text = "-";
            musicPlayer_musicAuthor.Text = "-";

            //Show temp time
            musicPlayer_musicCurrentTime.Text = "--:--";
            musicPlayer_musicProgress.Value = 0.0f;
            musicPlayer_musicTotalTime.Text = "--:--";

            //Show buttons
            musicPlayer_skipPreviousButton.IsEnabled = true;
            musicPlayer_playPauseButton.IsEnabled = true;
            musicPlayer_playPauseImage.Source = new Bitmap(AssetLoader.Open(new Uri("avares://Motoplay/Assets/play-icon.png")));
            musicPlayer_skipNextButton.IsEnabled = true;

            //Disable the volume text
            musicPlayer_volumeText.IsVisible = false;

            //If is desired to shuffle, do it
            if (appPrefs.loadedData.randomizeMusicList == true)
                musicPlayerFileList.Shuffle();

            //Automatically change the Player to first music of list
            string[] currentAndNext4Musics = new string[5];
            int index = musicPlayerCurrentPlayingIndex;
            int inListCount = 0;
            while (inListCount < 5)
            {
                //If the index is greather than the musics in list, reset the index
                if (index == musicPlayerFileList.Count)
                    index = 0;

                //Add to array of musics
                currentAndNext4Musics[inListCount] = musicPlayerFileList[index];

                //Increase the index
                index += 1;
                inListCount += 1;
            }
            musicPlayerHandler.ChangeMusicTo(currentAndNext4Musics, false);
        }

        //Sync the volume to UI
        UpdateVolumeBar();
    }

    private void MusicPlayerSkipToPrevious()
    {
        //Change the music index to previous
        musicPlayerCurrentPlayingIndex -= 1;
        if (musicPlayerCurrentPlayingIndex < 0)
            musicPlayerCurrentPlayingIndex = (musicPlayerFileList.Count - 1);

        //Generate the array of current music and next 4 musics
        string[] currentAndNext4Musics = new string[5];
        int index = musicPlayerCurrentPlayingIndex;
        int inListCount = 0;
        while (inListCount < 5)
        {
            //If the index is greather than the musics in list, reset the index
            if (index == musicPlayerFileList.Count)
                index = 0;

            //Add to array of musics
            currentAndNext4Musics[inListCount] = musicPlayerFileList[index];

            //Increase the index
            index += 1;
            inListCount += 1;
        }
        
        //Change the music player to the current new music
        musicPlayerHandler.ChangeMusicTo(currentAndNext4Musics, true);
    }

    private void MusicPlayerPlayPause()
    {
        //If is playing, pause it
        if (musicPlayerHandler.isPlaying() == true)
        {
            musicPlayerHandler.Pause();
            return;
        }

        //If is paused, play it
        if (musicPlayerHandler.isPlaying() == false)
        {
            musicPlayerHandler.Play();
            return;
        }
    }

    private void MusicPlayerStop()
    {
        //Stop the music player
        musicPlayerHandler.Stop();
    }

    private void MusicPlayerSkipToNext()
    {
        //Change the music index to next
        musicPlayerCurrentPlayingIndex += 1;
        if (musicPlayerCurrentPlayingIndex == musicPlayerFileList.Count)
            musicPlayerCurrentPlayingIndex = 0;

        //Generate the array of current music and next 4 musics
        string[] currentAndNext4Musics = new string[5];
        int index = musicPlayerCurrentPlayingIndex;
        int inListCount = 0;
        while (inListCount < 5)
        {
            //If the index is greather than the musics in list, reset the index
            if (index == musicPlayerFileList.Count)
                index = 0;

            //Add to array of musics
            currentAndNext4Musics[inListCount] = musicPlayerFileList[index];

            //Increase the index
            index += 1;
            inListCount += 1;
        }

        //Change the music player to the current new music
        musicPlayerHandler.ChangeMusicTo(currentAndNext4Musics, true);
    }

    private void MusicPlayerVolumeUp()
    {
        //Add volume
        appPrefs.loadedData.playerVolume += 15;

        //Fix bound
        if (appPrefs.loadedData.playerVolume > 150)
            appPrefs.loadedData.playerVolume = 150;

        //Save
        SaveAllPreferences();

        //Apply to media player
        if (musicPlayerHandler != null)
            musicPlayerHandler.SetVolume(appPrefs.loadedData.playerVolume);

        //Show the volume
        if (musicPlayerShowVolumeRoutine != null)
        {
            musicPlayerShowVolumeRoutine.Cancel();
            musicPlayerShowVolumeRoutine = null;
        }
        musicPlayerShowVolumeRoutine = CoroutineHandler.Start(ShowVolumeRoutine());

        //Sync the volume to UI
        UpdateVolumeBar();
    }

    private void MusicPlayerVolumeDown()
    {
        //Subtract volume
        appPrefs.loadedData.playerVolume -= 15;

        //Fix bound
        if (appPrefs.loadedData.playerVolume < 0)
            appPrefs.loadedData.playerVolume = 0;

        //Save
        SaveAllPreferences();

        //Apply to media player
        if (musicPlayerHandler != null)
            musicPlayerHandler.SetVolume(appPrefs.loadedData.playerVolume);

        //Show the volume
        if (musicPlayerShowVolumeRoutine != null)
        {
            musicPlayerShowVolumeRoutine.Cancel();
            musicPlayerShowVolumeRoutine = null;
        }
        musicPlayerShowVolumeRoutine = CoroutineHandler.Start(ShowVolumeRoutine());

        //Sync the volume to UI
        UpdateVolumeBar();
    }

    private void UpdateVolumeBar()
    {
        //Show the limits
        if (appPrefs.loadedData.playerVolume >= 100)
            musicPlayer_orangeLimit.IsVisible = true;
        if (appPrefs.loadedData.playerVolume < 100)
            musicPlayer_orangeLimit.IsVisible = false;
        if (appPrefs.loadedData.playerVolume >= 125)
            musicPlayer_redLimit.IsVisible = true;
        if (appPrefs.loadedData.playerVolume < 125)
            musicPlayer_redLimit.IsVisible = false;

        //Sync the save volume to UI
        musicPlayer_volumeProgress.Value = (((float)appPrefs.loadedData.playerVolume / 150.0f) * 100.0f);
    }

    private IEnumerator<Wait> SetSystemVolumeRoutine(int targetVolume)
    {
        //Add this task running
        AddTask("systemVolumeSet", "Sets the system volume.");

        //If the Binded CLI Process is already rented by another task, wait until release
        while (isBindedCliTerminalRented() == true)
            yield return new Wait(0.5f);
        //Rent the Binded CLI Process
        string rKey = RentTheBindedCliTerminal();



        //Wait time
        yield return new Wait(1.0f);

        //Send command to set all volumes to the target volume
        SendCommandToTerminalAndClearCurrentOutputLines(rKey, ("amixer -q -M sset Master "+targetVolume+"% ; amixer -q -M sset Headphone "+targetVolume+"% ; amixer -q -M sset PCM "+targetVolume+"%"));
        //Wait the end of command execution
        while (isLastCommandFinishedExecution(rKey) == false)
            yield return new Wait(0.1f);

        //Inform that was finished
        musicPlayerSetSystemVolumeRoutine = null;



        //Release the Binded CLI Process
        ReleaseTheBindedCliTerminal(rKey);

        //Remove the task running
        RemoveTask("systemVolumeSet");
    }

    private IEnumerator<Wait> ShowVolumeRoutine()
    {
        //Show the volume
        musicPlayer_volumeText.Text = (appPrefs.loadedData.playerVolume + "%");
        musicPlayer_volumeText.IsVisible = true;

        //Wait
        yield return new Wait(5.0f);

        //Hide the volume
        musicPlayer_volumeText.IsVisible = false;

        //Inform that was finished
        musicPlayerShowVolumeRoutine = null;
    }

    private void ToggleMusicPlayerDrawer()
    {
        //If the Drawer is opened
        if (isMusicPlayerDrawerOpen == true)
        {
            //If the routine is not running, start it
            if (openCloseMusicPlayerDrawerRoutine == null)
                openCloseMusicPlayerDrawerRoutine = CoroutineHandler.Start(CloseMusicPlayerDrawerRoutine());

            //Inform that is closed
            isMusicPlayerDrawerOpen = false;

            //Restart the Music Player, if is initialized at least one time
            if (musicPlayerHandler != null)
                InitializeTheMusicPlayer();

            //Cancel
            return;
        }

        //If the Drawer is closed
        if (isMusicPlayerDrawerOpen == false)
        {
            //If the routine is not running, start it
            if (openCloseMusicPlayerDrawerRoutine == null)
                openCloseMusicPlayerDrawerRoutine = CoroutineHandler.Start(OpenMusicPlayerDrawerRoutine());

            //Inform that is opened
            isMusicPlayerDrawerOpen = true;

            //Stop the Music Player, if is initialized at least one time
            if (musicPlayerHandler != null)
                MusicPlayerStop();

            //Render all musics found in external drives
            RenderAllMusicsOfDrives();

            //Update the connected bluetooth sound devices
            if (musicPlayerUpdatePairedBluetoothSoundList == null)
                musicPlayerUpdatePairedBluetoothSoundList = CoroutineHandler.Start(UpdatePairedSoundBluetoothDevices());

            //Cancel
            return;
        }
    }

    private IEnumerator<Wait> OpenMusicPlayerDrawerRoutine()
    {
        //Enable the background
        musicPlayer_drawerBackground.IsVisible = true;
        musicPlayer_drawerBackground.Opacity = 0.7;

        //Wait time
        yield return new Wait(0.2f);

        //Open the drawer
        musicPlayer_drawer.Margin = new Thickness(0.0f, -2.0f, 0.0f, 0.0f);

        //Wait until animation finishes
        yield return new Wait(0.3f);

        //Change the drawer handler icon
        musicPlayer_drawerHandlerIcon.Source = new Bitmap(AssetLoader.Open(new Uri("avares://Motoplay/Assets/drawer-handler-foreground-close.png")));

        //Inform that is finished
        openCloseMusicPlayerDrawerRoutine = null;
    }

    private IEnumerator<Wait> CloseMusicPlayerDrawerRoutine()
    {
        //Close the drawer
        musicPlayer_drawer.Margin = new Thickness(0.0f, -2.0f, -566.0f, 0.0f);

        //Wait until animation finishes
        yield return new Wait(0.3f);

        //Disable the background
        musicPlayer_drawerBackground.Opacity = 0.0;

        //Wait until animation finishes
        yield return new Wait(0.3f);

        //Disable the background completely
        musicPlayer_drawerBackground.IsVisible = false;

        //Change the drawer handler icon
        musicPlayer_drawerHandlerIcon.Source = new Bitmap(AssetLoader.Open(new Uri("avares://Motoplay/Assets/drawer-handler-foreground-open.png")));

        //Inform that is finished
        openCloseMusicPlayerDrawerRoutine = null;
    }
    
    private void RenderAllMusicsOfLibrary()
    {
        //Clear the current rendered musics in UI
        foreach (LibraryMusicItem item in instantiatedLibraryMusicsInUi)
            musicPlayer_libraryList.Children.Remove(item);
        instantiatedLibraryMusicsInUi.Clear();

        //Prepare the list of musics found
        List<string> foundMusicsPath = new List<string>();

        //Get the list of musics
        foreach (FileInfo file in (new DirectoryInfo((motoplayRootPath + "/Musics")).GetFiles()))
            if (Path.GetExtension(file.FullName).ToLower() == ".mp3")
                foundMusicsPath.Add(file.FullName);
        foreach (FileInfo file in (new DirectoryInfo((motoplayRootPath + "/Musics")).GetFiles()))
            if (Path.GetExtension(file.FullName).ToLower() == ".m4a")
                foundMusicsPath.Add(file.FullName);
        foreach (FileInfo file in (new DirectoryInfo((motoplayRootPath + "/Musics")).GetFiles()))
            if (Path.GetExtension(file.FullName).ToLower() == ".ogg")
                foundMusicsPath.Add(file.FullName);
        foreach (FileInfo file in (new DirectoryInfo((motoplayRootPath + "/Musics")).GetFiles()))
            if (Path.GetExtension(file.FullName).ToLower() == ".wmv")
                foundMusicsPath.Add(file.FullName);
        foreach (FileInfo file in (new DirectoryInfo((motoplayRootPath + "/Musics")).GetFiles()))
            if (Path.GetExtension(file.FullName).ToLower() == ".wav")
                foundMusicsPath.Add(file.FullName);

        //Render each music found
        foreach (string musicPath in foundMusicsPath)
        {
            //Instantiate and store reference for it
            LibraryMusicItem item = new LibraryMusicItem(this);
            instantiatedLibraryMusicsInUi.Add(item);
            musicPlayer_libraryList.Children.Add(item);
            //Set it up
            item.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            item.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
            item.Width = double.NaN;
            item.Height = double.NaN;
            //Fill this item
            item.SetMusicFileName(Path.GetFileName(musicPath));
            item.RegisterOnDeleteCallback((musicPath) => { DeleteMusicFromLibrary(musicPath); });
            item.Setup();
        }

        //Show or hide the empty musics warn
        if (instantiatedLibraryMusicsInUi.Count == 0)
            musicPlayer_libraryEmpty.IsVisible = true;
        if (instantiatedLibraryMusicsInUi.Count > 0)
            musicPlayer_libraryEmpty.IsVisible = false;
    }

    private void DeleteMusicFromLibrary(string musicFileName)
    {
        //Delete the music file
        File.Delete((motoplayRootPath + "/Musics/" + musicFileName));

        //Search the target music to remove from UI
        int targetItemIndex = -1;
        for (int i = 0; i < instantiatedLibraryMusicsInUi.Count; i++)
            if (instantiatedLibraryMusicsInUi[i].musicNameText.Text == musicFileName)
            {
                targetItemIndex = i;
                break;
            }

        //If not found the target, stop here
        if (targetItemIndex == -1)
            return;

        //Remove from UI
        musicPlayer_libraryList.Children.Remove(instantiatedLibraryMusicsInUi[targetItemIndex]);
        instantiatedLibraryMusicsInUi.RemoveAt(targetItemIndex);

        //If found music with the same name in list of drive musics, enable it
        foreach (DriveMusicItem item in instantiatedDriveMusicsInUi)
            if (item.musicNameText.Text == musicFileName)
                item.SetEnabled(true);

        //Show or hide the empty musics warn
        if (instantiatedLibraryMusicsInUi.Count == 0)
            musicPlayer_libraryEmpty.IsVisible = true;
        if (instantiatedLibraryMusicsInUi.Count > 0)
            musicPlayer_libraryEmpty.IsVisible = false;
    }

    private void AddMusicToLibrary(string sourceMusicFilePath)
    {
        //Try to copy the music
        try
        {
            //Copy the music file
            File.Copy(sourceMusicFilePath, (motoplayRootPath + "/Musics/" + Path.GetFileName(sourceMusicFilePath)));

            //Instantiate and store reference for it
            LibraryMusicItem item = new LibraryMusicItem(this);
            instantiatedLibraryMusicsInUi.Add(item);
            musicPlayer_libraryList.Children.Add(item);
            //Set it up
            item.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            item.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
            item.Width = double.NaN;
            item.Height = double.NaN;
            //Fill this item
            item.SetMusicFileName(Path.GetFileName(sourceMusicFilePath));
            item.RegisterOnDeleteCallback((musicPath) => { DeleteMusicFromLibrary(musicPath); });
            item.Setup();

            //If found music with the same name in list of drive musics, disable it
            foreach (DriveMusicItem ditem in instantiatedDriveMusicsInUi)
                if (ditem.musicNameText.Text == Path.GetFileName(sourceMusicFilePath))
                    ditem.SetEnabled(false);

            //Show or hide the empty musics warn
            if (instantiatedLibraryMusicsInUi.Count == 0)
                musicPlayer_libraryEmpty.IsVisible = true;
            if (instantiatedLibraryMusicsInUi.Count > 0)
                musicPlayer_libraryEmpty.IsVisible = false;
        }
        catch (Exception ex) { }
    }

    private void RenderAllMusicsOfDrives()
    {
        //Clear the current rendered musics in UI
        foreach (DriveMusicItem item in instantiatedDriveMusicsInUi)
            musicPlayer_drivesList.Children.Remove(item);
        instantiatedDriveMusicsInUi.Clear();

        //Prepare the list of musics found
        List<string> foundMusicsPath = new List<string>();
        List<string> foundMusicsDrive = new List<string>();

        //If is Windows...
        if (OperatingSystem.IsWindows() == true)
        {
            //Prepare the string of letters
            string[] letters = new string[] { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z" };

            //Check each connected drive
            foreach (string letter in letters)
                if (Directory.Exists((letter + @":\Musics")) == true)
                {
                    //Get the musics list
                    foreach (FileInfo file in (new DirectoryInfo((letter + @":\Musics")).GetFiles()))
                        if (Path.GetExtension(file.FullName).ToLower() == ".mp3")
                        {
                            foundMusicsPath.Add(file.FullName);
                            foundMusicsDrive.Add(letter);
                        }
                    foreach (FileInfo file in (new DirectoryInfo((letter + @":\Musics")).GetFiles()))
                        if (Path.GetExtension(file.FullName).ToLower() == ".m4a")
                        {
                            foundMusicsPath.Add(file.FullName);
                            foundMusicsDrive.Add(letter);
                        }
                    foreach (FileInfo file in (new DirectoryInfo((letter + @":\Musics")).GetFiles()))
                        if (Path.GetExtension(file.FullName).ToLower() == ".ogg")
                        {
                            foundMusicsPath.Add(file.FullName);
                            foundMusicsDrive.Add(letter);
                        }
                    foreach (FileInfo file in (new DirectoryInfo((letter + @":\Musics")).GetFiles()))
                        if (Path.GetExtension(file.FullName).ToLower() == ".wmv")
                        {
                            foundMusicsPath.Add(file.FullName);
                            foundMusicsDrive.Add(letter);
                        }
                    foreach (FileInfo file in (new DirectoryInfo((letter + @":\Musics")).GetFiles()))
                        if (Path.GetExtension(file.FullName).ToLower() == ".wav")
                        {
                            foundMusicsPath.Add(file.FullName);
                            foundMusicsDrive.Add(letter);
                        }
                }
        }

        //If is Linux...
        if (OperatingSystem.IsLinux() == true)
        {
            //Check each connected drive
            foreach (DirectoryInfo dir in (new DirectoryInfo(("/media/" + systemCurrentUsername)).GetDirectories()))
                if (Directory.Exists((dir.FullName + "/Musics")) == true)
                {
                    //Get the musics list
                    foreach (FileInfo file in (new DirectoryInfo((dir.FullName + "/Musics")).GetFiles()))
                        if (Path.GetExtension(file.FullName).ToLower() == ".mp3")
                        {
                            foundMusicsPath.Add(file.FullName);
                            foundMusicsDrive.Add(dir.Name);
                        }
                    foreach (FileInfo file in (new DirectoryInfo((dir.FullName + "/Musics")).GetFiles()))
                        if (Path.GetExtension(file.FullName).ToLower() == ".m4a")
                        {
                            foundMusicsPath.Add(file.FullName);
                            foundMusicsDrive.Add(dir.Name);
                        }
                    foreach (FileInfo file in (new DirectoryInfo((dir.FullName + "/Musics")).GetFiles()))
                        if (Path.GetExtension(file.FullName).ToLower() == ".ogg")
                        {
                            foundMusicsPath.Add(file.FullName);
                            foundMusicsDrive.Add(dir.Name);
                        }
                    foreach (FileInfo file in (new DirectoryInfo((dir.FullName + "/Musics")).GetFiles()))
                        if (Path.GetExtension(file.FullName).ToLower() == ".wmv")
                        {
                            foundMusicsPath.Add(file.FullName);
                            foundMusicsDrive.Add(dir.Name);
                        }
                    foreach (FileInfo file in (new DirectoryInfo((dir.FullName + "/Musics")).GetFiles()))
                        if (Path.GetExtension(file.FullName).ToLower() == ".wav")
                        {
                            foundMusicsPath.Add(file.FullName);
                            foundMusicsDrive.Add(dir.Name);
                        }
                }
        }

        //Render each music found
        for (int i = 0; i < foundMusicsPath.Count; i++)
        {
            //Instantiate and store reference for it
            DriveMusicItem item = new DriveMusicItem(this);
            instantiatedDriveMusicsInUi.Add(item);
            musicPlayer_drivesList.Children.Add(item);
            //Set it up
            item.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            item.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
            item.Width = double.NaN;
            item.Height = double.NaN;
            //Fill this item
            item.SetMusicPath(foundMusicsPath[i]);
            item.SetDriveName(foundMusicsDrive[i]);
            if (File.Exists((motoplayRootPath + "/Musics/" + Path.GetFileName(foundMusicsPath[i]))) == true)
                item.SetEnabled(false);
            if (File.Exists((motoplayRootPath + "/Musics/" + Path.GetFileName(foundMusicsPath[i]))) == false)
                item.SetEnabled(true);
            item.RegisterOnTransferCallback((musicFullPath) => { AddMusicToLibrary(musicFullPath); });
            item.Setup();
        }

        //Show or hide the empty musics warn
        if (instantiatedDriveMusicsInUi.Count == 0)
            musicPlayer_drivesEmpty.IsVisible = true;
        if (instantiatedDriveMusicsInUi.Count > 0)
            musicPlayer_drivesEmpty.IsVisible = false;
    }

    private IEnumerator<Wait> MusicPlayerVehicleMoveMonitor()
    {
        //Prepare the interval time
        Wait intervalTime = new Wait(1.5f);

        //Data variables
        bool wasPausedByMonitor = false;
        int currentAutoVolumeDefined = -1;

        //Start the monitor loop
        while (true)
        {
            //If the auto pause is enabled
            if (appPrefs.loadedData.autoPauseOnStopVehicle == true && activeObdConnection != null)
            {
                if (activeObdConnection.transmissionGear == 0 && musicPlayerHandler.isPlaying() == true)
                {
                    musicPlayerHandler.Pause();
                    wasPausedByMonitor = true;
                }
            }

            //If the auto pause is enabled
            if (appPrefs.loadedData.autoPlayOnVehicleMove == true && activeObdConnection != null)
            {
                if (activeObdConnection.transmissionGear > 0 && musicPlayerHandler.isPlaying() == false && wasPausedByMonitor == true)
                {
                    musicPlayerHandler.Play();
                    wasPausedByMonitor = false;
                }
            }

            //If the automatic volume is enabled
            if (appPrefs.loadedData.automaticVolume == true && activeObdConnection != null)
            {
                //Prepare the target volume
                int targetVolume = 0;

                //Detect the target volume, using speed marks
                if (activeObdConnection.vehicleSpeedKmh >= appPrefs.loadedData.mark1volumeSpeed)
                    targetVolume = appPrefs.loadedData.mark1volumeTarget;
                if (activeObdConnection.vehicleSpeedKmh >= appPrefs.loadedData.mark2volumeSpeed)
                    targetVolume = appPrefs.loadedData.mark2volumeTarget;
                if (activeObdConnection.vehicleSpeedKmh >= appPrefs.loadedData.mark3volumeSpeed)
                    targetVolume = appPrefs.loadedData.mark3volumeTarget;
                if (activeObdConnection.vehicleSpeedKmh >= appPrefs.loadedData.mark4volumeSpeed)
                    targetVolume = appPrefs.loadedData.mark4volumeTarget;
                if (activeObdConnection.vehicleSpeedKmh >= appPrefs.loadedData.mark5volumeSpeed)
                    targetVolume = appPrefs.loadedData.mark5volumeTarget;
                if (activeObdConnection.vehicleSpeedKmh >= appPrefs.loadedData.mark6volumeSpeed)
                    targetVolume = appPrefs.loadedData.mark6volumeTarget;
                if (activeObdConnection.vehicleSpeedKmh >= appPrefs.loadedData.mark7volumeSpeed)
                    targetVolume = appPrefs.loadedData.mark7volumeTarget;

                //Detect the additional volume boots, using RPM
                float maxRpmToConsider = (((float)appPrefs.loadedData.vehicleMaxRpm * 0.85f) - 1200.0f);   //<- 1200 RPM is the RPM of vehicle stopped
                float currentFixedRpm = ((float)activeObdConnection.engineRpm - 1200.0f);   //<- 1200 RPM is the RPM of vehicle stopped
                if (currentFixedRpm < 0)
                    currentFixedRpm = 0;
                float additionalVolumeBoostInPercent = (((float)appPrefs.loadedData.volumeBoostOnMaxRpm / 100.0f) * (currentFixedRpm / maxRpmToConsider));
                if (additionalVolumeBoostInPercent < 0.0f)
                    additionalVolumeBoostInPercent = 0.0f;
                if (additionalVolumeBoostInPercent > ((float)appPrefs.loadedData.volumeBoostOnMaxRpm / 100.0f))
                    additionalVolumeBoostInPercent = ((float)appPrefs.loadedData.volumeBoostOnMaxRpm / 100.0f);
                targetVolume += (int)((float)targetVolume * additionalVolumeBoostInPercent);

                //If the target volume found, is different from current...
                if (targetVolume != currentAutoVolumeDefined)
                {
                    //Fix the volume bound
                    if (targetVolume > 150)
                        targetVolume = 150;
                    if (targetVolume < 0)
                        targetVolume = 0;
                    if (targetVolume > appPrefs.loadedData.mark7volumeTarget)
                        targetVolume = appPrefs.loadedData.mark7volumeTarget;

                    //Set the new volume
                    appPrefs.loadedData.playerVolume = targetVolume;
                    if (musicPlayerHandler != null)
                        musicPlayerHandler.SetVolume(appPrefs.loadedData.playerVolume);
                    //Show the volume
                    if (musicPlayerShowVolumeRoutine != null)
                    {
                        musicPlayerShowVolumeRoutine.Cancel();
                        musicPlayerShowVolumeRoutine = null;
                    }
                    musicPlayerShowVolumeRoutine = CoroutineHandler.Start(ShowVolumeRoutine());
                    //Sync the volume to UI
                    UpdateVolumeBar();

                    //Inform the new current volume
                    currentAutoVolumeDefined = targetVolume;
                }
            }

            //Wait time
            yield return intervalTime;
        }
    }

    private IEnumerator<Wait> UpdatePairedSoundBluetoothDevices()
    {
        //Show the loading bar
        musicPlayer_devicesSearching.IsVisible = true;

        //Clear the current rendered devices in UI
        foreach (BluetoothSoundItem item in instantiatedSoundDevicesInUi)
            musicPlayer_devicesList.Children.Remove(item);
        instantiatedSoundDevicesInUi.Clear();

        //Add this task running
        AddTask("searchingPairedSounds", "Search by Paired Bluetooth Sound Devices.");

        //If the Binded CLI Process is already rented by another task, wait until release
        while (isBindedCliTerminalRented() == true)
            yield return new Wait(0.5f);
        //Rent the Binded CLI Process
        string rKey = RentTheBindedCliTerminal();



        //Wait time
        yield return new Wait(1.0f);

        //Send command to search by paired bluetooth devices
        SendCommandToTerminalAndClearCurrentOutputLines(rKey, "bluetoothctl devices");
        //Wait the end of command execution
        while (isLastCommandFinishedExecution(rKey) == false)
            yield return new Wait(0.1f);

        //Store the received lines form terminal
        string[] storedResponseLines = terminalReceivedOutputLines.ToArray();

        //Check each listed device
        foreach (string line in storedResponseLines)
        {
            //If is not a line of device, continues
            if (line.Contains("Device") == false)
                continue;

            //Wait time
            yield return new Wait(1.0f);

            //Get the device name and MAC
            string[] macAndName = line.Replace("Device ", "").Split(new[] { ' ' }, 2);
            string deviceMac = macAndName[0];
            string deviceName = macAndName[1];

            //Send a command to get information about device
            SendCommandToTerminalAndClearCurrentOutputLines(rKey, ("bluetoothctl info " + deviceMac));
            //Wait the end of command execution
            while (isLastCommandFinishedExecution(rKey) == false)
                yield return new Wait(0.1f);

            //Merge all lines in a unique string
            string resultString = "";
            foreach (string respLine in terminalReceivedOutputLines)
                resultString += (" " + respLine);

            //If is not a sound device, continues
            if (resultString.Contains("audio-headphones") == false && resultString.Contains("Headset") == false && resultString.Contains("Audio Sink") == false)
                continue;

            //Render this device on screen
            BluetoothSoundItem item = new BluetoothSoundItem(this);
            instantiatedSoundDevicesInUi.Add(item);
            musicPlayer_devicesList.Children.Add(item);
            //Set it up
            item.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            item.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
            item.Width = double.NaN;
            item.Height = double.NaN;
            //Fill this item
            item.SetDeviceMac(deviceMac);
            item.SetDeviceName(deviceName);
            item.RegisterOnTryConnectCallback((deviceMac) => { CoroutineHandler.Start(TryToConnectToPairedSoundBluetoothDevice(deviceMac)); });
        }

        //Hide the loading bar
        musicPlayer_devicesSearching.IsVisible = false;

        //Inform that was finished
        musicPlayerUpdatePairedBluetoothSoundList = null;



        //Release the Binded CLI Process
        ReleaseTheBindedCliTerminal(rKey);

        //Remove the task running
        RemoveTask("searchingPairedSounds");
    }

    public IEnumerator<Wait> TryToConnectToPairedSoundBluetoothDevice(string targetMac)
    {
        //Add this task running
        AddTask("connectToBluetoothSound", "Connect to Bluetooth Sound Device, if is not connected.");

        //If the Binded CLI Process is already rented by another task, wait until release
        while (isBindedCliTerminalRented() == true)
            yield return new Wait(0.5f);
        //Rent the Binded CLI Process
        string rKey = RentTheBindedCliTerminal();



        //Wait time
        yield return new Wait(1.0f);

        //Send command to check connected devices
        SendCommandToTerminalAndClearCurrentOutputLines(rKey, "bluetoothctl devices Connected");
        //Wait the end of command execution
        while (isLastCommandFinishedExecution(rKey) == false)
            yield return new Wait(0.1f);

        //Merge all lines in a unique string
        string resultString = "";
        foreach (string respLine in terminalReceivedOutputLines)
            resultString += (" " + respLine);

        //If in the connected devices not contains the target mac, continues...
        if (resultString.Contains(targetMac) == false)
        {
            //Wait time
            yield return new Wait(1.0f);

            //Send command to connect to device
            SendCommandToTerminalAndClearCurrentOutputLines(rKey, ("bluetoothctl connect " + targetMac));
            //Wait the end of command execution
            while (isLastCommandFinishedExecution(rKey) == false)
                yield return new Wait(0.1f);

            //Show the connection try result
            StringBuilder resultTxt = new StringBuilder();
            foreach (string respLine in terminalReceivedOutputLines)
            {
                if (respLine.Contains("> Done Command") == true)
                    continue;
                if (respLine.Contains("[") == true && respLine.Contains("]") == true)
                    continue;

                resultTxt.AppendLine(respLine);
            }
            ShowToast(GetStringApplicationResource("musicPlayer_bluetoothConnectTryResult").Replace("%r", ("\n\n" + resultTxt.ToString())), ToastDuration.Short, ToastType.Normal);
        }

        //If in the connected devices contains the target mac, warn
        if (resultString.Contains(targetMac) == true)
            ShowToast(GetStringApplicationResource("musicPlayer_bluetoothConnectTryAlready"), ToastDuration.Short, ToastType.Normal);



        //Release the Binded CLI Process
        ReleaseTheBindedCliTerminal(rKey);

        //Remove the task running
        RemoveTask("connectToBluetoothSound");
    }

    public IEnumerator<Wait> EmulateMouseMoveToSpeakerIconAndRightClick()
    {
        //Disable the button
        musicPlayer_openOutputSelectorButton.IsEnabled = false;

        //Add this task running
        AddTask("emualatintSpeakerRightClick", "Emulates the movement of Right Clicking on the Speaker icon.");

        //If the Binded CLI Process is already rented by another task, wait until release
        while (isBindedCliTerminalRented() == true)
            yield return new Wait(0.5f);
        //Rent the Binded CLI Process
        string rKey = RentTheBindedCliTerminal();



        //Wait time
        yield return new Wait(0.05f);

        //Send command of step 1
        SendCommandToTerminalAndClearCurrentOutputLines(rKey, ("wlrctl pointer move " + appPrefs.loadedData.outputSelectorEmulateMoveStep1x + " " + appPrefs.loadedData.outputSelectorEmulateMoveStep1y));
        //Wait the end of command execution
        while (isLastCommandFinishedExecution(rKey) == false)
            yield return new Wait(0.1f);

        //Send command of step 2
        SendCommandToTerminalAndClearCurrentOutputLines(rKey, ("wlrctl pointer move " + appPrefs.loadedData.outputSelectorEmulateMoveStep2x + " " + appPrefs.loadedData.outputSelectorEmulateMoveStep2y));
        //Wait the end of command execution
        while (isLastCommandFinishedExecution(rKey) == false)
            yield return new Wait(0.1f);

        //Send command of step 3
        SendCommandToTerminalAndClearCurrentOutputLines(rKey, ("wlrctl pointer click right"));
        //Wait the end of command execution
        while (isLastCommandFinishedExecution(rKey) == false)
            yield return new Wait(0.1f);

        //Enable the button
        musicPlayer_openOutputSelectorButton.IsEnabled = true;



        //Release the Binded CLI Process
        ReleaseTheBindedCliTerminal(rKey);

        //Remove the task running
        RemoveTask("emualatintSpeakerRightClick");
    }

    //Web Browser

    private void PrepareTheWebBrowser()
    {
        //...
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

        //Prepare the validation for textbox of "pref_panel_vehicleMaxRpm" preference
        pref_panel_vehicleMaxRpm.RegisterOnTextChangedValidationCallback((currentInput) => { return ValidateIfInputIsValidIntegerAndGetResult(currentInput); });
        pref_panel_minGear1RpmToChangeToGear2.RegisterOnTextChangedValidationCallback((currentInput) => { return ValidateIfInputIsValidIntegerAndGetResult(currentInput); });
        pref_panel_minGear1SpeedToChangeToGear2.RegisterOnTextChangedValidationCallback((currentInput) => { return ValidateIfInputIsValidIntegerAndGetResult(currentInput); });
        pref_panel_maxSpeedForGear1.RegisterOnTextChangedValidationCallback((currentInput) => { return ValidateIfInputIsValidIntegerAndGetResult(currentInput); });
        pref_panel_maxSpeedForGear2.RegisterOnTextChangedValidationCallback((currentInput) => { return ValidateIfInputIsValidIntegerAndGetResult(currentInput); });
        pref_panel_maxSpeedForGear3.RegisterOnTextChangedValidationCallback((currentInput) => { return ValidateIfInputIsValidIntegerAndGetResult(currentInput); });
        pref_panel_maxSpeedForGear4.RegisterOnTextChangedValidationCallback((currentInput) => { return ValidateIfInputIsValidIntegerAndGetResult(currentInput); });
        pref_panel_maxSpeedForGear5.RegisterOnTextChangedValidationCallback((currentInput) => { return ValidateIfInputIsValidIntegerAndGetResult(currentInput); });
        pref_panel_maxSpeedForGear6.RegisterOnTextChangedValidationCallback((currentInput) => { return ValidateIfInputIsValidIntegerAndGetResult(currentInput); });

        //Prepare validation for gears letters
        pref_panel_letterForVehicleStopped.RegisterOnTextChangedValidationCallback((currentInput) => 
        {
            //Prepare the value to return
            string toReturn = "";

            //Check if is empty, cancel here
            if (currentInput == "")
            {
                toReturn = "Enter a letter!";
                return toReturn;
            }
            //Check if have more than one character
            if (currentInput.Length > 1)
                toReturn = "Inser only one character!";

            //Return the value
            return toReturn;
        });
        pref_panel_letterForClutchPressed.RegisterOnTextChangedValidationCallback((currentInput) =>
        {
            //Prepare the value to return
            string toReturn = "";

            //Check if is empty, cancel here
            if (currentInput == "")
            {
                toReturn = "Enter a letter!";
                return toReturn;
            }
            //Check if have more than one character
            if (currentInput.Length > 1)
                toReturn = "Inser only one character!";

            //Return the value
            return toReturn;
        });

        //Prepare the auto hide of max speed for gear 6, field
        pref_panel_maxTransmissionGears.SelectionChanged += (s, e) => 
        {
            if (pref_panel_maxTransmissionGears.SelectedIndex == 0)
                pref_panel_maxSpeedForGear6_root.IsVisible = false;
            if (pref_panel_maxTransmissionGears.SelectedIndex == 1)
                pref_panel_maxSpeedForGear6_root.IsVisible = true;
        };
        if (appPrefs.loadedData.maxTransmissionGears < 6)
            pref_panel_maxSpeedForGear6_root.IsVisible = false;

        //Prepare the auto hide of equalizer options
        pref_player_equalizerProfile.SelectionChanged += (s, e) =>  { UpdateEqualizerBandOptionsVisibility(); };
        UpdateEqualizerBandOptionsVisibility();

        //Prepare the auto hide of automatic volume mark options
        pref_player_automaticVolume.IsCheckedChanged += (s, e) => { UpdateVolumeMarksOptionsVisibility(); };
        UpdateVolumeMarksOptionsVisibility();
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
        if (appPrefs.loadedData.maxOfObdConnectionRetry == 2)
            pref_panel_maxObdConnectTries.SelectedIndex = 0;
        if (appPrefs.loadedData.maxOfObdConnectionRetry == 3)
            pref_panel_maxObdConnectTries.SelectedIndex = 1;
        if (appPrefs.loadedData.maxOfObdConnectionRetry == 5)
            pref_panel_maxObdConnectTries.SelectedIndex = 2;
        if (appPrefs.loadedData.maxOfObdConnectionRetry == 15)
            pref_panel_maxObdConnectTries.SelectedIndex = 3;
        if (appPrefs.loadedData.maxOfObdConnectionRetry == 999999999)
            pref_panel_maxObdConnectTries.SelectedIndex = 4;
        //*** pref_panel_obdAdapterBaudRate
        if (appPrefs.loadedData.bluetoothBaudRate == 4096)
            pref_panel_obdAdapterBaudRate.SelectedIndex = 0;
        if (appPrefs.loadedData.bluetoothBaudRate == 4800)
            pref_panel_obdAdapterBaudRate.SelectedIndex = 1;
        if (appPrefs.loadedData.bluetoothBaudRate == 9600)
            pref_panel_obdAdapterBaudRate.SelectedIndex = 2;
        if (appPrefs.loadedData.bluetoothBaudRate == 10400)
            pref_panel_obdAdapterBaudRate.SelectedIndex = 3;
        if (appPrefs.loadedData.bluetoothBaudRate == 38400)
            pref_panel_obdAdapterBaudRate.SelectedIndex = 4;
        if (appPrefs.loadedData.bluetoothBaudRate == 41600)
            pref_panel_obdAdapterBaudRate.SelectedIndex = 5;
        if (appPrefs.loadedData.bluetoothBaudRate == 250000)
            pref_panel_obdAdapterBaudRate.SelectedIndex = 6;
        if (appPrefs.loadedData.bluetoothBaudRate == 500000)
            pref_panel_obdAdapterBaudRate.SelectedIndex = 7;
        //*** pref_panel_vehicleMaxRpm
        pref_panel_vehicleMaxRpm.textBox.Text = appPrefs.loadedData.vehicleMaxRpm.ToString();
        //*** pref_panel_rpmDisplayType
        pref_panel_rpmDisplayType.SelectedIndex = appPrefs.loadedData.rpmDisplayType;
        //*** pref_panel_rpmInterpolationSampleInterval
        if (appPrefs.loadedData.rpmInterpolationSampleIntervalMs == 100)
            pref_panel_rpmInterpolationSampleInterval.SelectedIndex = 0;
        if (appPrefs.loadedData.rpmInterpolationSampleIntervalMs == 250)
            pref_panel_rpmInterpolationSampleInterval.SelectedIndex = 1;
        if (appPrefs.loadedData.rpmInterpolationSampleIntervalMs == 350)
            pref_panel_rpmInterpolationSampleInterval.SelectedIndex = 2;
        if (appPrefs.loadedData.rpmInterpolationSampleIntervalMs == 500)
            pref_panel_rpmInterpolationSampleInterval.SelectedIndex = 3;
        if (appPrefs.loadedData.rpmInterpolationSampleIntervalMs == 700)
            pref_panel_rpmInterpolationSampleInterval.SelectedIndex = 4;
        if (appPrefs.loadedData.rpmInterpolationSampleIntervalMs == 850)
            pref_panel_rpmInterpolationSampleInterval.SelectedIndex = 5;
        if (appPrefs.loadedData.rpmInterpolationSampleIntervalMs == 1000)
            pref_panel_rpmInterpolationSampleInterval.SelectedIndex = 6;
        if (appPrefs.loadedData.rpmInterpolationSampleIntervalMs == 1250)
            pref_panel_rpmInterpolationSampleInterval.SelectedIndex = 7;
        if (appPrefs.loadedData.rpmInterpolationSampleIntervalMs == 1500)
            pref_panel_rpmInterpolationSampleInterval.SelectedIndex = 8;
        //*** pref_panel_rpmInterpolationAggressiveness
        if (appPrefs.loadedData.rpmInterpolationAggressiveness == 0.5f)
            pref_panel_rpmInterpolationAggressiveness.SelectedIndex = 0;
        if (appPrefs.loadedData.rpmInterpolationAggressiveness == 0.8f)
            pref_panel_rpmInterpolationAggressiveness.SelectedIndex = 1;
        if (appPrefs.loadedData.rpmInterpolationAggressiveness == 1.0f)
            pref_panel_rpmInterpolationAggressiveness.SelectedIndex = 2;
        if (appPrefs.loadedData.rpmInterpolationAggressiveness == 1.25f)
            pref_panel_rpmInterpolationAggressiveness.SelectedIndex = 3;
        if (appPrefs.loadedData.rpmInterpolationAggressiveness == 1.5f)
            pref_panel_rpmInterpolationAggressiveness.SelectedIndex = 4;
        if (appPrefs.loadedData.rpmInterpolationAggressiveness == 1.75f)
            pref_panel_rpmInterpolationAggressiveness.SelectedIndex = 5;
        if (appPrefs.loadedData.rpmInterpolationAggressiveness == 2.0f)
            pref_panel_rpmInterpolationAggressiveness.SelectedIndex = 6;
        if (appPrefs.loadedData.rpmInterpolationAggressiveness == 2.5f)
            pref_panel_rpmInterpolationAggressiveness.SelectedIndex = 7;
        if (appPrefs.loadedData.rpmInterpolationAggressiveness == 3.0f)
            pref_panel_rpmInterpolationAggressiveness.SelectedIndex = 8;
        //*** pref_panel_speedDisplayUnit
        pref_panel_speedDisplayUnit.SelectedIndex = appPrefs.loadedData.speedDisplayUnit;
        //*** pref_panel_rpmMiniGaugeDisplayUnit
        pref_panel_rpmMiniGaugeDisplayUnit.SelectedIndex = appPrefs.loadedData.rpmTextDisplayType;
        //*** pref_panel_temperatureUnit
        pref_panel_temperatureUnit.SelectedIndex = appPrefs.loadedData.temperatureUnit;
        //*** pref_panel_maxTransmissionGears
        if (appPrefs.loadedData.maxTransmissionGears == 5)
            pref_panel_maxTransmissionGears.SelectedIndex = 0;
        if (appPrefs.loadedData.maxTransmissionGears == 6)
            pref_panel_maxTransmissionGears.SelectedIndex = 1;
        //*** pref_panel_minGear1RpmToChangeToGear2
        pref_panel_minGear1RpmToChangeToGear2.textBox.Text = appPrefs.loadedData.minGear1RpmToChangeToGear2.ToString();
        //*** pref_panel_minGear1SpeedToChangeToGear2
        pref_panel_minGear1SpeedToChangeToGear2.textBox.Text = appPrefs.loadedData.minGear1SpeedToChangeToGear2.ToString();
        //*** pref_panel_maxSpeedForGear1
        pref_panel_maxSpeedForGear1.textBox.Text = appPrefs.loadedData.maxPossibleGear1Speed.ToString();
        //*** pref_panel_maxSpeedForGear2
        pref_panel_maxSpeedForGear2.textBox.Text = appPrefs.loadedData.maxPossibleGear2Speed.ToString();
        //*** pref_panel_maxSpeedForGear3
        pref_panel_maxSpeedForGear3.textBox.Text = appPrefs.loadedData.maxPossibleGear3Speed.ToString();
        //*** pref_panel_maxSpeedForGear4
        pref_panel_maxSpeedForGear4.textBox.Text = appPrefs.loadedData.maxPossibleGear4Speed.ToString();
        //*** pref_panel_maxSpeedForGear5
        pref_panel_maxSpeedForGear5.textBox.Text = appPrefs.loadedData.maxPossibleGear5Speed.ToString();
        //*** pref_panel_maxSpeedForGear6
        pref_panel_maxSpeedForGear6.textBox.Text = appPrefs.loadedData.maxPossibleGear6Speed.ToString();
        //*** pref_panel_letterForVehicleStopped
        pref_panel_letterForVehicleStopped.textBox.Text = appPrefs.loadedData.letterToUseAsGearStopped;
        //*** pref_panel_letterForClutchPressed
        pref_panel_letterForClutchPressed.textBox.Text = appPrefs.loadedData.letterToUseAsClutchPressed;
        //*** pref_panel_panelColorScheme
        pref_panel_panelColorScheme.SelectedIndex = appPrefs.loadedData.panelColorScheme;

        //Player Tab
        //*** pref_player_resetSystemVolumeOnPlay
        pref_player_resetSystemVolumeOnPlay.IsChecked = appPrefs.loadedData.resetSystemVolumeOnPlaySong;
        //*** pref_player_randomizeMusicList
        pref_player_randomizeMusicList.IsChecked = appPrefs.loadedData.randomizeMusicList;
        //*** pref_player_autoPauseOnStop
        pref_player_autoPauseOnStop.IsChecked = appPrefs.loadedData.autoPauseOnStopVehicle;
        //*** pref_player_autoPlayOnMove
        pref_player_autoPlayOnMove.IsChecked = appPrefs.loadedData.autoPlayOnVehicleMove;
        //*** pref_player_equalizerProfile
        pref_player_equalizerProfile.SelectedIndex = appPrefs.loadedData.equalizerProfile;
        //*** pref_player_equalizerAmplifier
        pref_player_equalizerAmplifier.Value = appPrefs.loadedData.equalizerAmplifierValue;
        //*** pref_player_equalizerBand31
        pref_player_equalizerBand31.Value = appPrefs.loadedData.equalizerBand31hz;
        //*** pref_player_equalizerBand62
        pref_player_equalizerBand62.Value = appPrefs.loadedData.equalizerBand62hz;
        //*** pref_player_equalizerBand125
        pref_player_equalizerBand125.Value = appPrefs.loadedData.equalizerBand125hz;
        //*** pref_player_equalizerBand250
        pref_player_equalizerBand250.Value = appPrefs.loadedData.equalizerBand250hz;
        //*** pref_player_equalizerBand500
        pref_player_equalizerBand500.Value = appPrefs.loadedData.equalizerBand500hz;
        //*** pref_player_equalizerBand1k
        pref_player_equalizerBand1k.Value = appPrefs.loadedData.equalizerBand1khz;
        //*** pref_player_equalizerBand2k
        pref_player_equalizerBand2k.Value = appPrefs.loadedData.equalizerBand2khz;
        //*** pref_player_equalizerBand4k
        pref_player_equalizerBand4k.Value = appPrefs.loadedData.equalizerBand4khz;
        //*** pref_player_equalizerBand8k
        pref_player_equalizerBand8k.Value = appPrefs.loadedData.equalizerBand8khz;
        //*** pref_player_equalizerBand16k
        pref_player_equalizerBand16k.Value = appPrefs.loadedData.equalizerBand16khz;
        //*** pref_player_speakerRightClickEmulationStep1x
        pref_player_speakerRightClickEmulationStep1x.Value = appPrefs.loadedData.outputSelectorEmulateMoveStep1x;
        //*** pref_player_speakerRightClickEmulationStep1y
        pref_player_speakerRightClickEmulationStep1y.Value = appPrefs.loadedData.outputSelectorEmulateMoveStep1y;
        //*** pref_player_speakerRightClickEmulationStep2x
        pref_player_speakerRightClickEmulationStep2x.Value = appPrefs.loadedData.outputSelectorEmulateMoveStep2x;
        //*** pref_player_speakerRightClickEmulationStep2y
        pref_player_speakerRightClickEmulationStep2y.Value = appPrefs.loadedData.outputSelectorEmulateMoveStep2y;
        //*** pref_player_automaticVolume
        pref_player_automaticVolume.IsChecked = appPrefs.loadedData.automaticVolume;
        //*** pref_player_volumeMark1speed
        pref_player_volumeMark1speed.Value = appPrefs.loadedData.mark1volumeSpeed;
        //*** pref_player_volumeMark1target
        pref_player_volumeMark1target.Value = appPrefs.loadedData.mark1volumeTarget;
        //*** pref_player_volumeMark2speed
        pref_player_volumeMark2speed.Value = appPrefs.loadedData.mark2volumeSpeed;
        //*** pref_player_volumeMark2target
        pref_player_volumeMark2target.Value = appPrefs.loadedData.mark2volumeTarget;
        //*** pref_player_volumeMark3speed
        pref_player_volumeMark3speed.Value = appPrefs.loadedData.mark3volumeSpeed;
        //*** pref_player_volumeMark3target
        pref_player_volumeMark3target.Value = appPrefs.loadedData.mark3volumeTarget;
        //*** pref_player_volumeMark4speed
        pref_player_volumeMark4speed.Value = appPrefs.loadedData.mark4volumeSpeed;
        //*** pref_player_volumeMark4target
        pref_player_volumeMark4target.Value = appPrefs.loadedData.mark4volumeTarget;
        //*** pref_player_volumeMark5speed
        pref_player_volumeMark5speed.Value = appPrefs.loadedData.mark5volumeSpeed;
        //*** pref_player_volumeMark5target
        pref_player_volumeMark5target.Value = appPrefs.loadedData.mark5volumeTarget;
        //*** pref_player_volumeMark6speed
        pref_player_volumeMark6speed.Value = appPrefs.loadedData.mark6volumeSpeed;
        //*** pref_player_volumeMark6target
        pref_player_volumeMark6target.Value = appPrefs.loadedData.mark6volumeTarget;
        //*** pref_player_volumeMark7speed
        pref_player_volumeMark7speed.Value = appPrefs.loadedData.mark7volumeSpeed;
        //*** pref_player_volumeMark7target
        pref_player_volumeMark7target.Value = appPrefs.loadedData.mark7volumeTarget;
        //*** pref_player_volumeBoostAtMaxRpm
        pref_player_volumeBoostAtMaxRpm.Value = appPrefs.loadedData.volumeBoostOnMaxRpm;
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
            appPrefs.loadedData.maxOfObdConnectionRetry = 2;
        if (pref_panel_maxObdConnectTries.SelectedIndex == 1)
            appPrefs.loadedData.maxOfObdConnectionRetry = 3;
        if (pref_panel_maxObdConnectTries.SelectedIndex == 2)
            appPrefs.loadedData.maxOfObdConnectionRetry = 5;
        if (pref_panel_maxObdConnectTries.SelectedIndex == 3)
            appPrefs.loadedData.maxOfObdConnectionRetry = 15;
        if (pref_panel_maxObdConnectTries.SelectedIndex == 4)
            appPrefs.loadedData.maxOfObdConnectionRetry = 999999999;
        //*** pref_panel_obdAdapterBaudRate
        if (pref_panel_obdAdapterBaudRate.SelectedIndex == 0)
            appPrefs.loadedData.bluetoothBaudRate = 4096;
        if (pref_panel_obdAdapterBaudRate.SelectedIndex == 1)
            appPrefs.loadedData.bluetoothBaudRate = 4800;
        if (pref_panel_obdAdapterBaudRate.SelectedIndex == 2)
            appPrefs.loadedData.bluetoothBaudRate = 9600;
        if (pref_panel_obdAdapterBaudRate.SelectedIndex == 3)
            appPrefs.loadedData.bluetoothBaudRate = 10400;
        if (pref_panel_obdAdapterBaudRate.SelectedIndex == 4)
            appPrefs.loadedData.bluetoothBaudRate = 38400;
        if (pref_panel_obdAdapterBaudRate.SelectedIndex == 5)
            appPrefs.loadedData.bluetoothBaudRate = 41600;
        if (pref_panel_obdAdapterBaudRate.SelectedIndex == 6)
            appPrefs.loadedData.bluetoothBaudRate = 250000;
        if (pref_panel_obdAdapterBaudRate.SelectedIndex == 7)
            appPrefs.loadedData.bluetoothBaudRate = 500000;
        //*** pref_panel_vehicleMaxRpm
        if (pref_panel_vehicleMaxRpm.hasError() == false)
            appPrefs.loadedData.vehicleMaxRpm = int.Parse(pref_panel_vehicleMaxRpm.textBox.Text);
        //*** pref_panel_rpmDisplayType
        appPrefs.loadedData.rpmDisplayType = pref_panel_rpmDisplayType.SelectedIndex;
        //*** pref_panel_rpmInterpolationSampleInterval
        if (pref_panel_rpmInterpolationSampleInterval.SelectedIndex == 0)
            appPrefs.loadedData.rpmInterpolationSampleIntervalMs = 100;
        if (pref_panel_rpmInterpolationSampleInterval.SelectedIndex == 1)
            appPrefs.loadedData.rpmInterpolationSampleIntervalMs = 250;
        if (pref_panel_rpmInterpolationSampleInterval.SelectedIndex == 2)
            appPrefs.loadedData.rpmInterpolationSampleIntervalMs = 350;
        if (pref_panel_rpmInterpolationSampleInterval.SelectedIndex == 3)
            appPrefs.loadedData.rpmInterpolationSampleIntervalMs = 500;
        if (pref_panel_rpmInterpolationSampleInterval.SelectedIndex == 4)
            appPrefs.loadedData.rpmInterpolationSampleIntervalMs = 700;
        if (pref_panel_rpmInterpolationSampleInterval.SelectedIndex == 5)
            appPrefs.loadedData.rpmInterpolationSampleIntervalMs = 850;
        if (pref_panel_rpmInterpolationSampleInterval.SelectedIndex == 6)
            appPrefs.loadedData.rpmInterpolationSampleIntervalMs = 1000;
        if (pref_panel_rpmInterpolationSampleInterval.SelectedIndex == 7)
            appPrefs.loadedData.rpmInterpolationSampleIntervalMs = 1250;
        if (pref_panel_rpmInterpolationSampleInterval.SelectedIndex == 8)
            appPrefs.loadedData.rpmInterpolationSampleIntervalMs = 1500;
        //*** pref_panel_rpmInterpolationAggressiveness
        if (pref_panel_rpmInterpolationAggressiveness.SelectedIndex == 0)
            appPrefs.loadedData.rpmInterpolationAggressiveness = 0.5f;
        if (pref_panel_rpmInterpolationAggressiveness.SelectedIndex == 1)
            appPrefs.loadedData.rpmInterpolationAggressiveness = 0.8f;
        if (pref_panel_rpmInterpolationAggressiveness.SelectedIndex == 2)
            appPrefs.loadedData.rpmInterpolationAggressiveness = 1.0f;
        if (pref_panel_rpmInterpolationAggressiveness.SelectedIndex == 3)
            appPrefs.loadedData.rpmInterpolationAggressiveness = 1.25f;
        if (pref_panel_rpmInterpolationAggressiveness.SelectedIndex == 4)
            appPrefs.loadedData.rpmInterpolationAggressiveness = 1.5f;
        if (pref_panel_rpmInterpolationAggressiveness.SelectedIndex == 5)
            appPrefs.loadedData.rpmInterpolationAggressiveness = 1.75f;
        if (pref_panel_rpmInterpolationAggressiveness.SelectedIndex == 6)
            appPrefs.loadedData.rpmInterpolationAggressiveness = 2.0f;
        if (pref_panel_rpmInterpolationAggressiveness.SelectedIndex == 7)
            appPrefs.loadedData.rpmInterpolationAggressiveness = 2.5f;
        if (pref_panel_rpmInterpolationAggressiveness.SelectedIndex == 8)
            appPrefs.loadedData.rpmInterpolationAggressiveness = 3.0f;
        //*** pref_panel_speedDisplayUnit
        appPrefs.loadedData.speedDisplayUnit = pref_panel_speedDisplayUnit.SelectedIndex;
        //*** pref_panel_rpmMiniGaugeDisplayUnit
        appPrefs.loadedData.rpmTextDisplayType = pref_panel_rpmMiniGaugeDisplayUnit.SelectedIndex;
        //*** pref_panel_temperatureUnit
        appPrefs.loadedData.temperatureUnit = pref_panel_temperatureUnit.SelectedIndex;
        //*** pref_panel_maxTransmissionGears
        if (pref_panel_maxTransmissionGears.SelectedIndex == 0)
            appPrefs.loadedData.maxTransmissionGears = 5;
        if (pref_panel_maxTransmissionGears.SelectedIndex == 1)
            appPrefs.loadedData.maxTransmissionGears = 6;
        //*** pref_panel_minGear1RpmToChangeToGear2
        if (pref_panel_minGear1RpmToChangeToGear2.hasError() == false)
            appPrefs.loadedData.minGear1RpmToChangeToGear2 = int.Parse(pref_panel_minGear1RpmToChangeToGear2.textBox.Text);
        //*** pref_panel_minGear1SpeedToChangeToGear2
        if (pref_panel_minGear1SpeedToChangeToGear2.hasError() == false)
            appPrefs.loadedData.minGear1SpeedToChangeToGear2 = int.Parse(pref_panel_minGear1SpeedToChangeToGear2.textBox.Text);
        //*** pref_panel_maxSpeedForGear1
        if (pref_panel_maxSpeedForGear1.hasError() == false)
            appPrefs.loadedData.maxPossibleGear1Speed = int.Parse(pref_panel_maxSpeedForGear1.textBox.Text);
        //*** pref_panel_maxSpeedForGear2
        if (pref_panel_maxSpeedForGear2.hasError() == false)
            appPrefs.loadedData.maxPossibleGear2Speed = int.Parse(pref_panel_maxSpeedForGear2.textBox.Text);
        //*** pref_panel_maxSpeedForGear3
        if (pref_panel_maxSpeedForGear3.hasError() == false)
            appPrefs.loadedData.maxPossibleGear3Speed = int.Parse(pref_panel_maxSpeedForGear3.textBox.Text);
        //*** pref_panel_maxSpeedForGear4
        if (pref_panel_maxSpeedForGear4.hasError() == false)
            appPrefs.loadedData.maxPossibleGear4Speed = int.Parse(pref_panel_maxSpeedForGear4.textBox.Text);
        //*** pref_panel_maxSpeedForGear5
        if (pref_panel_maxSpeedForGear5.hasError() == false)
            appPrefs.loadedData.maxPossibleGear5Speed = int.Parse(pref_panel_maxSpeedForGear5.textBox.Text);
        //*** pref_panel_maxSpeedForGear6
        if (pref_panel_maxSpeedForGear6.hasError() == false)
            appPrefs.loadedData.maxPossibleGear6Speed = int.Parse(pref_panel_maxSpeedForGear6.textBox.Text);
        //*** pref_panel_letterForVehicleStopped
        if (pref_panel_letterForVehicleStopped.hasError() == false)
            appPrefs.loadedData.letterToUseAsGearStopped = pref_panel_letterForVehicleStopped.textBox.Text;
        //*** pref_panel_letterForClutchPressed
        if (pref_panel_letterForClutchPressed.hasError() == false)
            appPrefs.loadedData.letterToUseAsClutchPressed = pref_panel_letterForClutchPressed.textBox.Text;
        //*** pref_panel_panelColorScheme
        appPrefs.loadedData.panelColorScheme = pref_panel_panelColorScheme.SelectedIndex;

        //Player Tab
        //*** pref_player_resetSystemVolumeOnPlay
        appPrefs.loadedData.resetSystemVolumeOnPlaySong = (bool)pref_player_resetSystemVolumeOnPlay.IsChecked;
        //*** pref_player_randomizeMusicList
        appPrefs.loadedData.randomizeMusicList = (bool)pref_player_randomizeMusicList.IsChecked;
        //*** pref_player_autoPauseOnStop
        appPrefs.loadedData.autoPauseOnStopVehicle = (bool)pref_player_autoPauseOnStop.IsChecked;
        //*** pref_player_autoPlayOnMove
        appPrefs.loadedData.autoPlayOnVehicleMove = (bool)pref_player_autoPlayOnMove.IsChecked;
        //*** pref_player_equalizerProfile
        appPrefs.loadedData.equalizerProfile = pref_player_equalizerProfile.SelectedIndex;
        //*** pref_player_equalizerAmplifier
        appPrefs.loadedData.equalizerAmplifierValue = (int)pref_player_equalizerAmplifier.Value;
        //*** pref_player_equalizerBand31
        appPrefs.loadedData.equalizerBand31hz = (int)pref_player_equalizerBand31.Value;
        //*** pref_player_equalizerBand62
        appPrefs.loadedData.equalizerBand62hz = (int)pref_player_equalizerBand62.Value;
        //*** pref_player_equalizerBand125
        appPrefs.loadedData.equalizerBand125hz = (int)pref_player_equalizerBand125.Value;
        //*** pref_player_equalizerBand250
        appPrefs.loadedData.equalizerBand250hz = (int)pref_player_equalizerBand250.Value;
        //*** pref_player_equalizerBand500
        appPrefs.loadedData.equalizerBand500hz = (int)pref_player_equalizerBand500.Value;
        //*** pref_player_equalizerBand1k
        appPrefs.loadedData.equalizerBand1khz = (int)pref_player_equalizerBand1k.Value;
        //*** pref_player_equalizerBand2k
        appPrefs.loadedData.equalizerBand2khz = (int)pref_player_equalizerBand2k.Value;
        //*** pref_player_equalizerBand4k
        appPrefs.loadedData.equalizerBand4khz = (int)pref_player_equalizerBand4k.Value;
        //*** pref_player_equalizerBand8k
        appPrefs.loadedData.equalizerBand8khz = (int)pref_player_equalizerBand8k.Value;
        //*** pref_player_equalizerBand16k
        appPrefs.loadedData.equalizerBand16khz = (int)pref_player_equalizerBand16k.Value;
        //*** pref_player_speakerRightClickEmulationStep1x
        appPrefs.loadedData.outputSelectorEmulateMoveStep1x = (int)pref_player_speakerRightClickEmulationStep1x.Value;
        //*** pref_player_speakerRightClickEmulationStep1y
        appPrefs.loadedData.outputSelectorEmulateMoveStep1y = (int)pref_player_speakerRightClickEmulationStep1y.Value;
        //*** pref_player_speakerRightClickEmulationStep2x
        appPrefs.loadedData.outputSelectorEmulateMoveStep2x = (int)pref_player_speakerRightClickEmulationStep2x.Value;
        //*** pref_player_speakerRightClickEmulationStep2y
        appPrefs.loadedData.outputSelectorEmulateMoveStep2y = (int)pref_player_speakerRightClickEmulationStep2y.Value;
        //*** pref_player_automaticVolume
        appPrefs.loadedData.automaticVolume = (bool)pref_player_automaticVolume.IsChecked;
        //*** pref_player_volumeMark1speed
        appPrefs.loadedData.mark1volumeSpeed = (int)pref_player_volumeMark1speed.Value;
        //*** pref_player_volumeMark1target
        appPrefs.loadedData.mark1volumeTarget = (int)pref_player_volumeMark1target.Value;
        //*** pref_player_volumeMark2speed
        appPrefs.loadedData.mark2volumeSpeed = (int)pref_player_volumeMark2speed.Value;
        //*** pref_player_volumeMark2target
        appPrefs.loadedData.mark2volumeTarget = (int)pref_player_volumeMark2target.Value;
        //*** pref_player_volumeMark3speed
        appPrefs.loadedData.mark3volumeSpeed = (int)pref_player_volumeMark3speed.Value;
        //*** pref_player_volumeMark3target
        appPrefs.loadedData.mark3volumeTarget = (int)pref_player_volumeMark3target.Value;
        //*** pref_player_volumeMark4speed
        appPrefs.loadedData.mark4volumeSpeed = (int)pref_player_volumeMark4speed.Value;
        //*** pref_player_volumeMark4target
        appPrefs.loadedData.mark4volumeTarget = (int)pref_player_volumeMark4target.Value;
        //*** pref_player_volumeMark5speed
        appPrefs.loadedData.mark5volumeSpeed = (int)pref_player_volumeMark5speed.Value;
        //*** pref_player_volumeMark5target
        appPrefs.loadedData.mark5volumeTarget = (int)pref_player_volumeMark5target.Value;
        //*** pref_player_volumeMark6speed
        appPrefs.loadedData.mark6volumeSpeed = (int)pref_player_volumeMark6speed.Value;
        //*** pref_player_volumeMark6target
        appPrefs.loadedData.mark6volumeTarget = (int)pref_player_volumeMark6target.Value;
        //*** pref_player_volumeMark7speed
        appPrefs.loadedData.mark7volumeSpeed = (int)pref_player_volumeMark7speed.Value;
        //*** pref_player_volumeMark7target
        appPrefs.loadedData.mark7volumeTarget = (int)pref_player_volumeMark7target.Value;
        //*** pref_player_volumeBoostAtMaxRpm
        appPrefs.loadedData.volumeBoostOnMaxRpm = (int)pref_player_volumeBoostAtMaxRpm.Value;

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

    private void UpdateEqualizerBandOptionsVisibility()
    {
        //Hide the equalizer band options if the equalizer profile is different from "Custom"
        pref_player_equalizerAmplifier_root.IsVisible = ((pref_player_equalizerProfile.SelectedIndex == 2) ? true : false);
        pref_player_equalizerBand31_root.IsVisible = ((pref_player_equalizerProfile.SelectedIndex == 2) ? true : false);
        pref_player_equalizerBand62_root.IsVisible = ((pref_player_equalizerProfile.SelectedIndex == 2) ? true : false);
        pref_player_equalizerBand125_root.IsVisible = ((pref_player_equalizerProfile.SelectedIndex == 2) ? true : false);
        pref_player_equalizerBand250_root.IsVisible = ((pref_player_equalizerProfile.SelectedIndex == 2) ? true : false);
        pref_player_equalizerBand500_root.IsVisible = ((pref_player_equalizerProfile.SelectedIndex == 2) ? true : false);
        pref_player_equalizerBand1k_root.IsVisible = ((pref_player_equalizerProfile.SelectedIndex == 2) ? true : false);
        pref_player_equalizerBand2k_root.IsVisible = ((pref_player_equalizerProfile.SelectedIndex == 2) ? true : false);
        pref_player_equalizerBand4k_root.IsVisible = ((pref_player_equalizerProfile.SelectedIndex == 2) ? true : false);
        pref_player_equalizerBand8k_root.IsVisible = ((pref_player_equalizerProfile.SelectedIndex == 2) ? true : false);
        pref_player_equalizerBand16k_root.IsVisible = ((pref_player_equalizerProfile.SelectedIndex == 2) ? true : false);
    }

    private void UpdateVolumeMarksOptionsVisibility()
    {
        //Hide the automatic volume marks options, if the automatic volume option is disabled
        pref_player_volumeMark1_root.IsVisible = (bool)pref_player_automaticVolume.IsChecked;
        pref_player_volumeMark2_root.IsVisible = (bool)pref_player_automaticVolume.IsChecked;
        pref_player_volumeMark3_root.IsVisible = (bool)pref_player_automaticVolume.IsChecked;
        pref_player_volumeMark4_root.IsVisible = (bool)pref_player_automaticVolume.IsChecked;
        pref_player_volumeMark5_root.IsVisible = (bool)pref_player_automaticVolume.IsChecked;
        pref_player_volumeMark6_root.IsVisible = (bool)pref_player_automaticVolume.IsChecked;
        pref_player_volumeMark7_root.IsVisible = (bool)pref_player_automaticVolume.IsChecked;
        pref_player_volumeBoostAtMaxRpm_root.IsVisible = (bool)pref_player_automaticVolume.IsChecked;
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

    private string ValidateIfInputIsValidIntegerAndGetResult(string input)
    {
        //Prepare the value to return
        string toReturn = "";

        //Check if is empty, cancel here
        if (input == "")
        {
            toReturn = "Enter a value!";
            return toReturn;
        }
        //Check if is a int number
        if (int.TryParse(input, out _) == false)
            toReturn = "Enter a number!";

        //Return the value
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