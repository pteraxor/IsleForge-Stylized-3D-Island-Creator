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


        }



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
            encoder.Frames.Add(BitmapFrame.Create(_baseLayer));

            // Save to the specified file
            using (var fileStream = new System.IO.FileStream(filePath, System.IO.FileMode.Create))
            {
                encoder.Save(fileStream);
            }
        }

    }
}
