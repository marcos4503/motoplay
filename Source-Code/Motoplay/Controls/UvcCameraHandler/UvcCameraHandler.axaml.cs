using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Coroutine;
using FlashCap;
using SkiaImageView;
using SkiaSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using static Motoplay.BluetoothDeviceItem.ClassDelegates;
using static Motoplay.Scripts.MusicPlayer.ClassDelegates;

namespace Motoplay;

/*
 * This script is resposible by the work of the "UvcCameraHandler" that represents
 * and manage UVC Cameras connected to system.
 * This Control requires "SkiaImageView" and "FlashCap" packages.
*/

public partial class UvcCameraHandler : UserControl
{
    //Classes of script
    public class UvcDevice()
    {
        //Public variables
        public CaptureDeviceDescriptor deviceRealRef = null;
        public int realIndexOnConnectedDevices = -1;
        public string name = "";
        public string description = "";
    }
    public class ClassDelegates
    {
        public delegate void OnUpdateDevices(ref List<UvcDevice> devices);
        public delegate void OnReceiveErrorOnStartSeeing();
        public delegate void OnStartedSeeing();
        public delegate void OnStoppedSeeing();
    }

    //Cache variables
    private List<UvcDevice> currentConnectedUvcDevices = new List<UvcDevice>();
    private CaptureDevice currentSeeingUvcDevice = null;
    private double realCountFrames = 0;
    private SKImageView alternativeSkiaImageView = null;

    //Private variables
    private int cameraProjectionQuality = -1;
    private bool enableMultiThread = false;
    private int maxQueuingFrames = -1;
    private bool showStatistics = false;
    private event ClassDelegates.OnUpdateDevices onUpdateDevices = null;
    private event ClassDelegates.OnReceiveErrorOnStartSeeing onReceiveErrorOnStartSeeing = null;
    private event ClassDelegates.OnStartedSeeing onStartedSeeing = null;
    private event ClassDelegates.OnStoppedSeeing onStoppedSeeing = null;

    //Core methods

    public UvcCameraHandler()
    {
        //Initialize the window normally on Avalonia side
        InitializeComponent();

        //Inform that is the DataConext of this User Control
        this.DataContext = this;
    }

    public void SetCameraProjectionQuality(int cameraProjectionQuality)
    {
        //Save the data
        this.cameraProjectionQuality = cameraProjectionQuality;
    }

    public void SetEnabledMultiThread(bool enableMultiThread)
    {
        //Save the data
        this.enableMultiThread = enableMultiThread;
    }

    public void SetMaxQueuingFrames(int maxQueuingFrames)
    {
        //Save the data
        this.maxQueuingFrames = maxQueuingFrames;
    }

    public void SetShowStatistics(bool showStatistics)
    {
        //Save the data
        this.showStatistics = showStatistics;
    }

    public void RegisterOnUpdateDevicesCallback(ClassDelegates.OnUpdateDevices onUpdateDevices)
    {
        //Register the event
        this.onUpdateDevices = onUpdateDevices;
    }

    public void RegisterOnReceiveErrorOnStartSeeingCallback(ClassDelegates.OnReceiveErrorOnStartSeeing onReceiveErrorOnStartSeeing)
    {
        //Register the event
        this.onReceiveErrorOnStartSeeing = onReceiveErrorOnStartSeeing;
    }

    public void RegisterOnStartedSeeingCallback(ClassDelegates.OnStartedSeeing onStartedSeeing)
    {
        //Register the event
        this.onStartedSeeing = onStartedSeeing;
    }

    public void RegisterOnStoppedSeeingCallback(ClassDelegates.OnStoppedSeeing onStoppedSeeing)
    {
        //Register the event
        this.onStoppedSeeing = onStoppedSeeing;
    }

    //Public methods

    public void Initialize()
    {
        //Start the UVC devices monitor routine
        CoroutineHandler.Start(ConnectedDevicesUpdateRoutine());

        //Setup the projection quality of SkiaImageView
        if (cameraProjectionQuality == 0)
            skiaImageView.ProjectionQuality = SkiaImageView.ProjectionQuality.Low;
        if (cameraProjectionQuality == 1)
            skiaImageView.ProjectionQuality = SkiaImageView.ProjectionQuality.Middle;
        if (cameraProjectionQuality == 2)
            skiaImageView.ProjectionQuality = SkiaImageView.ProjectionQuality.High;
        if (cameraProjectionQuality == 3)
            skiaImageView.ProjectionQuality = SkiaImageView.ProjectionQuality.Perfect;

        //Hide the statistics
        statistics_root.IsVisible = false;
    }

