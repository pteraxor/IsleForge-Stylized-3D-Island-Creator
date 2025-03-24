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


namespace Prototyping
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
        private bool _restrictToBaseLayer = true;
        private bool _isPreviewingStamp = false;


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

        public BaseMapDrawingPage()
        {
            InitializeComponent();
            this.Loaded += BaseMapDrawingPage_Loaded; 
        }

        private void BaseMapDrawingPage_Loaded(object sender, RoutedEventArgs e)
        {
            _drawCanvas = FindElementByTag<Canvas>(this, "DrawCanvas");

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

            if (_drawingMode == "Freehand")
            {
                _activeTool = new FreehandTool(color, brushSize);
                System.Diagnostics.Debug.WriteLine("Freehand tool initialized");
            }
            else
            {
                _activeTool = null;
            }
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

        private void RestrictToBaseLayerCheckbox_Checked(object sender, RoutedEventArgs e) { }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            SaveState(); //start by saving the state for the undo
        }


        private void Back_Click(object sender, RoutedEventArgs e)
        {
            //might add the option to go back
        }
        private void Next_Click(object sender, RoutedEventArgs e)
        {
            //for when it's time to to the next step. need to consider how to check for when it's okay to do so
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
            var undoButton = FindElementByTag<Button>(this, "UndoButton");
            var redoButton = FindElementByTag<Button>(this, "RedoButton");

            if (undoButton != null)
                undoButton.IsEnabled = _undoStack.Count > 0;

            if (redoButton != null)
                redoButton.IsEnabled = _redoStack.Count > 0;
        }

        #endregion


    }
}
