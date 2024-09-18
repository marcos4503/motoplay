﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Coroutine;
using MarcosTomaz.ATS;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System;
using System.Collections;
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
    private Process terminalCliProcess = null;
    private List<string> terminalReceivedOutputLines = new List<string>();
    private string currentTerminalCliRentKey = "";
    private Dictionary<string, string> runningTasks = new Dictionary<string, string>();
    private Process wvkbdProcess = null;
    private KeyFeedbackWindow wvkbdFeedbackWindow = null;
    private ActiveCoroutine wvkbdFeedbackRoutine = null;
    private ActiveCoroutine openKeyboardTipRoutine = null;

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

        //Recover the version of the application
        System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
        FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
        applicationVersion = fvi.FileVersion;

        //Load the correct language resource file for the app
        this.Resources.MergedDictionaries.Clear();
        string resourceDictionaryUri = "";
        switch ("en-us")
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
            keyboardParams.Append(("-L " + (int)((float)Screens.Primary.Bounds.Height * 0.45f)));
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

    //...
}