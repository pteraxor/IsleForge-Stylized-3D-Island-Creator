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
using IsleForge.DrawingTools;
using IsleForge.Helpers;

namespace IsleForge.Pages
{
    /// <summary>
    /// Interaction logic for EdgeEditingPage.xaml
    /// </summary>
    public partial class EdgeEditingPage : Page
    {
        private Canvas _baseCanvas;
        private Canvas _drawCanvas;
        private WriteableBitmap _baseLayer;
        private WriteableBitmap _editLayer;
        private Image _editLayerImage;

        private Button _nextButton;
        private ProgressBar _progressBar;

        private IDrawingTool _activeTool;

        private int _drawingToolSize = 10; //a backup default size

        private Stack<WriteableBitmap> _undoStack = new Stack<WriteableBitmap>();
        private Stack<WriteableBitmap> _redoStack = new Stack<WriteableBitmap>();
        private const int MaxHistory = 5;

        private string _drawingMode = "Freehand";

        private int _currentEdgeStyle = 1;
        private readonly Color[] _edgeColors = {
            Colors.Black,
            Color.FromRgb(250, 140, 50), //an orange that stands out, for the blended edge
            Color.FromRgb(100, 70, 180) //a purple that stands out for the other edge(might not be able to implement effectively)
        };
        private Func<Point, bool> _drawingMask;

        private HashSet<Point> DetectedEdges; //this will be for the edges, done numerically

        private const int TargetResolution = 600; //for the exporting of the data
        private bool CANDRAW = false;
        private int EdgeChangesMade = 0;

        public EdgeEditingPage()
        {
            InitializeComponent();
            this.Loaded += EdgeEditingPage_Loaded;
        }

        private void EdgeEditingPage_Loaded(object sender, RoutedEventArgs e)
        {
            _baseCanvas = HelperExtensions.FindElementByTag<Canvas>(this, "BasemapCanvas");
            _drawCanvas = HelperExtensions.FindElementByTag<Canvas>(this, "DrawCanvas");
            _baseLayer = BitmapManager.Get("BaseLayer");

            _nextButton = HelperExtensions.FindElementByTag<Button>(this, "NextBtn");

            _progressBar = HelperExtensions.FindElementByTag<ProgressBar>(this, "EdgeProgressBar");

            if (_baseCanvas != null && _baseLayer != null)
            {
                var image = new Image
                {
                    Source = _baseLayer,
                    Width = _baseLayer.PixelWidth,
                    Height = _baseLayer.PixelHeight,
                    Stretch = Stretch.None,
                    SnapsToDevicePixels = true
                };

                _baseCanvas.Children.Add(image);

                if (_baseLayer != null)
                {
                    _editLayer = new WriteableBitmap(
                        _baseLayer.PixelWidth,
                        _baseLayer.PixelHeight,
                        96, 96,
                        PixelFormats.Bgra32,
                        null);
                }

                Debug.WriteLine("BaseLayer successfully added to canvas.");
            }

            ProcessEdgesAsync(); //at the end, we can do this

        }

        #region button behavior

        private void ProcessMap_Click(object sender, RoutedEventArgs e)
        {
            ProcessEdgesAsync();
        }

        #endregion

        #region drawing tool prep

        private async Task ProcessEdgesAsync()
        {
            if (MapDataStore.IntermediateMap == null)
            {
                Debug.WriteLine("bad map");
                return;
            }

            var progress = new Progress<double>(value =>
            {
                _progressBar.Value = value * 100;
            });

            var edgePoints = await Task.Run(() =>
                DetectRegionEdges(MapDataStore.IntermediateMap, edgeThickness: 1, progress)
            );

            DetectedEdges = edgePoints;
            //a test to see what it does
            Debug.WriteLine($"Detected {edgePoints.Count} edge points.");


            if (DetectedEdges != null)
            {
                var edgeOverlay = CreateEdgeBitmap(_baseLayer.PixelWidth, _baseLayer.PixelHeight, DetectedEdges);

                // Copy black edge pixels from overlay into _editLayer
                using (edgeOverlay.GetBitmapContext())
                using (_editLayer.GetBitmapContext())
                {
                    for (int y = 0; y < _editLayer.PixelHeight; y++)
                    {
                        for (int x = 0; x < _editLayer.PixelWidth; x++)
                        {
                            var pixel = edgeOverlay.GetPixel(x, y);

                            if (pixel.A == 255 && pixel.R == 0 && pixel.G == 0 && pixel.B == 0)
                            {
                                _editLayer.SetPixel(x, y, Colors.Black);
                            }
                        }
                    }
                }

                var edgeImage = new Image
                {
                    Source = edgeOverlay,
                    Width = edgeOverlay.PixelWidth,
                    Height = edgeOverlay.PixelHeight,
                    Stretch = Stretch.None,
                    SnapsToDevicePixels = true,
                    Opacity = 1.0 
                };
                _drawCanvas.Children.Add(edgeImage);
            }

            _drawCanvas.Children.Add(new Image { Source = _editLayer });

            // Set the drawing mask now that edges are available
            _drawingMask = CreateEdgeMask();
            SetDrawingTool(); // to refresh the tool with the new mask




            //everything that is okay now
            _nextButton.IsEnabled = true;
            CANDRAW = true;
        }

