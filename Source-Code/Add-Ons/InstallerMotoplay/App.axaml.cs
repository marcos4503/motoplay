using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Coroutine;
using InstallerMotoplay.ViewModels;
using InstallerMotoplay.Views;
using System;
using System.Diagnostics;
using System.Timers;

namespace InstallerMotoplay;

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

    public override void OnFrameworkInitializationCompleted()
    {
        // Line below is needed to remove Avalonia data validation.
        // Without this line you will get duplicate validations from both Avalonia and CT
        BindingPlugins.DataValidators.RemoveAt(0);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow(desktop.Args) { DataContext = null };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            //...
        }

        base.OnFrameworkInitializationCompleted();



        //Prepare a new application thread for update loop of Coroutines
        coroutinesLoop_Timer = new Timer(33);      //<- Targeting 30 FPS
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