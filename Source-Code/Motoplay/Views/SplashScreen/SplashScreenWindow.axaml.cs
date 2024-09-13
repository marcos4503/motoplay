using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using System;
using System.Threading.Tasks;
using AsyncImageLoader;
using AsyncImageLoader.Loaders;
using Avalonia.Animation;
using System.Diagnostics;

namespace Motoplay;

/*
* This is the code responsible by the Splash Screen of the App
*/

public partial class SplashScreenWindow : Window
{
    //Cache variables
    private string appVersion = "";

    //Core methods

    public SplashScreenWindow()
    {
        //Initialize this Window normally
        InitializeComponent();
    }

    public async Task InitializeApp()
    {
        //Show final start message
        progressMsgText.Text = "Loading Motoplay";

        //Recover the version of the application
        System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
        System.Diagnostics.FileVersionInfo fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
        if (OperatingSystem.IsWindows() == true)
        {
            appVersion = fvi.FileVersion.Remove(fvi.FileVersion.Length - 1, 1);
            appVersion = appVersion.Remove(appVersion.Length - 1, 1);
        }
        if (OperatingSystem.IsLinux() == true)
            appVersion = fvi.FileVersion;
        //Show the app version
        appVersionDisplay.Text = appVersion;

        //Prepare the UI
        backgroundImageContainer.Background = null;
        logoImageContainer.Background = null;
        backgroundImage.Source = "avares://Motoplay/Assets/splash-background.png";
        logoImage.Source = "avares://Motoplay/Assets/splash-logo.png";
        ((Animation)this.Resources["topRectEntry"]).RunAsync(topRectFx);
        ((Animation)this.Resources["rightRectEntry"]).RunAsync(rightRectFx);

        //Prepare the timer data
        long startTime = DateTime.Now.Ticks;
        long currentTime = startTime;
        long elapsedTime = 0;

        //Start a timer of progress
        while ((currentTime - startTime) <= (TimeSpan.TicksPerSecond * 5))
        {
            //Update the current time
            currentTime = DateTime.Now.Ticks;

            //Update elapsed time
            elapsedTime = (currentTime - startTime);

            //Run on Main Thread
            Dispatcher.UIThread.Post(() => 
            {
                //Update the progressbar
                startProgressBar.Value = (int)((new TimeSpan(elapsedTime).TotalMilliseconds / 5000.0f) * 100.0f);
            }, DispatcherPriority.Render);

            //Wait time, equivalent to 30 FPS
            await Task.Delay(33);
        }

        //Show final start message
        progressMsgText.Text = "Starting Motoplay";

        //Wait final time
        await Task.Delay(1000);
    }
}