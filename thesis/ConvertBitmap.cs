

using System;
using System.Drawing;
using System.IO;

public class ConvertBitmap
{

    public event EventHandler<byte[]> data;

    public void convertToByteArray(Bitmap frame)
    {
        using (MemoryStream ms = new MemoryStream())
        {
            frame.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
            data?.Invoke(this, ms.ToArray());
        }
    }
}