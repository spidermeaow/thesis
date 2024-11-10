using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.Image;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
public class Detection
{
    private PredictionEngine<Input, Predictions> predictionEngine;
    private MLContext context;
    public const int rowCount = 13, columnCount = 13;
    private static readonly (float x, float y)[] boxAnchors = { (0.573f, 0.677f), (1.87f, 2.06f), (3.34f, 5.47f), (7.88f, 3.53f), (9.77f, 9.17f) };
    public const int featuresPerBox = 5;
    private Dictionary<string, string> labelCache = new Dictionary<string, string>();
    public event EventHandler<List<BoundingBoxes>> Position;
    private int originalHeight, originalWidth;
    public Detection(int originalWidth, int originalHeight)
    {
        this.originalHeight = originalHeight;
        this.originalWidth = originalWidth;
        this.context = new MLContext();
        var emptyData = new List<Input> { new Input() };
        var data = context.Data.LoadFromEnumerable(emptyData);
        var pipeline = context.Transforms.ResizeImages(resizing: ImageResizingEstimator.ResizingKind.Fill,
            outputColumnName: "data",
            imageWidth: ImageSettings.imageWidth,
            imageHeight: ImageSettings.imageHeight, inputColumnName: nameof(Input.Image))
                         .Append(context.Transforms.ExtractPixels(outputColumnName: "data"))
                         .Append(context.Transforms.ApplyOnnxModel(modelFile: "model.onnx",
                         outputColumnName: "model_outputs0",
                         inputColumnName: "data"));
        var model = pipeline.Fit(data);
        this.predictionEngine = context.Model.CreatePredictionEngine<Input, Predictions>(model);
        LoadLabels();
    }
    public void Prediction(Bitmap image)
    {
        var input = new Input { Image = image };
        var predictions = this.predictionEngine.Predict(input);
        var boundingBoxes = ParseOutputs(predictions.Result);
        var result = new ConcurrentBag<BoundingBoxes>();
        Parallel.For(0, boundingBoxes.Count, i =>
        {
            var box = boundingBoxes[i];
            SetBoundingBox(ref box);
            result.Add(new BoundingBoxes
            {
                Label = box.Description,
                X = box.Dimensions.X,
                Y = box.Dimensions.Y,
                Width = box.Dimensions.Width,
                Height = box.Dimensions.Height,
            });
        });

        Position?.Invoke(this, result.ToList());
    }

    private void SetBoundingBox(ref BoundingBox box)
    {
        box.Dimensions.X = Math.Max(box.Dimensions.X, 0);
        box.Dimensions.Y = Math.Max(box.Dimensions.Y, 0);
        box.Dimensions.Width = Math.Min(originalWidth - box.Dimensions.X, box.Dimensions.Width);
        box.Dimensions.Height = Math.Min(originalHeight - box.Dimensions.Y, box.Dimensions.Height);
        mapSize(ref box);
    }

