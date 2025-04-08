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
using System.Windows.Media.Imaging;
using Prototyping.Tools;
using System.Diagnostics;
using Prototyping.Helpers;
using System.Windows.Media.Media3D;
using System.IO;
using System.Numerics;
using System.Text.RegularExpressions;

namespace Prototyping.Pages
{
    /// <summary>
    /// Interaction logic for MeshMakerPage.xaml
    /// </summary>
    public partial class MeshMakerPage : Page
    {
        private Canvas _MapCanvas;

        private float[,] heightMap;
        private LabeledValue[,] labeledHeightMap;
        private float MAXVALUE = 100f;

        private Viewport3D _viewport3D;
        private Model3DGroup _modelGroup;



        public MeshMakerPage()
        {
            InitializeComponent();
            this.Loaded += MeshMakerPage_Loaded;
        }

        private void MeshMakerPage_Loaded(object sender, RoutedEventArgs e)
        {
            //_MapCanvas = HelperExtensions.FindElementByTag<Canvas>(this, "MapCanvas");

            _MapCanvas = HelperExtensions.FindElementByTag<Canvas>(this, "MapCanvas");

            _viewport3D = FindVisualChild<Viewport3D>(this);
            _modelGroup = FindSceneModelGroup(_viewport3D);


            TESTINGLOAD();
            //NORMALLOAD();


        }

        private void NORMALLOAD()
        {
            //_MapCanvas.Children.Clear();
            heightMap = MapDataStore.FinalHeightMap;
            labeledHeightMap = MapDataStore.AnnotatedHeightMap;
            //heightMap = LoadFloatArrayFromFile(System.IO.Path.Combine(baseDir, "solvedMap.txt"));
            MAXVALUE = GetMaxValue(heightMap);


        }

        private void CreateMesh_Click(object sender, RoutedEventArgs e)
        {
            //CreateLabeledHeightmapLayer(labeledHeightMap);
            //labeledHeightMap

            if (labeledHeightMap == null)
            {
                MessageBox.Show("Labeled heightmap not loaded.");
                return;
            }

            MeshGeometry3D mesh = CreateMeshGeometryFromHeightMap(labeledHeightMap);
            Point3D center = GetMeshCenter(labeledHeightMap);
            SetCameraToMesh(_viewport3D, center);

            // You can change the material to reflect the label later
            var material = new DiffuseMaterial(new SolidColorBrush(Colors.LightGray));

            var model = new GeometryModel3D(mesh, material)
            {
                BackMaterial = material
            };

            _modelGroup.Children.Clear(); // Remove any previous mesh
            _modelGroup.Children.Add(model);
        }

        private void ExportMesh_Click(object sender, RoutedEventArgs e)
        {
            if (heightMap == null)
            {
                MessageBox.Show("Heightmap not loaded.");
                return;
            }

            //CreateLabeledHeightmapLayer(labeledHeightMap);
            //CreateALayerHeightmap(heightMap);
            //return;

            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "heightmap.obj");
            //ExportHeightmapToObj(heightMap, path);
            ExportLabeledHeightmapToObj(labeledHeightMap, path);
            //ExportLabeledHeightmapWithAngleUVs(labeledHeightMap, path);
        }

        #region viewing helpers

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

        private Model3DGroup FindSceneModelGroup(Viewport3D viewport)
        {
            foreach (var child in viewport.Children)
            {
                var visual = child as ModelVisual3D;
                if (visual?.Content is Model3DGroup group)
                    return group;
            }
            return null;
        }


        #endregion

        #region viewing start

