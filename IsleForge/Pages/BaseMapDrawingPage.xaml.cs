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
using HelixToolkit.Wpf.SharpDX;
using IsleForge.DrawingTools;
using IsleForge.Helpers;
using IsleForge.PageStates;
using Polygon = System.Windows.Shapes.Polygon;

namespace IsleForge.Pages
{
    /// <summary>
    /// Interaction logic for BaseMapDrawingPage.xaml
    /// </summary>
    public partial class BaseMapDrawingPage : Page
    {
        #region top level variables
        private Canvas _drawCanvas;
        private Canvas _previewCanvas;
        private WriteableBitmap _bitmap;

        private IDrawingTool _activeTool;

        private int _drawingToolSize = 30; //a backup default size
        private int _currentLayer = 0;

        private readonly Color[] _layerColors = {
            Colors.Red,
            Color.FromRgb(0, 255, 0), // Built in green is not true full green, which will be easier to use color channels for layer seperation
            Colors.Blue
        };

        private Stack<WriteableBitmap> _undoStack = new Stack<WriteableBitmap>();
        private Stack<WriteableBitmap> _redoStack = new Stack<WriteableBitmap>();
        private const int MaxHistory = 15;

        private string _drawingMode = "Freehand";
        private string _stampShape = "Circle";
        private bool _restrictToBaseLayer = false;
        private bool _isPreviewingStamp = false;

        private double _canvasCoverage = 0; //this is to see how much is covered
        private Button _nextButton;

        #endregion

        public BaseMapDrawingPage()
        {
            InitializeComponent();
            this.Loaded += BaseMapDrawingPage_Loaded;
        }

        
        private void BaseMapDrawingPage_Loaded(object sender, RoutedEventArgs e)
        {
            _drawCanvas = HelperExtensions.FindElementByTag<Canvas>(this, "DrawCanvas");
            _previewCanvas = HelperExtensions.FindElementByTag<Canvas>(this, "PreviewCanvas");
            _nextButton = HelperExtensions.FindElementByTag<Button>(this, "NextBtn");


            if (_drawCanvas == null)
            {
                Debug.WriteLine("Could not find DrawCanvas. Check the Tag in XAML.");
                return;
            }
            //Debug.WriteLine("DrawCanvas found");

            if (PageStateStore.BaseMapState != null)
            {
                RestorePageFromStoredState(); // Restore state from memory
            }
            else
            {
                //getting the UI elemented that have settings saved
                _drawingToolSize = App.CurrentSettings.DefaultToolSize;
                // Brush size label
                var brushSizeLabel = HelperExtensions.FindElementByTag<TextBlock>(this, "BrushSizeLabel");
                if (brushSizeLabel != null)
                    brushSizeLabel.Text = _drawingToolSize.ToString();

                // Set brush size slider
                var slider = HelperExtensions.FindElementByTag<Slider>(this, "BrushSizeSlider");
                if (slider != null)
                    slider.Value = _drawingToolSize;

                InitBitmap(800, 600);
                _drawCanvas.Children.Add(new Image { Source = _bitmap });
                SetDrawingTool(); // Fresh init
            }

        }

        #region page state management

        private void RestorePageFromStoredState()
        {
            var state = PageStateStore.BaseMapState;

            _bitmap = state.Bitmap.Clone();
            _undoStack = new Stack<WriteableBitmap>(state.UndoStack.Reverse());
            _redoStack = new Stack<WriteableBitmap>(state.RedoStack.Reverse());
            _canvasCoverage = state.CanvasCoverage;

            _drawingToolSize = state.DrawingToolSize;
            _currentLayer = state.CurrentLayer;
            _drawingMode = state.DrawingMode;
            _stampShape = state.StampShape;
            _restrictToBaseLayer = state.RestrictToBaseLayer;

            _drawCanvas.Children.Clear(); // Clear any old images just to be safe
            _drawCanvas.Children.Add(new Image { Source = _bitmap });

            UpdateUIFromState(); //make sure the UI matches
            UpdateUndoRedoButtons(); //make sure the buttons fit the state

            Debug.WriteLine("Restored BaseMapPage state.");
        }

