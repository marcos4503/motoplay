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

    //*** PointerAngle Property ***//

    public static readonly StyledProperty<double> PointerAngleProperty = AvaloniaProperty.Register<PanelLogItem, double>(nameof(PointerAngle), 250.0f);

    public double PointerAngle
    {
        get { return (double)GetValue(PointerAngleProperty); }
        set { SetValue(PointerAngleProperty, value); }
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