using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Motoplay.Views;

namespace Motoplay;

/*
 * This script is resposible by the work of the "BluetoothDeviceItem" that represents
 * Bluetooth Devices found
*/

public partial class BluetoothDeviceItem : UserControl
{
    //Classes of script
    public class ClassDelegates
    {
        public delegate void OnClick(BluetoothDeviceItem btDeviceInfo);
    }

    //Private variables
    private event ClassDelegates.OnClick onClick = null;

    //Public variables
    public MainWindow instantiatedBy = null;

    //Core methods

    public BluetoothDeviceItem()
    {
        //Initialize the window normally on Avalonia side
        InitializeComponent();
    }

    public BluetoothDeviceItem(MainWindow instantiatedBy)
    {
        //Initialize the window normally on Avalonia side
        InitializeComponent();

        //Store reference of instantiation
        this.instantiatedBy = instantiatedBy;
    }

    public void SetDeviceName(string name)
    {
        //Defines the bluetooth device name
        btDeviceName.Text = name;
    }

    public void SetDeviceMAC(string mac)
    {
        //Defines the bluetooth device name
        btDeviceMac.Text = mac;
    }

    public void RegisterOnClickCallback(ClassDelegates.OnClick onClick)
    {
        //Register the event
        this.onClick = onClick;

        //Register the on click event
        rootElement.PointerPressed += (s, e) => { this.onClick(this); };
    }
}