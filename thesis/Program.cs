
using System;
using System.Drawing;
using System.Threading.Tasks;

namespace thesis
{
    internal class Program
    {
        static Webcam webcam;
        static Detection detection;
        static ConvertBitmap ConvertBitmap;
        static ConvertBoudingBox ConvertBoudingBox;
        static Websocket websocket_frame;
        static Websocket websocket_bounding;
        static volatile bool isprocess = false;
        static void Main(string[] args)
        {
            webcam = new Webcam();
            detection = new Detection(webcam.imageWidth, webcam.imageHeight);
            ConvertBitmap = new ConvertBitmap();
            ConvertBoudingBox = new ConvertBoudingBox();
            websocket_frame = new Websocket("8080");
            websocket_bounding = new Websocket("8082");
            webcam.NewFrame += Webcam_NewFrame;
            detection.Position += Detection_Position;
            ConvertBitmap.data += ConvertBitmap_data;
            ConvertBoudingBox.data += ConvertBoudingBox_data;
            webcam.Start();
            Console.ReadLine();
            websocket_bounding.Stop();
            websocket_frame.Stop();
            webcam.Stop();
        }
        private static void ConvertBoudingBox_data(object sender, byte[] boudingData)
        {
            websocket_bounding.Send(boudingData);
        }
        private static void ConvertBitmap_data(object sender, byte[] bitmapData)
        {

            websocket_frame.Send(bitmapData);
        }

        private static void Detection_Position(object sender, System.Collections.Generic.List<BoundingBoxes> boundingBoxes)
        {
            ConvertBoudingBox.ConvertToByteArray(boundingBoxes);
        }

        private static void Webcam_NewFrame(object sender, System.Drawing.Bitmap bitmap)
        {
            var frame1 = (Bitmap)bitmap.Clone();
            var frame2 = (Bitmap)bitmap.Clone();
            Task.Run(() =>
            {
                ConvertBitmap.convertToByteArray(frame1);
            });

            if (isprocess) return;
            Task.Run(() =>
            {
                isprocess = true;
                detection.Prediction(frame2);
                isprocess = false;
            });
        }
    }
}
