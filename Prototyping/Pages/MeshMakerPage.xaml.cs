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
        private LabeledValue[,] labeledHeightMapOriginal;

        private Dictionary<string, Point3DCollection> originalMeshPositions = new Dictionary<string, Point3DCollection>();


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

            labeledHeightMapOriginal = CloneHeightMap(labeledHeightMap); //so we have a backup
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
            CreateSeperateMeshes();
            return;
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

        private void UpdateSceneWithMeshes(Dictionary<string, MeshGeometry3D> meshes)
        {
            _modelGroup.Children.Clear();

            foreach (var kvp in meshes)
            {
                var label = kvp.Key;
                var mesh = kvp.Value;

                var color = GetColorForLabel(label); // assign color per label
                var material = new DiffuseMaterial(new SolidColorBrush(color));
                var model = new GeometryModel3D(mesh, material)
                {
                    BackMaterial = material
                };

                _modelGroup.Children.Add(model);
            }

            // Optional: reset camera based on updated mesh
            Point3D center = GetMeshCenter(labeledHeightMap);
            SetCameraToMesh(_viewport3D, center);
        }


        private void CreateSeperateMeshes()
        {
            var meshes = CreateMeshesByLabel(labeledHeightMap);
            _modelGroup.Children.Clear();

            foreach (var kvp in meshes)
            {
                var label = kvp.Key;
                var mesh = kvp.Value;

                var color = GetColorForLabel(label); // assign color per label
                var material = new DiffuseMaterial(new SolidColorBrush(color));
                var model = new GeometryModel3D(mesh, material)
                {
                    BackMaterial = material
                };

                // Save original positions for reset
                originalMeshPositions[label] = new Point3DCollection(mesh.Positions);

                // Tag the model with the label
                model.SetValue(FrameworkElement.TagProperty, label);

                //_modelGroup.Children.Add(new ModelVisual3D { Content = model });
                _modelGroup.Children.Add(model);
            }



            Point3D center = GetMeshCenter(labeledHeightMap);
            SetCameraToMesh(_viewport3D, center);

            //Debug.WriteLine("this is for sure called");

        }

        private void ExportMesh_Click(object sender, RoutedEventArgs e)
        {
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "heightmapGroup.obj");



            ExportModelGroupToObj(_modelGroup, path);

            return;


            if (heightMap == null)
            {
                MessageBox.Show("Heightmap not loaded.");
                return;
            }

            //CreateLabeledHeightmapLayer(labeledHeightMap);
            //CreateALayerHeightmap(heightMap);
            //return;

            //string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "heightmap.obj");
            //ExportHeightmapToObj(heightMap, path);
            ExportLabeledHeightmapToObj(labeledHeightMap, path);
            //ExportLabeledHeightmapWithAngleUVs(labeledHeightMap, path);
        }

        private void ResetNoise_Click(object sender, RoutedEventArgs e)
        {
            ResetMeshesToOriginal();
        }

        private void ResetMap()
        {
            //this will need to be done a different way later on
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            labeledHeightMap = LoadLabeledMapFromText(System.IO.Path.Combine(baseDir, "solvedMapWithLabels.txt"));
        }


        private void ApplyNoise_Click(object sender, RoutedEventArgs e)
        {
            //start noise application by reseting map
            //ResetMap();
            ResetMeshesToOriginal();

            float strength = HelperExtensions.GetFloatFromTag(this, "NoiseStrength", .2f);
            float scale = HelperExtensions.GetFloatFromTag(this, "NoiseScale", 0.1f);
            int octaves = (int)HelperExtensions.GetFloatFromTag(this, "NoiseOctaves", 4f);
            float lacunarity = HelperExtensions.GetFloatFromTag(this, "NoiseLacunarity", 2f);

            //ApplyPerlinNoiseToHeightMap(strength, scale, octaves, 0.5f, lacunarity);
            ApplyNoiseToMeshes(strength, scale, octaves, 0.5f, lacunarity);

            return;
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

            // Track index mapping for valid vertices only
            int[,] indexMap = new int[width, height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    indexMap[x, y] = -1; // -1 means "not added"

            int currentIndex = 0;

            // Step 1: Add valid positions to mesh
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float val = heightMap[x, y].Value;
                    if (val > 0f)
                    {
                        Point3D point = new Point3D(x, val, y);
                        mesh.Positions.Add(point);
                        indexMap[x, y] = currentIndex++;
                    }
                }
            }

            // Step 2: Add triangles only if all 4 points exist
            for (int y = 0; y < height - 1; y++)
            {
                for (int x = 0; x < width - 1; x++)
                {
                    int i00 = indexMap[x, y];
                    int i10 = indexMap[x + 1, y];
                    int i01 = indexMap[x, y + 1];
                    int i11 = indexMap[x + 1, y + 1];

                    // Ensure all four vertices are valid
                    if (i00 >= 0 && i10 >= 0 && i01 >= 0 && i11 >= 0)
                    {
                        // Triangle 1
                        mesh.TriangleIndices.Add(i00);
                        mesh.TriangleIndices.Add(i11);
                        mesh.TriangleIndices.Add(i10);

                        // Triangle 2
                        mesh.TriangleIndices.Add(i00);
                        mesh.TriangleIndices.Add(i01);
                        mesh.TriangleIndices.Add(i11);
                    }
                }
            }

            return mesh;
        }


        private MeshGeometry3D CreateMeshGeometryFromHeightMap2(LabeledValue[,] heightMap)
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

        private void ExportModelGroupToObj(Model3DGroup modelGroup, string filePath)
        {
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                writer.WriteLine("# Exported OBJ from Model3DGroup");

                int vertexOffset = 1;

                for (int modelIndex = 0; modelIndex < modelGroup.Children.Count; modelIndex++)
                {
                    if (!(modelGroup.Children[modelIndex] is GeometryModel3D geom))
                        continue;

                    string groupName = "group_" + modelIndex;

                    if (geom.Material is DiffuseMaterial mat && mat.Brush is SolidColorBrush brush)
                        groupName = brush.Color.ToString(); // Optional: use color as name

                    if (!(geom.Geometry is MeshGeometry3D mesh))
                        continue;

                    writer.WriteLine($"g {groupName}");

                    // Write vertices
                    foreach (var pos in mesh.Positions)
                        writer.WriteLine($"v {pos.X:0.######} {pos.Y:0.######} {pos.Z:0.######}");

                    // Write normals (optional)
                    bool hasNormals = mesh.Normals != null && mesh.Normals.Count == mesh.Positions.Count;
                    if (hasNormals)
                    {
                        foreach (var n in mesh.Normals)
                            writer.WriteLine($"vn {n.X:0.######} {n.Y:0.######} {n.Z:0.######}");
                    }

                    // Write UVs (optional)
                    bool hasUVs = mesh.TextureCoordinates != null && mesh.TextureCoordinates.Count == mesh.Positions.Count;
                    if (hasUVs)
                    {
                        foreach (var uv in mesh.TextureCoordinates)
                            writer.WriteLine($"vt {uv.X:0.######} {uv.Y:0.######}");
                    }

                    // Write faces
                    for (int i = 0; i < mesh.TriangleIndices.Count; i += 3)
                    {
                        int i0 = mesh.TriangleIndices[i] + vertexOffset;
                        int i1 = mesh.TriangleIndices[i + 1] + vertexOffset;
                        int i2 = mesh.TriangleIndices[i + 2] + vertexOffset;

                        if (hasNormals && hasUVs)
                            writer.WriteLine($"f {i0}/{i0}/{i0} {i1}/{i1}/{i1} {i2}/{i2}/{i2}");
                        else if (hasUVs)
                            writer.WriteLine($"f {i0}/{i0} {i1}/{i1} {i2}/{i2}");
                        else if (hasNormals)
                            writer.WriteLine($"f {i0}//{i0} {i1}//{i1} {i2}//{i2}");
                        else
                            writer.WriteLine($"f {i0} {i1} {i2}");
                    }

                    vertexOffset += mesh.Positions.Count;
                }
            }

            MessageBox.Show("OBJ exported to:\n" + filePath);
        }


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

        private LabeledValue[,] CloneHeightMap(LabeledValue[,] source)
        {
            int width = source.GetLength(0);
            int height = source.GetLength(1);
            var clone = new LabeledValue[width, height];

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    clone[x, y] = new LabeledValue(source[x, y].Value, source[x, y].Label);

            return clone;
        }

        private void ResetMeshesToOriginal()
        {
            foreach (var child in _modelGroup.Children)
            {
                if (child is GeometryModel3D geom && geom.Geometry is MeshGeometry3D mesh)
                {
                    string label = GetLabelFromGeometryModel(geom);
                    if (originalMeshPositions.ContainsKey(label))
                    {
                        mesh.Positions = new Point3DCollection(originalMeshPositions[label]);
                    }
                }
            }
        }



        #region mesh logic

        private void ApplyNoiseToMeshes(float strength = 1f, float scale = 0.1f, int octaves = 4, float persistence = 0.5f, float lacunarity = 2f, string[] targetLabels = null)
        {
            var noiseGen = new Prototyping.Helpers.NoiseGenerator(new Random().Next());

            foreach (var child in _modelGroup.Children)
            {
                if (child is GeometryModel3D geom && geom.Geometry is MeshGeometry3D mesh)
                {
                    string label = GetLabelFromGeometryModel(geom); 

                    if (targetLabels == null || targetLabels.Contains(label))
                    {
                        var positions = mesh.Positions;

                        for (int i = 0; i < positions.Count; i++)
                        {
                            var pos = positions[i];

                            float noise = noiseGen.FractalNoise((float)pos.X * scale, (float)pos.Z * scale, octaves, 1f, persistence, lacunarity);
                            positions[i] = new Point3D(pos.X, pos.Y + noise * strength, pos.Z);
                        }

                        mesh.Positions = positions; // re-assign to force UI update
                    }
                }
            }
        }

        private string GetLabelFromGeometryModel(GeometryModel3D model)
        {
            return model.GetValue(FrameworkElement.TagProperty) as string ?? "unknown";
        }

        private void ApplyPerlinNoiseToHeightMapFirst(float strength = 1f, float scale = 0.1f, int octaves = 4, float persistence = 0.5f, float lacunarity = 2f)
        {
            int width = labeledHeightMap.GetLength(0);
            int height = labeledHeightMap.GetLength(1);

            // Replace Unity random with System.Random
            Random rand = new Random();
            float offsetX = (float)(rand.NextDouble() * 10000f);
            float offsetY = (float)(rand.NextDouble() * 10000f);

            // Use your helper
            var noiseGen = new Prototyping.Helpers.NoiseGenerator(rand.Next());

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float original = labeledHeightMap[x, y].Value;
                    if (original > 0f) // Don't add noise to ocean floor
                    {
                        float noise = noiseGen.FractalNoise(x + offsetX, y + offsetY, octaves, scale, persistence, lacunarity);
                        labeledHeightMap[x, y].Value = original + noise * strength;
                    }
                }
            }
        }

        

        Dictionary<string, MeshGeometry3D> labelMeshes = new Dictionary<string, MeshGeometry3D>();
        Dictionary<string, MeshGeometry3D> seamMeshes = new Dictionary<string, MeshGeometry3D>();

        class MeshBuilder
        {
            public MeshGeometry3D Mesh = new MeshGeometry3D();
            private Dictionary<Point3D, int> vertexIndices = new Dictionary<Point3D, int>();

            public int AddVertex(Point3D pt)
            {
                if (vertexIndices.TryGetValue(pt, out int index))
                    return index;

                index = Mesh.Positions.Count;
                Mesh.Positions.Add(pt);
                vertexIndices[pt] = index;
                return index;
            }

            public void AddTriangle(Point3D a, Point3D b, Point3D c)
            {
                int ia = AddVertex(a);
                int ib = AddVertex(b);
                int ic = AddVertex(c);
                Mesh.TriangleIndices.Add(ia);
                Mesh.TriangleIndices.Add(ib);
                Mesh.TriangleIndices.Add(ic);
            }
        }

        private Dictionary<string, MeshGeometry3D> CreateMeshesByLabel(LabeledValue[,] map)
        {
            int width = map.GetLength(0);
            int height = map.GetLength(1);

            var labelMeshes = new Dictionary<string, MeshBuilder>();
            var seamMeshes = new Dictionary<string, MeshBuilder>();
            var uniqueLabels = new HashSet<string>();

            double angleThreshold = 80.0;

            for (int y = 0; y < height - 1; y++)
            {
                for (int x = 0; x < width - 1; x++)
                {
                    var v00 = map[x, y];
                    var v10 = map[x + 1, y];
                    var v01 = map[x, y + 1];
                    var v11 = map[x + 1, y + 1];

                    if (v00.Value <= 0 || v10.Value <= 0 || v01.Value <= 0 || v11.Value <= 0)
                        continue;

                    var labels = new[] { v00.Label, v10.Label, v01.Label, v11.Label };
                    foreach (var label in labels)
                        uniqueLabels.Add(label);

                    var labelSet = new HashSet<string>(labels);

                    Point3D p00 = new Point3D(x, v00.Value, y);
                    Point3D p10 = new Point3D(x + 1, v10.Value, y);
                    Point3D p01 = new Point3D(x, v01.Value, y + 1);
                    Point3D p11 = new Point3D(x + 1, v11.Value, y + 1);

                    // === Priority 1: "ramp" or "beach" ===
                    if (labelSet.Contains("ramp") || labelSet.Contains("beach"))
                    {
                        string fallbackLabel = GetMostCommonLabel(labels);
                        if (!labelMeshes.ContainsKey(fallbackLabel))
                            labelMeshes[fallbackLabel] = new MeshBuilder();

                        var builder = labelMeshes[fallbackLabel];
                        builder.AddTriangle(p00, p11, p10);
                        builder.AddTriangle(p00, p01, p11);
                        continue;
                    }

                    // === Priority 2: angle > threshold ===
                    Vector3D normal = CalculateAverageNormal(p00, p11, p10, p01);
                    normal.Normalize();
                    Vector3D up = new Vector3D(0, 1, 0);
                    double dot = Vector3D.DotProduct(normal, up);
                    dot = Math.Max(-1.0, Math.Min(1.0, dot)); // Clamp
                    double angleFromUp = Math.Acos(dot) * (180.0 / Math.PI);

                    if (angleFromUp >= angleThreshold)
                    {
                        string seamKey = string.Join("_", labelSet.OrderBy(l => l));
                        if (!seamMeshes.ContainsKey(seamKey))
                            seamMeshes[seamKey] = new MeshBuilder();

                        var builder = seamMeshes[seamKey];
                        builder.AddTriangle(p00, p11, p10);
                        builder.AddTriangle(p00, p01, p11);
                        continue;
                    }

                    // === Priority 3: label-based (non-beach/ramp, non-steep)
                    if (labelSet.Count == 1)
                    {
                        string label = labels[0];
                        if (!labelMeshes.ContainsKey(label))
                            labelMeshes[label] = new MeshBuilder();

                        var builder = labelMeshes[label];
                        builder.AddTriangle(p00, p11, p10);
                        builder.AddTriangle(p00, p01, p11);
                    }
                    else
                    {
                        string fallbackLabel = GetMostCommonLabel(labels);
                        if (!labelMeshes.ContainsKey(fallbackLabel))
                            labelMeshes[fallbackLabel] = new MeshBuilder();

                        var builder = labelMeshes[fallbackLabel];
                        builder.AddTriangle(p00, p11, p10);
                        builder.AddTriangle(p00, p01, p11);
                    }
                }
            }

            var finalMeshes = new Dictionary<string, MeshGeometry3D>();

            foreach (var kvp in labelMeshes)
                finalMeshes[kvp.Key] = kvp.Value.Mesh;

            foreach (var kvp in seamMeshes)
                finalMeshes["seam_" + kvp.Key] = kvp.Value.Mesh;

            string labelList = string.Join(", ", uniqueLabels.OrderBy(l => l));
            Debug.WriteLine("Unique labels found in mesh: " + labelList);

            return finalMeshes;
        }

        private string GetMostCommonLabel(string[] labels)
        {
            var counts = new Dictionary<string, int>();
            foreach (var label in labels)
            {
                if (!counts.ContainsKey(label))
                    counts[label] = 0;
                counts[label]++;
            }

            // Return the most frequent one
            return counts.OrderByDescending(kv => kv.Value).First().Key;
        }


        private Vector3D CalculateAverageNormal(Point3D p0, Point3D p1, Point3D p2, Point3D p3)
        {
            // Triangle 1: p0 → p1 → p2
            Vector3D n1 = Vector3D.CrossProduct(p1 - p0, p2 - p0);
            n1.Normalize();

            // Triangle 2: p0 → p2 → p3
            Vector3D n2 = Vector3D.CrossProduct(p2 - p0, p3 - p0);
            n2.Normalize();

            Vector3D average = n1 + n2;
            if (average.Length > 0)
                average.Normalize();

            return average;
        }


        private Color GetColorForLabel(string label)
        {

            //Base, beach, Mid, None, ramp, Top
            // You can use fixed colors for known labels:
            if (label == "Mid") return Colors.Green;
            if (label == "Base") return Colors.Green;
            if (label == "ramp") return Colors.Green;
            if (label == "Top") return Colors.Green;
            if (label == "none") return Colors.Blue;
            if (label == "beach") return Colors.Goldenrod;
            if (label == "cliff") return Colors.Gray;
            //WEIRDLOL
            if (label == "WEIRDLOL") return Colors.Magenta;

            return Colors.Gray; //defaulting here for now instead of the hash

            // Or generate consistent colors for unknown labels
            int hash = label.GetHashCode();
            byte r = (byte)(hash & 0xFF);
            byte g = (byte)((hash >> 8) & 0xFF);
            byte b = (byte)((hash >> 16) & 0xFF);
            return Color.FromRgb(r, g, b);
        }


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
