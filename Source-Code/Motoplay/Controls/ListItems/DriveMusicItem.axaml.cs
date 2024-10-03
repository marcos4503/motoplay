using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Motoplay.Views;
using System.IO;

namespace Motoplay;

/*
 * This script is resposible by the work of the "DriveMusicItem" that represents
 * musics files on External Drivers
*/

public partial class DriveMusicItem : UserControl
{
    //Classes of script
    public class ClassDelegates
    {
        public delegate void OnTransfer(string fileFullName);
    }

    //Private variables
    private event ClassDelegates.OnTransfer onTransfer = null;

    //Public variables
    public MainWindow instantiatedBy = null;
    public string musicFullPath = "";

    //Core methods

    public DriveMusicItem()
    {
        //Initialize the window normally on Avalonia side
        InitializeComponent();
    }

    public DriveMusicItem(MainWindow instantiatedBy)
    {
        //Initialize the window normally on Avalonia side
        InitializeComponent();

        //Store reference of instantiation
        this.instantiatedBy = instantiatedBy;
    }

    public void SetMusicPath(string musicPath)
    {
        //Show the file name
        musicNameText.Text = Path.GetFileName(musicPath);

        //Save the full path
        musicFullPath = musicPath;
    }

    public void SetDriveName(string driveName)
    {
        //Show the drive name
        driveNameText.Text = driveName.ToUpper();
    }

    public void RegisterOnTransferCallback(ClassDelegates.OnTransfer onTransfer)
    {
        //Register the event
        this.onTransfer = onTransfer;

        //Register the on click event
        musicTransferButton.Click += (s, e) => {
            //Disable the button
            musicTransferButton.IsEnabled = false;

            //Do the callback
            this.onTransfer(musicFullPath);
        };
    }

    //Public methods

    public void Setup()
    {
        //Set the callback to enable the delete button on click on music
        rootElement.PointerPressed += (s, e) => { musicTransferButton.IsVisible = !musicTransferButton.IsVisible; };

        //Disable the transfer button
        musicTransferButton.IsVisible = false;
    }

    public void SetEnabled(bool enabled)
    {
        //If is enabled
        if (enabled == true)
        {
            musicTransferButtonBg.IsVisible = true;
            musicTransferButton.IsEnabled = true;
            rootElement.IsHitTestVisible = true;
            cardElement.Background = new SolidColorBrush(new Color(255, 255, 255, 255));
            contentElement.Background = new SolidColorBrush(new Color(255, 255, 255, 255));
        }

        //If disabled
        if (enabled == false)
        {
            musicTransferButtonBg.IsVisible = false;
            musicTransferButton.IsVisible = false;
            musicTransferButton.IsEnabled = true;
            rootElement.IsHitTestVisible = false;
            cardElement.Background = new SolidColorBrush(new Color(255, 204, 204, 204));
            contentElement.Background = new SolidColorBrush(new Color(255, 204, 204, 204));
        }
    }
}