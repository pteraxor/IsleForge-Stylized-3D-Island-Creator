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
using System.Diagnostics;
using System.Windows.Media.Media3D;
using System.IO;
using IsleForge.Dialogues;
using IsleForge.Helpers;
using System.Reflection.Emit;

namespace IsleForge.Pages
{
    /// <summary>
    /// Interaction logic for TexturingPage.xaml
    /// </summary>
    public partial class TexturePage : Page
    {
        private Dictionary<string, MeshGeometry3D> _meshes;
        private Dictionary<string, Point3DCollection> _originalPositions;

        private Canvas _MapCanvas;
        private Viewport3D _viewport3D;
        private Model3DGroup _modelGroup;

        private ImageBrush _grassBrush;
        private ImageBrush _rockBrush;
        private ImageBrush _sandBrush;

        private const string DefaultGrass = "GrassAlbedo.png";
        private const string DefaultRock = "RockAlbedo.png";
        private const string DefaultSand = "SandAlbedo.png";

        private List<(double U, double V, string Label)> _collectedUVs = new();
        string[,] labelGrid;

        public TexturePage()
        {
            InitializeComponent();
            this.Loaded += TexturingPage_Loaded;
        }

        private void TexturingPage_Loaded(object sender, RoutedEventArgs e)
        {
            _meshes = MeshDataStore.Meshes;
            _originalPositions = MeshDataStore.OriginalMeshPositions;

            foreach (var kvp in _meshes)
            {
                string label = kvp.Key;
                var mesh = kvp.Value;
                Debug.WriteLine($"[InitMesh] Label in _meshes: {label}");
                //_modelGroup.Children.Add(new GeometryModel3D(mesh, material));
                if (kvp.Key == "cliff")
                {
                    Debug.WriteLine($"[VertexCount] cliff mesh has {kvp.Value.Positions.Count} vertices.");
                }
            }

            _MapCanvas = HelperExtensions.FindElementByTag<Canvas>(this, "MapCanvas");

            //load default textures
            _grassBrush = LoadTilingBrush(DefaultGrass);
            _rockBrush = LoadTilingBrush(DefaultRock);
            _sandBrush = LoadTilingBrush(DefaultSand);

            //this worked fine on the previous page
            //_viewport3D = FindVisualChild<Viewport3D>(this);
            //_modelGroup = new Model3DGroup();

            _viewport3D = FindViewport3D(this);
            _modelGroup = FindSceneModelGroup(_viewport3D);


            _modelGroup.Children.Clear();




            var grass = LoadBitmap("GrassAlbedo.png");
            var rock = LoadBitmap("RockAlbedo.png");
            var sand = LoadBitmap("SandAlbedo.png");


            var debugTexture = GenerateDebugLabelTexture(_meshes, 1024, 1024);

            SaveBitmapToFile(debugTexture, "terrain-debug.png");

            var finalTex = GenerateFinalTextureFromLabelGrid(
                labelGrid,
                grass, rock, sand,
                1024, 1024,
                tileScale: 0.05
            );

            //var material = new DiffuseMaterial(new ImageBrush(debugTexture) { Stretch = Stretch.Fill });
            var material = new DiffuseMaterial(new ImageBrush(finalTex) { Stretch = Stretch.Fill });

            _modelGroup.Children.Clear();

            //redo mesh making

            var combinedMesh = CombineMeshes(_meshes);
            ApplyTopDownUV(combinedMesh, 0.1);


            _modelGroup.Children.Clear();
            _modelGroup.Children.Add(new GeometryModel3D(combinedMesh, material));

            SetCameraToMesh();
        }

        #region custom texturing


        private void UploadTexture_Click(object sender, RoutedEventArgs e)
        {
            //UploadTextureForType("grass");
            var dialog = new TextureTargetDialog
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true)
            {
                string selectedType = dialog.SelectedType;
                if (!string.IsNullOrEmpty(selectedType))
                {
                    UploadTextureForType(selectedType);
                }
            }
        }

        private void UploadTextureForType(string type)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp",
                Title = $"Select {type} Texture"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var brush = LoadTilingBrushFromFile(openFileDialog.FileName);

                switch (type.ToLower())
                {
                    case "grass":
                        _grassBrush = brush;
                        break;
                    case "sand":
                        _sandBrush = brush;
                        break;
                    case "rock":
                        _rockBrush = brush;
                        break;
                }