        private void UpdateUIFromState()
        {
            // Brush size label
            var brushSizeLabel = HelperExtensions.FindElementByTag<TextBlock>(this, "BrushSizeLabel");
            if (brushSizeLabel != null)
                brushSizeLabel.Text = _drawingToolSize.ToString();

            // Set brush size slider
            var slider = HelperExtensions.FindElementByTag<Slider>(this, "BrushSizeSlider");
            if (slider != null)
                slider.Value = _drawingToolSize;

            // Set layer selector
            var layerSelector = HelperExtensions.FindElementByTag<ComboBox>(this, "LayerSelector");
            if (layerSelector != null)
                layerSelector.SelectedIndex = _currentLayer;

            // Set drawing mode dropdown
            var drawingModeSelector = HelperExtensions.FindElementByTag<ComboBox>(this, "DrawingModeSelector");
            if (drawingModeSelector != null)
            {
                var item = drawingModeSelector.Items
                    .Cast<ComboBoxItem>()
                    .FirstOrDefault(i => (string)i.Content == _drawingMode);
                if (item != null)
                    drawingModeSelector.SelectedItem = item;
            }

            // Set stamp shape selector
            var stampShapeSelector = HelperExtensions.FindElementByTag<ComboBox>(this, "StampShapeSelector");
            if (stampShapeSelector != null)
            {
                var item = stampShapeSelector.Items
                    .Cast<ComboBoxItem>()
                    .FirstOrDefault(i => (string)i.Content == _stampShape);
                if (item != null)
                    stampShapeSelector.SelectedItem = item;
            }

            // Checkbox state
            var restrictCheckbox = HelperExtensions.FindElementByTag<CheckBox>(this, "RestrictToBaseLayerCheckbox");
            if (restrictCheckbox != null)
                restrictCheckbox.IsChecked = _restrictToBaseLayer;

            SetDrawingTool(); // Refresh the tool with the updated config
        }


        private void SavePageStateBeforeLeaving()
        {
            PageStateStore.BaseMapState = new BaseMapPageState
            {
                Bitmap = _bitmap.Clone(),
                UndoStack = new Stack<WriteableBitmap>(_undoStack.Reverse()), // Reverse to preserve stack order
                RedoStack = new Stack<WriteableBitmap>(_redoStack.Reverse()),
                CanvasCoverage = _canvasCoverage,
                DrawingToolSize = _drawingToolSize,
                CurrentLayer = _currentLayer,
                DrawingMode = _drawingMode,
                StampShape = _stampShape,
                RestrictToBaseLayer = _restrictToBaseLayer
            };

        }


        #endregion

        #region initialization

        private void InitBitmap(int width, int height)
        {
            _bitmap = new WriteableBitmap(
                width,
                height,
                96, 96,
                PixelFormats.Bgra32,
                null); //what if I lower it
        }

        #endregion

        #region button behaviors
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            SaveState(); //start by saving the state for the undo

            if (_bitmap == null) return; //a safety check

            using (_bitmap.GetBitmapContext())
            {
                // Clear to transparent or white depending on your design
                _bitmap.Clear(Colors.Transparent); // or Colors.White
                _bitmap.AddDirtyRect(new Int32Rect(0, 0, _bitmap.PixelWidth, _bitmap.PixelHeight));
            }

            //change percentage
            _canvasCoverage = 0;
            BehaviorBasedOnPercentage(); //this will get called with the check, but in case that delays
            RefreshCanvas();

        }

        #endregion

        #region drawing tool changes