        private Func<Point, bool> CreateEdgeMask()
        {
            if (DetectedEdges == null || DetectedEdges.Count == 0)
                return _ => false;

            return (Point p) =>
            {
                Point rounded = new Point((int)p.X, (int)p.Y);
                return DetectedEdges.Contains(rounded);
            };
        }


        #endregion

        #region edge detection(numbers)



        public static HashSet<Point> DetectRegionEdges(float[,] map, int edgeThickness = 1, IProgress<double> progress = null)
        {
            int width = map.GetLength(0);
            int height = map.GetLength(1);
            HashSet<Point> edgePoints = new HashSet<Point>();

            for (int y = 1; y < height - 1; y++) // avoid borders
            {
                for (int x = 1; x < width - 1; x++)
                {
                    float current = map[x, y];
                    bool isEdge = false;

                    // Compare to 8 neighbors
                    for (int dy = -1; dy <= 1 && !isEdge; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;

                            int nx = x + dx;
                            int ny = y + dy;

                            float neighbor = map[nx, ny];

                            if (neighbor != current)
                            {
                                isEdge = true;
                                break;
                            }
                        }
                    }

                    if (isEdge)
                    {
                        // Expand edge to desired thickness
                        for (int dy = -edgeThickness; dy <= edgeThickness; dy++)
                        {
                            for (int dx = -edgeThickness; dx <= edgeThickness; dx++)
                            {
                                int ex = x + dx;
                                int ey = y + dy;

                                if (ex >= 0 && ex < width && ey >= 0 && ey < height)
                                {
                                    edgePoints.Add(new Point(ex, ey));
                                }
                            }
                        }
                    }
                }

                progress?.Report((double)y / (height - 1));
            }

            return edgePoints;
        }


        public static HashSet<Point> DetectRegionEdgesNoZero(float[,] map, int edgeThickness = 1, IProgress<double> progress = null)
        {
            int width = map.GetLength(0);
            int height = map.GetLength(1);
            HashSet<Point> edgePoints = new HashSet<Point>();

            for (int y = 1; y < height - 1; y++) // avoid borders
            {
                for (int x = 1; x < width - 1; x++)
                {
                    float current = map[x, y];

                    if (current == 0) continue; // skip unmarked

                    bool isEdge = false;

                    // Compare to 8 neighbors
                    for (int dy = -1; dy <= 1 && !isEdge; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;

                            int nx = x + dx;
                            int ny = y + dy;

                            float neighbor = map[nx, ny];

                            if (neighbor != current && neighbor != 0)
                            {
                                isEdge = true;
                                break;
                            }
                        }
                    }

                    if (isEdge)
                    {
                        // Expand edge to desired thickness
                        for (int dy = -edgeThickness; dy <= edgeThickness; dy++)
                        {
                            for (int dx = -edgeThickness; dx <= edgeThickness; dx++)
                            {
                                int ex = x + dx;
                                int ey = y + dy;

                                if (ex >= 0 && ex < width && ey >= 0 && ey < height)
                                {
                                    edgePoints.Add(new Point(ex, ey));
                                }
                            }
                        }
                    }
                }

                //progress reporting
                progress?.Report((double)y / (height - 1));
            }

