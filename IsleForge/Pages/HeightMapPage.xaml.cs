using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using IsleForge.Helpers;

namespace IsleForge.Pages
{
    /// <summary>
    /// Interaction logic for HeightMapPage.xaml
    /// </summary>
    public partial class HeightMapPage : Page
    {
        #region top level variables

        private Canvas _heightMapCanvas;
        private WriteableBitmap _heightMapLayer;
        private Image _heightMapLayerImage;

        private Button _nextButton;
        private ProgressBar _progressBar;

        private float TOPHEIGHT = 32;
        private float MIDHEIGHT = 22;
        private float BASEHEIGHT = 12;

        private const int SMALLER_RADIUS = 20;
        private const int LARGER_RADIUS = 30;
        private const float SMOOTHING_FACTOR = 0.8f;

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
        private float[,] bottomAllOnes;

        private float[,] solvedMap;

        #endregion

        public HeightMapPage()
        {
            InitializeComponent();
            this.Loaded += HeightMapMaker_Loaded;
        }

        private void HeightMapMaker_Loaded(object sender, RoutedEventArgs e)
        {
            _heightMapCanvas = HelperExtensions.FindElementByTag<Canvas>(this, "HeightMapCanvas");

            baseLayer = MapDataStore.BaseLayer;
            midLayer = MapDataStore.MidLayer;
            topLayer = MapDataStore.TopLayer;
            edgeLayer = MapDataStore.EdgeLayer;
            footprint = MapDataStore.FootPrint;

            _progressBar = HelperExtensions.FindElementByTag<ProgressBar>(this, "HeightProgressBar");

            footprintMask = GetInverseMask(footprint);
            bottomAllOnes = GetBottom(footprint);

            MapDataStore.MaxHeightShare = TOPHEIGHT;
            MapDataStore.MidHeightShare = MIDHEIGHT;
            MapDataStore.LowHeightShare = BASEHEIGHT;

            //CreateTheHeightMap();
            //CreateTheHeightMapAsync();
            Task.Run(() => CreateTheHeightMap());
            //Task.Run(() => CreateTheHeightMap_WithTiming());
            //
        }

        #region main processessing

        private async Task CreateTheHeightMapAsync()
        {
            //CreateTheHeightMap();
        }

