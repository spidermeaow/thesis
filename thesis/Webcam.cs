using AForge.Video.DirectShow;
using System.Drawing;
using System;

class Webcam
{
    private FilterInfoCollection videoDevices;
    private VideoCaptureDevice videoSource;
    public event EventHandler<Bitmap> NewFrame;
    public int imageWidth { get; }
    public int imageHeight { get; }
    public Webcam(int deviceNum = 0)
    {
        videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);

        if (videoDevices.Count == 0)
        {
            throw new Exception("No video devices found");
        }

        if (deviceNum > videoDevices.Count)
        {
            throw new Exception("invalid Video index");
        }

        videoSource = new VideoCaptureDevice(videoDevices[deviceNum].MonikerString);

        imageWidth = videoSource.VideoCapabilities[0].FrameSize.Width;
        imageHeight = videoSource.VideoCapabilities[0].FrameSize.Height;

        videoSource.NewFrame += OnNewFrame;

    }
    protected virtual void OnNewFrame(object sender, AForge.Video.NewFrameEventArgs eventArgs)
    {
        var frame = (Bitmap)eventArgs.Frame.Clone();
        frame.RotateFlip(RotateFlipType.RotateNoneFlipX);
        NewFrame?.Invoke(this, frame);
        frame.Dispose();
    }
    public void Start()
    {
        videoSource.Start();
    }
    public void Stop()
    {
        if (videoSource != null && this.videoSource.IsRunning)
        {
            videoSource.SignalToStop();
            videoSource.WaitForStop();
            videoSource = null;
        }
    }
}
