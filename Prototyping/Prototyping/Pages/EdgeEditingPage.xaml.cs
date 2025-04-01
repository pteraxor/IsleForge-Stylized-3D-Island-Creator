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

        public EdgeEditingPage()
        {
            InitializeComponent();
            //this.Loaded += EdgeEditingPage_Loaded;
            //this.Loaded += (s, e) => loadPause(); // cleaner
            //loadPause();
            Loaded += async (_, __) => await LoadPageAsync();
        }

        private async void loadPause()
        {
            await Task.Delay(100); // Let WPF fully render first

            EdgeEditingPage_Loaded();
        }

        private async Task LoadPageAsync()
        {
            // Get UI-bound resources first
            _baseCanvas = HelperExtensions.FindElementByTag<Canvas>(this, "BasemapCanvas");
            _drawCanvas = HelperExtensions.FindElementByTag<Canvas>(this, "DrawCanvas");
            _baseLayer = BitmapManager.Get("BaseLayer");

            // Dimensions
            int width = _baseLayer.PixelWidth;
            int height = _baseLayer.PixelHeight;
            int stride = width * 4;
            byte[] pixelData = new byte[height * stride];
            _baseLayer.CopyPixels(pixelData, stride, 0);

            // Create overlay bitmap on UI thread
            var overlay = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);

            // Get progress bar
            var progressBar = HelperExtensions.FindElementByTag<ProgressBar>(this, "EdgeProgressBar");
            if (progressBar != null)
            {
                progressBar.Visibility = Visibility.Visible;
                progressBar.Minimum = 0;
                progressBar.Maximum = height;
                progressBar.Value = 0;
            }

            // Run edge detection logic on background thread
            HashSet<Point> edgeMask = await Task.Run(() =>
            {
                return PerformEdgeDetectionFromBytesIntoBitmap(pixelData, overlay, width, height, y =>
                {
                    progressBar?.Dispatcher.Invoke(() => progressBar.Value = y);
                });
            });

            // Back on UI thread
            if (progressBar != null)
                progressBar.Visibility = Visibility.Collapsed;

            // Add overlay to canvas
            _drawCanvas.Children.Add(new Image
            {
                Source = overlay,
                Width = width,
                Height = height,
                IsHitTestVisible = false
            });

            // Create and overlay editable drawing bitmap
            _editLayer = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);

            _editLayerImage = new Image
            {
                Source = _editLayer,
                Width = width,
                Height = height
            };

            _drawCanvas.Children.Add(_editLayerImage);
            CopyBitmapInto(_editLayer, overlay); // black edges pre-filled

            // Setup drawing
            _drawingMask = p => edgeMask.Contains(new Point((int)p.X, (int)p.Y));
            SetDrawingTool();
        }

        private HashSet<Point> PerformEdgeDetectionFromBytesIntoBitmap(
    byte[] pixels,
    WriteableBitmap targetBitmap,
    int width,
    int height,
    Action<int> reportProgress)
        {
            HashSet<Point> edgePoints = new HashSet<Point>();
            int[] kernelX = { -1, 0, 1, -2, 0, 2, -1, 0, 1 };
            int[] kernelY = { -1, -2, -1, 0, 0, 0, 1, 2, 1 };
            int stride = width * 4;

            using (var context = targetBitmap.GetBitmapContext())
            {
                for (int y = 1; y < height - 1; y++)
                {
                    for (int x = 1; x < width - 1; x++)
                    {
                        int gradientX = 0;
                        int gradientY = 0;
                        int maxDiff = 0;

                        for (int ky = -1; ky <= 1; ky++)
                        {
                            for (int kx = -1; kx <= 1; kx++)
                            {
                                int nx = x + kx;
                                int ny = y + ky;
                                int i = ny * stride + nx * 4;

                                byte r = pixels[i + 2];
                                byte g = pixels[i + 1];
                                byte b = pixels[i];

                                int gray = (r + g + b) / 3;
                                int kernelIndex = (ky + 1) * 3 + (kx + 1);
                                gradientX += gray * kernelX[kernelIndex];
                                gradientY += gray * kernelY[kernelIndex];

                                // compare to center pixel
                                int centerIndex = y * stride + x * 4;
                                byte cr = pixels[centerIndex + 2];
                                byte cg = pixels[centerIndex + 1];
                                byte cb = pixels[centerIndex];

                                maxDiff = Math.Max(maxDiff,
                                    Math.Max(Math.Abs(r - cr), Math.Max(Math.Abs(g - cg), Math.Abs(b - cb))));
                            }
                        }

                        int magnitude = (int)Math.Sqrt(gradientX * gradientX + gradientY * gradientY);
                        int combined = magnitude + maxDiff;

                        if (combined > 25)
                            edgePoints.Add(new Point(x, y));
                    }

                    reportProgress?.Invoke(y);
                }

                // Expand and draw onto the target bitmap
                foreach (Point p in edgePoints.ToArray())
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int ex = (int)p.X + dx;
                            int ey = (int)p.Y + dy;

                            if (ex >= 0 && ex < width && ey >= 0 && ey < height)
                            {
                                targetBitmap.SetPixel(ex, ey, Colors.Black);
                                edgePoints.Add(new Point(ex, ey));
                            }
                        }
                    }
                }
            }

            return edgePoints;
        }



        private async Task LoadPageAsync2()
        {
            // Canvas and base layer
            _baseCanvas = HelperExtensions.FindElementByTag<Canvas>(this, "BasemapCanvas");
            _drawCanvas = HelperExtensions.FindElementByTag<Canvas>(this, "DrawCanvas");
            _baseLayer = BitmapManager.Get("BaseLayer");

            // Progress bar
            var progressBar = HelperExtensions.FindElementByTag<ProgressBar>(this, "EdgeProgressBar");
            if (progressBar != null)
            {
                progressBar.Visibility = Visibility.Visible;
                progressBar.Minimum = 0;
                progressBar.Maximum = _baseLayer.PixelHeight;
                progressBar.Value = 0;
            }

            // Extract pixels (on UI thread)
            int width = _baseLayer.PixelWidth;
            int height = _baseLayer.PixelHeight;
            int stride = width * 4;
            byte[] pixelData = new byte[height * stride];
            _baseLayer.CopyPixels(pixelData, stride, 0);

            // Run detection on background thread
            var result = await Task.Run(() =>
                PerformEdgeDetectionFromBytes(pixelData, width, height, y =>
                    progressBar?.Dispatcher.Invoke(() => progressBar.Value = y))
            );
            

            var overlay = result.Item1;
            var edgeMask = result.Item2;

            // Hide progress bar
            if (progressBar != null)
                progressBar.Visibility = Visibility.Collapsed;
            
            // Continue drawing setup...
            _drawingMask = p => true; // allows drawing anywhere by default
            _drawCanvas.Children.Add(new Image
            {
                Source = overlay,
                Width = overlay.PixelWidth,
                Height = overlay.PixelHeight,
                IsHitTestVisible = false
            });

            return;

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

            SetDrawingTool();
        }


        //private void EdgeEditingPage_Loaded(object sender, RoutedEventArgs e)
        private void EdgeEditingPage_Loaded()
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

            //progress bar
            ProgressBar edgeProgressBar = HelperExtensions.FindElementByTag<ProgressBar>(this, "EdgeProgressBar");

            if (edgeProgressBar != null)
            {
                edgeProgressBar.Visibility = Visibility.Visible;
            }
            // adding the overlay stuff
            //var result = PerformEdgeDetectionOverlay(_baseLayer, 1); //get the overlay

            // with progress bar
            var result = PerformEdgeDetectionOverlay(_baseLayer, 1, edgeProgressBar);

            WriteableBitmap overlay = result.Item1; //we get our overlay
            HashSet<Point> edgeMask = result.Item2; //we get our overlay mask
            _drawingMask = p => true; // allows drawing anywhere by default


            /*
            // Show visual overlay
            _baseCanvas.Children.Add(new Image
            {
                Source = overlay,
                Width = overlay.PixelWidth,
                Height = overlay.PixelHeight,
                IsHitTestVisible = false
            });

            // Use as mask in tool
            if (_activeTool != null)
            {
                _activeTool.Mask = delegate (Point p)
                {
                    return edgeMask.Contains(new Point((int)p.X, (int)p.Y));
                };
            }
            */
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

        private Tuple<WriteableBitmap, HashSet<Point>> PerformEdgeDetectionFromBytes(
    byte[] pixels, int width, int height, Action<int> reportProgress = null, int edgeThickness = 2)
        {
            WriteableBitmap edgeBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            HashSet<Point> edgePoints = new HashSet<Point>();

            int[] kernelX = { -1, 0, 1, -2, 0, 2, -1, 0, 1 };
            int[] kernelY = { -1, -2, -1, 0, 0, 0, 1, 2, 1 };
            int stride = width * 4;

            using (var edgeContext = edgeBitmap.GetBitmapContext())
            {
                for (int y = 1; y < height - 1; y++)
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
                                int ni = ny * stride + nx * 4;

                                byte r = pixels[ni + 2];
                                byte g = pixels[ni + 1];
                                byte b = pixels[ni + 0];

                                int grayscale = (r + g + b) / 3;

                                int kernelIndex = (ky + 1) * 3 + (kx + 1);
                                gradientX += grayscale * kernelX[kernelIndex];
                                gradientY += grayscale * kernelY[kernelIndex];

                                // Central pixel for diff comparison
                                int ci = y * stride + x * 4;
                                byte cr = pixels[ci + 2];
                                byte cg = pixels[ci + 1];
                                byte cb = pixels[ci + 0];

                                int diffR = Math.Abs(r - cr);
                                int diffG = Math.Abs(g - cg);
                                int diffB = Math.Abs(b - cb);

                                maxColorDifference = Math.Max(maxColorDifference, Math.Max(diffR, Math.Max(diffG, diffB)));
                            }
                        }

                        int magnitude = (int)Math.Sqrt(gradientX * gradientX + gradientY * gradientY);
                        int combined = magnitude + maxColorDifference;

                        if (combined > 25)
                        {
                            edgePoints.Add(new Point(x, y));
                        }
                    }

                    reportProgress?.Invoke(y);
                }

                // Draw thick edges
                foreach (var point in edgePoints.ToArray())
                {
                    for (int dy = -edgeThickness; dy <= edgeThickness; dy++)
                    {
                        for (int dx = -edgeThickness; dx <= edgeThickness; dx++)
                        {
                            int px = (int)point.X + dx;
                            int py = (int)point.Y + dy;

                            if (px >= 0 && px < width && py >= 0 && py < height)
                            {
                                edgeBitmap.SetPixel(px, py, Colors.Black);
                                edgePoints.Add(new Point(px, py));
                            }
                        }
                    }
                }
            }

            return Tuple.Create(edgeBitmap, edgePoints);
        }


        private Tuple<WriteableBitmap, HashSet<Point>> PerformEdgeDetectionOverlay(
    WriteableBitmap sourceBitmap,
    int edgeThickness,
    Action<int> reportProgress)
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
                for (int y = 1; y < height - 1; y++)
                {
                    for (int x = 1; x < width - 1; x++)
                    {
                        // edge detection logic (unchanged)
                    }

                    reportProgress?.Invoke(y);
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


        private WriteableBitmap PerformEdgeDetectionOverlayOriginal(WriteableBitmap sourceBitmap, int edgeThickness = 2)
        {
            int width = sourceBitmap.PixelWidth;
            int height = sourceBitmap.PixelHeight;

            // Create a new bitmap for detected edges
            WriteableBitmap edgeBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);

            int[] kernelX = { -1, 0, 1, -2, 0, 2, -1, 0, 1 };
            int[] kernelY = { -1, -2, -1, 0, 0, 0, 1, 2, 1 };

            HashSet<Point> edgePoints = new HashSet<Point>();

            using (var context = sourceBitmap.GetBitmapContext())
            using (var edgeContext = edgeBitmap.GetBitmapContext())
            {
                for (int y = 1; y < height - 1; y++) // Avoid borders
                {
                    for (int x = 1; x < width - 1; x++)
                    {
                        int gradientX = 0;
                        int gradientY = 0;
                        int maxColorDifference = 0;

                        // Process each kernel point
                        for (int ky = -1; ky <= 1; ky++)
                        {
                            for (int kx = -1; kx <= 1; kx++)
                            {
                                Color pixelColor = sourceBitmap.GetPixel(x + kx, y + ky);
                                int grayscale = (pixelColor.R + pixelColor.G + pixelColor.B) / 3; // Convert to grayscale

                                int kernelIndex = (ky + 1) * 3 + (kx + 1);
                                gradientX += grayscale * kernelX[kernelIndex];
                                gradientY += grayscale * kernelY[kernelIndex];

                                // Compute color differences in **all** channels
                                int diffR = Math.Abs(pixelColor.R - sourceBitmap.GetPixel(x, y).R);
                                int diffG = Math.Abs(pixelColor.G - sourceBitmap.GetPixel(x, y).G);
                                int diffB = Math.Abs(pixelColor.B - sourceBitmap.GetPixel(x, y).B);

                                // Use the largest color difference
                                maxColorDifference = Math.Max(maxColorDifference, Math.Max(diffR, Math.Max(diffG, diffB)));
                            }
                        }

                        // Compute gradient magnitude
                        int magnitude = (int)Math.Sqrt(gradientX * gradientX + gradientY * gradientY);

                        // Combine color edge detection and Sobel detection
                        int combinedEdge = magnitude + maxColorDifference;

                        // Set an adaptive threshold
                        int adaptiveThreshold = 25; // Adjust for sensitivity
                        if (combinedEdge > adaptiveThreshold)
                        {
                            edgePoints.Add(new Point(x, y)); // Store detected edge points
                        }
                    }
                }

                // Expand edges for thickness effect
                foreach (var point in edgePoints)
                {
                    for (int yOffset = -edgeThickness; yOffset <= edgeThickness; yOffset++)
                    {
                        for (int xOffset = -edgeThickness; xOffset <= edgeThickness; xOffset++)
                        {
                            int newX = (int)point.X + xOffset;
                            int newY = (int)point.Y + yOffset;

                            if (newX >= 0 && newX < width && newY >= 0 && newY < height)
                            {
                                edgeBitmap.SetPixel(newX, newY, Colors.Black);
                            }
                        }
                    }
                }
            }

            return edgeBitmap;
        }

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

        private Tuple<WriteableBitmap, HashSet<Point>> PerformEdgeDetectionOverlay(
    WriteableBitmap sourceBitmap,
    int edgeThickness,
    ProgressBar progressBar) // <-- progress bar support
        {
            int width = sourceBitmap.PixelWidth;
            int height = sourceBitmap.PixelHeight;

            WriteableBitmap edgeBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            HashSet<Point> edgePoints = new HashSet<Point>();

            int[] kernelX = { -1, 0, 1, -2, 0, 2, -1, 0, 1 };
            int[] kernelY = { -1, -2, -1, 0, 0, 0, 1, 2, 1 };

            if (progressBar != null)
            {
                progressBar.Minimum = 0;
                progressBar.Maximum = height - 2;
                progressBar.Value = 0;
                progressBar.Visibility = Visibility.Visible;
            }

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

                        int threshold = 25;
                        if (combinedEdge > threshold)
                        {
                            edgePoints.Add(new Point(x, y));
                        }
                    }

                    // Update progress bar after each row
                    if (progressBar != null)
                    {
                        Debug.WriteLine($"Tried updating bar: {y}");
                        //progressBar.Value = y;
                        int currentY = y;
                        progressBar.Dispatcher.Invoke(() =>
                        {
                            progressBar.Value = currentY;
                        }, System.Windows.Threading.DispatcherPriority.Background);
                    }
                }

                // Expand edges and mark on the overlay
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
                                edgePoints.Add(new Point(ex, ey));
                            }
                        }
                    }
                }

                if (progressBar != null)
                {
                    progressBar.Value = progressBar.Maximum;
                    progressBar.Visibility = Visibility.Collapsed;
                }
            }

            return Tuple.Create(edgeBitmap, edgePoints);
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
