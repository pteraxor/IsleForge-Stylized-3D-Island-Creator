﻿using System;
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
using IsleForge.PageStates;
using SharpDX.Direct3D11;

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

        private BitmapSource _grassBitmapSource;
        private BitmapSource _rockBitmapSource;
        private BitmapSource _sandBitmapSource;

        private MeshGeometry3D combinedMesh;
        private WriteableBitmap builtTexture;

        private Point3D cameraRecenterOffset;

        private const string DefaultGrass = "GrassAlbedo.png";
        private const string DefaultRock = "RockAlbedo.png";
        private const string DefaultSand = "SandAlbedo.png";

        public double TileScale = 5;

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


            _MapCanvas = HelperExtensions.FindElementByTag<Canvas>(this, "MapCanvas");


            //this worked fine on the previous page
            //_viewport3D = FindVisualChild<Viewport3D>(this);
            //_modelGroup = new Model3DGroup();

            _viewport3D = FindViewport3D(this);
            _modelGroup = FindSceneModelGroup(_viewport3D);


            _modelGroup.Children.Clear();


            _grassBitmapSource = LoadBitmap("GrassAlbedo.png");
            _rockBitmapSource = LoadBitmap("RockAlbedo.png");
            _sandBitmapSource = LoadBitmap("SandAlbedo.png");

            TileScale = App.CurrentSettings.TextureTiling;
            UpdateUIFromSaved();
            //here is a good stopping point

            Task.Run(() => LoadAfterPageLoads());

        }

        private void UpdateUIFromSaved()
        {
            var TilingLabel = HelperExtensions.FindElementByTag<TextBox>(this, "TextureSizeLabel");
            var TilingSlider = HelperExtensions.FindElementByTag<Slider>(this, "TextureSize");

            TilingLabel.Text = TileScale.ToString();
            TilingSlider.Value = TileScale;

        }

        #region async loading

        private async Task LoadAfterPageLoads3()
        {
            // Do all heavy calculations and data prep first
            var debugTexture = GenerateDebugLabelTexture(_meshes, 1024, 1024);
            builtTexture = GenerateFinalTextureFromLabelGrid(
                    labelGrid,
                    _grassBitmapSource, _rockBitmapSource, _sandBitmapSource,
                    1024, 1024,
                    tileScale: TileScale
                );
            //var combined = CombineMeshes(_meshes);
            combinedMesh = CombineMeshes(_meshes);
            Point3D desiredCenter = MeshDataStore.MeshCalculatedCenter;
            MeshGeometry3D centeredMesh = CenterMeshToPoint(combinedMesh, desiredCenter);

            combinedMesh = centeredMesh;
            ApplyTopDownUV(combinedMesh, 0.1);

            var material = new DiffuseMaterial(new ImageBrush(builtTexture) { Stretch = Stretch.Fill });

            //Only when everything is READY
            Application.Current.Dispatcher.Invoke(() =>
            {
                //Only light UI object creation here
                //builtTexture = builtTex;
                _modelGroup.Children.Clear();
                _modelGroup.Children.Add(new GeometryModel3D(combinedMesh, material));
                SetCameraToMesh();
            });
        }


        private async Task LoadAfterPageLoads()
        {
            
            Application.Current.Dispatcher.Invoke(() =>
            {

                var debugTexture = GenerateDebugLabelTexture(_meshes, 1024, 1024);


                builtTexture = GenerateFinalTextureFromLabelGrid(
                    labelGrid,
                    _grassBitmapSource, _rockBitmapSource, _sandBitmapSource,
                    1024, 1024,
                    tileScale: TileScale
                );

                //var material = new DiffuseMaterial(new ImageBrush(debugTexture) { Stretch = Stretch.Fill });
                var material = new DiffuseMaterial(new ImageBrush(builtTexture) { Stretch = Stretch.Fill });



                //redo mesh making

                combinedMesh = CombineMeshes(_meshes);
                Point3D desiredCenter = MeshDataStore.MeshCalculatedCenter;
                MeshGeometry3D centeredMesh = CenterMeshToPoint(combinedMesh, desiredCenter);

                combinedMesh = centeredMesh;

                ApplyTopDownUV(combinedMesh, 0.1);


                _modelGroup.Children.Clear();
                _modelGroup.Children.Add(new GeometryModel3D(combinedMesh, material));


                SetCameraToMesh();
            });

                     
        }

        #endregion


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
                var imagePath = openFileDialog.FileName;

                var brush = LoadTilingBrushFromFile(imagePath);
                var bitmap = LoadBitmapSourceFromFile(imagePath);//new BitmapImage(new Uri(imagePath, UriKind.Absolute));

                switch (type.ToLower())
                {
                    case "grass":
                        _grassBrush = brush;
                        _grassBitmapSource = bitmap;
                        break;
                    case "sand":
                        _sandBrush = brush;
                        _sandBitmapSource = bitmap;
                        break;
                    case "rock":
                        _rockBrush = brush;
                        _rockBitmapSource = bitmap;
                        break;
                }

                GoodRetexture();
            }
        }





        private void ResetTexture_Click(object sender, RoutedEventArgs e)
        {
            _grassBrush = LoadTilingBrush(DefaultGrass);
            _rockBrush = LoadTilingBrush(DefaultRock);
            _sandBrush = LoadTilingBrush(DefaultSand);

            _grassBitmapSource = LoadBitmap("GrassAlbedo.png");
            _rockBitmapSource = LoadBitmap("RockAlbedo.png");
            _sandBitmapSource = LoadBitmap("SandAlbedo.png");

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

            _modelGroup.Children.Clear();
            
            builtTexture = GenerateFinalTextureFromLabelGrid(
                labelGrid,
                _grassBitmapSource, _rockBitmapSource, _sandBitmapSource,
                1024, 1024,
                tileScale: TileScale
            );

            var material = new DiffuseMaterial(new ImageBrush(builtTexture) { Stretch = Stretch.Fill });
            _modelGroup.Children.Add(new GeometryModel3D(combinedMesh, material));

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

        #region UI changes

        private void TextureSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            TileScale = e.NewValue;

            var brushSizeLabel = HelperExtensions.FindElementByTag<TextBox>(this, "TextureSizeLabel");
            if (brushSizeLabel != null)
            {
                brushSizeLabel.Text = TileScale.ToString();
            }

            Debug.WriteLine($"tiling size changed to {TileScale}");
        }
        private void UseTextBox_Click(object sender, RoutedEventArgs e)
        {
            var textBox = HelperExtensions.FindElementByTag<TextBox>(this, "TextureSizeLabel");
            //string newText = textBox?.Text;
            var input = textBox.Text;

            //parse the result for a double
            if (double.TryParse(input, out double result))
            {
                TileScale = result;
                var TilingSlider = HelperExtensions.FindElementByTag<Slider>(this, "TextureSize");
                TilingSlider.Value = TileScale;
            }
            else
            {
                textBox.Text = TileScale.ToString();
            }
          

        }


        private void ExportTexture_Click(object sender, RoutedEventArgs e)
        {
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PNG Image|*.png",
                Title = "Save Generated Texture"
            };

            if (saveDialog.ShowDialog() == true)
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(builtTexture));

                using (var stream = new FileStream(saveDialog.FileName, FileMode.Create))
                {
                    encoder.Save(stream);
                }

                MessageBox.Show("Texture saved to:\n" + saveDialog.FileName, "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ExportMesh_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine($"UV count: {combinedMesh.TextureCoordinates.Count}");
            Debug.WriteLine($"Vertex count: {combinedMesh.Positions.Count}");

            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Wavefront OBJ|*.obj",
                Title = "Save Combined Mesh"
            };

            if (saveDialog.ShowDialog() == true)
            {
                using (var writer = new StreamWriter(saveDialog.FileName))
                {
                    writer.WriteLine("# Exported OBJ mesh");

                    // Write vertex positions
                    foreach (var p in combinedMesh.Positions)
                        writer.WriteLine($"v {p.X:0.######} {p.Y:0.######} {p.Z:0.######}");

                    // Write UVs (if available)
                    bool hasUVs = combinedMesh.TextureCoordinates != null &&
                                  combinedMesh.TextureCoordinates.Count == combinedMesh.Positions.Count;

                    if (hasUVs)
                    {
                        foreach (var uv in combinedMesh.TextureCoordinates)
                            writer.WriteLine($"vt {uv.X:0.######} {1.0 - uv.Y:0.######}"); // Flip V for compatibility
                    }

                    // Write faces
                    for (int i = 0; i < combinedMesh.TriangleIndices.Count; i += 3)
                    {
                        int i0 = combinedMesh.TriangleIndices[i] + 1;
                        int i1 = combinedMesh.TriangleIndices[i + 1] + 1;
                        int i2 = combinedMesh.TriangleIndices[i + 2] + 1;

                        if (hasUVs)
                            writer.WriteLine($"f {i0}/{i0} {i1}/{i1} {i2}/{i2}");
                        else
                            writer.WriteLine($"f {i0} {i1} {i2}");
                    }
                }

                MessageBox.Show("Mesh saved to:\n" + saveDialog.FileName, "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ExportMaterialFile(string objPath, string textureFileName, string matName)
        {
            string mtlPath = System.IO.Path.ChangeExtension(objPath, ".mtl");
            string materialName = matName;// "IslandPackedMat"; //

            using (var writer = new StreamWriter(mtlPath))
            {
                writer.WriteLine("# Exported MTL file");
                writer.WriteLine($"newmtl {materialName}");
                writer.WriteLine("Ka 1.000 1.000 1.000"); // Ambient color
                writer.WriteLine("Kd 1.000 1.000 1.000"); // Diffuse color
                writer.WriteLine("Ks 0.000 0.000 0.000"); // Specular color
                writer.WriteLine("d 1.0");                // Transparency (1 = opaque)
                writer.WriteLine("illum 1");              // Illumination model
                writer.WriteLine($"map_Kd {System.IO.Path.GetFileName(textureFileName)}"); // Diffuse texture
            }
        }

        private void ExportPacked_Click(object sender, RoutedEventArgs e)
        {
            string materialName = "IslandPackedMat";

            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Wavefront OBJ|*.obj",
                Title = "Save Combined Mesh"
            };

            if (saveDialog.ShowDialog() == true)
            {
                string objPath = saveDialog.FileName;
                string textureFileName = System.IO.Path.ChangeExtension(objPath, ".png");

                // 1. Save OBJ
                using (var writer = new StreamWriter(objPath))
                {
                    writer.WriteLine("# Exported OBJ mesh");
                    writer.WriteLine($"mtllib {System.IO.Path.GetFileNameWithoutExtension(objPath)}.mtl"); // <<< important
                    writer.WriteLine($"usemtl {materialName}");

                    foreach (var p in combinedMesh.Positions)
                        writer.WriteLine($"v {p.X:0.######} {p.Y:0.######} {p.Z:0.######}");

                    bool hasUVs = combinedMesh.TextureCoordinates != null &&
                                  combinedMesh.TextureCoordinates.Count == combinedMesh.Positions.Count;

                    if (hasUVs)
                    {
                        foreach (var uv in combinedMesh.TextureCoordinates)
                            writer.WriteLine($"vt {uv.X:0.######} {1.0 - uv.Y:0.######}");
                    }

                    for (int i = 0; i < combinedMesh.TriangleIndices.Count; i += 3)
                    {
                        int i0 = combinedMesh.TriangleIndices[i] + 1;
                        int i1 = combinedMesh.TriangleIndices[i + 1] + 1;
                        int i2 = combinedMesh.TriangleIndices[i + 2] + 1;

                        if (hasUVs)
                            writer.WriteLine($"f {i0}/{i0} {i1}/{i1} {i2}/{i2}");
                        else
                            writer.WriteLine($"f {i0} {i1} {i2}");
                    }
                }

                // 2. Save Texture PNG
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(builtTexture));
                using (var stream = new FileStream(textureFileName, FileMode.Create))
                {
                    encoder.Save(stream);
                }

                // 3. Save MTL
                ExportMaterialFile(objPath, textureFileName, materialName);

                MessageBox.Show($"Exported:\n{objPath}\n{textureFileName}\n{System.IO.Path.ChangeExtension(objPath, ".mtl")}", "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }


        #endregion

        #region carry over


        private void SetCameraToMesh()
        {

            var recalcCenterCamera = new Point3D(
                    MeshDataStore.CameraPosition.X - MeshDataStore.MeshCalculatedCenter.X,
                    MeshDataStore.CameraPosition.Y,
                    MeshDataStore.CameraPosition.Z - MeshDataStore.MeshCalculatedCenter.Z
                );

            var camera = new PerspectiveCamera
            {
                Position = recalcCenterCamera,
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
            var result = MessageBox.Show(
                    $"Are you sure you want to return to the previous page? your progress will not be saved",
                    "Return to previous page?",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

            if (result == MessageBoxResult.No)
            {
                return; // User said NO — cancel
            }

            //PageStateStore = null;

            if (this.NavigationService.CanGoBack)
            {
                this.NavigationService.GoBack();
            }

        }
        private void Next_Click(object sender, RoutedEventArgs e)
        {
            //for when it's time to to the next step. need to consider how to check for when it's okay to do so
            //NavigationService.Navigate(new TexturingPage());
        }

        #region trying new  methods


        private BitmapSource LoadBitmapSourceFromFile(string path)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();

            // Convert to Bgra32 if needed
            if (bitmap.Format != PixelFormats.Bgra32)
            {
                var converted = new FormatConvertedBitmap();
                converted.BeginInit();
                converted.Source = bitmap;
                converted.DestinationFormat = PixelFormats.Bgra32;
                converted.EndInit();
                converted.Freeze();
                return converted;
            }

            return bitmap;
        }


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

        

        private MeshGeometry3D CombineMeshes(Dictionary<string, MeshGeometry3D> meshes)
        {
            var combined = new MeshGeometry3D();
            var vertexMap = new Dictionary<(Point3D, Point), int>(); // (position, uv) → index
            int nextIndex = 0;

            foreach (var mesh in meshes.Values)
            {
                for (int i = 0; i < mesh.Positions.Count; i++)
                {
                    Point3D pos = mesh.Positions[i];
                    Point uv = mesh.TextureCoordinates.Count > i ? mesh.TextureCoordinates[i] : new Point(0, 0);

                    var key = (pos, uv);
                    if (!vertexMap.TryGetValue(key, out int index))
                    {
                        index = nextIndex++;
                        vertexMap[key] = index;
                        combined.Positions.Add(pos);
                        combined.TextureCoordinates.Add(uv);
                    }
                }
            }

            // Rebuild triangles using fused indices
            foreach (var mesh in meshes.Values)
            {
                for (int i = 0; i < mesh.TriangleIndices.Count; i += 3)
                {
                    int i0 = mesh.TriangleIndices[i];
                    int i1 = mesh.TriangleIndices[i + 1];
                    int i2 = mesh.TriangleIndices[i + 2];

                    var p0 = mesh.Positions[i0];
                    var p1 = mesh.Positions[i1];
                    var p2 = mesh.Positions[i2];

                    var uv0 = mesh.TextureCoordinates.Count > i0 ? mesh.TextureCoordinates[i0] : new Point(0, 0);
                    var uv1 = mesh.TextureCoordinates.Count > i1 ? mesh.TextureCoordinates[i1] : new Point(0, 0);
                    var uv2 = mesh.TextureCoordinates.Count > i2 ? mesh.TextureCoordinates[i2] : new Point(0, 0);

                    combined.TriangleIndices.Add(vertexMap[(p0, uv0)]);
                    combined.TriangleIndices.Add(vertexMap[(p1, uv1)]);
                    combined.TriangleIndices.Add(vertexMap[(p2, uv2)]);
                }
            }

            Debug.WriteLine($"[CombineMeshes] Final vertex count: {combined.Positions.Count}");
            return combined;
        }


        private void ApplyTopDownUV2(MeshGeometry3D mesh, double scale = 0.1)
        {
            var coords = new PointCollection();

            foreach (var pos in mesh.Positions)
                coords.Add(new Point(pos.X * scale, pos.Z * scale));

            mesh.TextureCoordinates = coords;
        }

        private void ApplyTopDownUV(MeshGeometry3D mesh, double scale = 0.1)
        {
            double minX = mesh.Positions.Min(p => p.X);
            double maxX = mesh.Positions.Max(p => p.X);
            double minZ = mesh.Positions.Min(p => p.Z);
            double maxZ = mesh.Positions.Max(p => p.Z);

            double rangeX = maxX - minX;
            double rangeZ = maxZ - minZ;

            var coords = new PointCollection();

            foreach (var pos in mesh.Positions)
            {
                double u = (pos.X - minX) / rangeX;
                double v = (pos.Z - minZ) / rangeZ;
                coords.Add(new Point(u, v));
            }

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


                    double u = ((double)x / width) * tileScale;
                    double v = ((double)y / height) * tileScale;
                    //u = u % 1.0;
                    //v = v % 1.0;

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

        private MeshGeometry3D CenterMeshToPoint(MeshGeometry3D mesh, Point3D targetCenter)
        {
            var recenteredMesh = new MeshGeometry3D();

            // Shift every point relative to the target center
            foreach (var point in mesh.Positions)
            {
                var newPoint = new Point3D(
                    point.X - targetCenter.X,
                    point.Y,
                    point.Z - targetCenter.Z
                );

                recenteredMesh.Positions.Add(newPoint);
            }

            // Copy triangle indices directly
            foreach (var index in mesh.TriangleIndices)
            {
                recenteredMesh.TriangleIndices.Add(index);
            }

            return recenteredMesh;
        }


        private MeshGeometry3D CenterMeshToPoint2(MeshGeometry3D mesh, Point3D newCenter)
        {
            var centeredMesh = new MeshGeometry3D();

            // Calculate current center
            Point3D currentCenter = new Point3D(
                mesh.Positions.Average(p => p.X),
                mesh.Positions.Average(p => p.Y),
                mesh.Positions.Average(p => p.Z)
            );

            // Calculate offset
            Vector3D offset = newCenter - currentCenter;

            // Shift each position
            foreach (var pt in mesh.Positions)
            {
                centeredMesh.Positions.Add(new Point3D(pt.X + offset.X, pt.Y + offset.Y, pt.Z + offset.Z));
            }

            // Copy triangle indices
            foreach (var index in mesh.TriangleIndices)
            {
                centeredMesh.TriangleIndices.Add(index);
            }

            return centeredMesh;
        }


        #endregion
    }
}