namespace Microsoft.Samples.Kinect.CoordinateMappingBasics
{
    using Microsoft.Kinect;
    using Microsoft.Samples.Kinect.DiscreteGestureBasics;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;

    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public readonly string[] backgroundUrl = new string[] { "..\\..\\Images\\Background1.png", "..\\..\\Images\\Background2.jpg", "..\\..\\Images\\Background3.jpg" };
        private readonly int bytesPerPixel = (PixelFormats.Bgr32.BitsPerPixel + 7) / 8;
        private KinectSensor kinectSensor = null;
        private CoordinateMapper coordinateMapper = null;
        private MultiSourceFrameReader multiFrameSourceReader = null;
        private WriteableBitmap bitmap = null;
        private uint bitmapBackBufferSize = 0;
        private DepthSpacePoint[] colorMappedToDepthPoints = null;
        private DrawingGroup drawingGroup;
        private DrawingImage imageSource;
        private string statusText = null;
        private int backgroundSelectedIndex = 0;


        public event PropertyChangedEventHandler PropertyChanged;
        //----------------------------------------------------
        IList<Body> _bodies;
        //----------------------------------------------------
        private List<GestureDetector> gestureDetectorList = null;
        private BodyFrameReader bodyFrameReader = null;
        private Body[] bodies = null;
        //private KinectBodyView kinectBodyView = null;


        public MainWindow()
        {
            this.kinectSensor = KinectSensor.GetDefault();
            this.multiFrameSourceReader = this.kinectSensor.OpenMultiSourceFrameReader(FrameSourceTypes.Depth | FrameSourceTypes.Color | FrameSourceTypes.BodyIndex | FrameSourceTypes.Body);
            this.multiFrameSourceReader.MultiSourceFrameArrived += this.Reader_MultiSourceFrameArrived;
            this.coordinateMapper = this.kinectSensor.CoordinateMapper;
            FrameDescription depthFrameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;
            int depthWidth = depthFrameDescription.Width;
            int depthHeight = depthFrameDescription.Height;
            FrameDescription colorFrameDescription = this.kinectSensor.ColorFrameSource.FrameDescription;
            int colorWidth = colorFrameDescription.Width;
            int colorHeight = colorFrameDescription.Height;
            this.colorMappedToDepthPoints = new DepthSpacePoint[colorWidth * colorHeight];
            this.bitmap = new WriteableBitmap(colorWidth, colorHeight, 96.0, 96.0, PixelFormats.Bgra32, null);

            // Calculate the WriteableBitmap back buffer size
            this.bitmapBackBufferSize = (uint)((this.bitmap.BackBufferStride * (this.bitmap.PixelHeight - 1)) + (this.bitmap.PixelWidth * this.bytesPerPixel));
            this.kinectSensor.IsAvailableChanged += this.Sensor_IsAvailableChanged;
            this.kinectSensor.Open();
            this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                           : Properties.Resources.NoSensorStatusText;
            this.DataContext = this;


            this.drawingGroup = new DrawingGroup();
            this.imageSource = new DrawingImage(this.drawingGroup);

            //Gesture relative setting
            this.bodyFrameReader = this.kinectSensor.BodyFrameSource.OpenReader();
            this.bodyFrameReader.FrameArrived += this.Reader_BodyFrameArrived;
            this.gestureDetectorList = new List<GestureDetector>();
            //this.kinectBodyView = new KinectBodyView(this.kinectSensor);


            this.InitializeComponent();
            backgroundSelection.ItemsSource = new string[]
           {
                "background_1",
                "background_2",
                "background_3"
           };
            BackgroundSelectedIndex = 0;
            backgroundImg.Source = new BitmapImage(new Uri("..\\..\\Images\\Background1.png", UriKind.Relative));

            //Gesture relative setting
            int maxBodies = this.kinectSensor.BodyFrameSource.BodyCount;
            for (int i = 0; i < maxBodies; ++i)
            {
                GestureDetector detector = new GestureDetector(this.kinectSensor, this);
                this.gestureDetectorList.Add(detector);
            }

        }

        //remove background
        private void Reader_MultiSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            int depthWidth = 0;
            int depthHeight = 0;

            DepthFrame depthFrame = null;
            ColorFrame colorFrame = null;
            BodyIndexFrame bodyIndexFrame = null;
            bool isBitmapLocked = false;

            MultiSourceFrame multiSourceFrame = e.FrameReference.AcquireFrame();

            // If the Frame has expired by the time we process this event, return.
            if (multiSourceFrame == null)
            {
                return;
            }

