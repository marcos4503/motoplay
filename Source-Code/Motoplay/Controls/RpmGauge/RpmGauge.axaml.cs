using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Motoplay;

/*
 * This script is resposible by the work of the "RPM Gauge"
*/

public partial class RpmGauge : UserControl
{
    //Core methods

    public RpmGauge()
    {
        //Initialize the window normally on Avalonia side
        InitializeComponent();

        //Inform that is the DataConext of this User Control
        this.DataContext = this;
    }

    //Custom Properties Registration

    //*** PrimaryPointerAngle Property ***//

    public static readonly StyledProperty<double> PrimaryPointerAngleProperty = AvaloniaProperty.Register<PanelLogItem, double>(nameof(PrimaryPointerAngle), 250.0f);

    public double PrimaryPointerAngle
    {
        get { return (double)GetValue(PrimaryPointerAngleProperty); }
        set { SetValue(PrimaryPointerAngleProperty, value); }
    }

    //*** SecondaryPointerAngle Property ***//

    public static readonly StyledProperty<double> SecondaryPointerAngleProperty = AvaloniaProperty.Register<PanelLogItem, double>(nameof(SecondaryPointerAngle), 0.0f);

    public double SecondaryPointerAngle
    {
        get { return (double)GetValue(SecondaryPointerAngleProperty); }
        set { SetValue(SecondaryPointerAngleProperty, value); }
    }

    //*** SecondayPointerVisible Property ***//

    public static readonly StyledProperty<bool> SecondayPointerVisibleProperty = AvaloniaProperty.Register<PanelLogItem, bool>(nameof(SecondayPointerVisible), true);

    public bool SecondayPointerVisible
    {
        get { return (bool)GetValue(SecondayPointerVisibleProperty); }
        set { SetValue(SecondayPointerVisibleProperty, value); }
    }

    //*** RpmValuesColor Property ***//

    public static readonly StyledProperty<string> RpmValuesColorProperty = AvaloniaProperty.Register<PanelLogItem, string>(nameof(RpmValuesColor), "#424242");

    public string RpmValuesColor
    {
        get { return (string)GetValue(RpmValuesColorProperty); }
        set { SetValue(RpmValuesColorProperty, value); }
    }

    //*** RpmValueAt0Percent Property ***//

    public static readonly StyledProperty<string> RpmValueAt0PercentProperty = AvaloniaProperty.Register<PanelLogItem, string>(nameof(RpmValueAt0Percent), "00");

    public string RpmValueAt0Percent
    {
        get { return (string)GetValue(RpmValueAt0PercentProperty); }
        set { SetValue(RpmValueAt0PercentProperty, value); }
    }

    //*** RpmValueAt0Visible Property ***//

    public static readonly StyledProperty<bool> RpmValueAt0VisibleProperty = AvaloniaProperty.Register<PanelLogItem, bool>(nameof(RpmValueAt0Visible), true);

    public bool RpmValueAt0Visible
    {
        get { return (bool)GetValue(RpmValueAt0VisibleProperty); }
        set { SetValue(RpmValueAt0VisibleProperty, value); }
    }

    //*** RpmValueAt12Visible Property ***//

    public static readonly StyledProperty<bool> RpmValueAt12VisibleProperty = AvaloniaProperty.Register<PanelLogItem, bool>(nameof(RpmValueAt12Visible), true);

    public bool RpmValueAt12Visible
    {
        get { return (bool)GetValue(RpmValueAt12VisibleProperty); }
        set { SetValue(RpmValueAt12VisibleProperty, value); }
    }

    //*** RpmValueAt25Percent Property ***//

    public static readonly StyledProperty<string> RpmValueAt25PercentProperty = AvaloniaProperty.Register<PanelLogItem, string>(nameof(RpmValueAt25Percent), "04");

    public string RpmValueAt25Percent
    {
        get { return (string)GetValue(RpmValueAt25PercentProperty); }
        set { SetValue(RpmValueAt25PercentProperty, value); }
    }

    //*** RpmValueAt25Visible Property ***//

    public static readonly StyledProperty<bool> RpmValueAt25VisibleProperty = AvaloniaProperty.Register<PanelLogItem, bool>(nameof(RpmValueAt25Visible), true);

