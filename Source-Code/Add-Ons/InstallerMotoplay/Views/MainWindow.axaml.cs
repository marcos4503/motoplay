using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Coroutine;
using InstallerMotoplay.Scripts;
using MarcosTomaz.ATS;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;

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
    private string[] receivedCliArgs = null;
    private string systemCurrentUsername = "";
    private string motoplayRootPath = "";
    private string applicationVersion = "";

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
        Mutex mutex = new Mutex(true, "InstallerMotoplay.Desktop", out created);

        //If already have a instance of the app running, cancel
        if (created == false)
        {
            //Warn and stop here
            MessageBoxManager.GetMessageBoxStandard("Error", "Motoplay Installer is already running!", ButtonEnum.Ok).ShowAsync();
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

        //Start the initialization of window, receiving all CLI arguments
        StartThisWindow();
    }

    private void StartThisWindow()
    {
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
        FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
        applicationVersion = fvi.FileVersion;

        //Show the application version at the bottom
        versionDisplay.Text = applicationVersion;

        //Prepare the UI
        doingTaskStatus.Text = "Initializing Motoplay Installer";
        toggleKeyboardButton.IsVisible = false;
        toggleKeyboardButton.Click += (s, e) => { ToggleVirtualKeyboard(); };
        //If is Windows, disable the keyboard button
        if (OperatingSystem.IsWindows() == true)
            toggleKeyboardButton.IsEnabled = false;

        //Start Coroutine to start the binded CLI terminal process
        CoroutineHandler.Start(StartBindedCliTerminalProcess());
    }

    private IEnumerator<Wait> StartBindedCliTerminalProcess()
    {
        //Wait a time
        yield return new Wait(8.0f);

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

            //Send started successfully message
            SendCommandToTerminalAndClearCurrentOutputLines("echo \"> Terminal Opened!\"");

            //Wait time
            threadTools.MakeThreadSleep(500);

            //Finish the thread...
            return new string[] { "none" };
        };
        asyncTask.onDoneTask_RunMainThread += (callerWindow, backgroundResult) =>
        {
            //Start the process of setup for "p7zip-full"
            CoroutineHandler.Start(SetupTheP7ZipFull());
        };
        asyncTask.Execute(AsyncTaskSimplified.ExecutionMode.NewDefaultThread);
    }

    private IEnumerator<Wait> SetupTheP7ZipFull() 
    {
        //If is Linux...
        if (OperatingSystem.IsLinux() == true)
        {
            //Inform that is checking
            doingTaskStatus.Text = "Checking Installation of \"p7zip-full\" Package";

            //Wait time
            yield return new Wait(1.0f);

            //Send a command to check if the "p7zip-full" is installed
            SendCommandToTerminalAndClearCurrentOutputLines("sudo dpkg -s p7zip-full");
            //Wait the end of command execution
            while (isLastCommandFinishedExecution() == false)
                yield return new Wait(0.1f);

            //If not installed, start installation
            if (isThermFoundInTerminalOutputLines("is not installed") == true)
            {
                //Inform that is installing
                doingTaskStatus.Text = "Installing \"p7zip-full\" Package";

                //Wait time
                yield return new Wait(5.0f);

                //Send a command to install the "p7zip-full"
                SendCommandToTerminalAndClearCurrentOutputLines("sudo apt-get install p7zip-full -y");
                //Wait the end of command execution
                while (isLastCommandFinishedExecution() == false)
                    yield return new Wait(0.1f);

                //Inform that is confirming
                doingTaskStatus.Text = "Confirming Installation of \"p7zip-full\" Package";

                //Wait time
                yield return new Wait(5.0f);

                //Send a command to check if the "p7zip-full" is installed
                SendCommandToTerminalAndClearCurrentOutputLines("sudo dpkg -s p7zip-full");
                //Wait the end of command execution
                while (isLastCommandFinishedExecution() == false)
                    yield return new Wait(0.1f);

                //If not installed, stop the program
                if (isThermFoundInTerminalOutputLines("is not installed") == true)
                {
                    var diag = MessageBoxManager.GetMessageBoxStandard("Error", "There was an error installing the Package. Please check your connection and try again!", ButtonEnum.Ok).ShowAsync();
                    while (diag.IsCompleted == false)
                        yield return new Wait(0.1f);
                    this.Close();
                }
            }

            //...
        }

        //If Windows, just continue...
        if (OperatingSystem.IsWindows() == true)
            AvaloniaDebug.WriteLine("The package \"p7zip-full\" is not necessary on Windows.");

        //Continue to next step...
        CoroutineHandler.Start(SetupTheCurl());
    }

    private IEnumerator<Wait> SetupTheCurl()
    {
        //If is Linux...
        if (OperatingSystem.IsLinux() == true)
        {
            //Inform that is checking
            doingTaskStatus.Text = "Checking Installation of \"curl\" Package";

            //Wait time
            yield return new Wait(1.0f);

            //Send a command to check if the "curl" is installed
            SendCommandToTerminalAndClearCurrentOutputLines("sudo dpkg -s curl");
            //Wait the end of command execution
            while (isLastCommandFinishedExecution() == false)
                yield return new Wait(0.1f);

            //If not installed, start installation
            if (isThermFoundInTerminalOutputLines("is not installed") == true)
            {
                //Inform that is installing
                doingTaskStatus.Text = "Installing \"curl\" Package";

                //Wait time
                yield return new Wait(5.0f);

                //Send a command to install the "curl"
                SendCommandToTerminalAndClearCurrentOutputLines("sudo apt-get install curl -y");
                //Wait the end of command execution
                while (isLastCommandFinishedExecution() == false)
                    yield return new Wait(0.1f);

                //Inform that is confirming
                doingTaskStatus.Text = "Confirming Installation of \"curl\" Package";

                //Wait time
                yield return new Wait(5.0f);

                //Send a command to check if the "curl" is installed
                SendCommandToTerminalAndClearCurrentOutputLines("sudo dpkg -s curl");
                //Wait the end of command execution
                while (isLastCommandFinishedExecution() == false)
                    yield return new Wait(0.1f);

                //If not installed, stop the program
                if (isThermFoundInTerminalOutputLines("is not installed") == true)
                {
                    var diag = MessageBoxManager.GetMessageBoxStandard("Error", "There was an error installing the Package. Please check your connection and try again!", ButtonEnum.Ok).ShowAsync();
                    while (diag.IsCompleted == false)
                        yield return new Wait(0.1f);
                    this.Close();
                }
            }

            //...
        }

        //If Windows, just continue...
        if (OperatingSystem.IsWindows() == true)
            AvaloniaDebug.WriteLine("The package \"curl\" is not necessary on Windows.");

        //Continue to next step...
        CoroutineHandler.Start(SetupTheMake());
    }

    private IEnumerator<Wait> SetupTheMake()
    {
        //If is Linux...
        if (OperatingSystem.IsLinux() == true)
        {
            //Inform that is checking
            doingTaskStatus.Text = "Checking Installation of \"make\" Package";

            //Wait time
            yield return new Wait(1.0f);

            //Send a command to check if the "make" is installed
            SendCommandToTerminalAndClearCurrentOutputLines("sudo dpkg -s make");
            //Wait the end of command execution
            while (isLastCommandFinishedExecution() == false)
                yield return new Wait(0.1f);

            //If not installed, start installation
            if (isThermFoundInTerminalOutputLines("is not installed") == true)
            {
                //Inform that is installing
                doingTaskStatus.Text = "Installing \"make\" Package";

                //Wait time
                yield return new Wait(5.0f);

                //Send a command to install the "make"
                SendCommandToTerminalAndClearCurrentOutputLines("sudo apt-get install make -y");
                //Wait the end of command execution
                while (isLastCommandFinishedExecution() == false)
                    yield return new Wait(0.1f);

                //Inform that is confirming
                doingTaskStatus.Text = "Confirming Installation of \"make\" Package";

                //Wait time
                yield return new Wait(5.0f);

                //Send a command to check if the "make" is installed
                SendCommandToTerminalAndClearCurrentOutputLines("sudo dpkg -s make");
                //Wait the end of command execution
                while (isLastCommandFinishedExecution() == false)
                    yield return new Wait(0.1f);

                //If not installed, stop the program
                if (isThermFoundInTerminalOutputLines("is not installed") == true)
                {
                    var diag = MessageBoxManager.GetMessageBoxStandard("Error", "There was an error installing the Package. Please check your connection and try again!", ButtonEnum.Ok).ShowAsync();
                    while (diag.IsCompleted == false)
                        yield return new Wait(0.1f);
                    this.Close();
                }
            }
        }

        //If Windows, just continue...
        if (OperatingSystem.IsWindows() == true)
            AvaloniaDebug.WriteLine("The package \"make\" is not necessary on Windows.");

        //Continue to next step...
        CoroutineHandler.Start(SetupTheWaylandClientDev());
    }

    private IEnumerator<Wait> SetupTheWaylandClientDev() 
    {
        //If is Linux...
        if (OperatingSystem.IsLinux() == true)
        {
            //Inform that is checking
            doingTaskStatus.Text = "Checking Installation of \"librust-wayland-client-dev\" Package";

            //Wait time
            yield return new Wait(1.0f);

            //Send a command to check if the "librust-wayland-client-dev" is installed
            SendCommandToTerminalAndClearCurrentOutputLines("sudo dpkg -s librust-wayland-client-dev");
            //Wait the end of command execution
            while (isLastCommandFinishedExecution() == false)
                yield return new Wait(0.1f);

            //If not installed, start installation
            if (isThermFoundInTerminalOutputLines("is not installed") == true)
            {
                //Inform that is installing
                doingTaskStatus.Text = "Installing \"librust-wayland-client-dev\" Package";

                //Wait time
                yield return new Wait(5.0f);

                //Send a command to install the "librust-wayland-client-dev"
                SendCommandToTerminalAndClearCurrentOutputLines("sudo apt-get install librust-wayland-client-dev -y");
                //Wait the end of command execution
                while (isLastCommandFinishedExecution() == false)
                    yield return new Wait(0.1f);

                //Inform that is confirming
                doingTaskStatus.Text = "Confirming Installation of \"librust-wayland-client-dev\" Package";

                //Wait time
                yield return new Wait(5.0f);

                //Send a command to check if the "librust-wayland-client-dev" is installed
                SendCommandToTerminalAndClearCurrentOutputLines("sudo dpkg -s librust-wayland-client-dev");
                //Wait the end of command execution
                while (isLastCommandFinishedExecution() == false)
                    yield return new Wait(0.1f);

                //If not installed, stop the program
                if (isThermFoundInTerminalOutputLines("is not installed") == true)
                {
                    var diag = MessageBoxManager.GetMessageBoxStandard("Error", "There was an error installing the Package. Please check your connection and try again!", ButtonEnum.Ok).ShowAsync();
                    while (diag.IsCompleted == false)
                        yield return new Wait(0.1f);
                    this.Close();
                }
            }

            //...
        }

        //If Windows, just continue...
        if (OperatingSystem.IsWindows() == true)
            AvaloniaDebug.WriteLine("The package \"librust-wayland-client-dev\" is not necessary on Windows.");

        //Continue to next step...
        CoroutineHandler.Start(SetupTheCairoDev());
    }

    private IEnumerator<Wait> SetupTheCairoDev()
    {
        //If is Linux...
        if (OperatingSystem.IsLinux() == true)
        {
            //Inform that is checking
            doingTaskStatus.Text = "Checking Installation of \"libcairo2-dev\" Package";

            //Wait time
            yield return new Wait(1.0f);

            //Send a command to check if the "libcairo2-dev" is installed
            SendCommandToTerminalAndClearCurrentOutputLines("sudo dpkg -s libcairo2-dev");
            //Wait the end of command execution
            while (isLastCommandFinishedExecution() == false)
                yield return new Wait(0.1f);

            //If not installed, start installation
            if (isThermFoundInTerminalOutputLines("is not installed") == true)
            {
                //Inform that is installing
                doingTaskStatus.Text = "Installing \"libcairo2-dev\" Package";

                //Wait time
                yield return new Wait(5.0f);

                //Send a command to install the "libcairo2-dev"
                SendCommandToTerminalAndClearCurrentOutputLines("sudo apt-get install libcairo2-dev -y");
                //Wait the end of command execution
                while (isLastCommandFinishedExecution() == false)
                    yield return new Wait(0.1f);

                //Inform that is confirming
                doingTaskStatus.Text = "Confirming Installation of \"libcairo2-dev\" Package";

                //Wait time
                yield return new Wait(5.0f);

                //Send a command to check if the "libcairo2-dev" is installed
                SendCommandToTerminalAndClearCurrentOutputLines("sudo dpkg -s libcairo2-dev");
                //Wait the end of command execution
                while (isLastCommandFinishedExecution() == false)
                    yield return new Wait(0.1f);

                //If not installed, stop the program
                if (isThermFoundInTerminalOutputLines("is not installed") == true)
                {
                    var diag = MessageBoxManager.GetMessageBoxStandard("Error", "There was an error installing the Package. Please check your connection and try again!", ButtonEnum.Ok).ShowAsync();
                    while (diag.IsCompleted == false)
                        yield return new Wait(0.1f);
                    this.Close();
                }
            }

            //...
        }

        //If Windows, just continue...
        if (OperatingSystem.IsWindows() == true)
            AvaloniaDebug.WriteLine("The package \"libcairo2-dev\" is not necessary on Windows.");

        //Continue to next step...
        CoroutineHandler.Start(SetupThePangoDev());
    }

    private IEnumerator<Wait> SetupThePangoDev()
    {
        //If is Linux...
        if (OperatingSystem.IsLinux() == true)
        {
            //Inform that is checking
            doingTaskStatus.Text = "Checking Installation of \"libghc-gi-pango-dev\" Package";

            //Wait time
            yield return new Wait(1.0f);

            //Send a command to check if the "libghc-gi-pango-dev" is installed
            SendCommandToTerminalAndClearCurrentOutputLines("sudo dpkg -s libghc-gi-pango-dev");
            //Wait the end of command execution
            while (isLastCommandFinishedExecution() == false)
                yield return new Wait(0.1f);

            //If not installed, start installation
            if (isThermFoundInTerminalOutputLines("is not installed") == true)
            {
                //Inform that is installing
                doingTaskStatus.Text = "Installing \"libghc-gi-pango-dev\" Package";

                //Wait time
                yield return new Wait(5.0f);

                //Send a command to install the "libghc-gi-pango-dev"
                SendCommandToTerminalAndClearCurrentOutputLines("sudo apt-get install libghc-gi-pango-dev -y");
                //Wait the end of command execution
                while (isLastCommandFinishedExecution() == false)
                    yield return new Wait(0.1f);

                //Inform that is confirming
                doingTaskStatus.Text = "Confirming Installation of \"libghc-gi-pango-dev\" Package";

                //Wait time
                yield return new Wait(5.0f);

                //Send a command to check if the "libghc-gi-pango-dev" is installed
                SendCommandToTerminalAndClearCurrentOutputLines("sudo dpkg -s libghc-gi-pango-dev");
                //Wait the end of command execution
                while (isLastCommandFinishedExecution() == false)
                    yield return new Wait(0.1f);

                //If not installed, stop the program
                if (isThermFoundInTerminalOutputLines("is not installed") == true)
                {
                    var diag = MessageBoxManager.GetMessageBoxStandard("Error", "There was an error installing the Package. Please check your connection and try again!", ButtonEnum.Ok).ShowAsync();
                    while (diag.IsCompleted == false)
                        yield return new Wait(0.1f);
                    this.Close();
                }
            }

            //...
        }

        //If Windows, just continue...
        if (OperatingSystem.IsWindows() == true)
            AvaloniaDebug.WriteLine("The package \"libghc-gi-pango-dev\" is not necessary on Windows.");

        //Continue to next step...
        CoroutineHandler.Start(SetupTheXKBCommon());
    }

    private IEnumerator<Wait> SetupTheXKBCommon() 
    {
        //If is Linux...
        if (OperatingSystem.IsLinux() == true)
        {
            //Inform that is checking
            doingTaskStatus.Text = "Checking Installation of \"libxkbcommon-dev\" Package";

            //Wait time
            yield return new Wait(1.0f);

            //Send a command to check if the "libxkbcommon-dev" is installed
            SendCommandToTerminalAndClearCurrentOutputLines("sudo dpkg -s libxkbcommon-dev");
            //Wait the end of command execution
            while (isLastCommandFinishedExecution() == false)
                yield return new Wait(0.1f);

            //If not installed, start installation
            if (isThermFoundInTerminalOutputLines("is not installed") == true)
            {
                //Inform that is installing
                doingTaskStatus.Text = "Installing \"libxkbcommon-dev\" Package";

                //Wait time
                yield return new Wait(5.0f);

                //Send a command to install the "libxkbcommon-dev"
                SendCommandToTerminalAndClearCurrentOutputLines("sudo apt-get install libxkbcommon-dev -y");
                //Wait the end of command execution
                while (isLastCommandFinishedExecution() == false)
                    yield return new Wait(0.1f);

                //Inform that is confirming
                doingTaskStatus.Text = "Confirming Installation of \"libxkbcommon-dev\" Package";

                //Wait time
                yield return new Wait(5.0f);

                //Send a command to check if the "libxkbcommon-dev" is installed
                SendCommandToTerminalAndClearCurrentOutputLines("sudo dpkg -s libxkbcommon-dev");
                //Wait the end of command execution
                while (isLastCommandFinishedExecution() == false)
                    yield return new Wait(0.1f);

                //If not installed, stop the program
                if (isThermFoundInTerminalOutputLines("is not installed") == true)
                {
                    var diag = MessageBoxManager.GetMessageBoxStandard("Error", "There was an error installing the Package. Please check your connection and try again!", ButtonEnum.Ok).ShowAsync();
                    while (diag.IsCompleted == false)
                        yield return new Wait(0.1f);
                    this.Close();
                }
            }

            //...
        }

        //If Windows, just continue...
        if (OperatingSystem.IsWindows() == true)
            AvaloniaDebug.WriteLine("The package \"libxkbcommon-dev\" is not necessary on Windows.");

        //Continue to next step...
        CoroutineHandler.Start(SetupTheWvkbdMobintl());
    }

    private IEnumerator<Wait> SetupTheWvkbdMobintl()
    {
        //If is Linux...
        if (OperatingSystem.IsLinux() == true)
        {
            //Inform that is checking
            doingTaskStatus.Text = "Checking Installation of \"wvkbd-mobintl\" Package";

            //Wait time
            yield return new Wait(1.0f);

            //Check if package "wvkbd-mobintl" is installed
            bool isWvkbdInstalled = File.Exists((motoplayRootPath + "/Keyboard/wvkbd-mobintl"));

            //If not installed, start installation
            if (isWvkbdInstalled == false)
            {
                //Inform that is downloading
                doingTaskStatus.Text = "Downloading \"wvkbd-mobintl\" Package";

                //Wait time
                yield return new Wait(5.0f);

                //Send a command to create "Keyboard" folder
                SendCommandToTerminalAndClearCurrentOutputLines(("mkdir \"" + motoplayRootPath + "/Keyboard" + "\""));
                //Wait the end of command execution
                while (isLastCommandFinishedExecution() == false)
                    yield return new Wait(0.1f);

                //Send a command to create "DownloadedFiles" folder
                SendCommandToTerminalAndClearCurrentOutputLines(("mkdir \"" + motoplayRootPath + "/DownloadedFiles" + "\""));
                //Wait the end of command execution
                while (isLastCommandFinishedExecution() == false)
                    yield return new Wait(0.1f);

                //Send a command to create delete "wvkbd-mobintl" previously downloaded ZIP
                SendCommandToTerminalAndClearCurrentOutputLines(("rm \"" + motoplayRootPath + "/DownloadedFiles/wvkbd-enhanced-for-motoplay.zip" + "\""));
                //Wait the end of command execution
                while (isLastCommandFinishedExecution() == false)
                    yield return new Wait(0.1f);

                //Send a command to download the "wvkbd-mobintl"
                SendCommandToTerminalAndClearCurrentOutputLines("curl -o \"" + motoplayRootPath + "/DownloadedFiles/wvkbd-enhanced-for-motoplay.zip" + "\" \"https://marcos4503.github.io/motoplay/Repository-Pages/wvkbd-enhanced-for-motoplay.zip\"");
                //Wait the end of command execution
                while (isLastCommandFinishedExecution() == false)
                    yield return new Wait(0.1f);

                //Inform that is downloading
                doingTaskStatus.Text = "Expanding Files of \"wvkbd-mobintl\" Package";

                //Wait time
                yield return new Wait(5.0f);

                //Send a command to extract files of "wvkbd-mobintl"
                SendCommandToTerminalAndClearCurrentOutputLines("unzip \"" + motoplayRootPath + "/DownloadedFiles/wvkbd-enhanced-for-motoplay.zip" + "\" -d \"" + motoplayRootPath + "/Keyboard" + "\"");
                //Wait the end of command execution
                while (isLastCommandFinishedExecution() == false)
                    yield return new Wait(0.1f);

                //Inform that is compiling
                doingTaskStatus.Text = "Installing \"wvkbd-mobintl\" Package";

                //Wait time
                yield return new Wait(5.0f);

                //Send a command to change directory to "wvkbd-mobintl" files
                SendCommandToTerminalAndClearCurrentOutputLines("cd \"" + motoplayRootPath + "/Keyboard" + "\"");
                //Wait the end of command execution
                while (isLastCommandFinishedExecution() == false)
                    yield return new Wait(0.1f);

                //Send a command to compile "wvkbd-mobintl" files
                SendCommandToTerminalAndClearCurrentOutputLines("make");
                //Wait the end of command execution
                while (isLastCommandFinishedExecution() == false)
                    yield return new Wait(0.1f);

                //Send a command to change to default directory
                SendCommandToTerminalAndClearCurrentOutputLines("cd ~");
                //Wait the end of command execution
                while (isLastCommandFinishedExecution() == false)
                    yield return new Wait(0.1f);

                //Inform that is compiling
                doingTaskStatus.Text = "Preparing \"wvkbd-mobintl\" Package";

                //Wait time
                yield return new Wait(5.0f);

                //Send a command to make the "wvkbd-mobintl" executable
                SendCommandToTerminalAndClearCurrentOutputLines("chmod +x \"" + motoplayRootPath + "/Keyboard/wvkbd-mobintl" + "\"");
                //Wait the end of command execution
                while (isLastCommandFinishedExecution() == false)
                    yield return new Wait(0.1f);
            }

            //Enable the keyboard button
            toggleKeyboardButton.IsVisible = true;
        }

        //If Windows, just continue...
        if (OperatingSystem.IsWindows() == true)
            AvaloniaDebug.WriteLine("The package \"wvkbd-mobintl\" is not necessary on Windows.");

        //Continue to next step...
        ContinueToInstaller();
    }

    private void ContinueToInstaller()
    {
        //Prepare the directory of persistent data
        if (Directory.Exists((motoplayRootPath + "/PersistentData")) == false)
            Directory.CreateDirectory((motoplayRootPath + "/PersistentData"));
        //Prepare the directory of install files
        if (Directory.Exists((motoplayRootPath + "/InstallFiles")) == true)
            Directory.Delete((motoplayRootPath + "/InstallFiles"), true);
        Directory.CreateDirectory((motoplayRootPath + "/InstallFiles"));

        //If have the "online" argument, skip to online mode
        if (receivedCliArgs != null)
            if (receivedCliArgs.Length >= 1)
                if (receivedCliArgs[0].Contains("online") == true)
                {
                    GetInstallFilesFromOnline();
                    return;
                }

        //Change the screens
        doingTasksRoot.IsVisible = false;
        updateMenu.IsVisible = true;
        if (File.Exists((motoplayRootPath + "/PersistentData/last-fd-name.txt")) == true)
            flashDriveNameInput.Text = File.ReadAllText((motoplayRootPath + "/PersistentData/last-fd-name.txt"));
        installFlashdriveButtonHelp.Tapped += (s, e) => { MessageBoxManager.GetMessageBoxStandard("Tip", "To install or update Motoplay App from a Flash Drive...\n\n1. Create a folder called \"Motoplay\" inside the Flash Drive.\n2. Place all the Motoplay files inside this folder created on the Flash Drive.\n3. Connect the Flash Drive to the Raspberry Pi.\n4. Enter the name of the Flash Drive in the Text Box below.\n5. Click on \"Flash Drive\" to start the update, using the files on the Flash Drive, instead of the files found Online, in the Motoplay repository.\n\nOnce this is done, just wait for the update, Motoplay will be updated normally and will start.", ButtonEnum.Ok).ShowAsync(); };

        //Setup the buttons
        installOnlineButton.Click += (s, e) => { GetInstallFilesFromOnline(); };
        installFlashdriveButton.Click += (s, e) => { GetInstallFilesFromFlashDrive(); };
    }

    private void GetInstallFilesFromOnline()
    {
        //Change the screen
        doingTasksRoot.IsVisible = true;
        updateMenu.IsVisible = false;

        //Inform status
        doingTaskStatus.Text = "Preparing To Download The Motoplay App";
        doingTasksBar.Maximum = 100;
        doingTasksBar.Value = 0;
        doingTasksBar.IsIndeterminate = true;

        //Start a new thread to copy the files
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

                //Inform the status
                threadTools.ReportNewProgress("Downloading Repository Information;0;0");

                //Wait some time
                threadTools.MakeThreadSleep(1000);

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

                //-------------------------- MOTOPLAY APP DOWNLOAD --------------------------//

                //Inform the status
                threadTools.ReportNewProgress("Downloading Motoplay App;100;0");

                //Wait some time
                threadTools.MakeThreadSleep(1000);

                //Load the repository info
                AppRepositoryInfo appInfo = new AppRepositoryInfo((rootPath + "/PersistentData/app-repository-info.json"));

                //Prepare the directory of download files
                if (Directory.Exists((rootPath + "/DownloadedFiles")) == true)
                    Directory.Delete((rootPath + "/DownloadedFiles"), true);
                Directory.CreateDirectory((rootPath + "/DownloadedFiles"));

                //Download all parts of the zip of app
                for (int i = 0; i < appInfo.loadedData.downloadPartsLinks.Length; i++)
                {
                    //Download this part
                    WebClient client = new WebClient();
                    client.DownloadProgressChanged += (s, e) =>
                    {
                        //Get the progress info
                        int maxValue = (int)(100 * appInfo.loadedData.downloadPartsLinks.Length);
                        int value = (int)((100 * i) + (int)(e.ProgressPercentage));

                        //Report the progress
                        threadTools.ReportNewProgress(("Downloading Motoplay App;" + maxValue + ";" + value));
                    };
                    client.DownloadFileAsync(new Uri(appInfo.loadedData.downloadPartsLinks[i]), ((rootPath + "/DownloadedFiles/" + appInfo.loadedData.downloadPartsLinks[i].Split("/").Last())));

                    //Wait until this download finish
                    while (client.IsBusy == true)
                        threadTools.MakeThreadSleep(33);
                }

                //Get the main ZIP file of all ZIP parts of Motoplay App downloaded
                string motoplayAppMainZipFilePath = ((rootPath + "/DownloadedFiles/" + appInfo.loadedData.downloadPartsLinks[0].Split("/").Last()));

                //-------------------------- EXTRACT MOTOPLAY APP DOWNLOADED FILES --------------------------//

                //Inform the status
                threadTools.ReportNewProgress("Expanding Downloaded Files;0;0");

                //Wait some time
                threadTools.MakeThreadSleep(1000);

                //Send a command to extract the downloaded file
                SendCommandToTerminalAndClearCurrentOutputLines(("7z x -y \"" + motoplayAppMainZipFilePath + "\" -o\"" + (rootPath + "/InstallFiles") + "\""));

                //Wait the end of command execution
                while (isLastCommandFinishedExecution() == false)
                    threadTools.MakeThreadSleep(100);

                //-------------- END --------------//

                //Wait some time
                threadTools.MakeThreadSleep(1000);

                //Return a success response
                return new string[] { "success" };
            }
            catch (Exception ex)
            {
                //Return a error response
                return new string[] { "error" };
            }

            //Finish the thread...
            return new string[] { "none" };
        };
        asyncTask.onNewProgress_RunMainThread += (callerWindow, newProgress) =>
        {
            //Split the progress
            string[] progressSplitted = newProgress.Split(";");
            string message = progressSplitted[0];
            int maxValue = int.Parse(progressSplitted[1]);
            int value = int.Parse(progressSplitted[2]);

            //If is a indetermined progress bar
            if (maxValue == 0 && value == 0)
            {
                doingTasksBar.Maximum = 100;
                doingTasksBar.Value = 0;
                doingTasksBar.IsIndeterminate = true;
            }
            //If is a determined progress bar
            if (maxValue != 0 || value != 0)
            {
                doingTasksBar.Maximum = maxValue;
                doingTasksBar.Value = value;
                doingTasksBar.IsIndeterminate = false;
            }

            //Show the progress message
            doingTaskStatus.Text = message;
        };
        asyncTask.onDoneTask_RunMainThread += (callerWindow, backgroundResult) =>
        {
            //If was error
            if (backgroundResult[0] != "success")
            {
                MessageBoxManager.GetMessageBoxStandard("Error", "There was a problem during installation. Please check your internet connection!", ButtonEnum.Ok).ShowAsync();
                this.Close();
                return;
            }

            //If was success
            if (backgroundResult[0] == "success")
                FinishInstallationUsingLoadedInstallFiles();
        };
        asyncTask.Execute(AsyncTaskSimplified.ExecutionMode.NewDefaultThread);
    }

    private void GetInstallFilesFromFlashDrive()
    {
        //If not typed nothing, cancel
        if (string.IsNullOrEmpty(flashDriveNameInput.Text) == true)
        {
            MessageBoxManager.GetMessageBoxStandard("Error", "Enter the name of a Flash Drive!", ButtonEnum.Ok).ShowAsync();
            return;
        }

        //Save the typed flash drive name
        File.WriteAllText((motoplayRootPath + "/PersistentData/last-fd-name.txt"), flashDriveNameInput.Text);

        //If the flash drive not exists, cancel
        if (Directory.Exists(("/media/" + systemCurrentUsername + "/" + flashDriveNameInput.Text)) == false)
        {
            MessageBoxManager.GetMessageBoxStandard("Error", "Flash Drive \"" + flashDriveNameInput.Text + "\" is not available!", ButtonEnum.Ok).ShowAsync();
            return;
        }
        //If the "Motoplay" folder not exists, cancel
        if (Directory.Exists(("/media/" + systemCurrentUsername + "/" + flashDriveNameInput.Text + "/Motoplay")) == false)
        {
            MessageBoxManager.GetMessageBoxStandard("Error", "The \"Motoplay\" folder containing the installation files does not exist inside Flash Drive \"" + flashDriveNameInput.Text + "\"!", ButtonEnum.Ok).ShowAsync();
            return;
        }

        //Change the screen
        doingTasksRoot.IsVisible = true;
        updateMenu.IsVisible = false;

        //Inform status
        doingTaskStatus.Text = "Copying Files From \"" + flashDriveNameInput.Text + "\" Drive";

        //Start a new thread to copy the files
        AsyncTaskSimplified asyncTask = new AsyncTaskSimplified(null, new string[] { motoplayRootPath, systemCurrentUsername, flashDriveNameInput.Text });
        asyncTask.onStartTask_RunMainThread += (callerWindow, startParams) => { };
        asyncTask.onExecuteTask_RunBackground += (callerWindow, startParams, threadTools) =>
        {
            //Get the needed params
            string rootPath = startParams[0];
            string user = startParams[1];
            string driveName = startParams[2];

            //Wait some time
            threadTools.MakeThreadSleep(1000);

            //Try to do the task
            try
            {
                //------------- START -------------//

                //Copy each file present in the drive
                foreach (FileInfo file in (new DirectoryInfo(("/media/" + user + "/" + driveName + "/Motoplay")).GetFiles()))
                    File.Copy(file.FullName, (rootPath + "/InstallFiles/" + Path.GetFileName(file.FullName)));

                //Copy each directory present in the drive
                foreach (DirectoryInfo dir in (new DirectoryInfo(("/media/" + user + "/" + driveName + "/Motoplay")).GetDirectories()))
                    CopyDirectory(dir.FullName, (rootPath + "/InstallFiles/" + dir.Name));

                //-------------- END --------------//

                //Wait some time
                threadTools.MakeThreadSleep(1000);

                //Return a success response
                return new string[] { "success" };
            }
            catch (Exception ex)
            {
                //Return a error response
                return new string[] { "error" };
            }

            //Finish the thread...
            return new string[] { "none" };
        };
        asyncTask.onDoneTask_RunMainThread += (callerWindow, backgroundResult) =>
        {
            //If was error
            if (backgroundResult[0] != "success")
            {
                MessageBoxManager.GetMessageBoxStandard("Error", "There was a problem copying files from the Flash Drive!", ButtonEnum.Ok).ShowAsync();
                this.Close();
                return;
            }

            //If was success
            if (backgroundResult[0] == "success")
                FinishInstallationUsingLoadedInstallFiles();
        };
        asyncTask.Execute(AsyncTaskSimplified.ExecutionMode.NewDefaultThread);
    }

    private void FinishInstallationUsingLoadedInstallFiles()
    {
        //Start the coroutine to finish the installation
        CoroutineHandler.Start(FinishInstallation());
    }

    private IEnumerator<Wait> FinishInstallation()
    {
        //Inform status
        doingTaskStatus.Text = "Stopping Processes of Motoplay";
        doingTasksBar.Maximum = 100;
        doingTasksBar.Value = 0;
        doingTasksBar.IsIndeterminate = true;

        //Wait time
        yield return new Wait(2.5f);

        //Send a command to stop possible process of running motoplay
        SendCommandToTerminalAndClearCurrentOutputLines("sudo pkill -f \"App/Motoplay.Desktop\"");
        //Wait the end of command execution
        while (isLastCommandFinishedExecution() == false)
            yield return new Wait(0.1f);

        //Wait time
        yield return new Wait(2.5f);

        //Inform status
        doingTaskStatus.Text = "Installing Motoplay App";

        //Wait time
        yield return new Wait(2.0f);

        //Send a command to delete the folder of motoplay app
        SendCommandToTerminalAndClearCurrentOutputLines("rm -r \"" + (motoplayRootPath + "/App") + "\"");
        //Wait the end of command execution
        while (isLastCommandFinishedExecution() == false)
            yield return new Wait(0.1f);

        //Send a command to copy install files as a new final folder
        SendCommandToTerminalAndClearCurrentOutputLines("cp -r \"" + (motoplayRootPath + "/InstallFiles") + "\" \"" + (motoplayRootPath + "/App") + "\"");
        //Wait the end of command execution
        while (isLastCommandFinishedExecution() == false)
            yield return new Wait(0.1f);

        //Inform status
        doingTaskStatus.Text = "Preparing Motoplay App";

        //Wait time
        yield return new Wait(1.0f);

        //Send a command to make the motoplay app, executable
        SendCommandToTerminalAndClearCurrentOutputLines("chmod +x \"" + (motoplayRootPath + "/App/Motoplay.Desktop") + "\"");
        //Wait the end of command execution
        while (isLastCommandFinishedExecution() == false)
            yield return new Wait(0.1f);



        //If already done post-install tasks, start motoplay
        if (File.Exists((motoplayRootPath + "/PersistentData/done-post-install-tasks.ok")) == true)
            CoroutineHandler.Start(FinishAndStartMotoplay());

        //If NEVER done post-install tasks, do it now
        if (File.Exists((motoplayRootPath + "/PersistentData/done-post-install-tasks.ok")) == false)
            CoroutineHandler.Start(DoPostInstallTasks());
    }

    private IEnumerator<Wait> DoPostInstallTasks()
    {
        //Inform status
        doingTaskStatus.Text = "Finishing Installation";

        //Wait time
        yield return new Wait(5.0f);

        //Create a .desktop file for the installer
        StringBuilder motoplayInstallerDskContent = new StringBuilder();
        motoplayInstallerDskContent.AppendLine("[Desktop Entry]");
        motoplayInstallerDskContent.AppendLine("Encoding=UTF-8");
        motoplayInstallerDskContent.AppendLine("Name=Motoplay Installer");
        motoplayInstallerDskContent.AppendLine("GenericName=motoplay,installer");
        motoplayInstallerDskContent.AppendLine("Categories=Other");
        motoplayInstallerDskContent.AppendLine("Comment=The Motoplay App installer or updater. Install, re-install or update the Motoplay by Internet or Flash Drive!");
        motoplayInstallerDskContent.AppendLine("Type=Application");
        motoplayInstallerDskContent.AppendLine("Terminal=false");
        motoplayInstallerDskContent.AppendLine(("Exec=" + motoplayRootPath + "/Installer/InstallerMotoplay.Desktop"));
        motoplayInstallerDskContent.AppendLine(("Icon=" + motoplayRootPath + "/Installer/Assets/motoplay-installer-logo-redistributable.png"));
        motoplayInstallerDskContent.AppendLine("StartupWMClass=InstallerMotoplay.Desktop");
        File.WriteAllText(("/home/" + systemCurrentUsername + "/.local/share/applications/Motoplay Installer.desktop"), motoplayInstallerDskContent.ToString());

        //Send a command to make the .desktop file of installer, executable
        SendCommandToTerminalAndClearCurrentOutputLines("chmod +x \"" + ("/home/" + systemCurrentUsername + "/.local/share/applications/Motoplay Installer.desktop") + "\"");
        //Wait the end of command execution
        while (isLastCommandFinishedExecution() == false)
            yield return new Wait(0.1f);

        //Wait time
        yield return new Wait(1.0f);

        //Create a .desktop file for the installer
        StringBuilder motoplayDskContent = new StringBuilder();
        motoplayDskContent.AppendLine("[Desktop Entry]");
        motoplayDskContent.AppendLine("Encoding=UTF-8");
        motoplayDskContent.AppendLine("Name=Motoplay");
        motoplayDskContent.AppendLine("GenericName=motoplay");
        motoplayDskContent.AppendLine("Categories=Other");
        motoplayDskContent.AppendLine("Comment=The Motoplay App!");
        motoplayDskContent.AppendLine("Type=Application");
        motoplayDskContent.AppendLine("Terminal=false");
        motoplayDskContent.AppendLine(("Exec=" + motoplayRootPath + "/App/Motoplay.Desktop"));
        motoplayDskContent.AppendLine(("Icon=" + motoplayRootPath + "/App/Assets/motoplay-logo-redistributable.png"));
        motoplayDskContent.AppendLine("StartupWMClass=Motoplay.Desktop");
        File.WriteAllText(("/home/" + systemCurrentUsername + "/.local/share/applications/Motoplay.desktop"), motoplayDskContent.ToString());

        //Send a command to make the .desktop file, executable
        SendCommandToTerminalAndClearCurrentOutputLines("chmod +x \"" + ("/home/" + systemCurrentUsername + "/.local/share/applications/Motoplay.desktop") + "\"");
        //Wait the end of command execution
        while (isLastCommandFinishedExecution() == false)
            yield return new Wait(0.1f);

        //Wait time
        yield return new Wait(1.0f);

        //Create a .desktop file for the keyboard
        StringBuilder motoplayKbdContent = new StringBuilder();
        motoplayKbdContent.AppendLine("[Desktop Entry]");
        motoplayKbdContent.AppendLine("Encoding=UTF-8");
        motoplayKbdContent.AppendLine("Name=Motoplay Virtual Keyboard");
        motoplayKbdContent.AppendLine("GenericName=keyboard");
        motoplayKbdContent.AppendLine("Categories=Other");
        motoplayKbdContent.AppendLine("Comment=The Virtual Keyboard of Motoplay App!");
        motoplayKbdContent.AppendLine("Type=Application");
        motoplayKbdContent.AppendLine("Terminal=false");
        motoplayKbdContent.AppendLine(("Exec=" + motoplayRootPath + "/Keyboard/wvkbd-mobintl"));
        motoplayKbdContent.AppendLine(("Icon=" + motoplayRootPath + "/App/Assets/motoplay-logo-redistributable.png"));
        motoplayKbdContent.AppendLine("StartupWMClass=wvkbd-mobintl");
        File.WriteAllText(("/home/" + systemCurrentUsername + "/.local/share/applications/Motoplay Virtual Keyboard.desktop"), motoplayKbdContent.ToString());

        //Send a command to make the .desktop file, executable
        SendCommandToTerminalAndClearCurrentOutputLines("chmod +x \"" + ("/home/" + systemCurrentUsername + "/.local/share/applications/Motoplay Virtual Keyboard.desktop") + "\"");
        //Wait the end of command execution
        while (isLastCommandFinishedExecution() == false)
            yield return new Wait(0.1f);

        //Wait time
        yield return new Wait(1.0f);

        //Send a command to update the desktop files database
        SendCommandToTerminalAndClearCurrentOutputLines("sudo update-desktop-database");
        //Wait the end of command execution
        while (isLastCommandFinishedExecution() == false)
            yield return new Wait(0.1f);

        //Wait time
        yield return new Wait(1.0f);

        //Inform status
        doingTaskStatus.Text = "Configuring Automatic Start";

        //Wait time
        yield return new Wait(1.0f);

        //Read the Wayland desktop Composer config file
        string waylandDesktopComposerCfg = File.ReadAllText(("/home/" + systemCurrentUsername + "/.config/wayfire.ini"));
        //If don't have the auto start configured, add it
        if (waylandDesktopComposerCfg.Contains("motoplay") == false)
        {
            waylandDesktopComposerCfg += ("\n\n[autostart]\nmotoplay = " + motoplayRootPath + "/App/Motoplay.Desktop");
            File.WriteAllText(("/home/" + systemCurrentUsername + "/.config/wayfire.ini"), waylandDesktopComposerCfg);
        }

        //Wait time
        yield return new Wait(1.0f);



        //Create the file of sinal of post tasks done
        File.WriteAllText((motoplayRootPath + "/PersistentData/done-post-install-tasks.ok"), "Ok");

        //Start the motoplay app
        CoroutineHandler.Start(FinishAndStartMotoplay());
    }

    private IEnumerator<Wait> FinishAndStartMotoplay()
    {
        //Inform status
        doingTaskStatus.Text = "Starting Motoplay App";

        //Wait time
        yield return new Wait(2.5f);

        //Send a command to open the motoplay
        SendCommandToTerminalAndClearCurrentOutputLines(("\"" + motoplayRootPath + "/App/Motoplay.Desktop\" & echo \"> ContinueInOtherThread\""));
        //Wait the end of command execution
        while (isLastCommandFinishedExecution() == false)
            yield return new Wait(0.1f);

        //Close this installer
        this.Close();
    }

    //Keyboard methods

    private void ToggleVirtualKeyboard()
    {
        //If is keyboard not open
        if (wvkbdProcess == null)
        {
            //Build the string of keyboard parameters
            StringBuilder keyboardParams = new StringBuilder();
            keyboardParams.Append(("-L " + (int)((float)Screens.Primary.Bounds.Height * 0.45f)));
            keyboardParams.Append(" --fn \"Segoe UI 16\"");
            keyboardParams.Append(" --bg 000000AA");
            keyboardParams.Append(" --fg 323333F0");
            keyboardParams.Append(" --fg-sp 141414F0");
            keyboardParams.Append(" --press 00AEFFFF");
            keyboardParams.Append(" --press-sp 00AEFFFF");
            keyboardParams.Append(" --text FFFFFFFF");
            keyboardParams.Append(" --text-sp FFFFFFFF");

            //Start the wvkbd process
            wvkbdProcess = new Process() { StartInfo = new ProcessStartInfo() { FileName = (motoplayRootPath + "/Keyboard/wvkbd-mobintl"), Arguments = keyboardParams.ToString() } };
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

    private void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        DirectoryInfo diSource = new DirectoryInfo(sourceDirectory);
        DirectoryInfo diTarget = new DirectoryInfo(targetDirectory);

        CopyDirectoryAll(diSource, diTarget);
    }

    private void CopyDirectoryAll(DirectoryInfo source, DirectoryInfo target)
    {
        Directory.CreateDirectory(target.FullName);

        // Copy each file into the new directory.
        foreach (FileInfo fi in source.GetFiles())
            fi.CopyTo(Path.Combine(target.FullName, fi.Name), true);

        // Copy each subdirectory using recursion.
        foreach (DirectoryInfo diSourceSubDir in source.GetDirectories())
        {
            DirectoryInfo nextTargetSubDir = target.CreateSubdirectory(diSourceSubDir.Name);
            CopyDirectoryAll(diSourceSubDir, nextTargetSubDir);
        }
    }
}