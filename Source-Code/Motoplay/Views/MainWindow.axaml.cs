using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Coroutine;
using MarcosTomaz.ATS;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Motoplay.Views;

/*
* This is the code responsible by the Motoplay App Window
*/

public partial class MainWindow : Window
{
    //Cache variables
    private IDictionary<string, string> runningTasks = new Dictionary<string, string>();
    private Process terminalCliProcess = null;
    private List<string> terminalReceivedOutputLines = new List<string>();
    private Process wvkbdProcess = null;

    //Private variables
    private string applicationVersion = "";
    private string[] receivedCliArgs = null;
    private string systemCurrentUsername = "";
    private string motoplayRootPath = "";
    private bool isCommandInProgress = false;

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
            MessageBoxManager.GetMessageBoxStandard("Error", "Motoplay App is already running!", ButtonEnum.Ok).ShowAsync();
            this.Close();
            return;
        }

        //Log the parameters found to console
        StringBuilder paramsStr = new StringBuilder();
        foreach (string item in cliArgs)
            paramsStr.Append(" " + item);
        Debug.WriteLine("Params Found:" + paramsStr.ToString() + ".");

        //Store the received CLIs
        receivedCliArgs = cliArgs;

        //Start the initialization of Window
        StartThisWindow();
    }

    private void StartThisWindow()
    {
        //Adjust this window, if is running on Windows PC, in testing
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

        //Recover the version of the application
        System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
        System.Diagnostics.FileVersionInfo fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
        if (OperatingSystem.IsWindows() == true)
        {
            applicationVersion = fvi.FileVersion.Remove(fvi.FileVersion.Length - 1, 1);
            applicationVersion = applicationVersion.Remove(applicationVersion.Length - 1, 1);
        }
        if (OperatingSystem.IsLinux() == true)
            applicationVersion = fvi.FileVersion;

        //Prepare and start the UI
        PrepareTheUI();
    }

    private void PrepareTheUI()
    {
        //Prepare the keyboard toggle button
        toggleKeyboardButton.Click += (s, e) =>
        {
            //If is keyboard not open
            if (wvkbdProcess == null)
            {
                //Build the string of keyboard parameters
                StringBuilder keyboardParams = new StringBuilder();
                keyboardParams.Append(("-L " + (int)((float)Screens.Primary.Bounds.Height * 0.45f)));
                keyboardParams.Append(" --fn \"Segoe UI 16\"");
                keyboardParams.Append(" --bg 000000AA");
                keyboardParams.Append(" --fg 323333EF");
                keyboardParams.Append(" --fg-sp 121212EF");
                keyboardParams.Append(" --press 021E4DFF");
                keyboardParams.Append(" --press-sp 021E4DFF");
                keyboardParams.Append(" --text FFFFFFFF");
                keyboardParams.Append(" --text-sp C7C7C7FF");
                keyboardParams.Append(" --landscape-layers landscape,special,emoji");

                //Start the wvkbd process
                wvkbdProcess = new Process() { StartInfo = new ProcessStartInfo() { FileName = "/usr/bin/wvkbd-mobintl", Arguments = keyboardParams.ToString() } };
                wvkbdProcess.Start();

                //Set the icon
                toggleKeyboardButtonImg.Source = new Bitmap(AssetLoader.Open(new Uri("avares://Motoplay/Assets/keyboard-off.png")));

                //Cancel here
                return;
            }

            //If is keyboard open
            if (wvkbdProcess != null)
            {
                //Stop the wvkbd process
                wvkbdProcess.Kill();
                wvkbdProcess = null;

                //Set the icon
                toggleKeyboardButtonImg.Source = new Bitmap(AssetLoader.Open(new Uri("avares://Motoplay/Assets/keyboard-on.png")));

                //Cancel here
                return;
            }
        };

        //Start a terminal for CLI process
        StartCliTerminalProcess();
    }

    private void StartCliTerminalProcess()
    {
        //Inform that read is in progress
        isCommandInProgress = true;

        //Start a new thread to start the process
        AsyncTaskSimplified asyncTask = new AsyncTaskSimplified(null, new string[] { });
        asyncTask.onStartTask_RunMainThread += (callerWindow, startParams) => { };
        asyncTask.onExecuteTask_RunBackground += (callerWindow, startParams, threadTools) =>
        {
            //Prepare the Terminal process
            Process terminalProcess = new Process();
            if (OperatingSystem.IsLinux() == true)
            {
                terminalProcess.StartInfo.FileName = "/bin/bash";
                terminalProcess.StartInfo.Arguments = "";
                terminalProcess.StartInfo.WorkingDirectory = @"./";
            }
            if (OperatingSystem.IsWindows() == true)
            {
                terminalProcess.StartInfo.FileName = "cmd.exe";
                terminalProcess.StartInfo.Arguments = "/k";
                terminalProcess.StartInfo.WorkingDirectory = @"C:\";
            }
            terminalProcess.StartInfo.UseShellExecute = false;
            terminalProcess.StartInfo.CreateNoWindow = true;
            terminalProcess.StartInfo.RedirectStandardInput = true;
            terminalProcess.StartInfo.RedirectStandardOutput = true;
            terminalProcess.StartInfo.RedirectStandardError = true;

            terminalProcess.ErrorDataReceived += new DataReceivedEventHandler((s, e) =>
            {
                //If don't have data, cancel
                if (String.IsNullOrEmpty(e.Data) == true)
                    return;

                //Get the string output for this line
                string currentLineOutput = e.Data;

                //Add this new line to list
                terminalReceivedOutputLines.Add(currentLineOutput);
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
            });

            //Start the process, enable output reading and store a reference for the process
            terminalProcess.Start();
            terminalProcess.BeginOutputReadLine();
            terminalProcess.BeginErrorReadLine();
            terminalCliProcess = terminalProcess;

            //Wait time
            threadTools.MakeThreadSleep(500);

            //Send started successfully message
            SendCommandToTerminalAndClearCurrentOutputLines("echo \"> Terminal Opened!\"");

            //Wait time
            threadTools.MakeThreadSleep(500);

            //Finish the thread...
            return new string[] { "none" };
        };
        asyncTask.onDoneTask_RunMainThread += (callerWindow, backgroundResult) =>
        {
            //Start the process of installation of "unclutter" setup
            CoroutineHandler.Start(SetupTheUnclutterCursorHider());
        };
        asyncTask.Execute(AsyncTaskSimplified.ExecutionMode.NewDefaultThread);
    }

    private IEnumerator<Wait> SetupTheUnclutterCursorHider()
    {
        //Add this task running
        AddTask("unclutter_setup", "Setup the Unclutter.");

        //Wait time
        yield return new Wait(1.0f);

        //If Linux, continue...
        if (OperatingSystem.IsLinux() == true)
        {
            //Send a command to check if the "unclutter" is installed
            SendCommandToTerminalAndClearCurrentOutputLines("sudo dpkg -s unclutter");
            //Wait the end of command execution
            while (isLastCommandFinishedExecution() == false)
                yield return new Wait(0.1f);

            //If not installed, start installation
            if (isThermFoundInTerminalOutputLines("is not installed") == true)
            {
                //Wait time
                yield return new Wait(1.0f);

                //Send a command to install the "unclutter"
                SendCommandToTerminalAndClearCurrentOutputLines("sudo apt-get install unclutter -y");
                //Wait the end of command execution
                while (isLastCommandFinishedExecution() == false)
                    yield return new Wait(0.1f);

                //Wait time
                yield return new Wait(5.0f);

                //Send a command to check if the "unclutter" is installed
                SendCommandToTerminalAndClearCurrentOutputLines("sudo dpkg -s unclutter");
                //Wait the end of command execution
                while (isLastCommandFinishedExecution() == false)
                    yield return new Wait(0.1f);

                //If not installed, stop the program
                if (isThermFoundInTerminalOutputLines("is not installed") == true)
                {
                    var diag = MessageBoxManager.GetMessageBoxStandard("Error", "There was a problem, when installing a required package. Check your Internet connection!", ButtonEnum.Ok).ShowAsync();
                    while (diag.IsCompleted == false)
                        yield return new Wait(0.1f);
                    this.Close();
                }
            }

            //If the "unclutter" is already installed, send command to hide the cursor
            SendCommandToTerminalAndClearCurrentOutputLines("unclutter -idle 0.15 -root & echo \">ContinueInOtherThread\"");
            //Wait the end of command execution
            while (isLastCommandFinishedExecution() == false)
                yield return new Wait(0.1f);
        }

        //If Windows, just continue...
        if (OperatingSystem.IsWindows() == true)
            Debug.WriteLine("The tool \"unclutter\" is not necessary on Windows.");

        //Remove the task running
        RemoveTask("unclutter_setup");

        //Start the setup of the Right Click simulator
        CoroutineHandler.Start(SetupTheRightClickSimulation());
    }

    private IEnumerator<Wait> SetupTheRightClickSimulation() 
    {
        //Add this task running
        AddTask("rightClick_setup", "Setup the Right Click simulator.");

        //Wait time
        yield return new Wait(1.0f);

        //If Linux, continue...
        if (OperatingSystem.IsLinux() == true)
        {

        }

        //If Windows, just continue...
        if (OperatingSystem.IsWindows() == true)
            Debug.WriteLine("The tools \"xdotool\" and \"xbindkeys\" is not necessary on Windows.");

        //Remove the task running
        RemoveTask("rightClick_setup");
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

    //Auxiliar methods

    private void SendCommandToTerminalAndClearCurrentOutputLines(string command)
    {
        //Clear the previous terminal output lines
        terminalReceivedOutputLines.Clear();

        //Send a command to terminal, with a embedded message of success, that will be sended at end of target command
        if (OperatingSystem.IsLinux() == true)
            terminalCliProcess.StandardInput.WriteLine((command + ";echo \"> Done Command!\""));
        if (OperatingSystem.IsWindows() == true)
            terminalCliProcess.StandardInput.WriteLine((command + "&echo \"> Done Command!\""));
    }

    private bool isLastCommandFinishedExecution()
    {
        //Prepare to return
        bool toReturn = false;

        //Check each line to get info
        foreach (string line in terminalReceivedOutputLines)
            if (line.Contains("> Done Command!") == true)
            {
                toReturn = true;
                break;
            }

        //Return the result
        return toReturn;
    }

    private bool isThermFoundInTerminalOutputLines(string therm)
    {
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
}