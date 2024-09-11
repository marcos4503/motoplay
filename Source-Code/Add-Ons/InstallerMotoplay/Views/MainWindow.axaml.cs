using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Coroutine;
using MarcosTomaz.ATS;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace InstallerMotoplay.Views;

/*
* This is the code responsible by the Motoplay Installer Window
*/

public partial class MainWindow : Window
{
    //Cache variables
    private Process terminalCliProcess = null;
    private List<string> terminalReceivedOutputLines = new List<string>();
    private Process wvkbdProcess = null;

    //Private variables
    private bool isCommandInProgress = false;

    //Core methods

    public MainWindow()
    {
        //Initialize the window normally on Avalonia side
        InitializeComponent();
    }

    public MainWindow(string[]? cliArgs) : this()
    {
        //Start the initialization of window, receiving all CLI arguments
        StartThisWindow(cliArgs);
    }

    private void StartThisWindow(string[]? cliArgs)
    {
        //Log the parameters found to console
        StringBuilder paramsStr = new StringBuilder();
        foreach(string item in cliArgs)
            paramsStr.Append(" " + item);
        Debug.WriteLine("Params Found:" + paramsStr.ToString() + ".");

        //Prepare the UI
        doingTaskStatus.Text = "Initializing";
        toggleKeyboardButton.IsVisible = false;
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
                toggleKeyboardButtonImg.Source = new Bitmap(AssetLoader.Open(new Uri("avares://InstallerMotoplay/Assets/keyboard-off.png")));

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
                toggleKeyboardButtonImg.Source = new Bitmap(AssetLoader.Open(new Uri("avares://InstallerMotoplay/Assets/keyboard-on.png")));

                //Cancel here
                return;
            }
        };

        //Start Coroutine to wait GUI really initialize, before initialize the window
        CoroutineHandler.Start(WaitGuiInitBeforeReallyContinue());
    }

    private IEnumerator<Wait> WaitGuiInitBeforeReallyContinue()
    {
        //Wait a time while GUI is initialized
        yield return new Wait(5.0f);

        //Start a terminal CLI process
        StartCliProcess();
    }

    private void StartCliProcess()
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
            //Start the process of installation of "wvkbd" tool, if needed
            CoroutineHandler.Start(InstallWvkbdIfNeeded());
        };
        asyncTask.Execute(AsyncTaskSimplified.ExecutionMode.NewDefaultThread);
    }

    private IEnumerator<Wait> InstallWvkbdIfNeeded()
    {
        //If Linux, continue...
        if (OperatingSystem.IsLinux() == true)
        {
            //Inform that is checking
            doingTaskStatus.Text = "Checking installation of Virtual Keyboard \"wvkbd\"";

            //Wait time
            yield return new Wait(3.0f);

            //Send a command to check if the "wvkbd" is installed
            SendCommandToTerminalAndClearCurrentOutputLines("sudo dpkg -s wvkbd");
            //Wait the end of command execution
            while (isLastCommandFinishedExecution() == false)
                yield return new Wait(0.1f);

            //If not installed, start installation
            if (isThermFoundInTerminalOutputLines("is not installed") == true)
            {
                //Inform that is installing
                doingTaskStatus.Text = "Installing Virtual Keyboard \"wvkbd\"";

                //Wait time
                yield return new Wait(5.0f);

                //Send a command to install the "wvkbd"
                SendCommandToTerminalAndClearCurrentOutputLines("sudo apt-get install wvkbd");
                //Wait the end of command execution
                while (isLastCommandFinishedExecution() == false)
                    yield return new Wait(0.1f);

                //Inform that is checking
                doingTaskStatus.Text = "Checking installation of Virtual Keyboard \"wvkbd\"";

                //Wait time
                yield return new Wait(5.0f);

                //Send a command to check if the "wvkbd" is installed
                SendCommandToTerminalAndClearCurrentOutputLines("sudo dpkg -s wvkbd");
                //Wait the end of command execution
                while (isLastCommandFinishedExecution() == false)
                    yield return new Wait(0.1f);

                //If not installed, stop the program
                if (isThermFoundInTerminalOutputLines("is not installed") == true)
                {
                    MessageBoxManager.GetMessageBoxStandard("Error", "There was an error installing the Package. Please check your connection and try again!", ButtonEnum.Ok).ShowAsync();
                    this.Close();
                }
            }

            //Enable the keyboard button
            toggleKeyboardButton.IsVisible = true;
        }

        //If Windows, just continue...
        if (OperatingSystem.IsWindows() == true)
            Debug.WriteLine("The tool \"wvkbd\" is not necessary on Windows.");

        //...
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
            if(line.Contains("> Done Command!") == true)
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