using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore;
using System.Collections.ObjectModel;
using LiveChartsCore.Defaults;
using System.Collections.Generic;
using System;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace Motoplay;

public partial class RealtimeChart : UserControl
{
    //Cache variables
    private List<DateTimePoint> tempValues = new List<DateTimePoint>();
    private DateTimeAxis tempTimeAxis = null;

    //Private variables
    private ObservableCollection<ISeries> currentChartSeries = null;
    private Axis[] currentChartXaxis = null;

    //Core methods

    public RealtimeChart()
    {
        //Initialize the window normally on Avalonia side
        InitializeComponent();

        //Inform that is the DataConext of this User Control
        this.DataContext = this;

        //Initialize the chart
        InitializeChart();
    }

    private void InitializeChart()
    {
        //Generate a base series
        currentChartSeries = null;
        ObservableCollection<ISeries> observableCollection = new ObservableCollection<ISeries>();
        LineSeries<DateTimePoint> lineSeries = new LineSeries<DateTimePoint>();
        lineSeries.Values = tempValues;
        lineSeries.Fill = null;
        lineSeries.GeometryFill = null;
        lineSeries.GeometryStroke = null;
        observableCollection.Add(lineSeries);
        currentChartSeries = observableCollection;

        //Generate a base axis
        currentChartXaxis = new Axis[1];
        tempTimeAxis = new DateTimeAxis(TimeSpan.FromSeconds(1), Formatter);
        tempTimeAxis.CustomSeparators = GetSeparators();
        tempTimeAxis.AnimationsSpeed = TimeSpan.FromMilliseconds(0);
        tempTimeAxis.SeparatorsPaint = new SolidColorPaint(SKColors.Black.WithAlpha(100));
        currentChartXaxis[0] = tempTimeAxis;

        //Render the new data in the chart
        chart.Series = currentChartSeries;
        chart.XAxes = currentChartXaxis;
    }

    //Public methods

    public void AddValue(double value)
    {
        //Add a new value to chart
        tempValues.Add(new DateTimePoint(DateTime.Now, value));
        if (tempValues.Count > 300)
            tempValues.RemoveAt(0);

        //Update the separator
        tempTimeAxis.CustomSeparators = GetSeparators();
    }

    //Auxiliar methods

    private static string Formatter(DateTime date)
    {
        var secsAgo = (DateTime.Now - date).TotalSeconds;

        return secsAgo < 1 ? "now" : $"{secsAgo:N0}s";
    }

    private double[] GetSeparators()
    {
        var now = DateTime.Now;

        return new double[]
        {
            now.AddSeconds(-300).Ticks,
            now.AddSeconds(-240).Ticks,
            now.AddSeconds(-180).Ticks,
            now.AddSeconds(-120).Ticks,
            now.AddSeconds(-60).Ticks,
            now.AddSeconds(-30).Ticks,
            now.AddSeconds(-5).Ticks,
            now.Ticks
        };
    }
}