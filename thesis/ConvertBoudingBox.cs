
using System;
using System.Collections.Generic;
using System.Text.Json;
public class ConvertBoudingBox
{
    public event EventHandler<byte[]> data;
    public void ConvertToByteArray(List<BoundingBoxes> boundingBoxes)
    {
        var jsonString = JsonSerializer.Serialize(boundingBoxes);
        var byteArray = System.Text.Encoding.UTF8.GetBytes(jsonString);
        data?.Invoke(this, byteArray);
    }
}