        private async Task CreateTheHeightMap_WithTiming()
        {
            //getting an idea of how much time these things take to base progress bar on
            var totalWatch = Stopwatch.StartNew();
            var stepWatch = Stopwatch.StartNew();

            void LogStep(string description)
            {
                stepWatch.Stop();
                Debug.WriteLine($"[{description}] {stepWatch.ElapsedMilliseconds} ms");
                stepWatch.Restart();
            }

            Debug.WriteLine("===== Starting detailed HeightMap timing =====");

            // Start
            stepWatch.Restart();

            var HeightBaseLayer = ConvertArrayToHeight(baseLayer, BASEHEIGHT);
            LogStep("Convert baseLayer");

            var HeightMidLayer = ConvertArrayToHeight(midLayer, MIDHEIGHT);
            LogStep("Convert midLayer");

            var HeightTopLayer = ConvertArrayToHeight(topLayer, TOPHEIGHT);
            LogStep("Convert topLayer");

            midBaseEdges = DetectLogicalEdges(baseLayer, midLayer, topLayer);
            LogStep("Detect midBaseEdges");

            topMidEdges = DetectEdgeBetweenAdjacentLayers(midLayer, topLayer);
            LogStep("Detect topMidEdges");

            bottomBaseEdges = DetectSobelEdges(baseLayer);
            LogStep("Detect bottomBaseEdges");

            topBaseEdges = DetectLogicalEdges(topLayer, baseLayer, midLayer);
            LogStep("Detect topBaseEdges");

            var sub1 = SubtractWithRadius(topBaseEdges, topMidEdges, 2);
            LogStep("Subtract topBaseEdges - topMidEdges");

            var sub2 = SubtractWithRadius(sub1, midBaseEdges, 2);
            LogStep("Subtract sub1 - midBaseEdges");

            var topBaseEdgesWork = sub2;
            var matchedTopBaseEdges = MatchEdgeLabelsByProximity(edgeLayer, topBaseEdgesWork);
            LogStep("Match topBaseEdges to proximity");

            var expandedTopBaseEdges = ExpandEdgeInfluenceClean(matchedTopBaseEdges, SMALLER_RADIUS, LARGER_RADIUS);
            LogStep("Expand topBaseEdges");

            var TopBaseBlendResult = BlendMaskOverwrite(baseLayer, topLayer, expandedTopBaseEdges, BASEHEIGHT, TOPHEIGHT);
            LogStep("Blend top base layers");

            var TopBaseSmoothed = SmoothVertices2D(TopBaseBlendResult, factor: SMOOTHING_FACTOR, iterations: 70);
            LogStep("Smooth TopBaseBlendResult");

            var matchedTopMidEdges = MatchEdgeLabelsByProximity(edgeLayer, topMidEdges);
            LogStep("Match topMidEdges to proximity");

            var expandedTopMidEdges = ExpandEdgeInfluenceClean(matchedTopMidEdges, SMALLER_RADIUS, LARGER_RADIUS);
            LogStep("Expand topMidEdges");

            var TopMidBlendResult = BlendMaskOverwrite(midLayer, topLayer, expandedTopMidEdges, MIDHEIGHT, TOPHEIGHT);
            LogStep("Blend top-mid layers");

            var TopMidSmoothed = SmoothVertices2D(TopMidBlendResult, factor: SMOOTHING_FACTOR, iterations: 70);
            LogStep("Smooth TopMidBlendResult");

            sub1 = SubtractWithRadius(midBaseEdges, topMidEdges, 2);
            LogStep("Subtract midBaseEdges - topMidEdges");

            var topMidEdgesWork = sub1;
            var matchedmidBaseEdges = MatchEdgeLabelsByProximity(edgeLayer, topMidEdgesWork);
            LogStep("Match midBaseEdges proximity");

            var expandedmidBaseEdges = ExpandEdgeInfluenceClean(matchedmidBaseEdges, SMALLER_RADIUS, LARGER_RADIUS);
            LogStep("Expand midBaseEdges");

            var midBaseBlendResult = BlendMaskOverwrite(baseLayer, midLayer, expandedmidBaseEdges, BASEHEIGHT, MIDHEIGHT);
            LogStep("Blend mid-base layers");

            var midBaseSmoothed = SmoothVertices2D(midBaseBlendResult, factor: SMOOTHING_FACTOR, iterations: 70);
            LogStep("Smooth midBaseBlendResult");

            var matchedBottomBaseEdges = MatchEdgeLabelsByProximity(edgeLayer, bottomBaseEdges);
            LogStep("Match bottomBaseEdges proximity");

            var expandedBottomBaseEdges = ExpandEdgeInfluenceClean(matchedBottomBaseEdges, (int)(SMALLER_RADIUS * 1.3f), (int)(LARGER_RADIUS * 1.3f));
            LogStep("Expand bottomBaseEdges");

            var BottomBaseBlendResult = BlendMaskOverwrite(bottomAllOnes, baseLayer, expandedBottomBaseEdges, 0f, BASEHEIGHT);
            LogStep("Blend bottom layers");

            var BottomBaseSmoothed = SmoothVertices2D(BottomBaseBlendResult, factor: (SMOOTHING_FACTOR * 0.8f), iterations: 230, ignoreZeroes: false);
            LogStep("Smooth BottomBaseBlendResult");

            var labeledBaseMap = CreateLabeledMap(HeightBaseLayer, "Base");
            LogStep("Create labeledBaseMap");

            var labeledMidLayer = CreateLabeledMap(HeightMidLayer, "Mid");
            LogStep("Create labeledMidLayer");

            var labeledTopLayer = CreateLabeledMap(HeightTopLayer, "Top");
            LogStep("Create labeledTopLayer");

            var labeledTopBaseSmoothed = CreateLabeledMap(TopBaseSmoothed, "ramp");
            LogStep("Create labeledTopBaseSmoothed");

            var labeledTopMidSmoothed = CreateLabeledMap(TopMidSmoothed, "ramp");
            LogStep("Create labeledTopMidSmoothed");

            var labeledmidBaseSmoothed = CreateLabeledMap(midBaseSmoothed, "ramp");
            LogStep("Create labeledmidBaseSmoothed");

            var labeledBottomBaseSmoothed = CreateLabeledMap(BottomBaseSmoothed, "beach");
            LogStep("Create labeledBottomBaseSmoothed");

            var solvedMapWithLabels = labeledBaseMap;
            solvedMapWithLabels = OverlayLabeledMaps(solvedMapWithLabels, labeledBottomBaseSmoothed);
            solvedMapWithLabels = OverlayLabeledMaps(solvedMapWithLabels, labeledMidLayer);
            solvedMapWithLabels = OverlayLabeledMaps(solvedMapWithLabels, labeledTopLayer);
            LogStep("Overlay labeled maps");

            float[,] LowerMask = SubtractLabeledMapsToFloatArray(labeledBaseMap, labeledBottomBaseSmoothed);
            LogStep("Create LowerMask");

            solvedMapWithLabels = SmartOverlayLabeledWithMask(solvedMapWithLabels, labeledTopBaseSmoothed, LowerMask);
            solvedMapWithLabels = SmartOverlayLabeledWithMask(solvedMapWithLabels, labeledTopMidSmoothed, LowerMask);
            solvedMapWithLabels = SmartOverlayLabeledWithMask(solvedMapWithLabels, labeledmidBaseSmoothed, LowerMask);
            LogStep("Smart overlay smooth ramps");

            RemoveNegativesFromLabeledMap(solvedMapWithLabels);
            LogStep("Remove negatives from final map");

            SmoothLabeledMap(solvedMapWithLabels, 0.4f, 8, ignoreZeroes: false);
            LogStep("Final smoothing of labeled map");

            MapDataStore.AnnotatedHeightMap = solvedMapWithLabels;
            LogStep("Save Annotated Map");

            Application.Current.Dispatcher.Invoke(() =>
            {
                CreateLabeledHeightmapLayer(solvedMapWithLabels);
            });
            LogStep("Render heightmap layer");

            totalWatch.Stop();
            Debug.WriteLine($"==== TOTAL HEIGHTMAP TIME: {totalWatch.ElapsedMilliseconds} ms ====");
        }