    private void mapSize(ref BoundingBox box)
    {
        box.Dimensions.X = originalWidth * box.Dimensions.X / ImageSettings.imageWidth;
        box.Dimensions.Y = originalHeight * box.Dimensions.Y / ImageSettings.imageHeight;
        box.Dimensions.Width = originalWidth * box.Dimensions.Width / ImageSettings.imageWidth;
        box.Dimensions.Height = originalHeight * box.Dimensions.Height / ImageSettings.imageHeight;
    }
    private void LoadLabels()
    {
        var labels = File.ReadAllLines("labels.txt");
        foreach (var label in labels)
        {
            this.labelCache[label] = label;
        }
    }
    private List<BoundingBox> ParseOutputs(float[] modelOutput, float probabilityThreshold = .5f)
    {
        var boxes = new ConcurrentBag<BoundingBox>();
        var labels = this.labelCache.Values.ToArray();

        Parallel.For(0, rowCount, row =>
        {
            for (int column = 0; column < columnCount; column++)
            {
                for (int box = 0; box < boxAnchors.Length; box++)
                {
                    var channel = box * (labels.Length + featuresPerBox);
                    var boundingBoxPrediction = ExtractBoundingBoxPrediction(modelOutput, row, column, channel);
                    var mappedBoundingBox = MapBoundingBoxToCell(row, column, box, boundingBoxPrediction);
                    if (boundingBoxPrediction.Confidence < probabilityThreshold)
                        continue;

                    float[] classProbabilities = ExtractClassProbabilities(modelOutput, row, column, channel, boundingBoxPrediction.Confidence, labels);
                    var (topProbability, topIndex) = classProbabilities.Select((probability, index) => (Score: probability, Index: index)).Max();
                    if (topProbability < probabilityThreshold)
                        continue;

                    boxes.Add(new BoundingBox
                    {
                        Dimensions = mappedBoundingBox,
                        Confidence = topProbability,
                        Label = labels[topIndex]
                    });
                }
            }
        });

        return boxes.ToList();
    }
    private static BoundingBoxDimensions MapBoundingBoxToCell(int row, int column, int box, BoundingBoxPrediction boxDimensions)
    {
        const float cellWidth = ImageSettings.imageWidth / columnCount;
        const float cellHeight = ImageSettings.imageHeight / rowCount;
        var mappedBox = new BoundingBoxDimensions
        {
            X = (row + Sigmoid(boxDimensions.X)) * cellWidth,
            Y = (column + Sigmoid(boxDimensions.Y)) * cellHeight,
            Width = (float)Math.Exp(boxDimensions.Width) * cellWidth * boxAnchors[box].x,
            Height = (float)Math.Exp(boxDimensions.Height) * cellHeight * boxAnchors[box].y,
        };
        mappedBox.X -= mappedBox.Width / 2;
        mappedBox.Y -= mappedBox.Height / 2;
        return mappedBox;
    }
    private static BoundingBoxPrediction ExtractBoundingBoxPrediction(float[] modelOutput, int row, int column, int channel)
    {
        return new BoundingBoxPrediction
        {
            X = modelOutput[GetOffset(row, column, channel++)],
            Y = modelOutput[GetOffset(row, column, channel++)],
            Width = modelOutput[GetOffset(row, column, channel++)],
            Height = modelOutput[GetOffset(row, column, channel++)],
            Confidence = Sigmoid(modelOutput[GetOffset(row, column, channel++)])
        };
    }
    private static float[] ExtractClassProbabilities(float[] modelOutput, int row, int column, int channel, float confidence, string[] labels)
    {
        var classProbabilitiesOffset = channel + featuresPerBox;
        float[] classProbabilities = new float[labels.Length];
        for (int classProbability = 0; classProbability < labels.Length; classProbability++)
            classProbabilities[classProbability] = modelOutput[GetOffset(row, column, classProbability + classProbabilitiesOffset)];
        return Softmax(classProbabilities).Select(p => p * confidence).ToArray();
    }
    private static float Sigmoid(float value)
    {
        var k = (float)Math.Exp(value);
        return k / (1.0f + k);
    }
    private static float[] Softmax(float[] classProbabilities)
    {
        var max = classProbabilities.Max();
        var exp = classProbabilities.Select(v => Math.Exp(v - max));
        var sum = exp.Sum();
        return exp.Select(v => (float)v / (float)sum).ToArray();
    }
    private static int GetOffset(int row, int column, int channel)
    {
        const int channelStride = rowCount * columnCount;
        return (channel * channelStride) + (column * columnCount) + row;
    }
}
public class ImageSettings
{
    public const int imageHeight = 416;
    public const int imageWidth = 416;
}
public class Input
{
    [ImageType(ImageSettings.imageHeight, ImageSettings.imageWidth)]
    public Bitmap Image { get; set; }
}
public class Predictions
{
    [ColumnName("model_outputs0")]
    public float[] Result { get; set; }
}
class BoundingBoxPrediction : BoundingBoxDimensions
{
    public float Confidence { get; set; }
}
public class BoundingBox
{
    public BoundingBoxDimensions Dimensions { get; set; }
    public string Label { get; set; }
    public float Confidence { get; set; }
    public RectangleF Rect
    {
        get { return new RectangleF(Dimensions.X, Dimensions.Y, Dimensions.Width, Dimensions.Height); }
    }
    public Color BoxColor { get; set; }
    public string Description => $"{Label} ({(Confidence * 100).ToString("0")}%)";
    private static readonly Color[] classColors = new Color[]
    {
        Color.Khaki, Color.Fuchsia, Color.Silver, Color.RoyalBlue,
        Color.Green, Color.DarkOrange, Color.Purple, Color.Gold,
        Color.Red, Color.Aquamarine, Color.Lime, Color.AliceBlue,
        Color.Sienna, Color.Orchid, Color.Tan, Color.LightPink,
        Color.Yellow, Color.HotPink, Color.OliveDrab, Color.SandyBrown,
        Color.DarkTurquoise
    };
    public static Color GetColor(int index) => index < classColors.Length ? classColors[index] : classColors[index % classColors.Length];
}
public class BoundingBoxDimensions
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Height { get; set; }
    public float Width { get; set; }
}

public class BoundingBoxes
{
    public string Label { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }

}