using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Motoplay.Views;

namespace Motoplay;

/*
 * This script is resposible by the work of the "BluetoothSoundItem" that represents
 * a paired Bluetooth Device
*/

public partial class BluetoothSoundItem : UserControl
{
    //Classes of script
    public class ClassDelegates
    {
        public delegate void OnTryConnect(string deviceMac);
    }

    //Private variables
    private event ClassDelegates.OnTryConnect onTryConnect = null;

    //Public variables
    public MainWindow instantiatedBy = null;
    public string deviceMac = "";

    //Core methods

    public BluetoothSoundItem()
    {
        //Initialize the window normally on Avalonia side
        InitializeComponent();
    }

    public BluetoothSoundItem(MainWindow instantiatedBy)
    {
        //Initialize the window normally on Avalonia side
        InitializeComponent();

        //Store reference of instantiation
        this.instantiatedBy = instantiatedBy;
    }

    public void SetDeviceName(string deviceName)
    {
        //Show the device name
        deviceNameText.Text = deviceName;
    }

    public void SetDeviceMac(string deviceMac)
    {
        //Save the information
        this.deviceMac = deviceMac;
    }

    public void RegisterOnTryConnectCallback(ClassDelegates.OnTryConnect onTryConnect)
    {
        //Register the event
        this.onTryConnect = onTryConnect;

        //Register the on click event
        connectButton.Click += (s, e) => {
            //Disable the button
            connectButton.IsEnabled = false;

            //Do the callback
            this.onTryConnect(deviceMac);
        };
    }
}