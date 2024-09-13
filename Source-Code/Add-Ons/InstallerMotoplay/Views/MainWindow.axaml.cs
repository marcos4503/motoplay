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
    private string applicationVersion = "";
    private string[] cliArgs = null;
    private string systemTargetUsername = "";
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
        Mutex mutex = new Mutex(true, "InstallerMotoplay.Desktop", out created);

        //If already have a instance of the app running, cancel
        if(created == false)
        {
            //Warn and stop here
            MessageBoxManager.GetMessageBoxStandard("Error", "Motoplay Installer is already running!", ButtonEnum.Ok).ShowAsync();
            this.Close();
            return;
        }

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

        //Save the CLI arguments
        this.cliArgs = cliArgs;

        //If is Linux...
        if (OperatingSystem.IsLinux() == true)
        {
            //Find the active user name
            systemTargetUsername = Directory.GetCurrentDirectory().Replace("/home/", "").Split("/")[0];
            //Build the root path for motoplay
            motoplayRootPath = (@"/home/" + systemTargetUsername + "/Motoplay");
            //Create the root folder if not exists
            if (Directory.Exists(motoplayRootPath) == false)
                Directory.CreateDirectory(motoplayRootPath);
        }
        //If is Windows...
        if (OperatingSystem.IsWindows() == true)
        {
            //Find the active user name
            systemTargetUsername = "dummy";
            //Build the root path for motoplay
            motoplayRootPath = (@"C:\Motoplay");
            //Create the root folder if not exists
            if (Directory.Exists(motoplayRootPath) == false)
                Directory.CreateDirectory(motoplayRootPath);
        }

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

        //Show the application version at the bottom
        versionDisplay.Text = applicationVersion;

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
                SendCommandToTerminalAndClearCurrentOutputLines("sudo apt-get install wvkbd -y");
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
                    var diag = MessageBoxManager.GetMessageBoxStandard("Error", "There was an error installing the Package. Please check your connection and try again!", ButtonEnum.Ok).ShowAsync();
                    while (diag.IsCompleted == false)
                        yield return new Wait(0.1f);
                    this.Close();
                }
            }

            //Enable the keyboard button
            toggleKeyboardButton.IsVisible = true;
        }

        //If Windows, just continue...
        if (OperatingSystem.IsWindows() == true)
            Debug.WriteLine("The tool \"wvkbd\" is not necessary on Windows.");

        //Start the process of installation of "p7zip-full" tool, if needed
        CoroutineHandler.Start(InstallP7ZipFullIfNeeded());
    }

    private IEnumerator<Wait> InstallP7ZipFullIfNeeded()
    {
        //If Linux, continue...
        if (OperatingSystem.IsLinux() == true)
        {
            //Inform that is checking
            doingTaskStatus.Text = "Checking installation of Compactor \"p7zip-full\"";

            //Wait time
            yield return new Wait(3.0f);

            //Send a command to check if the "p7zip-full" is installed
            SendCommandToTerminalAndClearCurrentOutputLines("sudo dpkg -s p7zip-full");
            //Wait the end of command execution
            while (isLastCommandFinishedExecution() == false)
                yield return new Wait(0.1f);

            //If not installed, start installation
            if (isThermFoundInTerminalOutputLines("is not installed") == true) 
            {
                //Inform that is installing
                doingTaskStatus.Text = "Installing Compactor \"p7zip-full\"";

                //Wait time
                yield return new Wait(5.0f);

                //Send a command to install the "p7zip-full"
                SendCommandToTerminalAndClearCurrentOutputLines("sudo apt-get install p7zip-full -y");
                //Wait the end of command execution
                while (isLastCommandFinishedExecution() == false)
                    yield return new Wait(0.1f);

                //Inform that is checking
                doingTaskStatus.Text = "Checking installation of Compactor \"p7zip-full\"";

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
            Debug.WriteLine("The tool \"p7zip-full\" is not necessary on Windows.");

        //Continue to the installer
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
        if (cliArgs != null)
            if (cliArgs.Length >= 1)
                if (cliArgs[0].Contains("online") == true)
                {
                    GetFilesFromOnline();
                    return;
                }

        //Change the screens
        doingTasksRoot.IsVisible = false;
        updateMenu.IsVisible = true;
        if (File.Exists((motoplayRootPath + "/PersistentData/last-fd-name.txt")) == true)
            flashDriveNameInput.Text = File.ReadAllText((motoplayRootPath + "/PersistentData/last-fd-name.txt"));
        installFlashdriveButtonHelp.Tapped += (s, e) => { MessageBoxManager.GetMessageBoxStandard("Tip", "To install or update Motoplay App from a Flash Drive...\n\n1. Create a folder called \"Motoplay\" inside the Flash Drive.\n2. Place all the Motoplay files inside this folder created on the Flash Drive.\n3. Connect the Flash Drive to the Raspberry Pi.\n4. Enter the name of the Flash Drive in the Text Box below.\n5. Click on \"Flash Drive\" to start the update, using the files on the Flash Drive, instead of the files found Online, in the Motoplay repository.\n\nOnce this is done, just wait for the update, Motoplay will be updated normally and will start.", ButtonEnum.Ok).ShowAsync(); };

        //Setup the buttons
        installOnlineButton.Click += (s, e) => { GetFilesFromOnline(); };
        installFlashdriveButton.Click += (s, e) => { GetFilesFromFlashDrive(); };
    }

    private void GetFilesFromOnline()
    {
        //Start the coroutine to download the current installation files of online repository
        CoroutineHandler.Start(DownloadFilesIndexOfRepository());
    }

    private IEnumerator<Wait> DownloadFilesIndexOfRepository() 
    {
        //Change the screen
        doingTasksRoot.IsVisible = true;
        updateMenu.IsVisible = false;

        //Inform status
        doingTaskStatus.Text = "Downloading files index for Motoplay App";

        //Wait time
        yield return new Wait(1.0f);

        //Start a new thread to copy the files
        AsyncTaskSimplified asyncTask = new AsyncTaskSimplified(null, new string[] { motoplayRootPath });
        asyncTask.onStartTask_RunMainThread += (callerWindow, startParams) => { };
        asyncTask.onExecuteTask_RunBackground += (callerWindow, startParams, threadTools) =>
        {
            //Get the needed params
            string rootPath = startParams[0];

            //Wait some time
            threadTools.MakeThreadSleep(1000);

            //Try to do the task
            try
            {
                //------------- START -------------//

                //If the file already exists, delete it
                if (File.Exists((rootPath + "/PersistentData/app-repository-info.json")) == true)
                    File.Delete((rootPath + "/PersistentData/app-repository-info.json"));

                //Prepare the target download URL
                string downloadUrl = @"https://marcos4503.github.io/motoplay/Repository-Pages/motoplay-data-info.json";
                string saveAsPath = (rootPath + "/PersistentData/app-repository-info.json");
                //Download the "Mods" folder sync
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
                MessageBoxManager.GetMessageBoxStandard("Error", "There was a problem during installation. Please check your internet connection!", ButtonEnum.Ok).ShowAsync();
                this.Close();
                return;
            }

            //If was success
            if (backgroundResult[0] == "success")
                CoroutineHandler.Start(DownloadMotoplayFilesOfRepository());
        };
        asyncTask.Execute(AsyncTaskSimplified.ExecutionMode.NewDefaultThread);
    }

    private IEnumerator<Wait> DownloadMotoplayFilesOfRepository()
    {
        //Change the screen
        doingTasksRoot.IsVisible = true;
        updateMenu.IsVisible = false;

        //Inform status
        doingTaskStatus.Text = "Downloading Motoplay App";

        //Change progress bar to finite
        doingTasksBar.Maximum = 100;
        doingTasksBar.Value = 0;
        doingTasksBar.IsIndeterminate = false;

        //Wait time
        yield return new Wait(1.0f);

        //Start a new thread to download the files
        AsyncTaskSimplified asyncTask = new AsyncTaskSimplified(null, new string[] { motoplayRootPath });
        asyncTask.onStartTask_RunMainThread += (callerWindow, startParams) => { };
        asyncTask.onExecuteTask_RunBackground += (callerWindow, startParams, threadTools) =>
        {
            //Get the needed params
            string rootPath = startParams[0];

            //Wait some time
            threadTools.MakeThreadSleep(1000);

            //Try to do the task
            try
            {
                //------------- START -------------//

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
                        //Report the progress
                        threadTools.ReportNewProgress((((100 * i) + (int)(e.ProgressPercentage)).ToString() + ":" + (100 * appInfo.loadedData.downloadPartsLinks.Length).ToString()));
                    };
                    client.DownloadFileAsync(new Uri(appInfo.loadedData.downloadPartsLinks[i]), ((rootPath + "/DownloadedFiles/" + appInfo.loadedData.downloadPartsLinks[i].Split("/").Last())));

                    //Wait until this download finish
                    while (client.IsBusy == true)
                        threadTools.MakeThreadSleep(33);
                }

                //-------------- END --------------//

                //Wait some time
                threadTools.MakeThreadSleep(1000);

                //Return a success response
                return new string[] { "success", ((rootPath + "/DownloadedFiles/" + appInfo.loadedData.downloadPartsLinks[0].Split("/").Last())) };
            }
            catch (Exception ex)
            {
                //Return a error response
                return new string[] { "error", "" };
            }

            //Finish the thread...
            return new string[] { "none", "" };
        };
        asyncTask.onNewProgress_RunMainThread += (callerWindow, newProgress) =>
        {
            //Get the two numbers of progress
            string[] progressParts = newProgress.Split(":");

            //Report the new progress
            doingTasksBar.Maximum = int.Parse(progressParts[1]);
            doingTasksBar.Value = int.Parse(progressParts[0]);
            doingTasksBar.IsIndeterminate = false;
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
                CoroutineHandler.Start(ExpandDownloadedMotoplayFilesOfRepository(backgroundResult[1]));
        };
        asyncTask.Execute(AsyncTaskSimplified.ExecutionMode.NewDefaultThread);
    }

    private IEnumerator<Wait> ExpandDownloadedMotoplayFilesOfRepository(string zipFilePath) 
    {
        //Change the screen
        doingTasksRoot.IsVisible = true;
        updateMenu.IsVisible = false;

        //Inform status
        doingTaskStatus.Text = "Expanding downloaded Files";

        //Change progress bar to finite
        doingTasksBar.Maximum = 100;
        doingTasksBar.Value = 50;
        doingTasksBar.IsIndeterminate = true;

        //Wait time
        yield return new Wait(1.0f);

        //Start a new thread to extract the files
        AsyncTaskSimplified asyncTask = new AsyncTaskSimplified(null, new string[] { motoplayRootPath, zipFilePath });
        asyncTask.onStartTask_RunMainThread += (callerWindow, startParams) => { };
        asyncTask.onExecuteTask_RunBackground += (callerWindow, startParams, threadTools) =>
        {
            //Get the needed params
            string rootPath = startParams[0];
            string fileToExpand = startParams[1];

            //Wait some time
            threadTools.MakeThreadSleep(1000);

            //Try to do the task
            try
            {
                //------------- START -------------//

                //Send a command to extract the downloaded file
                SendCommandToTerminalAndClearCurrentOutputLines(("7z x -y \"" + fileToExpand + "\" -o\"" + (rootPath + "/InstallFiles") + "\""));

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
        asyncTask.onDoneTask_RunMainThread += (callerWindow, backgroundResult) =>
        {
            //If was error
            if (backgroundResult[0] != "success")
            {
                MessageBoxManager.GetMessageBoxStandard("Error", "There was a problem during installation!", ButtonEnum.Ok).ShowAsync();
                this.Close();
                return;
            }

            //If was success
            if (backgroundResult[0] == "success")
                FinishInstallationUsingLoadedInstallFiles();
        };
        asyncTask.Execute(AsyncTaskSimplified.ExecutionMode.NewDefaultThread);
    }

    private void GetFilesFromFlashDrive()
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
        if (Directory.Exists(("/media/" + systemTargetUsername + "/" + flashDriveNameInput.Text)) == false)
        {
            MessageBoxManager.GetMessageBoxStandard("Error", "Flash Drive \"" + flashDriveNameInput.Text + "\" is not available!", ButtonEnum.Ok).ShowAsync();
            return;
        }
        //If the "Motoplay" folder not exists, cancel
        if (Directory.Exists(("/media/" + systemTargetUsername + "/" + flashDriveNameInput.Text + "/Motoplay")) == false)
        {
            MessageBoxManager.GetMessageBoxStandard("Error", "The \"Motoplay\" folder containing the installation files does not exist inside Flash Drive \"" + flashDriveNameInput.Text + "\"!", ButtonEnum.Ok).ShowAsync();
            return;
        }

        //Start the files load coroutine
        CoroutineHandler.Start(CopyInstallationFilesFromFlashDrive());
    }

    private IEnumerator<Wait> CopyInstallationFilesFromFlashDrive()
    {
        //Change the screen
        doingTasksRoot.IsVisible = true;
        updateMenu.IsVisible = false;

        //Inform status
        doingTaskStatus.Text = "Copying files from \"" + flashDriveNameInput.Text + "\" Drive";

        //Wait time
        yield return new Wait(1.0f);

        //Start a new thread to copy the files
        AsyncTaskSimplified asyncTask = new AsyncTaskSimplified(null, new string[] { motoplayRootPath, systemTargetUsername, flashDriveNameInput.Text });
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
        doingTaskStatus.Text = "Stopping processes of Motoplay";

        //Wait time
        yield return new Wait(5.0f);

        //Send a command to stop possible process of running motoplay
        SendCommandToTerminalAndClearCurrentOutputLines("sudo pkill -f \"App/Motoplay.Desktop\"");
        //Wait the end of command execution
        while (isLastCommandFinishedExecution() == false)
            yield return new Wait(0.1f);

        //Inform status
        doingTaskStatus.Text = "Installing Motoplay App";

        //Wait time
        yield return new Wait(5.0f);

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
        yield return new Wait(5.0f);

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
        File.WriteAllText(("/home/" + systemTargetUsername + "/.local/share/applications/Motoplay Installer.desktop"), motoplayInstallerDskContent.ToString());

        //Send a command to make the .desktop file of installer, executable
        SendCommandToTerminalAndClearCurrentOutputLines("chmod +x \"" + ("/home/" + systemTargetUsername + "/.local/share/applications/Motoplay Installer.desktop") + "\"");
        //Wait the end of command execution
        while (isLastCommandFinishedExecution() == false)
            yield return new Wait(0.1f);

        //Wait time
        yield return new Wait(5.0f);

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
        File.WriteAllText(("/home/" + systemTargetUsername + "/.local/share/applications/Motoplay.desktop"), motoplayDskContent.ToString());

        //Send a command to make the .desktop file, executable
        SendCommandToTerminalAndClearCurrentOutputLines("chmod +x \"" + ("/home/" + systemTargetUsername + "/.local/share/applications/Motoplay.desktop") + "\"");
        //Wait the end of command execution
        while (isLastCommandFinishedExecution() == false)
            yield return new Wait(0.1f);

        //Wait time
        yield return new Wait(5.0f);

        //Send a command to update the desktop files database
        SendCommandToTerminalAndClearCurrentOutputLines("sudo update-desktop-database");
        //Wait the end of command execution
        while (isLastCommandFinishedExecution() == false)
            yield return new Wait(0.1f);

        //Wait time
        yield return new Wait(5.0f);

        //Inform status
        doingTaskStatus.Text = "Configuring Automatic Start";

        //Wait time
        yield return new Wait(5.0f);

        //Read the Wayland desktop Composer config file
        string waylandDesktopComposerCfg = File.ReadAllText(("/home/" + systemTargetUsername + "/.config/wayfire.ini"));
        //If don't have the auto start configured, add it
        if (waylandDesktopComposerCfg.Contains("motoplay") == false)
        {
            waylandDesktopComposerCfg += ("\n\n[autostart]\nmotoplay = " + motoplayRootPath + "/App/Motoplay.Desktop");
            File.WriteAllText(("/home/" + systemTargetUsername + "/.config/wayfire.ini"), waylandDesktopComposerCfg);
        }

        //Wait time
        yield return new Wait(5.0f);



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
        yield return new Wait(5.0f);

        //Send a command to open the motoplay
        SendCommandToTerminalAndClearCurrentOutputLines((motoplayRootPath + "/App/Motoplay.Desktop"));

        //Close this installer
        this.Close();
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