        private async Task CreateTheHeightMap()
        {
            if (_progressBar != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _progressBar.Visibility = Visibility.Visible;
                    _progressBar.Value = 0;
                });
            }

            void UpdateProgress(double percent)
            {
                if (_progressBar != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _progressBar.Value = percent;
                    });
                }                   
            }

            //get main layers
            var HeightBaseLayer = ConvertArrayToHeight(baseLayer, BASEHEIGHT);
            var HeightMidLayer = ConvertArrayToHeight(midLayer, MIDHEIGHT);
            var HeightTopLayer = ConvertArrayToHeight(topLayer, TOPHEIGHT);
            UpdateProgress(0.3);

            //get edges using edge detection
            midBaseEdges = DetectLogicalEdges(baseLayer, midLayer, topLayer);
            topMidEdges = DetectEdgeBetweenAdjacentLayers(midLayer, topLayer);
            bottomBaseEdges = DetectSobelEdges(baseLayer);
            topBaseEdges = DetectLogicalEdges(topLayer, baseLayer, midLayer);
            UpdateProgress(4.2);

            //now we need to computer different segments
            #region top base

            //creating the top base edges by subtracting things that don't count for them
            var sub1 = SubtractWithRadius(topBaseEdges, topMidEdges, 2);
            var sub2 = SubtractWithRadius(sub1, midBaseEdges, 2);
            var topBaseEdgesWork = sub2;

            //matching the edges to the drawn map
            var matchedTopBaseEdges = MatchEdgeLabelsByProximity(edgeLayer, topBaseEdgesWork);
            //expanding the edges
            var expandedTopBaseEdges = ExpandEdgeInfluenceClean(matchedTopBaseEdges, SMALLER_RADIUS, LARGER_RADIUS);
            //get the expanded area with both heights in it
            var TopBaseBlendResult = BlendMaskOverwrite(baseLayer, topLayer, expandedTopBaseEdges, BASEHEIGHT, TOPHEIGHT);
            UpdateProgress(7);
            //ExportLayerToText("blendResult.txt", TopBaseBlendResult);

            //smooth that area
            var TopBaseSmoothed = SmoothVertices2D(TopBaseBlendResult, factor: SMOOTHING_FACTOR, iterations: 70);
            UpdateProgress(14);
            //CreateALayerHeightmap(TopBaseSmoothed);
            #endregion

            #region topmid
            // match edges to user drawn edges
            var matchedTopMidEdges = MatchEdgeLabelsByProximity(edgeLayer, topMidEdges);
            // expand edge redius
            var expandedTopMidEdges = ExpandEdgeInfluenceClean(matchedTopMidEdges, SMALLER_RADIUS, LARGER_RADIUS);
            // create mask area both heights
            var TopMidBlendResult = BlendMaskOverwrite(midLayer, topLayer, expandedTopMidEdges, MIDHEIGHT, TOPHEIGHT);
            UpdateProgress(18);
            //smooth heights together
            var TopMidSmoothed = SmoothVertices2D(TopMidBlendResult, factor: SMOOTHING_FACTOR, iterations: 70);
            UpdateProgress(25);

            //CreateALayerHeightmap(TopMidSmoothed);
            //CreateALayer(topMidEdges, Colors.Gold);

            #endregion

            #region midbase
            sub1 = SubtractWithRadius(midBaseEdges, topMidEdges, 2);//topMidEdges
            var topMidEdgesWork = sub1;
            UpdateProgress(26);
            // match edges to user drawn edges
            var matchedmidBaseEdges = MatchEdgeLabelsByProximity(edgeLayer, topMidEdgesWork);
            UpdateProgress(29);
            // expand edge redius
            var expandedmidBaseEdges = ExpandEdgeInfluenceClean(matchedmidBaseEdges, SMALLER_RADIUS, LARGER_RADIUS);
            //expandedmidBaseEdges = SubtractWithRadius(expandedmidBaseEdges, footprint, 1); //now this seems to bleed over a little trying to fix that
            // create mask area both heights
            var midBaseBlendResult = BlendMaskOverwrite(baseLayer, midLayer, expandedmidBaseEdges, BASEHEIGHT, MIDHEIGHT);
            UpdateProgress(32);
            //smooth heights together
            var midBaseSmoothed = SmoothVertices2D(midBaseBlendResult, factor: SMOOTHING_FACTOR, iterations: 70);
            UpdateProgress(37);

            //CreateALayerHeightmap(midBaseSmoothed);
            //CreateALayer(expandedmidBaseEdges, Colors.Gold);

            #endregion

            #region basefloor
            //sub1 = SubtractWithRadius(midBaseEdges, topMidEdges, 2);//topMidEdges
            var bottomBaseEdgesWork = bottomBaseEdges;
            // match edges to user drawn edges
            var matchedBottomBaseEdges = MatchEdgeLabelsByProximity(edgeLayer, bottomBaseEdgesWork);
            UpdateProgress(50);
            // expand edge redius
            var expandedBottomBaseEdges = ExpandEdgeInfluenceClean(matchedBottomBaseEdges, (int)(SMALLER_RADIUS * 1.3f), (int)(LARGER_RADIUS *  1.3f));
            // create mask area both heights
            var BottomBaseBlendResult = BlendMaskOverwrite(bottomAllOnes, baseLayer, expandedBottomBaseEdges, 0f, BASEHEIGHT);
            UpdateProgress(52);
            //smooth heights together
            var BottomBaseSmoothed = SmoothVertices2D(BottomBaseBlendResult, factor: (SMOOTHING_FACTOR * 0.8f), iterations: 230, ignoreZeroes: false);
            UpdateProgress(70);
            //CreateALayerHeightmap(BottomBaseSmoothed);
            // CreateALayer(expandedBottomBaseEdges, Colors.Gold);

            //return;
            #endregion

            var labeledBaseMap = CreateLabeledMap(HeightBaseLayer, "Base");
            var labeledMidLayer = CreateLabeledMap(HeightMidLayer, "Mid");
            var labeledTopLayer = CreateLabeledMap(HeightTopLayer, "Top");
            var labeledTopBaseSmoothed = CreateLabeledMap(TopBaseSmoothed, "ramp");
            var labeledTopMidSmoothed = CreateLabeledMap(TopMidSmoothed, "ramp");
            UpdateProgress(80);
            var labeledmidBaseSmoothed = CreateLabeledMap(midBaseSmoothed, "ramp");
            UpdateProgress(81);
            var labeledBottomBaseSmoothed = CreateLabeledMap(BottomBaseSmoothed, "beach");
            UpdateProgress(82);

            var solvedMapWithLabels = labeledBaseMap;
            UpdateProgress(83);

            //ordering these is important so they don't overwrite weirdly
            solvedMapWithLabels = OverlayLabeledMaps(solvedMapWithLabels, labeledBottomBaseSmoothed);

            //the higher layers go on over it
            solvedMapWithLabels = OverlayLabeledMaps(solvedMapWithLabels, labeledMidLayer);
            solvedMapWithLabels = OverlayLabeledMaps(solvedMapWithLabels, labeledTopLayer);

            //maybe another weird mask
            //expandedBottomBaseEdges
            var expandedEdgeMasking = CreateLabeledMap(expandedBottomBaseEdges, "na");

            //making a new mask for the other smoother things
            float[,] LowerMask = SubtractLabeledMapsToFloatArray(labeledBaseMap, labeledBottomBaseSmoothed);//CombineLabeledMapsToFloatArray(labeledBottomBaseSmoothed, labeledBaseMap);
            UpdateProgress(84);

            //SubtractLabeledMapsToFloatArray

            solvedMapWithLabels = SmartOverlayLabeledWithMask(solvedMapWithLabels, labeledTopBaseSmoothed, LowerMask);
            solvedMapWithLabels = SmartOverlayLabeledWithMask(solvedMapWithLabels, labeledTopMidSmoothed, LowerMask);
            solvedMapWithLabels = SmartOverlayLabeledWithMask(solvedMapWithLabels, labeledmidBaseSmoothed, LowerMask);
            UpdateProgress(85);


            RemoveNegativesFromLabeledMap(solvedMapWithLabels);
            UpdateProgress(87);

            SmoothLabeledMap(solvedMapWithLabels, 0.4f, 8, ignoreZeroes: false);
            UpdateProgress(95);

            //SaveLabeledMapToText("solvedMapWithLabels.txt", solvedMapWithLabels);
            MapDataStore.AnnotatedHeightMap = solvedMapWithLabels;

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_progressBar != null)
                {
                    _progressBar.Value = 100;
                    _progressBar.Visibility = Visibility.Collapsed;               
                }
                    
                CreateLabeledHeightmapLayer(solvedMapWithLabels);

            });

            MapDataStore.MaxHeightShare = TOPHEIGHT;

        }

        #endregion

        #region basic mapping

        private void CreateALayerHeightmap(float[,] layer)
        {
            int width = layer.GetLength(0);
            int height = layer.GetLength(1);

            WriteableBitmap bmp = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);

            using (var context = bmp.GetBitmapContext())
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        float value = layer[x, y];

                        byte gray = (byte)Math.Max(0, Math.Min(255, (value / TOPHEIGHT) * 255));
                        Color c = (value < 0f)
                            ? Colors.Transparent
                            : Color.FromArgb(255, gray, gray, gray);

                        bmp.SetPixel(x, y, c);
                    }
                }
            }

            var image = new Image
            {
                Source = bmp,
                Width = bmp.PixelWidth,
                Height = bmp.PixelHeight,
                Stretch = Stretch.None,
                SnapsToDevicePixels = true,
                IsHitTestVisible = false
            };

            _heightMapCanvas.Children.Add(image);
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
                            //Debug.WriteLine("found a 2");
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

        #endregion

        #region expanded data type

        public void SmoothLabeledMap(
    LabeledValue[,] labeledMap,
    float factor = 0.75f,
    int iterations = 50,
    bool ignoreZeroes = true)
        {
            int width = labeledMap.GetLength(0);
            int height = labeledMap.GetLength(1);

            // Step 1: Extract values
            float[,] raw = new float[width, height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    raw[x, y] = labeledMap[x, y].Value;

            // Step 2: Smooth with your logic
            float[,] smoothed = SmoothVertices2D(raw, factor, iterations, ignoreZeroes);
            //SmoothVertices2D(solvedMap, factor: 0.4f, iterations: 3, ignoreZeroes: false);

            // Step 3: Re-apply only .Value — preserve label
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float val = smoothed[x, y];
                    labeledMap[x, y].Value = val;
                    // label remains unchanged
                }
            }
        }

        public void RemoveNegativesFromLabeledMap(LabeledValue[,] map)
        {
            int width = map.GetLength(0);
            int height = map.GetLength(1);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (map[x, y].Value < 0f)
                    {
                        map[x, y].Value = 0f;
                        map[x, y].Label = "None"; // or "Masked" or whatever placeholder
                    }
                }
            }
        }


        public static LabeledValue[,] CreateLabeledMap(float[,] source, string label)
        {
            int width = source.GetLength(0);
            int height = source.GetLength(1);
            var result = new LabeledValue[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float val = source[x, y];
                    if (val > 0f)
                        result[x, y] = new LabeledValue(val, label);
                    else
                        result[x, y] = new LabeledValue(val, "None");
                }
            }

            return result;
        }

        public static LabeledValue[,] OverlayLabeledMaps(LabeledValue[,] mapA, LabeledValue[,] mapB)
        {
            int width = mapA.GetLength(0);
            int height = mapA.GetLength(1);
            var result = new LabeledValue[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    result[x, y] = (mapB[x, y].Value > 0f)
                        ? mapB[x, y]
                        : mapA[x, y];
                }
            }

            return result;
        }

        public static LabeledValue[,] SmartOverlayLabeledWithMask(
    LabeledValue[,] mapA,
    LabeledValue[,] mapB,
    float[,] mask)
        {
            int width = mapA.GetLength(0);
            int height = mapA.GetLength(1);
            var result = new LabeledValue[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (mask[x, y] > 0f && mapB[x, y].Value >= 0f)
                    {
                        result[x, y] = mapB[x, y];
                    }
                    else
                    {
                        result[x, y] = mapA[x, y];
                    }
                }
            }

            return result;
        }

        public void CreateLabeledHeightmapLayer(LabeledValue[,] labeledMap)
        {
            int width = labeledMap.GetLength(0);
            int height = labeledMap.GetLength(1);
            float[,] valuesOnly = new float[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    valuesOnly[x, y] = labeledMap[x, y].Value;
                }
            }

            CreateALayerHeightmap(valuesOnly);
        }



        public static void SaveLabeledMapToText(string path, LabeledValue[,] map)
        {
            int width = map.GetLength(0);
            int height = map.GetLength(1);
            var sb = new StringBuilder();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    sb.Append(map[x, y].ToString()).Append(" ");
                }
                sb.AppendLine();
            }

            System.IO.File.WriteAllText(path, sb.ToString());
        }

        public static LabeledValue[,] LoadLabeledMapFromText(string path)
        {
            var lines = System.IO.File.ReadAllLines(path);
            int height = lines.Length;
            int width = lines[0].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;

            var result = new LabeledValue[width, height];

            for (int y = 0; y < height; y++)
            {
                var tokens = lines[y].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                for (int x = 0; x < width; x++)
                {
                    var parts = tokens[x].Split('|');
                    float value = float.Parse(parts[0]);
                    string label = parts.Length > 1 ? parts[1] : "";
                    result[x, y] = new LabeledValue(value, label);
                }
            }

            return result;
        }



        #endregion

        #region edge work

        private float[,] DetectEdgeBetweenAdjacentLayers(float[,] layerA, float[,] layerB)
        {
            int width = layerA.GetLength(0);
            int height = layerA.GetLength(1);
            float[,] result = new float[width, height];

            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    bool aHere = layerA[x, y] == 1f;
                    bool bHere = layerB[x, y] == 1f;

                    // Only check if we're not overlapping
                    if (!aHere && !bHere)
                        continue;

                    // Check 8 neighbors
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0)
                                continue;

                            int nx = x + dx;
                            int ny = y + dy;

                            if (layerA[nx, ny] == 1f && layerB[x, y] == 1f)
                            {
                                result[x, y] = 1f;
                                break;
                            }

                            if (layerB[nx, ny] == 1f && layerA[x, y] == 1f)
                            {
                                result[x, y] = 1f;
                                break;
                            }
                        }
                    }
                }
            }

            return result;
        }


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

        #region map combine operations

        private float[,] OverlayMaps(float[,] mapA, float[,] mapB)
        {
            int width = mapA.GetLength(0);
            int height = mapA.GetLength(1);
            float[,] result = new float[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    //result[x, y] = (mapB[x, y] != 0f) ? mapB[x, y] : mapA[x, y];
                    result[x, y] = (mapB[x, y] > 0f) ? mapB[x, y] : mapA[x, y];
                }
            }

            return result;
        }

        private float[,] SmartOverlayWithMask(float[,] mapA, float[,] mapB, float[,] mask)
        {
            int width = mapA.GetLength(0);
            int height = mapA.GetLength(1);
            float[,] result = new float[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (mask[x, y] > 0f && mapB[x, y] >= 0f)
                    {
                        result[x, y] = mapB[x, y];
                    }
                    else
                    {
                        result[x, y] = mapA[x, y];
                    }
                }
            }

            return result;
        }

        public float[,] CombineLabeledMapsToFloatArray(LabeledValue[,] mapA, LabeledValue[,] mapB)
        {
            int width = mapA.GetLength(0);
            int height = mapA.GetLength(1);

            float[,] result = new float[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float a = mapA[x, y].Value;
                    float b = mapB[x, y].Value;

                    if (a > 0)
                        result[x, y] = 1;
                    else if (b > 0)
                        result[x, y] = 1;
                    else
                        result[x, y] = 0f;
                }
            }

            return result;
        }

        public float[,] SubtractLabeledMapsToFloatArray(LabeledValue[,] mapA, LabeledValue[,] mapB)
        {
            int width = mapA.GetLength(0);
            int height = mapA.GetLength(1);

            float[,] result = new float[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float a = mapA[x, y].Value;
                    float b = mapB[x, y].Value;

                    if (b > 0)
                    {
                        //taking a tiny bit of a threshold here
                        float tinyMargin = BASEHEIGHT * 0.02f;
                        float HighBoundsForLowMask = BASEHEIGHT - tinyMargin;
                        //this way, the mask for the overlays does not right over the area where it goes into the sea
                        //but it also does not always end right before the edge.
                        //I might increase the margin as needed

                        if (b < HighBoundsForLowMask) //this is kinda important, that I don't want to erase this part
                        {
                            result[x, y] = 0;
                        }
                        else
                        {
                            result[x, y] = 1;
                        }                           
                    }                        
                    else if (a > 0)
                        result[x, y] = 1;
                    else
                        result[x, y] = 0f;
                }
            }

            return result;
        }

        public LabeledValue[,] SimpleCombineLabeledMaps(LabeledValue[,] mapA, LabeledValue[,] mapB)
        {
            int width = mapA.GetLength(0);
            int height = mapA.GetLength(1);

            LabeledValue[,] result = new LabeledValue[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var a = mapA[x, y];
                    var b = mapB[x, y];

                    // Use value from A unless it's <= 0 and B has a positive value
                    if (a.Value > 0)
                    {
                        result[x, y] = a;
                    }
                    else if (b.Value > 0)
                    {
                        result[x, y] = b;
                    }
                    else
                    {
                        result[x, y] = a; // keep empty (or zero) value from A
                    }
                }
            }

            return result;
        }



        private float[,] SubtractWithRadius(float[,] mapA, float[,] mapB, int radius)
        {
            int width = mapA.GetLength(0);
            int height = mapA.GetLength(1);
            float[,] result = new float[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (mapA[x, y] == 1f)
                    {
                        bool foundMatch = false;

                        // Check neighbors within radius
                        for (int dy = -radius; dy <= radius && !foundMatch; dy++)
                        {
                            for (int dx = -radius; dx <= radius && !foundMatch; dx++)
                            {
                                int nx = x + dx;
                                int ny = y + dy;

                                if (nx >= 0 && ny >= 0 && nx < width && ny < height)
                                {
                                    if (dx * dx + dy * dy <= radius * radius)
                                    {
                                        if (mapB[nx, ny] == 1f)
                                        {
                                            foundMatch = true;
                                        }
                                    }
                                }
                            }
                        }

                        result[x, y] = foundMatch ? 0f : 1f;
                    }
                }
            }

            return result;
        }

        private float[,] ConvertArrayToHeight(float[,] map, float heightValue)
        {
            int width = map.GetLength(0);
            int height = map.GetLength(1);
            float[,] result = new float[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (map[x, y] > 0f)
                    {
                        result[x, y] = heightValue;
                    }
                    else
                    {
                        result[x, y] = -10;
                    }
                }
            }

            return result;
        }

        private float[,] RemoveMaskingNegatives(float[,] map)
        {
            int width = map.GetLength(0);
            int height = map.GetLength(1);
            float[,] result = new float[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (map[x, y] < 0f)
                    {
                        result[x, y] = 0;
                    }
                    else
                    {
                        result[x, y] = map[x, y];
                    }

                }
            }

            return result;
        }



        #endregion


        #region edge expanders

        private float[,] ExpandEdgeInfluenceClean(float[,] labeledEdges, int radius2 = 10, int radius3 = 18)
        {
            int width = labeledEdges.GetLength(0);
            int height = labeledEdges.GetLength(1);
            float[,] expanded = new float[width, height]; // Start empty — no 1s copied

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float val = labeledEdges[x, y];

                    if (val == 2f)
                        DrawCircularRadius(expanded, x, y, width, height, radius2, 2f);
                    else if (val == 3f)
                        DrawCircularRadius(expanded, x, y, width, height, radius3, 3f);
                }
            }

            //OverlayEdgeOnExpanded(expanded, labeledEdges);

            return expanded;
        }

        private void OverlayEdgeOnExpanded(float[,] expanded, float[,] originalEdges)
        {
            int width = expanded.GetLength(0);
            int height = expanded.GetLength(1);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (expanded[x, y] == 0f && originalEdges[x, y] == 1f)
                    {
                        expanded[x, y] = 1f;
                    }
                }
            }
        }

        private float[,] ExpandEdgeInfluence(float[,] labeledEdges, int radius2 = 6, int radius3 = 12)
        {
            int width = labeledEdges.GetLength(0);
            int height = labeledEdges.GetLength(1);

            float[,] expanded = (float[,])labeledEdges.Clone();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float val = labeledEdges[x, y];

                    if (val == 2f)
                        DrawCircularRadius(expanded, x, y, width, height, radius2, 2f);
                    else if (val == 3f)
                        DrawCircularRadius(expanded, x, y, width, height, radius3, 3f);
                }
            }

            return expanded;
        }

        private void DrawCircularRadius(float[,] map, int centerX, int centerY, int width, int height, int radius, float value)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int nx = centerX + dx;
                    int ny = centerY + dy;

                    if (nx >= 0 && ny >= 0 && nx < width && ny < height)
                    {
                        if (dx * dx + dy * dy <= radius * radius)
                        {
                            if (map[nx, ny] == 0f)
                            {
                                map[nx, ny] = value;
                            }
                        }
                    }
                }
            }
        }


        #endregion

        #region smoothing heights

        private float[,] SmoothVertices2D(float[,] input, float factor = 0.75f, int iterations = 50, Boolean ignoreZeroes = true)
        {
            int width = input.GetLength(0);
            int height = input.GetLength(1);

            float min = float.MaxValue;
            float max = float.MinValue;

            // Step 0: Determine valid min and max (ignore negatives)
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float val = input[x, y];
                    if (ignoreZeroes == true)
                    {
                        if (val > 0f)
                        {
                            if (val < min) min = val;
                            if (val > max) max = val;
                        }
                    }
                    else
                    {
                        if (val >= 0f)
                        {
                            if (val < min) min = val;
                            if (val > max) max = val;
                        }
                    }

                }
            }

            Debug.WriteLine($"Min: {min}");
            Debug.WriteLine($"Max: {max}");

            float[,] current = (float[,])input.Clone();
            float[,] next = new float[width, height];

            for (int iter = 0; iter < iterations; iter++)
            {
                for (int y = 1; y < height - 1; y++)
                {
                    for (int x = 1; x < width - 1; x++)
                    {
                        if (current[x, y] < 0f)
                        {
                            next[x, y] = current[x, y]; // preserve -10 regions
                            continue;
                        }

                        float sum = 0f;
                        int count = 0;

                        for (int dy = -1; dy <= 1; dy++)
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                if (dx == 0 && dy == 0) continue;

                                float neighbor = current[x + dx, y + dy];
                                if (neighbor >= 0f)
                                {
                                    sum += neighbor;
                                    count++;
                                }
                            }
                        }

                        if (count > 0)
                        {
                            float avg = sum / count;
                            float smoothed = (1f - factor) * current[x, y] + factor * avg;

                            // Clamp within valid min/max
                            next[x, y] = Math.Max(min, Math.Min(max, smoothed));
                        }
                        else
                        {
                            next[x, y] = current[x, y];
                        }
                    }
                }

                // Swap buffers
                float[,] temp = current;
                current = next;
                next = temp;
            }

            return current;
        }

        private float[,] BlendMaskOverwrite(float[,] mapA, float[,] mapB, float[,] mask, float valueA, float valueB)
        {
            int countA = 0, countB = 0;
            int width = mask.GetLength(0);
            int height = mask.GetLength(1);
            float[,] result = new float[width, height];

            // Step 0: bad values
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (mask[x, y] < 1f)
                    {
                        result[x, y] = -10;
                    }
                }
            }

            // Step 1: Base layer (valueA)
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (mask[x, y] > 1f && mapA[x, y] == 1f)
                    {
                        result[x, y] = valueA;
                        countA++;
                    }
                }
            }

            // Step 2: Top layer (valueB) — overwrite
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (mask[x, y] > 1f && mapB[x, y] == 1f)
                    {
                        result[x, y] = valueB;
                        countB++;
                    }
                }
            }

            Debug.WriteLine($"Blend result: valueA written {countA} times, valueB written {countB} times");
            return result;
        }

        #endregion

        #region mask helpers

        private float[,] GetBottom(float[,] input)
        {
            int width = input.GetLength(0);
            int height = input.GetLength(1);
            float[,] output = new float[width, height];

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    output[x, y] = 1f;

            return output;
        }

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

        #region back and next

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                    $"Are you sure you want to return to the previous page? your progress will not be saved",
                    "Return to previous page?",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

            if (result == MessageBoxResult.No)
            {
                return; // User said NO — cancel
            }

           // SavePageStateBeforeLeaving();

            if (this.NavigationService.CanGoBack)
            {
                this.NavigationService.GoBack();
            }
                
        }
        private void Next_Click(object sender, RoutedEventArgs e)
        {
            //Debug.WriteLine($"steps done: {EdgeChangesMade}");
            this.NavigationService?.Navigate(new MeshMakerPage());         
        }

        #endregion
    }
}