    public bool RpmValueAt25Visible
    {
        get { return (bool)GetValue(RpmValueAt25VisibleProperty); }
        set { SetValue(RpmValueAt25VisibleProperty, value); }
    }

    //*** RpmValueAt38Visible Property ***//

    public static readonly StyledProperty<bool> RpmValueAt38VisibleProperty = AvaloniaProperty.Register<PanelLogItem, bool>(nameof(RpmValueAt38Visible), true);

    public bool RpmValueAt38Visible
    {
        get { return (bool)GetValue(RpmValueAt38VisibleProperty); }
        set { SetValue(RpmValueAt38VisibleProperty, value); }
    }

    //*** RpmValueAt50Percent Property ***//

    public static readonly StyledProperty<string> RpmValueAt50PercentProperty = AvaloniaProperty.Register<PanelLogItem, string>(nameof(RpmValueAt50Percent), "07");

    public string RpmValueAt50Percent
    {
        get { return (string)GetValue(RpmValueAt50PercentProperty); }
        set { SetValue(RpmValueAt50PercentProperty, value); }
    }

    //*** RpmValueAt50Visible Property ***//

    public static readonly StyledProperty<bool> RpmValueAt50VisibleProperty = AvaloniaProperty.Register<PanelLogItem, bool>(nameof(RpmValueAt50Visible), true);

    public bool RpmValueAt50Visible
    {
        get { return (bool)GetValue(RpmValueAt50VisibleProperty); }
        set { SetValue(RpmValueAt50VisibleProperty, value); }
    }

    //*** RpmValueAt62Visible Property ***//

    public static readonly StyledProperty<bool> RpmValueAt62VisibleProperty = AvaloniaProperty.Register<PanelLogItem, bool>(nameof(RpmValueAt62Visible), true);

    public bool RpmValueAt62Visible
    {
        get { return (bool)GetValue(RpmValueAt62VisibleProperty); }
        set { SetValue(RpmValueAt62VisibleProperty, value); }
    }

    //*** RpmValueAt75Percent Property ***//

    public static readonly StyledProperty<string> RpmValueAt75PercentProperty = AvaloniaProperty.Register<PanelLogItem, string>(nameof(RpmValueAt75Percent), "10");

    public string RpmValueAt75Percent
    {
        get { return (string)GetValue(RpmValueAt75PercentProperty); }
        set { SetValue(RpmValueAt75PercentProperty, value); }
    }

    //*** RpmValueAt75Visible Property ***//

    public static readonly StyledProperty<bool> RpmValueAt75VisibleProperty = AvaloniaProperty.Register<PanelLogItem, bool>(nameof(RpmValueAt75Visible), true);

    public bool RpmValueAt75Visible
    {
        get { return (bool)GetValue(RpmValueAt75VisibleProperty); }
        set { SetValue(RpmValueAt75VisibleProperty, value); }
    }

    //*** RpmValueAt88Visible Property ***//

    public static readonly StyledProperty<bool> RpmValueAt8VisibleProperty = AvaloniaProperty.Register<PanelLogItem, bool>(nameof(RpmValueAt88Visible), true);

    public bool RpmValueAt88Visible
    {
        get { return (bool)GetValue(RpmValueAt8VisibleProperty); }
        set { SetValue(RpmValueAt8VisibleProperty, value); }
    }

    //*** RpmValueAt100Percent Property ***//

    public static readonly StyledProperty<string> RpmValueAt100PercentProperty = AvaloniaProperty.Register<PanelLogItem, string>(nameof(RpmValueAt100Percent), "14");

    public string RpmValueAt100Percent
    {
        get { return (string)GetValue(RpmValueAt100PercentProperty); }
        set { SetValue(RpmValueAt100PercentProperty, value); }
    }

    //*** RpmValueAt100Visible Property ***//

    public static readonly StyledProperty<bool> RpmValueAt100VisibleProperty = AvaloniaProperty.Register<PanelLogItem, bool>(nameof(RpmValueAt100Visible), true);

    public bool RpmValueAt100Visible
    {
        get { return (bool)GetValue(RpmValueAt100VisibleProperty); }
        set { SetValue(RpmValueAt100VisibleProperty, value); }
    }
}