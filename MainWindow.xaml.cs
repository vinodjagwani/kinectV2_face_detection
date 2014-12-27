using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Emgu.CV.Structure;
using Emgu.CV;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Microsoft.Kinect;
using System.IO;
using System.Threading;
using System.Drawing.Imaging;
using System.Xml;
using System.Diagnostics;
using Emgu.CV.GPU;
using System.Data;
using System.Data.SqlClient;



namespace KinectfaceProject
{

    public partial class MainWindow : Window
    {


        DispatcherTimer timer;
        static string path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        bool doneBodySaveData = false;
        XmlDocument docu = new XmlDocument();
        private bool doBody = true;
        private WriteableBitmap bitmap = null;
        Image<Bgra, Byte> currentColorImageFrame;
        private readonly int bytesPerPixel = (PixelFormats.Bgr32.BitsPerPixel + 7) / 8;
        private KinectSensor kinectSensor = null;
        private MultiSourceFrameReader reader = null;
        private byte[] pixels = null;
        private Body[] bodies = null;
        private CoordinateMapper coordinateMapper = null;
        
        public ImageSource ImageSource
        {
            get
            {
                return this.bitmap;
            }
        }


        public string StatusText
        {
            get { return (string)GetValue(StatusTextProperty); }
            set { SetValue(StatusTextProperty, value); }
        }

        public static readonly DependencyProperty StatusTextProperty =
            DependencyProperty.Register("StatusText", typeof(string), typeof(MainWindow), new PropertyMetadata(""));

        public MainWindow()
        {
            
            this.kinectSensor = KinectSensor.Default;
            if (this.kinectSensor != null)
            {

                this.coordinateMapper = this.kinectSensor.CoordinateMapper;
                this.kinectSensor.Open();
                FrameDescription ColorframeDescription = this.kinectSensor.ColorFrameSource.FrameDescription;
                this.reader = this.kinectSensor.OpenMultiSourceFrameReader(FrameSourceTypes.Color | FrameSourceTypes.Body);
                this.bodies = new Body[this.kinectSensor.BodyFrameSource.BodyCount];
                this.pixels = new byte[ColorframeDescription.Width * ColorframeDescription.Height * this.bytesPerPixel];
                this.bitmap = new WriteableBitmap(ColorframeDescription.Width, ColorframeDescription.Height, 96.0, 96.0, PixelFormats.Bgr32, null);
                StatusText = "Detect";
            }
            this.DataContext = this;
            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;
            InitializeComponent();
        }


        void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (this.reader != null)
            {

                this.reader.Dispose();
                this.reader = null;
            }


            if (this.bodies != null)
            {
                foreach (Body body in this.bodies)
                {
                    if (body != null)
                    {
                        body.Dispose();
                    }
                }
            }

            if (this.kinectSensor != null)
            {
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }

         }

        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
           
