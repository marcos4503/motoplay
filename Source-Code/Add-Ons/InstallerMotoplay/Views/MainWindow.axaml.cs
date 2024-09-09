using Avalonia.Controls;
using System;
using System.Diagnostics;
using System.Text;

namespace InstallerMotoplay.Views;

/*
* This is the code responsible by the Motoplay Installer Window
*/

public partial class MainWindow : Window
{
    //Private variables
    //...

    //Core methods

    public MainWindow()
    {
        //Initialize the window normally on Avalonia side
        InitializeComponent();
    }

    public MainWindow(string[]? cliArgs) : this()
    {
        //Start the real initialization of window, receiving all CLI arguments
        StartThisWindow(cliArgs);
    }

    public void StartThisWindow(string[]? cliArgs)
    {
        //Log the parameters found to console
        StringBuilder paramsStr = new StringBuilder();
        foreach(string item in cliArgs)
            paramsStr.Append(" " + item);
        Debug.WriteLine("Params Found:" + paramsStr.ToString() + ".");

        //...
    }
}