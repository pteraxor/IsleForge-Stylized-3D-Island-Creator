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
using System.Windows.Media.Imaging;
using Prototyping.Tools;
using System.Diagnostics;
using Prototyping.Helpers;

namespace Prototyping.Pages
{
    /// <summary>
    /// Interaction logic for HeightMapMaker.xaml
    /// </summary>
    public partial class HeightMapMaker : Page
    {

        private Canvas _heightMapCanvas;
        private WriteableBitmap _heightMapLayer;
        private Image _heightMapLayerImage;

        private float TOPHEIGHT = 100;
        private float MIDHEIGHT = 100;
        private float BASEHEIGHT = 100;

        private float[,] baseLayer = MapDataStore.BaseLayer;
        private float[,] midLayer = MapDataStore.MidLayer;
        private float[,] topLayer = MapDataStore.TopLayer;
        private float[,] edgeLayer = MapDataStore.EdgeLayer;
        private float[,] footprint = MapDataStore.FootPrint;

        private float[,] footprintMask;

        private float[,] topMidEdges;// TopMidEdges;
        private float[,] topBaseEdges;// TopBaseEdges;
        private float[,] midBaseEdges;// MidBaseEdges;

        private float[,] bottomBaseEdges;

        private float[,] solvedMap;

        public HeightMapMaker()
        {
            InitializeComponent();
            this.Loaded += HeightMapMaker_Loaded;
        }

        private void TESTINGLOAD()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            baseLayer = LoadFloatArrayFromFile(System.IO.Path.Combine(baseDir, "BaseLayer.txt"));
            midLayer = LoadFloatArrayFromFile(System.IO.Path.Combine(baseDir, "MidLayer.txt"));
            topLayer = LoadFloatArrayFromFile(System.IO.Path.Combine(baseDir, "TopLayer.txt"));
            edgeLayer = LoadFloatArrayFromFile(System.IO.Path.Combine(baseDir, "EdgeLayer.txt"));
            footprint = LoadFloatArrayFromFile(System.IO.Path.Combine(baseDir, "Footprint.txt"));

            //footprintMask = GetInverseMask(footprint);

            Debug.WriteLine("baseLayer count: " + CountOnes(baseLayer));
            Debug.WriteLine("midLayer count: " + CountOnes(midLayer));
            Debug.WriteLine("topLayer count: " + CountOnes(topLayer));
            //Debug.WriteLine("Mask count: " + CountOnes(footprintMask)); // should be sparse

            Debug.WriteLine("Test data loaded.");
        }

        private void HeightMapMaker_Loaded(object sender, RoutedEventArgs e)
        {
            _heightMapCanvas = HelperExtensions.FindElementByTag<Canvas>(this, "HeightMapCanvas");

            TESTINGLOAD();

            footprintMask = GetInverseMask(footprint);
        }

        private void ProcessMap_Click(object sender, RoutedEventArgs e)
        {

            //topMidEdges = DetectEdges(topLayer, midLayer, footprintMask);
            midBaseEdges = DetectLogicalEdges(baseLayer, midLayer, topLayer);
            //ExportLayerToText("midBaseEdges.txt", midBaseEdges);
            Debug.WriteLine("made it past this");
            CreateALayer(midBaseEdges, Colors.Aqua);

            topMidEdges = DetectLogicalEdges(topLayer, midLayer, baseLayer);
            
            CreateALayer(topMidEdges, Colors.Purple);

            topBaseEdges = DetectLogicalEdges(topLayer, baseLayer, midLayer);
            var matchedtopBaseEdges = MatchEdgeLabelsByProximity(edgeLayer, topBaseEdges);
            //ExportLayerToText("matchedtopBaseEdges.txt", matchedtopBaseEdges);
            CreateALayer(matchedtopBaseEdges, Colors.Indigo);

            bottomBaseEdges = DetectSobelEdges(baseLayer);
            var matchedbottomBaseEdges = MatchEdgeLabelsByProximity(edgeLayer, bottomBaseEdges);
            //CreateALayer(matchedbottomBaseEdges, Colors.Gold);
            //CreateALayer(bottomBaseEdges, Colors.Gold);

            //topBaseEdges

            /*
            // Render the edge map as a bitmap
            var edgeBitmap = RenderEdgeImage(midBaseEdges, Colors.Red);

            // Create an Image control to show it
            var image = new Image
            {
                Source = edgeBitmap,
                Width = edgeBitmap.PixelWidth,
                Height = edgeBitmap.PixelHeight,
                Stretch = Stretch.None,
                SnapsToDevicePixels = true,
                IsHitTestVisible = false
            };

            // Clear canvas first if needed
            //_heightMapCanvas.Children.Clear();
            _heightMapCanvas.Children.Add(image);
            */
        }

