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
using Prototyping.Tools;
using System.Diagnostics;
using Prototyping.Helpers;

namespace Prototyping.Pages
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

        private IDrawingTool _activeTool;

        private int _drawingToolSize = 10; //a backup default size

        private Stack<WriteableBitmap> _undoStack = new Stack<WriteableBitmap>();
        private Stack<WriteableBitmap> _redoStack = new Stack<WriteableBitmap>();
        private const int MaxHistory = 5;

        private string _drawingMode = "Freehand";

        private int _currentEdgeStyle = 0;
        private readonly Color[] _edgeColors = {
            Colors.Black,
            Color.FromRgb(250, 140, 50), //an orange that stands out, for the blended edge
            Color.FromRgb(100, 70, 180) //a purple that stands out for the other edge(might not be able to implement effectively)
        };
        private Func<Point, bool> _drawingMask;

        private const int TargetResolution = 250; //for the exporting of the data

        public EdgeEditingPage()
        {
            InitializeComponent();
            this.Loaded += EdgeEditingPage_Loaded;

        }

        private void EdgeEditingPage_Loaded(object sender, RoutedEventArgs e)
        {
            //get all of the canvas information, and 
            _baseCanvas = HelperExtensions.FindElementByTag<Canvas>(this, "BasemapCanvas");
            _drawCanvas = HelperExtensions.FindElementByTag<Canvas>(this, "DrawCanvas");
            _baseLayer = BitmapManager.Get("BaseLayer");

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

                Debug.WriteLine("BaseLayer successfully added to canvas.");
            }

            // adding the overlay stuff
            var result = PerformEdgeDetectionOverlay(_baseLayer, 1); //get the overlay

            WriteableBitmap overlay = result.Item1; //we get our overlay
            HashSet<Point> edgeMask = result.Item2; //we get our overlay mask
            _drawingMask = p => true; // allows drawing anywhere by default


            _drawCanvas.Children.Add(new Image
            {
                Source = overlay,
                Width = overlay.PixelWidth,
                Height = overlay.PixelHeight,
                IsHitTestVisible = false
            });

            // Create the drawing layer
            _editLayer = new WriteableBitmap(
                _baseLayer.PixelWidth,
                _baseLayer.PixelHeight,
                96, 96,
                PixelFormats.Bgra32,
                null);

            _editLayerImage = new Image
            {
                Source = _editLayer,
                Width = _editLayer.PixelWidth,
                Height = _editLayer.PixelHeight
            };

            _drawCanvas.Children.Add(_editLayerImage);
            CopyBitmapInto(_editLayer, overlay);


            _drawingMask = p => edgeMask.Contains(new Point((int)p.X, (int)p.Y));

            //Call tool setup here
            //SetDrawingTool(p => edgeMask.Contains(new Point((int)p.X, (int)p.Y)));
            SetDrawingTool();

        }

        #region overlay helper

        private void CopyBitmapInto(WriteableBitmap target, WriteableBitmap source)
        {
            int width = Math.Min(target.PixelWidth, source.PixelWidth);
            int height = Math.Min(target.PixelHeight, source.PixelHeight);

            using (var targetContext = target.GetBitmapContext())
            using (var sourceContext = source.GetBitmapContext())
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        Color color = source.GetPixel(x, y);
                        target.SetPixel(x, y, color);
                    }
                }
            }
        }


        #endregion



        #region edge detection


        private Tuple<WriteableBitmap, HashSet<Point>> PerformEdgeDetectionOverlay(
    WriteableBitmap sourceBitmap, int edgeThickness = 2)
        {
            int width = sourceBitmap.PixelWidth;
            int height = sourceBitmap.PixelHeight;

            WriteableBitmap edgeBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            HashSet<Point> edgePoints = new HashSet<Point>();

            int[] kernelX = { -1, 0, 1, -2, 0, 2, -1, 0, 1 };
            int[] kernelY = { -1, -2, -1, 0, 0, 0, 1, 2, 1 };

            using (var context = sourceBitmap.GetBitmapContext())
            using (var edgeContext = edgeBitmap.GetBitmapContext())
            {
                for (int y = 1; y < height - 1; y++) // Avoid edges
                {
                    for (int x = 1; x < width - 1; x++)
                    {
                        int gradientX = 0;
                        int gradientY = 0;
                        int maxColorDifference = 0;

                        for (int ky = -1; ky <= 1; ky++)
                        {
                            for (int kx = -1; kx <= 1; kx++)
                            {
                                int nx = x + kx;
                                int ny = y + ky;
                                Color pixelColor = sourceBitmap.GetPixel(nx, ny);
                                int grayscale = (pixelColor.R + pixelColor.G + pixelColor.B) / 3;

                                int kernelIndex = (ky + 1) * 3 + (kx + 1);
                                gradientX += grayscale * kernelX[kernelIndex];
                                gradientY += grayscale * kernelY[kernelIndex];

                                Color center = sourceBitmap.GetPixel(x, y);
                                int diffR = Math.Abs(pixelColor.R - center.R);
                                int diffG = Math.Abs(pixelColor.G - center.G);
                                int diffB = Math.Abs(pixelColor.B - center.B);

                                maxColorDifference = Math.Max(maxColorDifference,
                                    Math.Max(diffR, Math.Max(diffG, diffB)));
                            }
                        }

                        int magnitude = (int)Math.Sqrt(gradientX * gradientX + gradientY * gradientY);
                        int combinedEdge = magnitude + maxColorDifference;

                        int threshold = 25; // Sensitivity
                        if (combinedEdge > threshold)
                        {
                            edgePoints.Add(new Point(x, y));
                        }
                    }
                }

                // Expand edges to make them thicker and draw them
                foreach (Point p in edgePoints.ToArray())
                {
                    for (int dy = -edgeThickness; dy <= edgeThickness; dy++)
                    {
                        for (int dx = -edgeThickness; dx <= edgeThickness; dx++)
                        {
                            int ex = (int)p.X + dx;
                            int ey = (int)p.Y + dy;

                            if (ex >= 0 && ex < width && ey >= 0 && ey < height)
                            {
                                edgeBitmap.SetPixel(ex, ey, Colors.Black);
                                edgePoints.Add(new Point(ex, ey)); // Add expanded point to mask
                            }
                        }
                    }
                }
            }

            return Tuple.Create(edgeBitmap, edgePoints);
        }


        #endregion

        #region text conversion
        private void ProcessMap_Click(object sender, RoutedEventArgs e)
        {
            // Step 1: Resample to target size
            WriteableBitmap resizedBase = BitmapManager.ResizeBitmap(_baseLayer, TargetResolution, TargetResolution);
            WriteableBitmap resizedEdit = BitmapManager.ResizeBitmap(_editLayer, TargetResolution, TargetResolution);

            float[,] baseLayer = new float[TargetResolution, TargetResolution];
            float[,] midLayer = new float[TargetResolution, TargetResolution];
            float[,] topLayer = new float[TargetResolution, TargetResolution];
            float[,] edgeLayer = new float[TargetResolution, TargetResolution];

            using (var baseContext = resizedBase.GetBitmapContext())
            using (var editContext = resizedEdit.GetBitmapContext())
            {
                for (int y = 0; y < TargetResolution; y++)
                {
                    for (int x = 0; x < TargetResolution; x++)
                    {
                        Color basePixel = resizedBase.GetPixel(x, y);

                        if (basePixel.R > basePixel.G && basePixel.R > basePixel.B)
                            baseLayer[x, y] = 1f;
                        else if (basePixel.G > basePixel.R && basePixel.G > basePixel.B)
                            midLayer[x, y] = 1f;
                        else if (basePixel.B > basePixel.R && basePixel.B > basePixel.G)
                            topLayer[x, y] = 1f;

                        Color edgePixel = resizedEdit.GetPixel(x, y);

                        if (edgePixel == Colors.Black)
                            edgeLayer[x, y] = 1f;
                        else if (edgePixel == Color.FromRgb(250, 140, 50))
                            edgeLayer[x, y] = 2f;
                        else if (edgePixel == Color.FromRgb(100, 70, 180))
                            edgeLayer[x, y] = 3f;
                        else
                            edgeLayer[x, y] = 0f; // transparent or unmarked
                    }
                }
            }

            // Store for next page
            MapDataStore.BaseLayer = baseLayer;
            MapDataStore.MidLayer = midLayer;
            MapDataStore.TopLayer = topLayer;
            MapDataStore.EdgeLayer = edgeLayer;

            // Optional: export for inspection
            ExportLayerToText("BaseLayer.txt", baseLayer);
            ExportLayerToText("MidLayer.txt", midLayer);
            ExportLayerToText("TopLayer.txt", topLayer);
            ExportLayerToText("EdgeLayer.txt", edgeLayer);

            MessageBox.Show("Processing complete!");
        }

        private void ExportLayerToText(string filename, float[,] data)
        {
            var sb = new StringBuilder();

            int width = data.GetLength(0);
            int height = data.GetLength(1);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    sb.Append(data[x, y].ToString("0.0")).Append(" ");
                }
                sb.AppendLine();
            }

            System.IO.File.WriteAllText(filename, sb.ToString());
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
            SaveState(); //start by saving the state for the undo

            Point pos = e.GetPosition(_drawCanvas);
            _activeTool?.OnMouseDown(pos, _editLayer);
            //System.Diagnostics.Debug.WriteLine("OnMouseDown");
            //Debug.WriteLine($"Active Tool: {_activeTool?.GetType().Name ?? "null"}");

        }

        private void DrawCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            Point pos = e.GetPosition(_drawCanvas);
            _activeTool?.OnMouseMove(pos, _editLayer);
        }

        private void DrawCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
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

            //recreate the active tool with the new size
            SetDrawingTool();

            Debug.WriteLine($"Brush size changed to {_drawingToolSize}");
        }
        #endregion

        #region saveState

        private void SaveState()
        {
            if (_editLayer == null) return;

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

            _redoStack.Push(_editLayer.Clone());
            _editLayer = _undoStack.Pop();

            UpdateUndoRedoButtons();
            RefreshCanvas(); // redraw the image
        }

        private void RedoButton_Click(object sender, RoutedEventArgs e)
        {
            if (_redoStack.Count == 0) return;

            _undoStack.Push(_editLayer.Clone());
            _editLayer = _redoStack.Pop();

            UpdateUndoRedoButtons();
            RefreshCanvas(); // redraw the image
        }

        private void RefreshCanvas()
        {
            _drawCanvas.Children.Clear();
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


        private void Back_Click(object sender, RoutedEventArgs e)
        {
            //might add the option to go back
        }
        private void Next_Click(object sender, RoutedEventArgs e)
        {
            //for when it's time to to the next step. need to consider how to check for when it's okay to do so

        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Open a save file dialog to select the location to save the image
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PNG Files (*.png)|*.png",
                DefaultExt = ".png",
                Title = "Save Canvas"
            };

            // Show the dialog and save the file if the user selects a path
            if (saveFileDialog.ShowDialog() == true)
            {
                SaveCanvasToFile(saveFileDialog.FileName);
                MessageBox.Show("Canvas saved successfully!", "Save Canvas", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void SaveCanvasToFile(string filePath)
        {
            // Create a BitmapEncoder to save the WriteableBitmap
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(_editLayer));

            // Save to the specified file
            using (var fileStream = new System.IO.FileStream(filePath, System.IO.FileMode.Create))
            {
                encoder.Save(fileStream);
            }
        }

    }
}