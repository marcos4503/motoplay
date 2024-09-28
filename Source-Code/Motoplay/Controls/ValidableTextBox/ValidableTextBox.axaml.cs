using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace Motoplay;

/*
 * This script is resposible by the work of the "Validable Text Box"
*/

public partial class ValidableTextBox : UserControl
{
    //Classes of script
    public class ClassDelegates
    {
        public delegate string OnTextChangedValidation(string currentValue);
    }

    //Private methods
    private IBrush defaultTextboxBorderBrush = null;
    private IBrush defaultTextboxSelectionBrush = null;
    private IBrush defaultTextboxForegroundBrush = null;
    private event ClassDelegates.OnTextChangedValidation onTextChangedValidation;

    //Core methods

    public ValidableTextBox()
    {
        //Initialize the window normally on Avalonia side
        InitializeComponent();

        //Inform that is the DataConext of this User Control
        this.DataContext = this;
    }

    //Public methods

    public void RegisterOnTextChangedValidationCallback(ClassDelegates.OnTextChangedValidation onTextChangedValidation)
    {
        //Register the event
        this.onTextChangedValidation = onTextChangedValidation;

        //Instruct to call the event even when the text was changed
        textBox.TextChanged += (s, e) => { CallOnTextChangedValidationCallback(); };
    }

    public void CallOnTextChangedValidationCallback()
    {
        //If not captured the original brushes, capture it
        if (defaultTextboxBorderBrush == null)
        {
            defaultTextboxBorderBrush = textBox.BorderBrush;
            defaultTextboxSelectionBrush = textBox.SelectionBrush;
            defaultTextboxForegroundBrush = textBox.Foreground;
        }

        //Prepare the result storage
        string validationResult = "";

        //Execute the callback and catch the result
        if (this.onTextChangedValidation != null)
            validationResult = this.onTextChangedValidation(textBox.Text);

        //If have error
        if (validationResult != "")
        {
            error.IsVisible = true;
            errorTxt.Text = validationResult;
            textBox.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 255, 0, 0));
            textBox.SelectionBrush = new SolidColorBrush(Color.FromArgb(255, 255, 0, 0));
            textBox.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 0, 0));
        }

        //If don't have error
        if (validationResult == "")
        {
            error.IsVisible = false;
            errorTxt.Text = "";
            textBox.BorderBrush = defaultTextboxBorderBrush;
            textBox.SelectionBrush = defaultTextboxSelectionBrush;
            textBox.Foreground = defaultTextboxForegroundBrush;
        }
    }

    public bool hasError()
    {
        //Prepare the value to return
        bool toReturn = false;

        //Force the validation code to run
        CallOnTextChangedValidationCallback();

        //If the error is visible, return the response
        if (error.IsVisible == true)
            toReturn = true;

        //Return the value
        return toReturn;
    }

    //Custom Properties Registration

    //*** LabelName Property ***//

    public static readonly StyledProperty<string> LabelNameProperty = AvaloniaProperty.Register<PanelLogItem, string>(nameof(LabelName), "Label");

    public string LabelName
    {
        get { return (string)GetValue(LabelNameProperty); }
        set { SetValue(LabelNameProperty, value); }
    }

    //*** LabelVisible Property ***//

    public static readonly StyledProperty<bool> LabelVisibleProperty = AvaloniaProperty.Register<PanelLogItem, bool>(nameof(LabelVisible), true);

    public bool LabelVisible
    {
        get { return (bool)GetValue(LabelVisibleProperty); }
        set { SetValue(LabelVisibleProperty, value); }
    }
}