        private MeshGeometry3D CreateMeshGeometryFromHeightMap(LabeledValue[,] heightMap)
        {
            int width = heightMap.GetLength(0);
            int height = heightMap.GetLength(1);

            MeshGeometry3D mesh = new MeshGeometry3D();

            // Track vertex indices for triangle generation
            int[,] indexMap = new int[width, height];
            int currentIndex = 0;

            // Step 1: Add Positions and build index map
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float z = heightMap[x, y].Value;
                    Point3D point = new Point3D(x, z, y);

                    mesh.Positions.Add(point);
                    indexMap[x, y] = currentIndex++;
                }
            }

            // Step 2: Add Triangle Indices
            for (int y = 0; y < height - 1; y++)
            {
                for (int x = 0; x < width - 1; x++)
                {
                    int i0 = indexMap[x, y];
                    int i1 = indexMap[x + 1, y];
                    int i2 = indexMap[x, y + 1];
                    int i3 = indexMap[x + 1, y + 1];

                    // Triangle 1
                    mesh.TriangleIndices.Add(i0);
                    mesh.TriangleIndices.Add(i3);
                    mesh.TriangleIndices.Add(i1);

                    // Triangle 2
                    mesh.TriangleIndices.Add(i0);
                    mesh.TriangleIndices.Add(i2);
                    mesh.TriangleIndices.Add(i3);
                }
            }

            return mesh;
        }


        private Point3D GetMeshCenter(LabeledValue[,] heightMap)
        {
            int width = heightMap.GetLength(0);
            int height = heightMap.GetLength(1);

            double sumX = 0;
            double sumZ = 0;
            double sumY = 0;
            int count = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float val = heightMap[x, y].Value;
                    if (val > 0f)
                    {
                        sumX += x;
                        sumZ += y;
                        sumY += val;
                        count++;
                    }
                }
            }

            if (count == 0)
                return new Point3D(width / 2.0, 0, height / 2.0); // fallback

            return new Point3D(sumX / count, sumY / count, sumZ / count);
        }

        private void SetCameraToMesh(Viewport3D viewport, Point3D center)
        {
            // Pull the camera back and up from the center
            Vector3D offset = new Vector3D(0, 300, 50);
            Point3D position = center + offset;

            Vector3D lookDirection = center - position;
            lookDirection.Normalize();

            var camera = new PerspectiveCamera
            {
                Position = position,
                LookDirection = lookDirection,
                UpDirection = new Vector3D(0, 1, 0),
                FieldOfView = 60
            };

            viewport.Camera = camera;
        }



        #endregion

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

        private void ExportLabeledHeightmapToObj(LabeledValue[,] labeledMap, string filePath)
        {
            int width = labeledMap.GetLength(0);
            int height = labeledMap.GetLength(1);

            Dictionary<string, List<string>> groupedFaces = new Dictionary<string, List<string>>();
            Dictionary<string, int> vertexIndexMap = new Dictionary<string, int>(); // key: "x,y", value: index
            StringBuilder vertexBuilder = new StringBuilder();
            int currentIndex = 1;

            // 1. Write all vertices and assign indices
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float value = labeledMap[x, y].Value;
                    string key = x + "," + y;

                    vertexBuilder.AppendLine(string.Format("v {0} {1} {2}", x, value, y));
                    vertexIndexMap[key] = currentIndex;
                    currentIndex++;
                }
            }

            // 2. Generate faces and assign them to groups by label
            for (int y = 0; y < height - 1; y++)
            {
                for (int x = 0; x < width - 1; x++)
                {
                    string key00 = x + "," + y;
                    string key10 = (x + 1) + "," + y;
                    string key01 = x + "," + (y + 1);
                    string key11 = (x + 1) + "," + (y + 1);

                    int i0 = vertexIndexMap[key00];
                    int i1 = vertexIndexMap[key10];
                    int i2 = vertexIndexMap[key01];
                    int i3 = vertexIndexMap[key11];

                    string label00 = labeledMap[x, y].Label;
                    string label10 = labeledMap[x + 1, y].Label;
                    string label01 = labeledMap[x, y + 1].Label;
                    string label11 = labeledMap[x + 1, y + 1].Label;

                    string label = MostCommonLabel(label00, label10, label01, label11);

                    if (!groupedFaces.ContainsKey(label))
                    {
                        groupedFaces[label] = new List<string>();
                    }

                    groupedFaces[label].Add(string.Format("f {0} {1} {2}", i0, i3, i1));
                    groupedFaces[label].Add(string.Format("f {0} {1} {2}", i0, i2, i3));
                }
            }

            // 3. Write to file
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                // Write all vertices first
                writer.Write(vertexBuilder.ToString());

                // Write grouped faces
                foreach (KeyValuePair<string, List<string>> group in groupedFaces)
                {
                    writer.WriteLine("g " + group.Key);

                    foreach (string face in group.Value)
                    {
                        writer.WriteLine(face);
                    }
                }
            }

            MessageBox.Show("Labeled OBJ exported.");
        }


        private string MostCommonLabel(params string[] labels)
        {
            return labels.GroupBy(l => l)
                         .OrderByDescending(g => g.Count())
                         .First().Key;
        }

       


        #endregion

        #region mesh logic

       

        


        #endregion

        #region testing

        public void CreateLabeledHeightmapLayer(LabeledValue[,] labeledMap)
        {
            int width = labeledMap.GetLength(0);
            int height = labeledMap.GetLength(1);
            float[,] valuesOnly = new float[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    valuesOnly[x, y] = labeledMap[x, y].Value;
                }
            }

            CreateALayerHeightmap(valuesOnly);
        }
        private void TESTINGLOAD()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            heightMap = LoadFloatArrayFromFile(System.IO.Path.Combine(baseDir, "solvedMap.txt"));
            labeledHeightMap = LoadLabeledMapFromText(System.IO.Path.Combine(baseDir, "solvedMapWithLabels.txt"));
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

        public static LabeledValue[,] LoadLabeledMapFromText(string path)
        {
            var lines = System.IO.File.ReadAllLines(path);
            int height = lines.Length;
            int width = lines[0].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;

            var result = new LabeledValue[width, height];

            for (int y = 0; y < height; y++)
            {
                var tokens = lines[y].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                for (int x = 0; x < width; x++)
                {
                    var parts = tokens[x].Split('|');
                    float value = float.Parse(parts[0]);
                    string label = parts.Length > 1 ? parts[1] : "";
                    result[x, y] = new LabeledValue(value, label);
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