            timer = new DispatcherTimer();
            timer.Tick += new EventHandler(timer_Tick);
            timer.Interval = new TimeSpan(0, 0, 0, 0, 30);
            if (this.reader != null)
            {
                this.reader.MultiSourceFrameArrived += reader_MultiSourceFrameArrived;

            }
            timer.Start();
        
        }

        private void reader_MultiSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            
            MultiSourceFrameReference frameReference = e.FrameReference;
            MultiSourceFrame multiSourceFrame = null;
            ColorFrame colorFrame = null;
            BodyFrame bodyFrame = null;

            try
            {
                multiSourceFrame = frameReference.AcquireFrame();
               
                if (multiSourceFrame != null)
                {
                    using (multiSourceFrame)
                    {
                        ColorFrameReference colorFrameReference = multiSourceFrame.ColorFrameReference;
                        BodyFrameReference bodyFrameReference = multiSourceFrame.BodyFrameReference;
                        colorFrame = colorFrameReference.AcquireFrame();
                        bodyFrame = bodyFrameReference.AcquireFrame();
                        if (colorFrame != null && bodyFrame != null)
                        {


                            FrameDescription colorFrameDescription = colorFrame.FrameDescription;

                            if ((colorFrameDescription.Width == this.bitmap.PixelWidth) && (colorFrameDescription.Height == this.bitmap.PixelHeight))
                            {

                                if (colorFrame.RawColorImageFormat == ColorImageFormat.Bgra)
                                {
                                    colorFrame.CopyRawFrameDataToArray(this.pixels);
                                }
                                else
                                {
                                    colorFrame.CopyConvertedFrameDataToArray(this.pixels, ColorImageFormat.Bgra);
                                }


                                System.Drawing.Bitmap bitmapColorImageFrame = new System.Drawing.Bitmap(colorFrameDescription.Width, colorFrameDescription.Height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
                                System.Drawing.Imaging.BitmapData bitmapDataColorImageFrame = bitmapColorImageFrame.LockBits(new System.Drawing.Rectangle(0, 0, colorFrameDescription.Width, colorFrameDescription.Height), System.Drawing.Imaging.ImageLockMode.WriteOnly, bitmapColorImageFrame.PixelFormat);
                                IntPtr ptr = bitmapDataColorImageFrame.Scan0;
                                Marshal.Copy(this.pixels, 0, ptr, colorFrameDescription.Width * colorFrameDescription.Height * bytesPerPixel);
                                bitmapColorImageFrame.UnlockBits(bitmapDataColorImageFrame);
                                currentColorImageFrame = new Image<Bgra, Byte>(bitmapColorImageFrame);
                                this.bitmap.WritePixels(new Int32Rect(0, 0, colorFrameDescription.Width, colorFrameDescription.Height), this.pixels, colorFrameDescription.Width * this.bytesPerPixel, 0);
                            }

                        if (doBody)
                            {
                                bodyFrame.GetAndRefreshBodyData(this.bodies);
                                canvas.Children.Clear();
                                foreach (Body body in this.bodies)
                                {

                                    if (body.IsTracked)
                                    {
                                        TextBox.Text = body.TrackingId.ToString();
                                        IReadOnlyDictionary<JointType, Joint> joints = body.Joints;
                                        Dictionary<JointType, Point> jointPoints = new Dictionary<JointType, Point>();
                                        ColorSpacePoint colorSpacePoint = this.coordinateMapper.MapCameraPointToColorSpace(joints[JointType.Head].Position);
                                        double userDistance = joints[JointType.Neck].Position.Z;
                                        Console.WriteLine("BodyDistance:- "+userDistance);
                                        if (userDistance >= 1.4 && userDistance <= 1.6)
                                        {
                                        Rectangle myx = new Rectangle();
                                        myx.Stroke = Brushes.Red;
                                        myx.StrokeThickness = 4;
                                        myx.Height = (int)260 / userDistance;
                                        myx.Width = (int)220 / userDistance;
                                        canvas.Children.Add(myx);
                                        int Xoffset = (int)(100 / userDistance);
                                        int Yoffset = (int)(90 / userDistance);
                                        int positionX = (int)colorSpacePoint.X - Xoffset;
                                        int positionY = (int)colorSpacePoint.Y - Yoffset;
                                        Canvas.SetLeft(myx, positionX);
                                        Canvas.SetTop(myx, positionY);
                                         }
                                     }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                if (colorFrame != null)
                {
                    colorFrame.Dispose();
                    colorFrame = null;
                }
                if (bodyFrame != null)
                {
                    bodyFrame.Dispose();
                    bodyFrame = null;
                }
            }
        }





        void timer_Tick(object sender, EventArgs e)
        {

          if (this.reader != null)
            {
                this.reader.MultiSourceFrameArrived += reader_MultiSourceFrameArrived;
            
            }

        }



       
        private bool save_training_data(System.Drawing.Image face_data, string name, string namePerson, string directoryName)
        {
            
            try
            {
                Random rand = new Random();
                bool file_create = true;
                string uniqueName = name + "_" + namePerson;
                string facename = "face_" + uniqueName + "_" + rand.Next().ToString() + ".jpg";
                while (file_create)
                {

                    if (!File.Exists(directoryName + facename))
                    {
                        file_create = false;
                    }
                    else
                    {
                        facename = "face_" + uniqueName + "_" + rand.Next().ToString() + ".jpg";
                    }
                }


                if (Directory.Exists(directoryName))
                {
                    face_data.Save(directoryName + facename, ImageFormat.Jpeg);
                }
                else
                {
                    Directory.CreateDirectory(directoryName);
                    face_data.Save(directoryName + facename, ImageFormat.Jpeg);
                }
                if (File.Exists(directoryName + "TrainedLabels.xml"))
                {
                    bool loading = true;
                    while (loading)
                    {
                        try
                        {
                            docu.Load(directoryName + "TrainedLabels.xml");
                            loading = false;
                        }
                        catch
                        {
                            docu = null;
                            docu = new XmlDocument();
                            Thread.Sleep(10);
                        }
                    }
                    XmlElement root = docu.DocumentElement;
                    XmlElement face_D = docu.CreateElement("FACE");
                    XmlElement name_D = docu.CreateElement("NAME");
                    XmlElement file_D = docu.CreateElement("FILE");
                    name_D.InnerText = uniqueName;
                    file_D.InnerText = facename;
                    face_D.AppendChild(name_D);
                    face_D.AppendChild(file_D);
                    root.AppendChild(face_D);
                    docu.Save(directoryName + "TrainedLabels.xml");
                }
                else
                {
                    FileStream FS_Face = File.OpenWrite(directoryName + "TrainedLabels.xml");
                    using (XmlWriter writer = XmlWriter.Create(FS_Face))
                    {
                        writer.WriteStartDocument();
                        writer.WriteStartElement("Faces_For_Training");
                        writer.WriteStartElement("FACE");
                        writer.WriteElementString("NAME", uniqueName);
                        writer.WriteElementString("FILE", facename);
                        writer.WriteEndElement();
                        writer.WriteEndElement();
                        writer.WriteEndDocument();
                    }
                    FS_Face.Close();
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return false;
            }

        }


        private void SaveDataButton_Click(object sender, RoutedEventArgs e)
        {
            if (TextBox1.Text != null && TextBox1.Text != "")
            {
                foreach (Body body in this.bodies)
                {
                    //Body body = this.bodies[5];
                    if (body.IsTracked)
                    {

                        IReadOnlyDictionary<JointType, Joint> joints = body.Joints;
                        Dictionary<JointType, Point> jointPoints = new Dictionary<JointType, Point>();
                        ColorSpacePoint colorSpacePoint = this.coordinateMapper.MapCameraPointToColorSpace(joints[JointType.Head].Position);
                        double userDistance = joints[JointType.Neck].Position.Z;
                        if (userDistance >= 1.4 && userDistance <= 1.6)
                        {

                            Rectangle myx = new Rectangle();
                            myx.Stroke = Brushes.Black;
                            myx.StrokeThickness = 4;
                            myx.Height = (int)260 / userDistance;
                            myx.Width = (int)220 / userDistance;
                            canvas.Children.Add(myx);
                            int Xoffset = (int)(100 / userDistance);
                            int Yoffset = (int)(90 / userDistance);
                            int positionX = (int)colorSpacePoint.X - Xoffset;
                            int positionY = (int)colorSpacePoint.Y - Yoffset;
                            //Canvas.SetLeft(myx, positionX);
                            //Canvas.SetTop(myx, positionY);

                            Image<Gray, byte> face = currentColorImageFrame.Copy(new System.Drawing.Rectangle(positionX, positionY, (int)myx.Width, (int)myx.Height)).Convert<Gray, byte>().Resize(100, 100, Emgu.CV.CvEnum.INTER.CV_INTER_CUBIC);
                            face._EqualizeHist();
                            string name = TextBox1.Text;
                            if (currentColorImageFrame != null)
                            {

                                for (int i = 0; i < 11; i++)
                                {

                                    doneBodySaveData = save_training_data(face.ToBitmap(), name, body.TrackingId.ToString(), path + "/FaceDB/");

                                }
                            }
                            face.Dispose();
                        }
                    }
                }
            }
            else
            {
                MessageBox.Show("Enter name of employee");
            }
        }


    }
}