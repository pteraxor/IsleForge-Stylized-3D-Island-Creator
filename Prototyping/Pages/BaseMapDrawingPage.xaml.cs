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
using Prototyping.Helpers;
using System.Diagnostics;


namespace Prototyping.Pages
{
    /// <summary>
    /// Interaction logic for BaseMapDrawingPage.xaml
    /// </summary>
    public partial class BaseMapDrawingPage : Page
    {
        private Canvas _drawCanvas;
        private WriteableBitmap _bitmap;
        //private bool _isDrawing;
        //private Point _lastPoint;

        private IDrawingTool _activeTool;

        private int _drawingToolSize = 10; //a backup default size
        private int _currentLayer = 0;
        private readonly Color[] _layerColors = {
            Colors.Red,
            Color.FromRgb(0, 255, 0), // Built in green is not true full green, which will be easier to use color channels for layer seperation
            Colors.Blue
        };

        private Stack<WriteableBitmap> _undoStack = new Stack<WriteableBitmap>();
        private Stack<WriteableBitmap> _redoStack = new Stack<WriteableBitmap>();
        private const int MaxHistory = 5;

        private string _drawingMode = "Freehand";
        private string _stampShape = "Circle";
        private bool _restrictToBaseLayer = false;
        private bool _isPreviewingStamp = false;

        #region initialization
        /*
        private T FindElementByTag<T>(DependencyObject parent, string tag) where T : FrameworkElement
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T t && t.Tag?.ToString() == tag)
                    return t;

                var result = FindElementByTag<T>(child, tag);
                if (result != null)
                    return result;
            }
            return null;
        }
        */

        public BaseMapDrawingPage()
        {
            InitializeComponent();
            this.Loaded += BaseMapDrawingPage_Loaded; 
        }

        private void BaseMapDrawingPage_Loaded(object sender, RoutedEventArgs e)
        {
            _drawCanvas = HelperExtensions.FindElementByTag<Canvas>(this, "DrawCanvas");

            if (_drawCanvas != null)
            {
                Debug.WriteLine("DrawCanvas found");

                InitBitmap(800, 600);

                _drawCanvas.Children.Add(new Image { Source = _bitmap });

                SetDrawingTool(); // call after bitmap is initialized
            }
            else
            {
                Debug.WriteLine("Could not find DrawCanvas. Check the Tag in XAML.");
            }
        }


        private void InitBitmap(int width, int height)
        {
            _bitmap = new WriteableBitmap(
                width,
                height,
                96, 96,
                PixelFormats.Bgra32,
                null);
        }
        #endregion

        #region canvas mouse behavior

        private void DrawCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SaveState(); //start by saving the state for the undo

            Point pos = e.GetPosition(_drawCanvas);
            _activeTool?.OnMouseDown(pos, _bitmap);
            //System.Diagnostics.Debug.WriteLine("OnMouseDown");
            //Debug.WriteLine($"Active Tool: {_activeTool?.GetType().Name ?? "null"}");

        }

        private void DrawCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            Point pos = e.GetPosition(_drawCanvas);
            _activeTool?.OnMouseMove(pos, _bitmap);
        }

        private void DrawCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            Point pos = e.GetPosition(_drawCanvas);
            _activeTool?.OnMouseUp(pos, _bitmap);
        }
        #endregion

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

                // other tools will be here
                default:
                    _activeTool = new FreehandTool(color, brushSize); //this should be the default tool anyway
                    break;
            }
     
            _activeTool.Mask = GetCurrentMask(); //we update the mask anytime there are tool changes.

        }

        private void DrawingModeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var combo = sender as ComboBox;
            var selected = (combo.SelectedItem as ComboBoxItem)?.Content.ToString();

            var tag = combo.Tag?.ToString();

            if (tag == "DrawingMode")
            {
                _drawingMode = selected ?? "Freehand";
                _isPreviewingStamp = _drawingMode == "Stamp";
                //RenderPreview();
            }

            SetDrawingTool();
        }

        //temp buttons to match UI layout
        private void StampShapeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

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

        private Func<Point, bool> GetCurrentMask()
        {
            if (_restrictToBaseLayer && _currentLayer > 0)
                return p => CanDrawOnlyOnBase(p);

            return _ => true;
        }



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

            RefreshCanvas();

        }


        private void Back_Click(object sender, RoutedEventArgs e)
        {
            //might add the option to go back
        }
        private void Next_Click(object sender, RoutedEventArgs e)
        {
            //for when it's time to to the next step. need to consider how to check for when it's okay to do so
            
            // Save a clone of the current bitmap into the manager
            if (_bitmap != null)
            {
                BitmapManager.Set("BaseLayer", _bitmap.Clone());
               // BitmapManager.SetBitmap("BaseLayer", _bitmap.Clone());

            }


            // Navigate to next step (example)
            this.NavigationService?.Navigate(new EdgeEditingPage());
        }

        


        #region UI event parameter changes
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

            //recreate the active tool with the new size
            SetDrawingTool();

            Debug.WriteLine($"Brush size changed to {_drawingToolSize}");
        }
        #endregion


        private void RenderPreview()
        {
            //if (PreviewCanvas == null) return; // Safety check
            // Clear the preview canvas
            //PreviewCanvas.Children.Clear();

        }

        #region saveState

        private void SaveState()
        {
            if (_bitmap == null) return;

            // Clone current bitmap and add to undo stack
            var clone = _bitmap.Clone();

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

    }
}