        private void SetDrawingTool()
        {
            Debug.WriteLine("set tool called");
            Color color = _layerColors[_currentLayer];
            int brushSize = _drawingToolSize;


            switch (_drawingMode)
            {
                case "Freehand":
                    _activeTool = new FreehandTool(color, brushSize);
                    break;
                case "Paint Bucket":
                    _activeTool = new PaintBucketTool(color);
                    break;
                case "Eraser":
                    _activeTool = new EraserTool(brushSize);
                    break;
                case "Stamp":
                    _activeTool = new StampTool(_stampShape, brushSize, color);
                    break;
                // other tools will be here
                default:
                    _activeTool = new FreehandTool(color, brushSize); //this should be the default tool anyway
                    break;
            }

            _activeTool.Mask = GetCurrentMask(); //we update the mask anytime there are tool changes.
        }

        private Func<Point, bool> GetCurrentMask()
        {
            if (_restrictToBaseLayer && _currentLayer > 0)
                return p => CanDrawOnlyOnBase(p);

            return _ => true;
        }

        private bool CanDrawOnlyOnBase(Point p)
        {
            if (_bitmap == null)
                return false;

            int x = (int)p.X;
            int y = (int)p.Y;

            if (x < 0 || y < 0 || x >= _bitmap.PixelWidth || y >= _bitmap.PixelHeight)
                return false;

            Color color = _bitmap.GetPixel(x, y);
            return color == Colors.Red;
        }

        #endregion

        #region canvas mouse behavior

        private void DrawCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SaveState(); //start by saving the state for the undo

