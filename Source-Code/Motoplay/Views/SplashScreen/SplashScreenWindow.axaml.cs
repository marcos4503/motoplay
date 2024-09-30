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
        //Show start message
        progressMsgText.Text = "Loading Motoplay App";

        //Recover the version of the application
        System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
        FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
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
        int loadingTimeSecs = 1;

        //Start a timer of progress
        while ((currentTime - startTime) <= (TimeSpan.TicksPerSecond * loadingTimeSecs))
        {
            //Update the current time
            currentTime = DateTime.Now.Ticks;

            //Update elapsed time
            elapsedTime = (currentTime - startTime);

            //Run on Main Thread
            Dispatcher.UIThread.Post(() => 
            {
                //Update the progressbar
                startProgressBar.Value = (int)((new TimeSpan(elapsedTime).TotalMilliseconds / (float)(loadingTimeSecs * 1000)) * 100.0f);
            }, DispatcherPriority.Render);

            //Wait time, equivalent to 30 FPS
            await Task.Delay(33);
        }

        //Show final start message
        progressMsgText.Text = "Initializing";

        //Wait final time
        await Task.Delay(1000);
    }
}