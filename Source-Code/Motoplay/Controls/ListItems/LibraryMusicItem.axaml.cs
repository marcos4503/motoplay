using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Motoplay.Views;

namespace Motoplay;

/*
 * This script is resposible by the work of the "LibraryMusicItem" that represents
 * musics files on Music Player library
*/

public partial class LibraryMusicItem : UserControl
{
    //Classes of script
    public class ClassDelegates
    {
        public delegate void OnDelete(string fileName);
    }

    //Private variables
    private event ClassDelegates.OnDelete onDelete = null;

    //Public variables
    public MainWindow instantiatedBy = null;

    //Core methods

    public LibraryMusicItem()
    {
        //Initialize the window normally on Avalonia side
        InitializeComponent();
    }

    public LibraryMusicItem(MainWindow instantiatedBy)
    {
        //Initialize the window normally on Avalonia side
        InitializeComponent();

        //Store reference of instantiation
        this.instantiatedBy = instantiatedBy;
    }

    public void SetMusicFileName(string fileName)
    {
        //Show the file name
        musicNameText.Text = fileName;
    }

    public void RegisterOnDeleteCallback(ClassDelegates.OnDelete onDelete)
    {
        //Register the event
        this.onDelete = onDelete;

        //Register the on click event
        musicDeleteButton.Click += (s, e) => {
            //Disable the button
            musicDeleteButton.IsEnabled = false;

            //Do the callback
            this.onDelete(musicNameText.Text);
        };
    }

    //Public methods

    public void Setup()
    {
        //Set the callback to enable the delete button on click on music
        rootElement.PointerPressed += (s, e) => { musicDeleteButton.IsVisible = !musicDeleteButton.IsVisible; };

        //Disable the delete button
        musicDeleteButton.IsVisible = false;
    }
}