            Point pos = e.GetPosition(_drawCanvas);
            _activeTool?.OnMouseDown(pos, _bitmap);
            //Debug.WriteLine($"Active Tool: {_activeTool?.GetType().Name ?? "null"}");
        }
        private void DrawCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            Point pos = e.GetPosition(_drawCanvas);           
            _activeTool?.OnMouseMove(pos, _bitmap);

            DrawToolPreview(pos);
        }

        private void DrawCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            Point pos = e.GetPosition(_drawCanvas);
            _activeTool?.OnMouseUp(pos, _bitmap);
            //LogCanvasCoverage();
            _ = LogCanvasCoverageAsync();
        }
        #endregion

        #region preview tools
        private void DrawToolPreview(Point position)
        {
            if (_previewCanvas == null || _activeTool == null)
                return;

            _previewCanvas.Children.Clear();

            Shape previewShape = null;

            if (_drawingMode == "Freehand" || _drawingMode == "Eraser")
            {
                previewShape = new Ellipse
                {
                    Width = _drawingToolSize * 2,
                    Height = _drawingToolSize * 2,
                    Stroke = Brushes.Black,
                    StrokeDashArray = new DoubleCollection { 2, 2 },
                    StrokeThickness = 1
                };

                Canvas.SetLeft(previewShape, position.X - _drawingToolSize);
                Canvas.SetTop(previewShape, position.Y - _drawingToolSize);
            }
            else if (_drawingMode == "Stamp")
            {
                int sides = _stampShape switch
                {
                    "Triangle" => 3,
                    "Square" => 4,
                    "Hexagon" => 6,
                    _ => 0
                };

                if (sides > 0)
                {
                    previewShape = CreatePolygonPreview(sides, _drawingToolSize, Brushes.Black);

                    Canvas.SetLeft(previewShape, position.X - _drawingToolSize);
                    Canvas.SetTop(previewShape, position.Y - _drawingToolSize);
                }
                else if (_stampShape == "Circle")
                {
                    previewShape = new Ellipse
                    {
                        Width = _drawingToolSize * 2,
                        Height = _drawingToolSize * 2,
                        Stroke = Brushes.Black,
                        StrokeDashArray = new DoubleCollection { 2, 2 },
                        StrokeThickness = 1
                    };

                    Canvas.SetLeft(previewShape, position.X - _drawingToolSize);
                    Canvas.SetTop(previewShape, position.Y - _drawingToolSize);
                }
            }

            if (previewShape != null)
                _previewCanvas.Children.Add(previewShape);
        }

        private Polygon CreatePolygonPreview(int sides, double radius, Brush stroke)
        {
            var polygon = new Polygon
            {
                Stroke = stroke,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 2, 2 },
                Points = new PointCollection()
            };

            double angleOffset = sides == 3 ? -Math.PI / 2 : (sides == 4 ? Math.PI / 4 : 0);

            for (int i = 0; i < sides; i++)
            {
                double angle = Math.PI * 2 * i / sides + angleOffset;
                double x = radius + radius * Math.Cos(angle);
                double y = radius + radius * Math.Sin(angle);
                polygon.Points.Add(new Point(x, y));
            }

            return polygon;
        }


        #endregion

        #region UI event parameter changes

        private void DrawingModeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Debug.WriteLine($"called selection change");
            var combo = sender as ComboBox;
            var selected = (combo.SelectedItem as ComboBoxItem)?.Content.ToString();

            var tag = combo.Tag?.ToString();

            if (tag == "DrawingMode")
            {
                _drawingMode = selected ?? "Freehand";
                //_isPreviewingStamp = _drawingMode == "Stamp";
                //RenderPreview();
            }

            SetDrawingTool();
        }
        private void LayerSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            int selectedIndex = comboBox?.SelectedIndex ?? 0;

            _currentLayer = selectedIndex;

            Debug.WriteLine($"Layer changed to {_currentLayer}");

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

        private void RestrictToBaseLayerCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            var checkbox = sender as CheckBox;

            _restrictToBaseLayer = checkbox?.IsChecked ?? false;

            if (_activeTool != null)
            {
                _activeTool.Mask = GetCurrentMask();
            }

            Debug.WriteLine($"RestrictToBaseLayer: {_restrictToBaseLayer}");
        } //when the checkbox changes, we send the mask and activate it. The mask is updated with tool changes, so it will keep up

        private void StampShapeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var StampShapeSelector = sender as ComboBox;
            _stampShape = (StampShapeSelector.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Circle";

            Debug.WriteLine($"_stampShape: {_stampShape}");
            SetDrawingTool();
        }

        #endregion

        #region save states

        private void SaveState()
        {
            if (_bitmap == null) return;

            // Clone current bitmap and add to undo stack
            var clone = _bitmap.Clone();

            _undoStack.Push(clone);
         

            if (_undoStack.Count >= MaxHistory)
            {
                var tempList = _undoStack.Reverse().ToList();
                tempList.RemoveAt(0); // Remove oldest
                _undoStack = new Stack<WriteableBitmap>(tempList);
            }

            // Clear redo history, as new action breaks forward history
            _redoStack.Clear();
            UpdateUndoRedoButtons();

            //LogCanvasCoverage(); //trying to see how much is covered
            _ = LogCanvasCoverageAsync();
        }

        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            if (_undoStack.Count == 0) return;

            _redoStack.Push(_bitmap.Clone());
            _bitmap = _undoStack.Pop();

            UpdateUndoRedoButtons();
            RefreshCanvas(); // redraw the image
        }

        private void RedoButton_Click(object sender, RoutedEventArgs e)
        {
            if (_redoStack.Count == 0) return;

            _undoStack.Push(_bitmap.Clone());
            _bitmap = _redoStack.Pop();

            UpdateUndoRedoButtons();
            RefreshCanvas(); // redraw the image
        }

        private void RefreshCanvas()
        {
            _drawCanvas.Children.Clear();
            _drawCanvas.Children.Add(new Image { Source = _bitmap });
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


        private async Task LogCanvasCoverageAsync()
        {
            if (_bitmap == null) return;

            int width = _bitmap.PixelWidth;
            int height = _bitmap.PixelHeight;
            int totalPixels = width * height;
            int stride = width * (_bitmap.Format.BitsPerPixel / 8);
            byte[] pixelData = new byte[height * stride];
            _bitmap.CopyPixels(pixelData, stride, 0);

            double percentage = await Task.Run(() =>
            {
                int coveredPixels = 0;

                for (int i = 3; i < pixelData.Length; i += 4)
                {
                    byte alpha = pixelData[i];
                    if (alpha > 0)
                        coveredPixels++;
                }

                return (coveredPixels / (double)totalPixels) * 100.0;
            });

            Debug.WriteLine($"Canvas Coverage: {percentage:F2}%");

            _canvasCoverage = percentage;
            BehaviorBasedOnPercentage(); // Runs back on the main thread
        }


        private void BehaviorBasedOnPercentage()
        {
            if (_canvasCoverage > 0.1)
            {
                Debug.WriteLine($"Canvas Coverage: {_canvasCoverage:F2}% exceeds threshold to move to next step");
                _nextButton.IsEnabled = true;
            }
            else
            {
                Debug.WriteLine($"Canvas Coverage: {_canvasCoverage:F2}% too low for next step");
                _nextButton.IsEnabled = false;
            }
        }



        #region save to file
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
            encoder.Frames.Add(BitmapFrame.Create(_bitmap));

            // Save to the specified file
            using (var fileStream = new System.IO.FileStream(filePath, System.IO.FileMode.Create))
            {
                encoder.Save(fileStream);
            }
        }
        #endregion

        #region data exporting

        private void ExportIntermediateMap()
        {
            if (_bitmap == null)
            {
                //MessageBox.Show("No bitmap to process.");
                return;
            }

            int width = _bitmap.PixelWidth;
            int height = _bitmap.PixelHeight;
            float[,] intermediateMap = new float[width, height];

            using (var context = _bitmap.GetBitmapContext())
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        Color pixel = _bitmap.GetPixel(x, y);

                        // Ignore transparent pixels (optional)
                        if (pixel.A < 10)
                        {
                            intermediateMap[x, y] = 0f;
                            continue;
                        }

                        // Dominant channel mapping
                        if (pixel.R > pixel.G && pixel.R > pixel.B)
                        {
                            intermediateMap[x, y] = 1f; // Red = Base
                        }
                        else if (pixel.G > pixel.R && pixel.G > pixel.B)
                        {
                            intermediateMap[x, y] = 2f; // Green = Mid
                        }
                        else if (pixel.B > pixel.R && pixel.B > pixel.G)
                        {
                            intermediateMap[x, y] = 3f; // Blue = Top
                        }
                        else
                        {
                            intermediateMap[x, y] = 0f; // Unmarked or neutral
                        }
                    }
                }
            }

            // Store it
            MapDataStore.IntermediateMap = intermediateMap;

            // Export for next step
            ExportLayerToText("IntermediateMap.txt", intermediateMap);
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
                    sb.Append(data[x, y].ToString("0")).Append(" ");
                }
                sb.AppendLine();
            }

            System.IO.File.WriteAllText(filename, sb.ToString());
        }


        #endregion

        #region back and next buttons

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            //might add the option to go back
            var result = MessageBox.Show(
                    $"Are you sure you want to return to the welcome page? your progress will not be saved",
                    "Return to Welcome Page?",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

            if (result == MessageBoxResult.No)
            {
                return; // User said NO — cancel
            }

            PageStateStore.BaseMapState = null; //clear this out if we are returning to the beggining

            //this.NavigationService?.Navigate();
            if (this.NavigationService.CanGoBack)
                this.NavigationService.GoBack();

        }
        private void Next_Click(object sender, RoutedEventArgs e)
        {
            if (_canvasCoverage < 6)
            {
                var result = MessageBox.Show(
                    $"Only a small amount of the canvas is covered, are you sure you have drawn all you want to?",
                    "Low Coverage Warning",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    return; // User said NO — cancel
                }
                // If YES, we keep going
            }
            //for when it's time to to the next step. need to consider how to check for when it's okay to do so
            //Debug.WriteLine($"after the message");

            // Save a clone of the current bitmap into the manager
            if (_bitmap != null)
            {
               ExportIntermediateMap();
               BitmapManager.Set("BaseLayer", _bitmap.Clone());                
            }
            //save a state for everything
            SavePageStateBeforeLeaving();


            // Navigate to next step
            this.NavigationService?.Navigate(new EdgeEditingPage());
        }

        #endregion

    }
}
