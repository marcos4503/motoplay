using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Coroutine;
using Motoplay.ViewModels;
using Motoplay.Views;
using System;
using System.Timers;

namespace Motoplay;

public partial class App : Application
{
    //Cache variables
    private Timer coroutinesLoop_Timer = null;
    private DateTime coroutinesLoop_lastTime = DateTime.Now;
    private DateTime coroutinesLoop_currentTime = DateTime.Now;

    //Core methods

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        // Line below is needed to remove Avalonia data validation.
        // Without this line you will get duplicate validations from both Avalonia and CT
        BindingPlugins.DataValidators.RemoveAt(0);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            //Start a Splash Screen
            SplashScreenWindow splashScreen = new SplashScreenWindow();
            splashScreen.Show();

            //Wait the Splash Screen presentation
            await splashScreen.InitializeApp();

            //Prepare the Main Window
            MainWindow mainWindow = new MainWindow(desktop.Args) { DataContext = null };

            //Start and show the Main Window
            desktop.MainWindow = mainWindow;
            mainWindow.Show();

            //Close the Splash Screen after Main Window shows
            splashScreen.Close();

            //============= OLD INITIALIZATION CODE  WITHOUT SPLASH SCREEN =============//
            //desktop.MainWindow = new MainWindow { DataContext = null };
            //==========================================================================//
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            //...
        }

        base.OnFrameworkInitializationCompleted();



        //Prepare a new application thread for update loop of Coroutines
        coroutinesLoop_Timer = new Timer(22);      //<- Targeting 45 FPS
        coroutinesLoop_Timer.Elapsed += (s, e) => {
            //Get current time
            coroutinesLoop_currentTime = DateTime.Now;

            //Do Coroutines update tick, in Main Thread
            //Dispatcher.UIThread.Post(() => { CoroutineHandler.Tick(coroutinesLoop_currentTime - coroutinesLoop_lastTime); }, DispatcherPriority.MaxValue);
            Dispatcher.UIThread.Invoke(() => { CoroutineHandler.Tick(coroutinesLoop_currentTime - coroutinesLoop_lastTime); }, DispatcherPriority.MaxValue);

            //Update the last time
            coroutinesLoop_lastTime = coroutinesLoop_currentTime;
        };
        coroutinesLoop_Timer.Start();
        //Inform the initial time for the Coroutines update loop
        coroutinesLoop_lastTime = DateTime.Now;
    }
}
