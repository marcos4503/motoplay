using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.Collections.Generic;

namespace Motoplay;

public partial class BarsChart : UserControl
{
    //Cache variables
    private ObservableCollection<double> tempObservableCollection = null;
    private List<string> tempLabelList = null;

    //Private variables
    private ISeries[] currentChartSeries = null;
    private Axis[] currentChartXaxis = null;

    //Core methods

    public BarsChart()
    {
        //Initialize the window normally on Avalonia side
        InitializeComponent();

        //Inform that is the DataConext of this User Control
        this.DataContext = this;

        //Initialize the chart and add a sample data
        InitializeChartAndAddSampleData();
    }

    private void InitializeChartAndAddSampleData()
    {
        //Generate a place holder series
        currentChartSeries = new ISeries[1];
        ColumnSeries<double> columns = new ColumnSeries<double>();
        columns.Values = new ObservableCollection<double> { 5, 10, 7 };
        columns.Padding = 8;
        columns.MaxBarWidth = double.PositiveInfinity;
        currentChartSeries[0] = columns;

        //Generate a place holder X axis
        currentChartXaxis = new Axis[1];
        Axis axis = new Axis();
        axis.Labels = new string[] { "Item 1", "Item 2", "Item 3" };
        axis.LabelsRotation = 0;
        axis.SeparatorsPaint = new SolidColorPaint(new SKColor(200, 200, 200));
        axis.SeparatorsAtCenter = false;
        axis.TicksPaint = new SolidColorPaint(new SKColor(35, 35, 35));
        axis.TicksAtCenter = true;
        axis.ForceStepToMin = true;
        axis.MinStep = 1;
        currentChartXaxis[0] = axis;

        //Render the new data in the chart
        chart.Series = currentChartSeries;
        chart.XAxes = currentChartXaxis;
    }

    //Public methods

    public void InitializeForUse()
    {
        //Initialize the cache
        tempObservableCollection = new ObservableCollection<double>();
        tempLabelList = new List<string>();
    }

    public void AddBar(string label, double startValue)
    {
        //Add the new bar
        tempObservableCollection.Add(startValue);
        tempLabelList.Add(label);
    }

    public void BuildChart()
    {
        //Update the initialized series
        currentChartSeries[0].Values = tempObservableCollection;

        //Update the initialized xAxis
        currentChartXaxis[0].Labels = tempLabelList.ToArray();

        //Render the new data in the chart
        chart.Series = currentChartSeries;
        chart.XAxes = currentChartXaxis;
    }

    public void UpdateBar(int barIndex, double newValue)
    {
        //Update the value in the desired bar
        tempObservableCollection[barIndex] = newValue;
    }
}