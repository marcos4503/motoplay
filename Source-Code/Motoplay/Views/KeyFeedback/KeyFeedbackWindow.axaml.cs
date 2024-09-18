using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Motoplay;

/*
* This is the code responsible by the Letter Keyboard Feedback
*/

public partial class KeyFeedbackWindow : Window
{
    //Private variables
    private string startingTitleOfWindow = "";

    //Core methods

    public KeyFeedbackWindow()
    {
        //Initialize the window normally on Avalonia side
        InitializeComponent();

        //Get the original title of window
        startingTitleOfWindow = this.Title;
    }

    public void SetLetter(string letter)
    {
        //Show in title
        this.Title = letter;
    }

    public void RemoveLetter()
    {
        //Restore the original title of window
        this.Title = startingTitleOfWindow;
    }


    public int GetWindowWidth()
    {
        //Return the window width
        return (int)(letterRoot.Width);
    }

    public int GetWindowHeight()
    {
        //Return the window height
        return (int)(letterRoot.Height);
    }
}