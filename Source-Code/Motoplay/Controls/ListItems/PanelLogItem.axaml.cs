using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Motoplay;

/*
 * This script is resposible by the work of the "PanelLogItem" that represents
 * logs sended by the OBD Adapter Handler
*/

public partial class PanelLogItem : UserControl
{
    //Core methods

    public PanelLogItem()
    {
        //Initialize the window normally on Avalonia side
        InitializeComponent();

        //Inform that is the DataConext of this User Control
        this.DataContext = this;
    }

    //Custom Properties Registration

    //*** Message Property ***//

    public static readonly StyledProperty<string> MessageProperty = AvaloniaProperty.Register<PanelLogItem, string>(nameof(Message), "Place Holder.");

    public string Message
    {
        get { return (string)GetValue(MessageProperty); }
        set { SetValue(MessageProperty, value); }
    }

    //*** Time Property ***//

    public static readonly StyledProperty<string> TimeProperty = AvaloniaProperty.Register<PanelLogItem, string>(nameof(Time), "--:--:--");

    public string Time
    {
        get { return (string)GetValue(TimeProperty); }
        set { SetValue(TimeProperty, value); }
    }

    //*** ShowDivider Property ***//

    public static readonly StyledProperty<bool> ShowDividerProperty = AvaloniaProperty.Register<PanelLogItem, bool>(nameof(ShowDivider), true);

    public bool ShowDivider
    {
        get { return (bool)GetValue(ShowDividerProperty); }
        set { SetValue(ShowDividerProperty, value); }
    }
}