                //RetextureScene();
                GoodRetexture();
            }
        }


        private void ResetTexture_Click(object sender, RoutedEventArgs e)
        {
            _grassBrush = LoadTilingBrush(DefaultGrass);
            _rockBrush = LoadTilingBrush(DefaultRock);
            _sandBrush = LoadTilingBrush(DefaultSand);

            //RetextureScene();
            GoodRetexture();
        }



        #endregion

        #region texture upload helpers

        private ImageBrush LoadTilingBrushFromFile(string path, double tileScale = 0.1)
        {
            var image = new BitmapImage(new Uri(path, UriKind.Absolute));

            return new ImageBrush(image)
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, tileScale, tileScale),
                ViewportUnits = BrushMappingMode.RelativeToBoundingBox,
                Stretch = Stretch.Fill
            };
        }

        #endregion

        #region texturing images

        private void GoodRetexture()
        {
            return;
            //_grassBrush = LoadTilingBrush(DefaultGrass);
            //_rockBrush = LoadTilingBrush(DefaultRock);
            //_sandBrush = LoadTilingBrush(DefaultSand);

            _viewport3D = FindViewport3D(this);
            _modelGroup = FindSceneModelGroup(_viewport3D);

            _modelGroup.Children.Clear();


            foreach (var kvp in _meshes)
            {
                string label = kvp.Key;
                var mesh = kvp.Value;

                //ApplyTextureCoordinatesLabel(mesh, 0.1, label); // UVs scaled for tiling

                var material = new DiffuseMaterial(GetTextureForLabel(label));

                var model = new GeometryModel3D(mesh, material)
                {
                    BackMaterial = material
                };

                _modelGroup.Children.Add(model);
            }



            SetCameraToMesh();
        }


        private ImageBrush LoadTilingBrush(string relativePath, double tileScale = 0.1)
        {
            Debug.WriteLine("Entered Tiling brush");
            try
            {
                var uri = new Uri($"pack://application:,,,/IsleForge;component/Resources/Textures/{relativePath}", UriKind.Absolute);
                var image = new BitmapImage(uri)
                {
                    CacheOption = BitmapCacheOption.OnLoad
                };

                Debug.WriteLine($"Loaded texture: {relativePath} ({image.PixelWidth}x{image.PixelHeight})");

                return new ImageBrush(image)
                {
                    TileMode = TileMode.Tile,
                    Viewport = new Rect(0, 0, tileScale, tileScale),
                    ViewportUnits = BrushMappingMode.RelativeToBoundingBox,
                    Stretch = Stretch.Fill
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load texture '{relativePath}': {ex.Message}");
                return null; // don't wrap a brush in an ImageBrush
            }
        }


        private ImageBrush GetTextureForLabel(string label)
        {
            switch (label)
            {
                case "beach":
                    return _sandBrush;
                case "cliff":
                    return _rockBrush;
                case "Top":
                    return _grassBrush;
                case "Base":
                    return _grassBrush;
                case "Mid":
                    return _grassBrush;
                case "ramp":
                    return _grassBrush;
                default:
                    return _rockBrush; // fallback
            }
        }

        #endregion

        #region uneasy helpers

        private Viewport3D FindViewport3D(DependencyObject parent)
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is Viewport3D viewport)
                    return viewport;

                var result = FindViewport3D(child);
                if (result != null)
                    return result;
            }
            return null;
        }

        private Model3DGroup FindSceneModelGroup(Viewport3D viewport)
        {
            foreach (var child in viewport.Children)
            {
                if (child is ModelVisual3D visual && visual.Content is Model3DGroup group)
                    return group;
            }
            return null;
        }


        #endregion

        #region carry over


        private void SetCameraToMesh()
        {
            var camera = new PerspectiveCamera
            {
                Position = MeshDataStore.CameraPosition,
                LookDirection = MeshDataStore.CameraLookDirection,
                UpDirection = MeshDataStore.CameraUpDirection,
                FieldOfView = 60
            };

            _viewport3D.Camera = camera;
        }


        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                    return typedChild;

                T result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }
            return null;
        }

        #endregion

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            //might add the option to go back
        }
        private void Next_Click(object sender, RoutedEventArgs e)
        {
            //for when it's time to to the next step. need to consider how to check for when it's okay to do so
            //NavigationService.Navigate(new TexturingPage());
        }

        #region trying new  methods




        private BitmapSource LoadBitmap(string relativePath)
        {
            try
            {
                var uri = new Uri($"pack://application:,,,/IsleForge;component/Resources/Textures/{relativePath}", UriKind.Absolute);
                var image = new BitmapImage(uri)
                {
                    CacheOption = BitmapCacheOption.OnLoad
                };
                return image;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load bitmap '{relativePath}': {ex.Message}");
                return null;
            }
        }


        private WriteableBitmap GenerateDebugLabelTexture(
    Dictionary<string, MeshGeometry3D> meshes,
    int textureWidth,
    int textureHeight)
        {
            var labelMap = new Dictionary<(int, int), string>();
            var usedLabels = new HashSet<string>();
            var labelPixelCounts = new Dictionary<string, int>();
            labelGrid = new string[textureWidth, textureHeight];

            // Reproject UVs same as ApplyTextureCoordinates (X/Z scaled)
            var allUVs = new List<Point>();
            foreach (var mesh in meshes.Values)
            {
                foreach (var pos in mesh.Positions)
                {
                    allUVs.Add(new Point(pos.X * 0.1, pos.Z * 0.1));
                }
            }

            double minU = allUVs.Min(p => p.X);
            double maxU = allUVs.Max(p => p.X);
            double minV = allUVs.Min(p => p.Y);
            double maxV = allUVs.Max(p => p.Y);
            double rangeU = maxU - minU;
            double rangeV = maxV - minV;

            foreach (var kvp in meshes)
            {
                string label = kvp.Key;
                var mesh = kvp.Value;
                string normalizedLabel = NormalizeLabel(label);

                for (int i = 0; i < mesh.Positions.Count; i++)
                {
                    var pos = mesh.Positions[i];

                    double u = (pos.X * 0.1 - minU) / rangeU;
                    double v = (pos.Z * 0.1 - minV) / rangeV;
                    
                    int x = Math.Clamp((int)(u * textureWidth), 0, textureWidth - 1);
                    int y = Math.Clamp((int)(v * textureHeight), 0, textureHeight - 1);

                    
                    usedLabels.Add(normalizedLabel);
                    if (labelPixelCounts.ContainsKey(normalizedLabel))
                        labelPixelCounts[normalizedLabel]++;
                    else
                        labelPixelCounts[normalizedLabel] = 1;


                    // Paint a small square around the UV point to fill in more area
                    int radius = normalizedLabel == "cliff" ? 7 : 4;
                    //int radius = 2;
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        for (int dx = -radius; dx <= radius; dx++)
                        {
                            if (dx * dx + dy * dy <= radius * radius) // circle 
                            {
                                int px = x + dx;
                                int py = y + dy;

                                if (px >= 0 && px < textureWidth && py >= 0 && py < textureHeight)
                                {
                                    labelMap[(px, py)] = normalizedLabel;
                                }
                            }
                        }
                    }


                }
            }

            Color? GetColorForLabel(string label)
            {
                if (LabelColors.TryGetValue(label, out var color))
                    return color;

                return null; // explicitly skip unknown labels
            }

            var wb = new WriteableBitmap(textureWidth, textureHeight, 96, 96, PixelFormats.Bgra32, null);
            byte[] pixels = new byte[textureWidth * textureHeight * 4];

            for (int y = 0; y < textureHeight; y++)
            {
                for (int x = 0; x < textureWidth; x++)
                {
                    string label = labelMap.TryGetValue((x, y), out var lbl) ? lbl : "unknown";

                    var colorOpt = GetColorForLabel(label);

                    if (colorOpt is Color color)
                    {
                        labelGrid[x, y] = GetTerrainTypeFromColor(color);
                        int idx = (y * textureWidth + x) * 4;
                        pixels[idx + 0] = color.B;
                        pixels[idx + 1] = color.G;
                        pixels[idx + 2] = color.R;
                        pixels[idx + 3] = color.A;
                    }
                  
                }
            }

            wb.WritePixels(new Int32Rect(0, 0, textureWidth, textureHeight), pixels, textureWidth * 4, 0);

            Debug.WriteLine("--- Label pixel counts in GenerateDebugLabelTexture() ---");
            foreach (var kvp in labelPixelCounts.OrderByDescending(kvp => kvp.Value))
            {
                Debug.WriteLine($"• {kvp.Key}: {kvp.Value} pixels");
            }
            Debug.WriteLine("----------------------------------------------------------");

            return wb;
        }

        private string GetTerrainTypeFromColor(Color color)
        {
            if (color == Colors.ForestGreen) return "grass";
            if (color == Colors.SandyBrown) return "sand";
            if (color == Colors.Purple) return "rock";
            return "rock"; // fallback
        }

        private static readonly Dictionary<string, Color> LabelColors = new()
        {
            ["Mid"] = Colors.ForestGreen,
            ["ramp"] = Colors.ForestGreen,
            ["Base"] = Colors.ForestGreen,
            ["Top"] = Colors.ForestGreen,
            ["beach"] = Colors.SandyBrown,
            ["cliff"] = Colors.Purple,
            ["None"] = Colors.Purple,

            /*
            ["seam_Mid_None"] = Colors.Gray,
            ["seam_Base_Mid"] = Colors.Green,
            ["seam_Base_Mid_None"] = Colors.Gray,
            ["seam_Base_None"] = Colors.Gray,
            ["seam_Mid_ramp"] = Colors.Green,
            ["seam_Mid_ramp_Top"] = Colors.Green,
            ["seam_Mid_Top"] = Colors.Gray,
            ["seam_ramp_Top"] = Colors.Green,
            ["seam_Base_Mid_Top"] = Colors.Green,
            ["seam_Base_Top"] = Colors.Green,
            ["seam_None_Top"] = Colors.Gray,
            ["seam_Base_None_Top"] = Colors.Gray,
            ["seam_Mid_None_ramp"] = Colors.Gray,
            ["seam_None_ramp"] = Colors.Green,
            ["seam_Base_ramp"] = Colors.Green,
            ["seam_Base_ramp_Top"] = Colors.Green,
            ["seam_Base_Mid_ramp"] = Colors.Green,
            ["seam_Base_None_ramp"] = Colors.Gray,
            ["seam_Base_beach"] = Colors.SandyBrown,
            ["seam_beach_None"] = Colors.Gray,
            ["seam_Base_beach_None"] = Colors.Gray,
            */
        };


        private string NormalizeLabel(string rawLabel)
        {
            if (string.IsNullOrWhiteSpace(rawLabel))
                return "cliff";

            if (rawLabel.StartsWith("seam_"))
            {
               
                return "cliff"; // fallback if unknown seam
            }

            if (new[] { "Mid", "ramp", "Base", "Top", "beach" }.Contains(rawLabel))
                return rawLabel;

            return "cliff"; // catch-all fallback
        }

        private void SaveBitmapToFile(WriteableBitmap bitmap, string filename)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            string directory = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "IsleForge_Debug"
            );

            System.IO.Directory.CreateDirectory(directory);

            string path = System.IO.Path.Combine(directory, filename);

            using (var stream = new System.IO.FileStream(path, System.IO.FileMode.Create))
            {
                encoder.Save(stream);
            }

            Debug.WriteLine($"Saved debug texture to: {path}");
        }

        private MeshGeometry3D CombineMeshes(Dictionary<string, MeshGeometry3D> meshes)
        {
            var combined = new MeshGeometry3D();
            int indexOffset = 0;

            foreach (var mesh in meshes.Values)
            {
                foreach (var pos in mesh.Positions)
                    combined.Positions.Add(pos);

                foreach (var index in mesh.TriangleIndices)
                    combined.TriangleIndices.Add(index + indexOffset);

                indexOffset += mesh.Positions.Count;
            }

            return combined;
        }

        private void ApplyTopDownUV(MeshGeometry3D mesh, double scale = 0.1)
        {
            var coords = new PointCollection();

            foreach (var pos in mesh.Positions)
                coords.Add(new Point(pos.X * scale, pos.Z * scale));

            mesh.TextureCoordinates = coords;
        }

        private WriteableBitmap GenerateFinalTextureFromLabelGrid(
    string[,] labelGrid,
    BitmapSource grassTex,
    BitmapSource rockTex,
    BitmapSource sandTex,
    int width,
    int height,
    double tileScale = 0.05)
        {
            var wb = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            byte[] finalPixels = new byte[width * height * 4];

            var texLookup = new Dictionary<string, BitmapSource>
            {
                ["grass"] = grassTex,
                ["rock"] = rockTex,
                ["sand"] = sandTex
            };

            int tileWidth = (int)(1.0 / tileScale);

            foreach (var kvp in texLookup)
            {
                if (kvp.Value.Format != PixelFormats.Bgra32)
                    throw new InvalidOperationException($"Texture for {kvp.Key} must be Bgra32.");
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    string type = labelGrid[x, y];
                    if (string.IsNullOrEmpty(type))
                        type = "rock";
                    if (!texLookup.TryGetValue(type, out var sourceTex))
                        sourceTex = rockTex; // fallback

                    int srcW = sourceTex.PixelWidth;
                    int srcH = sourceTex.PixelHeight;

                    double u = (x * tileScale) % 1.0;
                    double v = (y * tileScale) % 1.0;

                    int tx = (int)(u * srcW) % srcW;
                    int ty = (int)(v * srcH) % srcH;

                    byte[] pixelData = new byte[4];
                    var rect = new Int32Rect(tx, ty, 1, 1);
                    sourceTex.CopyPixels(rect, pixelData, 4, 0);

                    int idx = (y * width + x) * 4;
                    finalPixels[idx + 0] = pixelData[0];
                    finalPixels[idx + 1] = pixelData[1];
                    finalPixels[idx + 2] = pixelData[2];
                    finalPixels[idx + 3] = 255;
                }
            }

            wb.WritePixels(new Int32Rect(0, 0, width, height), finalPixels, width * 4, 0);
            return wb;
        }



        #endregion
    }
}