            // We use a try/finally to ensure that we clean up before we exit the function.  
            // This includes calling Dispose on any Frame objects that we may have and unlocking the bitmap back buffer.
            try
            {
                depthFrame = multiSourceFrame.DepthFrameReference.AcquireFrame();
                colorFrame = multiSourceFrame.ColorFrameReference.AcquireFrame();
                bodyIndexFrame = multiSourceFrame.BodyIndexFrameReference.AcquireFrame();

                // If any frame has expired by the time we process this event, return.
                // The "finally" statement will Dispose any that are not null.
                if ((depthFrame == null) || (colorFrame == null) || (bodyIndexFrame == null))
                {
                    return;
                }

                // Process Depth
                FrameDescription depthFrameDescription = depthFrame.FrameDescription;

                depthWidth = depthFrameDescription.Width;
                depthHeight = depthFrameDescription.Height;

                // Access the depth frame data directly via LockImageBuffer to avoid making a copy
                using (KinectBuffer depthFrameData = depthFrame.LockImageBuffer())
                {
                    this.coordinateMapper.MapColorFrameToDepthSpaceUsingIntPtr(
                        depthFrameData.UnderlyingBuffer,
                        depthFrameData.Size,
                        this.colorMappedToDepthPoints);
                }

                // We're done with the DepthFrame 
                depthFrame.Dispose();
                depthFrame = null;

                // Process Color

                // Lock the bitmap for writing
                this.bitmap.Lock();
                isBitmapLocked = true;

                colorFrame.CopyConvertedFrameDataToIntPtr(this.bitmap.BackBuffer, this.bitmapBackBufferSize, ColorImageFormat.Bgra);

                // We're done with the ColorFrame 
                colorFrame.Dispose();
                colorFrame = null;


                // We'll access the body index data directly to avoid a copy
                using (KinectBuffer bodyIndexData = bodyIndexFrame.LockImageBuffer())
                {
                    unsafe
                    {
                        byte* bodyIndexDataPointer = (byte*)bodyIndexData.UnderlyingBuffer;

                        int colorMappedToDepthPointCount = this.colorMappedToDepthPoints.Length;

                        fixed (DepthSpacePoint* colorMappedToDepthPointsPointer = this.colorMappedToDepthPoints)
                        {
                            // Treat the color data as 4-byte pixels
                            uint* bitmapPixelsPointer = (uint*)this.bitmap.BackBuffer;

                            // Loop over each row and column of the color image
                            // Zero out any pixels that don't correspond to a body index
                            for (int colorIndex = 0; colorIndex < colorMappedToDepthPointCount; ++colorIndex)
                            {
                                float colorMappedToDepthX = colorMappedToDepthPointsPointer[colorIndex].X;
                                float colorMappedToDepthY = colorMappedToDepthPointsPointer[colorIndex].Y;

                                // The sentinel value is -inf, -inf, meaning that no depth pixel corresponds to this color pixel.
                                if (!float.IsNegativeInfinity(colorMappedToDepthX) &&
                                    !float.IsNegativeInfinity(colorMappedToDepthY))
                                {
                                    // Make sure the depth pixel maps to a valid point in color space
                                    int depthX = (int)(colorMappedToDepthX + 0.5f);
                                    int depthY = (int)(colorMappedToDepthY + 0.5f);

                                    // If the point is not valid, there is no body index there.
                                    if ((depthX >= 0) && (depthX < depthWidth) && (depthY >= 0) && (depthY < depthHeight))
                                    {
                                        int depthIndex = (depthY * depthWidth) + depthX;

                                        // If we are tracking a body for the current pixel, do not zero out the pixel
                                        if (bodyIndexDataPointer[depthIndex] != 0xff)
                                        {
                                            continue;
                                        }
                                    }
                                }
                                bitmapPixelsPointer[colorIndex] = 0;
                            }
                        }
                        this.bitmap.AddDirtyRect(new Int32Rect(0, 0, this.bitmap.PixelWidth, this.bitmap.PixelHeight));
                    }
                }

                // Body
                using (var frame = multiSourceFrame.BodyFrameReference.AcquireFrame())
                {
                    if (frame != null)
                    {
                        canvas.Children.Clear();

                        _bodies = new Body[frame.BodyFrameSource.BodyCount];

                        frame.GetAndRefreshBodyData(_bodies);

                        foreach (var body in _bodies)
                        {
                            if (body.IsTracked)
                            {
                                foreach (Joint joint in body.Joints.Values)
                                {
                                    if (joint.JointType != JointType.HandLeft && joint.JointType != JointType.HandRight)
                                    {
                                        continue;
                                    }
                                    if (joint.TrackingState == TrackingState.Tracked)
                                    {
                                        CameraSpacePoint position = joint.Position;
                                        Point point = new Point();

                                        ColorSpacePoint colorPoint = kinectSensor.CoordinateMapper.MapCameraPointToColorSpace(position);
                                        point.X = float.IsInfinity(colorPoint.X) ? 0 : colorPoint.X;
                                        point.Y = float.IsInfinity(colorPoint.Y) ? 0 : colorPoint.Y;

                                        if (joint.JointType == JointType.HandLeft)
                                        {
                                            DrawItem(body.HandLeftState, point);
                                        }
                                        else if (joint.JointType == JointType.HandRight)
                                        {
                                            DrawItem(body.HandRightState, point);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                if (isBitmapLocked)
                {
                    this.bitmap.Unlock();
                }

                if (depthFrame != null)
                {
                    depthFrame.Dispose();
                }

                if (colorFrame != null)
                {
                    colorFrame.Dispose();
                }

                if (bodyIndexFrame != null)
                {
                    bodyIndexFrame.Dispose();
                }
            }
        }



        private void Reader_BodyFrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            bool dataReceived = false;

            using (BodyFrame bodyFrame = e.FrameReference.AcquireFrame())
            {
                if (bodyFrame != null)
                {
                    if (this.bodies == null)
                    {
                        this.bodies = new Body[bodyFrame.BodyCount];
                    }
                    bodyFrame.GetAndRefreshBodyData(this.bodies);
                    dataReceived = true;
                }
            }

            if (dataReceived)
            {
                if (this.bodies != null)
                {
                    int maxBodies = this.kinectSensor.BodyFrameSource.BodyCount;
                    for (int i = 0; i < maxBodies; ++i)
                    {
                        Body body = this.bodies[i];
                        ulong trackingId = body.TrackingId;

                        if (trackingId != this.gestureDetectorList[i].TrackingId)
                        {
                            this.gestureDetectorList[i].TrackingId = trackingId;
                            this.gestureDetectorList[i].IsPaused = trackingId == 0;
                        }
                    }
                }
            }
        }




        private void DrawItem(HandState handState, Point handPosition)
        {
            string url = "";

            switch (handState)
            {
                case HandState.Closed:
                    url = "..\\..\\..\\Images\\1.png";
                    break;
                case HandState.Open:
                    url = "..\\..\\..\\Images\\2.jpg";
                    break;
                case HandState.Lasso:
                    url = "..\\..\\..\\Images\\3.jpg";
                    break;
            }

            if (!url.Equals(""))
            {
                ImageBrush b = new ImageBrush(new BitmapImage(new Uri(url, UriKind.Relative)));
                Canvas ca = new Canvas();
                ca.Height = 150;
                ca.Width = 150;
                ca.Background = b;

                Canvas.SetLeft(ca, handPosition.X - ca.Width / 2);
                Canvas.SetTop(ca, handPosition.Y - ca.Height / 2);
                canvas.Children.Add(ca);
            }
        }


        public ImageSource Portrait
        {//man with face image
            get
            {
                return this.bitmap;
            }
        }
        public ImageSource ImageSource
        {
            get
            {
                return this.imageSource;
            }
        }
        public string StatusText
        {
            get
            {
                return this.statusText;
            }

            set
            {
                if (this.statusText != value)
                {
                    this.statusText = value;

                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("StatusText"));
                    }
                }
            }
        }
        public int BackgroundSelectedIndex
        {
            get
            {
                return this.backgroundSelectedIndex;
            }

            set
            {
                if (this.backgroundSelectedIndex != value)
                {
                    this.backgroundSelectedIndex = value;

                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("BackgroundSelectedIndex"));
                    }
                }
            }
        }


        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (this.bodyFrameReader != null)
            {
                // BodyFrameReader is IDisposable
                this.bodyFrameReader.FrameArrived -= this.Reader_BodyFrameArrived;
                this.bodyFrameReader.Dispose();
                this.bodyFrameReader = null;
            }
            if (this.multiFrameSourceReader != null)
            {
                // MultiSourceFrameReder is IDisposable
                this.multiFrameSourceReader.Dispose();
                this.multiFrameSourceReader = null;
            }

            if (this.kinectSensor != null)
            {
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }

            if (this.gestureDetectorList != null)
            {
                // The GestureDetector contains disposable members (VisualGestureBuilderFrameSource and VisualGestureBuilderFrameReader)
                foreach (GestureDetector detector in this.gestureDetectorList)
                {
                    detector.Dispose();
                }

                this.gestureDetectorList.Clear();
                this.gestureDetectorList = null;
            }
        }
        /// <summary>
        /// Handles the event which the sensor becomes unavailable (E.g. paused, closed, unplugged).
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Sensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
        {
            this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                            : Properties.Resources.SensorNotAvailableStatusText;
        }
        private void ShowPineapple(object sender, RoutedEventArgs e)
        {
            if (showpineapple.IsChecked == true)
            {
                pineapple.Visibility = Visibility.Visible;
            }
            else
            {
                pineapple.Visibility = Visibility.Hidden;
            }

        }
        private void SetBackground(object sender, RoutedEventArgs e)
        {
            backgroundImg.Source = new BitmapImage(new Uri(backgroundUrl[backgroundSelection.SelectedIndex], UriKind.Relative));
        }
    }
}