    public void StartSeeLiveUvcDeviceWithFeature(int realIndexOfUvcCamera, int featureId)
    {
        //Stop seeing current UVC Device, if is seeing
        StopSeeLiveUvcDevice();

        //Get referece for the real selected device
        CaptureDeviceDescriptor selectedDevice = currentConnectedUvcDevices[realIndexOfUvcCamera].deviceRealRef;

        //Start a new thread to prepare and start seeing the UVC Device
        new Thread(async () =>
        {
            //Try to start seeing...
            try
            {
                //Prepare the Frame processor Loop and store this as current seeing device
                currentSeeingUvcDevice = await selectedDevice.OpenAsync(selectedDevice.Characteristics[featureId], TranscodeFormats.Auto, enableMultiThread, maxQueuingFrames, async bufferScope =>
                {
                    //Get bytes of current frame
                    byte[] frameImageData = bufferScope.Buffer.ExtractImage();

                    //Convert to bitmap (example)
                    //MemoryStream memoryStream = new MemoryStream(frameImageData);
                    //Bitmap bitmap = System.Drawing.Bitmap.FromStream(ms);

                    //Get the image converted into a SkiaImageView Bitmap
                    SKBitmap skiaBitmap = SKBitmap.Decode(frameImageData);
                    //Get statistics
                    realCountFrames += 1;
                    long currentFrameIndex = bufferScope.Buffer.FrameIndex;
                    TimeSpan currentTimeStamp = bufferScope.Buffer.Timestamp;

                    //Release the buffer now, because is not necessary
                    bufferScope.ReleaseNow();

                    //Run on main thread...
                    Dispatcher.UIThread.Invoke(() =>
                    {
                        //If is desired statistics
                        if (showStatistics == true)
                        {
                            statisticFps.Text = ("Real " + (int)(realCountFrames / currentTimeStamp.TotalSeconds) + " FPS - Index " + (int)((double)currentFrameIndex / currentTimeStamp.TotalSeconds) + " FPS");
                            statisticResolution.Text = (skiaBitmap.Width + "x" + skiaBitmap.Height + " - " + skiaBitmap.ColorType.ToString().ToUpper());
                        }

                        //Show the current frame on UI, in "SKImageView" component
                        if (alternativeSkiaImageView == null)
                            skiaImageView.Source = skiaBitmap;
                        if (alternativeSkiaImageView != null)
                            alternativeSkiaImageView.Source = skiaBitmap;

                        //Dispose from the SkiaImageView Bitmap
                        skiaBitmap.Dispose();

                    }, DispatcherPriority.MaxValue);

                    //...
                });

                //Start the seeing
                currentSeeingUvcDevice.StartAsync();

                //If is desired to show the statistics, enable it
                if (showStatistics == true)
                    Dispatcher.UIThread.Invoke(() => { statistics_root.IsVisible = true; }, DispatcherPriority.MaxValue);

                //Send the callback of start
                if (onStartedSeeing != null)
                    Dispatcher.UIThread.Invoke(() => { onStartedSeeing(); }, DispatcherPriority.MaxValue);
            }
            catch (Exception e)
            {
                //Send the error message
                if (onReceiveErrorOnStartSeeing != null)
                    Dispatcher.UIThread.Invoke(() => { onReceiveErrorOnStartSeeing(); }, DispatcherPriority.MaxValue);
            }

            //...
        }).Start();
    }

    public void SetAlternativeSkiaImageView(SKImageView alternativeSkiaImageView)
    {
        //Set the alternativa Skia Image View
        this.alternativeSkiaImageView = alternativeSkiaImageView;
    }

    public void StopSeeLiveUvcDevice()
    {
        //Try to stop seeing the UVC Device...
        try
        {
            //Stop seeing the UVC Device, and release it, if exists one at the moment
            if (currentSeeingUvcDevice != null)
            {
                currentSeeingUvcDevice.StopAsync();
                currentSeeingUvcDevice.DisposeAsync();
            }
        }
        catch (Exception e) { }
        
        //Reset the SkiaImageView component
        skiaImageView.Source = null;
        if (alternativeSkiaImageView != null)
            alternativeSkiaImageView.Source = null;

        //Disable the statistics
        realCountFrames = 0;
        statistics_root.IsVisible = false;

        //Send the callback of start
        if (onStoppedSeeing != null)
            Dispatcher.UIThread.Invoke(() => { onStoppedSeeing(); }, DispatcherPriority.MaxValue);
    }

    //Auxiliar methods

    private IEnumerator<Wait> ConnectedDevicesUpdateRoutine()
    {
        //Prepare the interval time
        Wait intervalTime = new Wait(15.0f);

        //Start the monitor loop
        while (true)
        {
            //Get UVC devices
            CaptureDevices captureDevices = new CaptureDevices();
            List<CaptureDeviceDescriptor> foundUvcDevicesList = new List<CaptureDeviceDescriptor>();
            foreach (CaptureDeviceDescriptor dev in captureDevices.EnumerateDescriptors())
                if (dev.Characteristics.Length >= 1)
                    foundUvcDevicesList.Add(dev);

            //If the quantity of found devices is different from the cache, force cache update now
            if (foundUvcDevicesList.Count != currentConnectedUvcDevices.Count)
            {
                UpdateCurrentConnectedUvcDevicesList(foundUvcDevicesList.ToArray());
                continue;
            }

            //If the quantity of found devices is equal to the cache, check if have difference
            if (foundUvcDevicesList.Count == currentConnectedUvcDevices.Count)
            {
                //Prepare the information
                bool needUpdateToCache = false;

                //Check if need to update
                for (int i = 0; i < foundUvcDevicesList.Count; i++)
                    if (currentConnectedUvcDevices[i].name != foundUvcDevicesList[i].Name)
                        needUpdateToCache = true;

                //If need update, run the updater
                if (needUpdateToCache == true)
                    UpdateCurrentConnectedUvcDevicesList(foundUvcDevicesList.ToArray());
            }

            //Wait time
            yield return intervalTime;
        }
    }

    private void UpdateCurrentConnectedUvcDevicesList(CaptureDeviceDescriptor[] captureDevices)
    {
        //Clear the list of current connected UVC devices
        currentConnectedUvcDevices.Clear();

        //Refill the list
        for (int i = 0; i < captureDevices.Length; i++)
        {
            //Create a instance of device
            UvcDevice uvcDevice = new UvcDevice();
            uvcDevice.deviceRealRef = captureDevices[i];
            uvcDevice.realIndexOnConnectedDevices = i;
            uvcDevice.name = captureDevices[i].Name;
            uvcDevice.description = captureDevices[i].Description;

            //Add to list of current connected UVC devices
            currentConnectedUvcDevices.Add(uvcDevice);
        }

        //Send a callback
        if (onUpdateDevices != null)
            onUpdateDevices(ref currentConnectedUvcDevices);
    }
}