            return edgePoints;
        }

        private WriteableBitmap CreateEdgeBitmap(int width, int height, HashSet<Point> edges)
        {
            var edgeBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);

            using (edgeBitmap.GetBitmapContext())
            {
                foreach (var p in edges)
                {
                    int x = (int)p.X;
                    int y = (int)p.Y;

                    if (x >= 0 && x < width && y >= 0 && y < height)
                    {
                        edgeBitmap.SetPixel(x, y, Colors.Black);
                    }
                }
            }

            return edgeBitmap;
        }


        #endregion

        

        #region drawing tools
        private void SetDrawingTool()
        {
            Color paintColor = _edgeColors[_currentEdgeStyle];
            int brushSize = _drawingToolSize;

            switch (_drawingMode)
            {
                case "Freehand":
                    _activeTool = new FreehandTool(paintColor, brushSize);
                    break;
                // Add more tools later
                default:
                    _activeTool = new FreehandTool(paintColor, brushSize);
                    break;
            }

            _activeTool.Mask = _drawingMask; // always uses the current stored mask
        }

        #endregion

        #region canvas mouse behavior

        private void DrawCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if(CANDRAW == false)
            {
                return;
            }

            SaveState(); //start by saving the state for the undo

            Point pos = e.GetPosition(_drawCanvas);
            _activeTool?.OnMouseDown(pos, _editLayer);

        }

        private void DrawCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (CANDRAW == false)
            {
                return;
            }
            Point pos = e.GetPosition(_drawCanvas);
            _activeTool?.OnMouseMove(pos, _editLayer);
        }

        private void DrawCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (CANDRAW == false)
            {
                return;
            }
            Point pos = e.GetPosition(_drawCanvas);
            _activeTool?.OnMouseUp(pos, _editLayer);
        }
        #endregion

        #region UI event parameter changes
        private void EdgeStyleSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            int selectedIndex = comboBox?.SelectedIndex ?? 0;

            _currentEdgeStyle = selectedIndex;

            Debug.WriteLine($"Layer changed to {_currentEdgeStyle}");

            // Recreate the tool with the new color
            SetDrawingTool();
        }

        private void BrushSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _drawingToolSize = (int)e.NewValue;

            var brushSizeLabel = HelperExtensions.FindElementByTag<TextBlock>(this, "BrushSizeLabel");
            if (brushSizeLabel != null)
            {
                brushSizeLabel.Text = _drawingToolSize.ToString();
            }
            //recreate the active tool with the new size
            SetDrawingTool();

            Debug.WriteLine($"Brush size changed to {_drawingToolSize}");
        }
        #endregion

        #region saveState

        private void SaveState()
        {
            if (_editLayer == null) return;


            EdgeChangesMade += 1;//increment this
            
            // Clone current bitmap and add to undo stack
            var clone = _editLayer.Clone();

            _undoStack.Push(clone);

            // Keep undo stack limited in size
            if (_undoStack.Count > MaxHistory)
                _undoStack = new Stack<WriteableBitmap>(_undoStack.Reverse().Take(MaxHistory).Reverse());

            // Clear redo history, as new action breaks forward history
            _redoStack.Clear();
            UpdateUndoRedoButtons();
        }

        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            if (_undoStack.Count == 0) return;

            EdgeChangesMade -= 1;
            if(EdgeChangesMade < 0)
            {
                EdgeChangesMade = 0; //safety
            }

            _redoStack.Push(_editLayer.Clone());
            _editLayer = _undoStack.Pop();

            UpdateUndoRedoButtons();
            RefreshCanvas(); // redraw the image
        }

        private void RedoButton_Click(object sender, RoutedEventArgs e)
        {
            if (_redoStack.Count == 0) return;

            EdgeChangesMade += 1; //needs to be done here as well

            _undoStack.Push(_editLayer.Clone());
            _editLayer = _redoStack.Pop();

            UpdateUndoRedoButtons();
            RefreshCanvas(); // redraw the image
        }

        private void RefreshCanvas()
        {
            _drawCanvas.Children.Clear();
            //_drawCanvas.Children.Add(new Image { Source = _editLayer });

            // Re-add edge overlay (static, from DetectedEdges)
            if (DetectedEdges != null)
            {
                var edgeOverlay = CreateEdgeBitmap(_baseLayer.PixelWidth, _baseLayer.PixelHeight, DetectedEdges);
                var edgeImage = new Image
                {
                    Source = edgeOverlay,
                    Width = edgeOverlay.PixelWidth,
                    Height = edgeOverlay.PixelHeight,
                    Stretch = Stretch.None,
                    SnapsToDevicePixels = true,
                    Opacity = 1.0
                };

                _drawCanvas.Children.Add(edgeImage);
            }

            // Re-add the _editLayer so it's visible and editable
            _drawCanvas.Children.Add(new Image { Source = _editLayer });
        }

        private void UpdateUndoRedoButtons()
        {
            var undoButton = HelperExtensions.FindElementByTag<Button>(this, "UndoButton");
            var redoButton = HelperExtensions.FindElementByTag<Button>(this, "RedoButton");

            if (undoButton != null)
                undoButton.IsEnabled = _undoStack.Count > 0;

            if (redoButton != null)
                redoButton.IsEnabled = _redoStack.Count > 0;
        }

        #endregion

        #region processing for next step

        private void ProcessMapForHeightMap()
        {
            if (_baseLayer == null || _editLayer == null)
            {
                MessageBox.Show("Layers not initialized.");
                return;
            }

            int width = _baseLayer.PixelWidth;
            int height = _baseLayer.PixelHeight;

            float[,] baseLayer = new float[width, height];
            float[,] midLayer = new float[width, height];
            float[,] topLayer = new float[width, height];
            float[,] edgeLayer = new float[width, height];
            float[,] footprint = new float[width, height];

            using (var baseContext = _baseLayer.GetBitmapContext())
            using (var editContext = _editLayer.GetBitmapContext())
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        // Analyze base layer (R/G/B)
                        Color basePixel = _baseLayer.GetPixel(x, y);

                        if (basePixel.R > basePixel.G && basePixel.R > basePixel.B)
                        {
                            baseLayer[x, y] = 1f;
                            footprint[x, y] = 1f;
                        }
                        else if (basePixel.G > basePixel.R && basePixel.G > basePixel.B)
                        {
                            baseLayer[x, y] = 1f;
                            midLayer[x, y] = 1f;
                            footprint[x, y] = 1f;
                        }
                        else if (basePixel.B > basePixel.R && basePixel.B > basePixel.G)
                        {
                            baseLayer[x, y] = 1f;
                            topLayer[x, y] = 1f;
                            footprint[x, y] = 1f;
                        }

                        // Analyze edit layer (edges)
                        Color edgePixel = _editLayer.GetPixel(x, y);

                        if (edgePixel.A == 255 && edgePixel.R == 0 && edgePixel.G == 0 && edgePixel.B == 0)
                        {
                            //Debug.WriteLine("a shear edge");
                            edgeLayer[x, y] = 1f;
                        }                           
                        else if (edgePixel == Color.FromRgb(250, 140, 50))
                        {
                            //Debug.WriteLine("a 2 edge");
                            edgeLayer[x, y] = 2f;
                        }                           
                        else if (edgePixel == Color.FromRgb(100, 70, 180))
                        {
                            //Debug.WriteLine("a 3 edge");
                            edgeLayer[x, y] = 3f;
                        }
                        else
                        {
                            edgeLayer[x, y] = 0f; // transparent or unmarked
                        }
                            
                    }
                }
            }

            DebugPrintFloatCounts("Edge", edgeLayer);
            // Store for next page
            MapDataStore.BaseLayer = baseLayer;
            MapDataStore.MidLayer = midLayer;
            MapDataStore.TopLayer = topLayer;
            MapDataStore.EdgeLayer = edgeLayer;
            MapDataStore.FootPrint = footprint;

            Debug.WriteLine("Processing complete!");
        }

        private void DebugPrintFloatCounts(string label, float[,] layer)
        {
            Dictionary<float, int> counts = new Dictionary<float, int>();

            int width = layer.GetLength(0);
            int height = layer.GetLength(1);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float value = layer[x, y];

                    if (!counts.ContainsKey(value))
                        counts[value] = 1;
                    else
                        counts[value]++;
                }
            }

            Debug.WriteLine($"--- {label} Layer Float Value Counts ---");
            foreach (var kvp in counts.OrderBy(kvp => kvp.Key))
            {
                Debug.WriteLine($"Value {kvp.Key}: {kvp.Value} pixels");
            }
        }


        #endregion

        #region back and next
        private void Back_Click(object sender, RoutedEventArgs e)
        {
            //might add the option to go back
            var result = MessageBox.Show(
                    $"Are you sure you want to return to the previous page? your progress will not be saved",
                    "Return to previous page?",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

            if (result == MessageBoxResult.No)
            {
                return; // User said NO — cancel
            }
            //this.NavigationService?.Navigate();
            if (this.NavigationService.CanGoBack)
                this.NavigationService.GoBack();

        }
        private void Next_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine($"steps done: {EdgeChangesMade}");
            
            //another dialogue box, like before
            if (EdgeChangesMade <= 0)
            {
                var result = MessageBox.Show(
                    $"No Changes have been made to the edge layer. This will result in all shear edges. Is this okay?",
                    "Default Edges Warning",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    return; // User said NO — cancel
                }
                // If YES, we keep going
            }

            ProcessMapForHeightMap();
            this.NavigationService?.Navigate(new HeightMapPage());

        }

        #endregion

    }
}
