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
using System.Windows.Media.Imaging;
using Prototyping.Tools;
using System.Diagnostics;
using Prototyping.Helpers;
using System.Windows.Media.Media3D;
using System.IO;

namespace Prototyping.Pages
{
    /// <summary>
    /// Interaction logic for MeshMakerPage.xaml
    /// </summary>
    public partial class MeshMakerPage : Page
    {
        private Canvas _MapCanvas;

        private float[,] heightMap;
        private float MAXVALUE = 100f;

        public MeshMakerPage()
        {
            InitializeComponent();
            this.Loaded += MeshMakerPage_Loaded;
        }

        private void MeshMakerPage_Loaded(object sender, RoutedEventArgs e)
        {
            _MapCanvas = HelperExtensions.FindElementByTag<Canvas>(this, "MapCanvas");

            //TESTINGLOAD();
            NORMALLOAD();


        }

        private void NORMALLOAD()
        {
            _MapCanvas.Children.Clear();
            heightMap = MapDataStore.FinalHeightMap;
            //heightMap = LoadFloatArrayFromFile(System.IO.Path.Combine(baseDir, "solvedMap.txt"));
            MAXVALUE = GetMaxValue(heightMap);

            
        }

        private void CreateMesh_Click(object sender, RoutedEventArgs e)
        {
            if (heightMap == null)
            {
                MessageBox.Show("Heightmap not loaded.");
                return;
            }

            CreateALayerHeightmap(heightMap);
            //return;

            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "heightmap.obj");
            ExportHeightmapToObj(heightMap, path);
        }

        #region mesh exporting

        private void ExportHeightmapToObj(float[,] heightMap, string filePath)
        {
            int width = heightMap.GetLength(0);
            int height = heightMap.GetLength(1);

            using (StreamWriter writer = new StreamWriter(filePath))
            {
                // 1. Write vertices
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        float z = heightMap[x, y];
                        writer.WriteLine($"v {x} {z} {y}");
                    }
                }

                // 2. Write faces
                // Vertex indices in OBJ are 1-based
                for (int y = 0; y < height - 1; y++)
                {
                    for (int x = 0; x < width - 1; x++)
                    {
                        int i0 = x + y * width + 1;
                        int i1 = (x + 1) + y * width + 1;
                        int i2 = x + (y + 1) * width + 1;
                        int i3 = (x + 1) + (y + 1) * width + 1;

                        // Two triangles per square
                        writer.WriteLine($"f {i0} {i3} {i1}");
                        writer.WriteLine($"f {i0} {i2} {i3}");
                    }
                }
            }

            MessageBox.Show("Heightmap exported to OBJ.");
        }
        #endregion

        #region testing
        private void TESTINGLOAD()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            heightMap = LoadFloatArrayFromFile(System.IO.Path.Combine(baseDir, "solvedMap.txt"));
            MAXVALUE = GetMaxValue(heightMap);
            //footprintMask = GetInverseMask(footprint);

            //Debug.WriteLine("baseLayer count: " + CountOnes(baseLayer));


            Debug.WriteLine("Test data loaded.");

            //CreateALayerHeightmap(heightMap);
        }

        private float[,] LoadFloatArrayFromFile(string filePath)
        {
            var lines = System.IO.File.ReadAllLines(filePath);
            int height = lines.Length;
            int width = lines[0].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;

            float[,] result = new float[width, height];

            for (int y = 0; y < height; y++)
            {
                var tokens = lines[y].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                for (int x = 0; x < width; x++)
                {
                    if (float.TryParse(tokens[x], out float val))
                        result[x, y] = val;
                    else
                        result[x, y] = 0f;
                }
            }

            return result;
        }

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

                        byte gray = (byte)Math.Max(0, Math.Min(255, (value / MAXVALUE) * 255));
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

            _MapCanvas.Children.Add(image);
        }

        #endregion

        private float GetMaxValue(float[,] data)
        {
            int width = data.GetLength(0);
            int height = data.GetLength(1);

            float max = float.MinValue;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (data[x, y] > max)
                    {
                        max = data[x, y];
                    }
                }
            }

            return max;
        }


        

    }
}
