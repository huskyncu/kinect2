//------------------------------------------------------------------------------
// <copyright file="GestureDetector.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.DiscreteGestureBasics
{
    using System;
    using System.Collections.Generic;
    using System.Windows;
    using Microsoft.Kinect;
    using Microsoft.Kinect.VisualGestureBuilder;
    using Microsoft.Samples.Kinect.CoordinateMappingBasics;
    using System.Windows.Media.Imaging;
    using System.Threading;
    using System.ComponentModel;

    /// <summary>
    /// Gesture Detector class which listens for VisualGestureBuilderFrame events from the service
    /// and updates the associated GestureResultView object with the latest results for the 'Seated' gesture
    /// </summary>
    public class GestureDetector : IDisposable
    {
        /// <summary> Path to the gesture database that was trained with VGB </summary>
        private readonly string gestureDatabase = "C:\\Users\\j8792\\Documents\\Kinect Studio\\Repository\\test.gbd";

        private readonly string summonGestureName = "Raise_hand";

        private readonly string changeGestureName = "Clap_handsProgress";

        /// <summary> Gesture frame source which should be tied to a body tracking ID </summary>
        private VisualGestureBuilderFrameSource vgbFrameSource = null;

        /// <summary> Gesture frame reader which will handle gesture events coming from the sensor </summary>
        private VisualGestureBuilderFrameReader vgbFrameReader = null;
        private MainWindow mainWindow;

        private bool[] backgroundBuffer = new bool[20];
        private int backgroundBufferIndex = 0;

        /// <summary>
        /// Initializes a new instance of the GestureDetector class along with the gesture frame source and reader
        /// </summary>
        /// <param name="kinectSensor">Active sensor to initialize the VisualGestureBuilderFrameSource object with</param>
        /// <param name="gestureResultView">GestureResultView object to store gesture results of a single body to</param>
        public GestureDetector(KinectSensor kinectSensor, MainWindow mainWindow)
        {
            if (kinectSensor == null)
            {
                throw new ArgumentNullException("kinectSensor");
            }
            this.mainWindow = mainWindow;
            // create the vgb source. The associated body tracking ID will be set when a valid body frame arrives from the sensor.
            this.vgbFrameSource = new VisualGestureBuilderFrameSource(kinectSensor, 0);

            // open the reader for the vgb frames
            this.vgbFrameReader = this.vgbFrameSource.OpenReader();
           
            if (this.vgbFrameReader != null)
            {
                this.vgbFrameReader.IsPaused = true;
                this.vgbFrameReader.FrameArrived += this.Reader_GestureFrameArrived;
            }
            for(int i =0;i< backgroundBuffer.Length; i++)
            {
                backgroundBuffer[i] = false;
            }
            using (VisualGestureBuilderDatabase database = new VisualGestureBuilderDatabase(this.gestureDatabase))
            {
                foreach (Gesture gesture in database.AvailableGestures)
                {
                    if (gesture.Name.Equals(this.summonGestureName))
                    {
                        this.vgbFrameSource.AddGesture(gesture);
                    }else if (gesture.Name.Equals(this.changeGestureName))
                    {
                        this.vgbFrameSource.AddGesture(gesture);
                    }
                }
            }
        }
       

        public ulong TrackingId
        {
            get
            {
                return this.vgbFrameSource.TrackingId;
            }

            set
            {
                if (this.vgbFrameSource.TrackingId != value)
                {
                    this.vgbFrameSource.TrackingId = value;
                }
            }
        }

      
        public bool IsPaused
        {
            get
            {
                return this.vgbFrameReader.IsPaused;
            }

            set
            {
                if (this.vgbFrameReader.IsPaused != value)
                {
                    this.vgbFrameReader.IsPaused = value;
                }
            }
        }

        
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.vgbFrameReader != null)
                {
                    this.vgbFrameReader.FrameArrived -= this.Reader_GestureFrameArrived;
                    this.vgbFrameReader.Dispose();
                    this.vgbFrameReader = null;
                }

                if (this.vgbFrameSource != null)
                {
                    this.vgbFrameSource.Dispose();
                    this.vgbFrameSource = null;
                }
            }
        }

        
        private void Reader_GestureFrameArrived(object sender, VisualGestureBuilderFrameArrivedEventArgs e)
        {
            VisualGestureBuilderFrameReference frameReference = e.FrameReference;
            using (VisualGestureBuilderFrame frame = frameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    // get the discrete gesture results which arrived with the latest frame
                    IReadOnlyDictionary<Gesture, DiscreteGestureResult> discreteResults = frame.DiscreteGestureResults;
                    IReadOnlyDictionary<Gesture, ContinuousGestureResult> continuousResults = frame.ContinuousGestureResults;

                    if (discreteResults != null)
                    {
                        // we only have one gesture in this source object, but you can get multiple gestures
                        foreach (Gesture gesture in this.vgbFrameSource.Gestures)
                        {
                           
                            if (gesture.Name.Equals(this.summonGestureName) && gesture.GestureType == GestureType.Discrete)
                            {
                                DiscreteGestureResult dResult;
                                discreteResults.TryGetValue(gesture, out dResult);

                                if (dResult != null)
                                {
                                    if (dResult.Confidence > 0.8)
                                    {
                                        mainWindow.pineapple.Dispatcher.BeginInvoke(
                                            new Action(() => { mainWindow.pineapple.Visibility = Visibility.Visible; }), null);
                                    }
                                    else
                                    {
                                        mainWindow.pineapple.Dispatcher.BeginInvoke(
                                            new Action(() => { mainWindow.pineapple.Visibility = Visibility.Hidden; }), null);
                                    }
                                }
                            }
                            else if (gesture.Name.Equals(this.changeGestureName) && gesture.GestureType == GestureType.Continuous)
                            {
                                ContinuousGestureResult cResult;
                                continuousResults.TryGetValue(gesture, out cResult);
                                if (cResult != null)
                                {
                                    if (cResult.Progress > 0.7)
                                    {
                                        backgroundBuffer[backgroundBufferIndex] = true;
                                    }
                                    else
                                    {
                                        backgroundBuffer[backgroundBufferIndex] = false;    
                                    }
                                    backgroundBufferIndex++;
                                    backgroundBufferIndex = backgroundBufferIndex % backgroundBuffer.Length;
                                   
                                    int doEvent = 0;
                                    foreach(bool b in backgroundBuffer)
                                    {
                                        if (b)
                                        {
                                            doEvent++;
                                        }
                                    }
                                    if (doEvent == backgroundBuffer.Length/2)
                                    {
                                        int index = (mainWindow.backgroundSelection.SelectedIndex + 1) % mainWindow.backgroundUrl.Length;

                                        mainWindow.BackgroundSelectedIndex = index;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