        private void CreateALayer(float[,] sentEdges, Color sentColor)
        {
            var edgeBitmap = RenderEdgeImage(sentEdges, sentColor);

            // Create an Image control to show it
            var image = new Image
            {
                Source = edgeBitmap,
                Width = edgeBitmap.PixelWidth,
                Height = edgeBitmap.PixelHeight,
                Stretch = Stretch.None,
                SnapsToDevicePixels = true,
                IsHitTestVisible = false
            };

            // Clear canvas first if needed
            //_heightMapCanvas.Children.Clear();
            _heightMapCanvas.Children.Add(image);
        }

        //footprintMask = GetInverseMask(footprint);

        #region testing helpers

        private void ExportLayerToText(string filename, float[,] data)
        {
            var sb = new StringBuilder();

            int width = data.GetLength(0);
            int height = data.GetLength(1);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    sb.Append(data[x, y].ToString("0")).Append(" ");
                }
                sb.AppendLine();
            }

            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filename);
            System.IO.File.WriteAllText(path, sb.ToString());

            Debug.WriteLine($"Exported layer to {path}");
        }



        private int CountOnes(float[,] layer)
        {
            int count = 0;
            for (int y = 0; y < layer.GetLength(1); y++)
                for (int x = 0; x < layer.GetLength(0); x++)
                    if (layer[x, y] == 1f)
                        count++;
            return count;
        }


        private float[,] LoadFloatArrayFromFile(string filePath)
        {
            var lines = System.IO.File.ReadAllLines(filePath);
            int height = lines.Length;
            int width = lines[0].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;

            float[,] result = new float[width, height];

            for (int y = 0; y < height; y++)
            {
                var tokens = lines[y].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                for (int x = 0; x < width; x++)
                {
                    if (float.TryParse(tokens[x], out float val))
                        result[x, y] = val;
                    else
                        result[x, y] = 0f;
                }
            }

            return result;
        }




        #endregion

        #region mask helpers

        private float[,] GetInverseMask(float[,] input)
        {
            int width = input.GetLength(0);
            int height = input.GetLength(1);
            float[,] output = new float[width, height];

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    output[x, y] = input[x, y] == 0f ? 1f : 0f;

            return output;
        }

        #endregion

        #region edge work

        private float[,] DetectLogicalEdges(float[,] layerA, float[,] layerB, float[,] excludeLayer)
        {
            int width = layerA.GetLength(0);
            int height = layerA.GetLength(1);

            float[,] intersection = new float[width, height];
            float[,] union = new float[width, height];

            // Step 1: Compute intersection & union
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    intersection[x, y] = (layerA[x, y] == 1f && layerB[x, y] == 1f) ? 1f : 0f;
                    union[x, y] = (layerA[x, y] == 1f || layerB[x, y] == 1f) ? 1f : 0f;
                }
            }

            // Step 2: Apply Sobel edge detection
            float[,] intersectionEdges = ApplySobelEdgeDetectionFloat(intersection, width, height);
            float[,] unionEdges = ApplySobelEdgeDetectionFloat(union, width, height);

            // Step 3: Subtract union edges from intersection edges
            float[,] finalEdges = new float[width, height];
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    if (intersectionEdges[x, y] == 1f && unionEdges[x, y] == 0f)
                    {
                        finalEdges[x, y] = 1f;
                    }
                }
            }

            // Step 4: Remove overlaps with excludeLayer
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    if (excludeLayer[x, y] == 1f)
                    {
                        finalEdges[x, y] = 0f;
                    }
                }
            }

            return finalEdges;
        }


        private float[,] ApplySobelEdgeDetectionFloat(float[,] map, int width, int height)
        {
            float[,] edgeMap = new float[width, height];

            int[,] sobelX = {
        { -1, 0, 1 },
        { -2, 0, 2 },
        { -1, 0, 1 }
    };

            int[,] sobelY = {
        { -1, -2, -1 },
        {  0,  0,  0 },
        {  1,  2,  1 }
    };

            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    float gradientX = 0f, gradientY = 0f;

                    for (int j = -1; j <= 1; j++)
                    {
                        for (int i = -1; i <= 1; i++)
                        {
                            float value = map[x + i, y + j];
                            gradientX += value * sobelX[j + 1, i + 1];
                            gradientY += value * sobelY[j + 1, i + 1];
                        }
                    }

                    float gradient = (float)Math.Sqrt((gradientX * gradientX) + (gradientY * gradientY));
                    edgeMap[x, y] = (gradient > 0) ? 1f : 0f; // Binary edge
                }
            }

            return edgeMap;
        }

        private float[,] DetectSobelEdges(float[,] layer)
        {
            int width = layer.GetLength(0);
            int height = layer.GetLength(1);
            float[,] edgeMap = new float[width, height];

            int[,] sobelX = {
        { -1, 0, 1 },
        { -2, 0, 2 },
        { -1, 0, 1 }
    };

            int[,] sobelY = {
        { -1, -2, -1 },
        {  0,  0,  0 },
        {  1,  2,  1 }
    };

            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    float gx = 0f, gy = 0f;

                    for (int j = -1; j <= 1; j++)
                    {
                        for (int i = -1; i <= 1; i++)
                        {
                            float val = layer[x + i, y + j];
                            gx += sobelX[j + 1, i + 1] * val;
                            gy += sobelY[j + 1, i + 1] * val;
                        }
                    }

                    float mag = (float)Math.Sqrt(gx * gx + gy * gy);
                    edgeMap[x, y] = mag > 0f ? 1f : 0f;
                }
            }

            return edgeMap;
        }

        private float[,] MatchEdgeLabelsByProximity(float[,] sourceLabeledEdges, float[,] targetEdges)
        {
            int width = targetEdges.GetLength(0);
            int height = targetEdges.GetLength(1);

            float[,] result = new float[width, height];

            // Step 1: Collect labeled edge points from source map
            var labeledPoints = new List<LabeledPoint>();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float label = sourceLabeledEdges[x, y];
                    if (label == 1f || label == 2f || label == 3f)
                    {
                        labeledPoints.Add(new LabeledPoint { X = x, Y = y, Label = label });
                    }
                }
            }

            // Step 2: For each target edge point, find the closest source edge
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (targetEdges[x, y] != 1f)
                        continue;

                    float minDistSq = float.MaxValue;
                    float nearestLabel = 0f;

                    foreach (var lp in labeledPoints)
                    {
                        float dx = lp.X - x;
                        float dy = lp.Y - y;
                        float distSq = dx * dx + dy * dy;

                        if (distSq < minDistSq)
                        {
                            minDistSq = distSq;
                            nearestLabel = lp.Label;
                        }
                    }

                    result[x, y] = nearestLabel;
                }
            }

            return result;
        }

        
        private class LabeledPoint
        {
            public int X;
            public int Y;
            public float Label;
        }



        #endregion

        #region debugging visuals
        private Color AdjustBrightnessAdditive(Color color, float brightnessDelta)
        {
            double h, s, v;
            RgbToHsv(color, out h, out s, out v);

            v += brightnessDelta;
            v = Math.Max(0, Math.Min(1, v)); // Clamp between 0–1

            return HsvToRgb(h, s, v);
        }


        private void RgbToHsv(Color color, out double h, out double s, out double v)
        {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;

            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;

            h = 0;
            if (delta > 0)
            {
                if (max == r)
                    h = 60 * (((g - b) / delta + 6) % 6);
                else if (max == g)
                    h = 60 * (((b - r) / delta) + 2);
                else if (max == b)
                    h = 60 * (((r - g) / delta) + 4);
            }

            s = (max == 0) ? 0 : delta / max;
            v = max;
        }

        private Color HsvToRgb(double h, double s, double v)
        {
            double c = v * s;
            double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
            double m = v - c;

            double r = 0, g = 0, b = 0;

            if (h < 60)
            {
                r = c; g = x; b = 0;
            }
            else if (h < 120)
            {
                r = x; g = c; b = 0;
            }
            else if (h < 180)
            {
                r = 0; g = c; b = x;
            }
            else if (h < 240)
            {
                r = 0; g = x; b = c;
            }
            else if (h < 300)
            {
                r = x; g = 0; b = c;
            }
            else
            {
                r = c; g = 0; b = x;
            }

            byte R = (byte)(Math.Round((r + m) * 255));
            byte G = (byte)(Math.Round((g + m) * 255));
            byte B = (byte)(Math.Round((b + m) * 255));

            return Color.FromRgb(R, G, B);
        }



        private WriteableBitmap RenderEdgeImage(float[,] edgeMap, Color color)
        {
            int width = edgeMap.GetLength(0);
            int height = edgeMap.GetLength(1);

            WriteableBitmap bmp = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);

            using (var context = bmp.GetBitmapContext())
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (edgeMap[x, y] == 1f)
                        {
                            bmp.SetPixel(x, y, color);
                        }
                        else if (edgeMap[x, y] == 2f)
                        {
                            Debug.WriteLine("found a 2");
                            Color adjusted = AdjustBrightnessAdditive(color, .5f);
                            bmp.SetPixel(x, y, adjusted);
                            //bmp.SetPixel(x, y, color);
                        }
                        else if (edgeMap[x, y] == 3f)
                        {
                            Color adjusted = AdjustBrightnessAdditive(color, -0.5f);
                            bmp.SetPixel(x, y, adjusted);
                            //bmp.SetPixel(x, y, color);
                        }
                        else
                        {
                            bmp.SetPixel(x, y, Colors.Transparent);
                        }
                            
                    }
                }
            }

            return bmp;
        }


        #endregion

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            //might add the option to go back
        }
        private void Next_Click(object sender, RoutedEventArgs e)
        {
            //for when it's time to to the next step. need to consider how to check for when it's okay to do so
        }

    }
}
