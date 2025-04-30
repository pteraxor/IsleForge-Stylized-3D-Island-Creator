using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using IsleForge.Helpers;
using IsleForge.PageStates;
using SharpDX;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;

namespace IsleForge.Pages
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

        private float MAXVALUE = 32f;
        private float MIDVALUE = 22f;
        private float LOWVALUE = 12f;

        private float noiseStrength = 0.5f;
        private float noiseScale = 0.07f;
        private int noiseOctaves = 4;
        private float noiseLacunarity = 2.0f;

        private Viewport3D _viewport3D;
        private Model3DGroup _modelGroup;

        private Boolean meshCreated = false;

        Dictionary<string, MeshGeometry3D> labelMeshes = new Dictionary<string, MeshGeometry3D>();
        Dictionary<string, MeshGeometry3D> seamMeshes = new Dictionary<string, MeshGeometry3D>();

        public MeshMakerPage()
        {
            InitializeComponent();
            this.Loaded += MeshMakerPage_Loaded;
        }

        private void MeshMakerPage_LoadedBeforeStates(object sender, RoutedEventArgs e)
        {
            _MapCanvas = HelperExtensions.FindElementByTag<Canvas>(this, "MapCanvas");

            _viewport3D = FindVisualChild<Viewport3D>(this);
            _modelGroup = FindSceneModelGroup(_viewport3D);

            MAXVALUE = MapDataStore.MaxHeightShare;
            MIDVALUE = MapDataStore.MidHeightShare;
            LOWVALUE = MapDataStore.LowHeightShare;

            LoadDataFromHeightMap();

        }

        private void MeshMakerPage_Loaded(object sender, RoutedEventArgs e)
        {
            _MapCanvas = HelperExtensions.FindElementByTag<Canvas>(this, "MapCanvas");

            _viewport3D = FindVisualChild<Viewport3D>(this);
            _modelGroup = FindSceneModelGroup(_viewport3D);

            MAXVALUE = MapDataStore.MaxHeightShare;
            MIDVALUE = MapDataStore.MidHeightShare;
            LOWVALUE = MapDataStore.LowHeightShare;

            if (PageStateStore.MeshMakerState != null)
            {
                RestorePageFromStoredState();
            }
            else
            {
                //load defaults from settings
                noiseStrength = App.CurrentSettings.NoiseStrength;
                noiseScale = App.CurrentSettings.NoiseScale;
                noiseOctaves = App.CurrentSettings.NoiseOctaves;
                noiseLacunarity = App.CurrentSettings.NoiseLacunarity;

                UpdateUIFromSaved();
                LoadDataFromHeightMap();
            }
        }


        #region buttons

        private void CreateMesh_Click(object sender, RoutedEventArgs e)
        {
            CreateSeperateMeshes();
        }
        private void ResetNoise_Click(object sender, RoutedEventArgs e)
        {
            ResetMeshesToOriginal();
        }

        private void ApplyNoise_Click(object sender, RoutedEventArgs e)
        {
            //start noise application by reseting map
            ResetMeshesToOriginal();

            noiseStrength = HelperExtensions.GetFloatFromTag(this, "NoiseStrength", .5f);
            noiseScale = HelperExtensions.GetFloatFromTag(this, "NoiseScale", 0.07f);
            noiseOctaves = (int)HelperExtensions.GetFloatFromTag(this, "NoiseOctaves", 4);
            noiseLacunarity = HelperExtensions.GetFloatFromTag(this, "NoiseLacunarity", 2f);

            ApplyNoiseToMeshes(noiseStrength, noiseScale, noiseOctaves, 0.5f, noiseLacunarity);

            return;
        }

        private void ExportMesh_Click(object sender, RoutedEventArgs e)
        {
            // string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "heightmapGroup.obj");

            //ExportModelGroupToObj(_modelGroup, path);
            string folderPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MeshExports");
            ExportEachMeshToObj(_modelGroup, folderPath);

            return;

        }

        #endregion

        #region loading

        private void LoadDataFromHeightMap()
        {
            //heightMap = MapDataStore.FinalHeightMap;
            labeledHeightMap = MapDataStore.AnnotatedHeightMap;
            MAXVALUE = MapDataStore.MaxHeightShare;//GetMaxValue(labeledHeightMap);
            //Debug.WriteLine("Test data loaded.");
            Debug.WriteLine($"MAXVALUE: {MAXVALUE}");
        }

        private float GetMaxValue(LabeledValue[,] data)
        {
            int width = data.GetLength(0);
            int height = data.GetLength(1);

            float max = float.MinValue;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float current = data[x, y].Value;

                    if (current > max)
                    {
                        max = current;
                    }
                }
            }

            return max;
        }

        #endregion

        #region mesh reset

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
        private string GetLabelFromGeometryModel(GeometryModel3D model)
        {
            return model.GetValue(FrameworkElement.TagProperty) as string ?? "unknown";
        }

        private string GetLabelFromGeometryModelCliff(GeometryModel3D model)
        {
            string label = model.GetValue(FrameworkElement.TagProperty) as string;

            return label switch
            {
                "Mid" => "Mid",
                "ramp" => "ramp",
                "Base" => "Base",
                "Top" => "Top",
                "beach" => "beach",
                null or "" => "cliff",
                _ => "cliff" // default fallback
            };
        }


        #endregion

        #region noise application

        private void ApplyNoiseToMeshes(float strength = 1f, float scale = 0.1f, int octaves = 4, float persistence = 0.5f, float lacunarity = 2f, string[] targetLabels = null)
        {
            var noiseGen = new IsleForge.Helpers.NoiseGenerator(new Random().Next());

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

        #endregion

        #region mesh creation

        private void CreateSeperateMeshes()
        {
            var meshes = CreateMeshesByLabel(labeledHeightMap);
            _modelGroup.Children.Clear();

            var subdividedMeshes = new Dictionary<string, MeshGeometry3D>();

            foreach (var kvp in meshes)
            {
                string label = kvp.Key;
                var mesh = kvp.Value;

                if (label.StartsWith("seam_"))
                {
                    subdividedMeshes[label] = SubdivideMesh(mesh);
                }
                else
                {
                    subdividedMeshes[label] = mesh;
                }
            }

            meshes = subdividedMeshes;

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
            MeshDataStore.MeshCalculatedCenter = center; //storing this now
            var RadiusIdea = EstimateNonZeroRadius(labeledHeightMap, center);
            Debug.WriteLine($"Radius: {RadiusIdea}");
            SetCameraToMesh(_viewport3D, center, (float)RadiusIdea);


            //Debug.WriteLine("this is for sure called");
            CreatedInitialMesh();
        }

        private void CreatedInitialMesh()
        {
            Debug.WriteLine("did initial mesh void");
            meshCreated = true;

            var applyNoiseButton = HelperExtensions.FindElementByTag<Button>(this, "ApplyNoiseButton");
            applyNoiseButton.IsEnabled = true;
            var resetNoiseButton = HelperExtensions.FindElementByTag<Button>(this, "ResetNoiseButton");
            resetNoiseButton.IsEnabled = true;
            var NextButton = HelperExtensions.FindElementByTag<Button>(this, "NextButton");
            NextButton.IsEnabled = true;

        }

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

            //trying threshold logic
            double gradientThreshold = 0.2; //control seam sensitivity

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
                    var labelSet = new HashSet<string>(labels);
                    foreach (var label in labelSet) uniqueLabels.Add(label);

                    Point3D p00 = new Point3D(x, v00.Value, y);
                    Point3D p10 = new Point3D(x + 1, v10.Value, y);
                    Point3D p01 = new Point3D(x, v01.Value, y + 1);
                    Point3D p11 = new Point3D(x + 1, v11.Value, y + 1);

                    // Same label for all 4 corners
                    if (labelSet.Count == 1)
                    {
                        string label = labels[0];
                        if (!labelMeshes.ContainsKey(label))
                            labelMeshes[label] = new MeshBuilder();

                        var builder = labelMeshes[label];
                        builder.AddTriangle(p00, p11, p10);
                        builder.AddTriangle(p00, p01, p11);
                        continue;
                    }

                    /*
                    //Use gradient to decide split direction(pre threshold version)
                    Vector3D gradient = new Vector3D(
                        (v10.Value - v00.Value + v11.Value - v01.Value), // x direction (left→right)
                        0,
                        (v01.Value - v00.Value + v11.Value - v10.Value)  // z direction (top→bottom)
                    );
                    gradient.Normalize();
                    */

                    //trying threshold version
                    double dx = (v10.Value - v00.Value + v11.Value - v01.Value);
                    double dz = (v01.Value - v00.Value + v11.Value - v10.Value);
                    Vector3D gradient = new Vector3D(dx, 0, dz);
                    double gradientStrength = gradient.Length;
                    //end trying threshold version

                    // Decide based on height difference across diagonals
                    double diag1 = Math.Abs(v00.Value - v11.Value);
                    double diag2 = Math.Abs(v10.Value - v01.Value);
                    bool splitDiag1 = diag1 >= diag2;

                    string seamKey = "seam_" + string.Join("_", labelSet.OrderBy(l => l));

                    /*
                    //pre threshold version
                    if (!seamMeshes.ContainsKey(seamKey))
                        seamMeshes[seamKey] = new MeshBuilder();
                    */
                    //thresh start
                    if (gradientStrength < gradientThreshold)
                    {
                        // Consider this quad "flat enough", fallback to dominant label
                        string fallbackLabel = GetMostCommonLabel(labels);
                        if (!labelMeshes.ContainsKey(fallbackLabel))
                            labelMeshes[fallbackLabel] = new MeshBuilder();

                        var builder = labelMeshes[fallbackLabel];

                        if (diag1 > diag2)
                        {
                            builder.AddTriangle(p00, p11, p10);
                            builder.AddTriangle(p00, p01, p11);
                        }
                        else
                        {
                            builder.AddTriangle(p00, p01, p10);
                            builder.AddTriangle(p01, p11, p10);
                        }
                        continue;
                    }

                    // If slope is steep enough, create seam
                    if (!seamMeshes.ContainsKey(seamKey))
                        seamMeshes[seamKey] = new MeshBuilder();
                    //thresh end

                    var seamBuilder = seamMeshes[seamKey];

                    if (splitDiag1)
                    {
                        seamBuilder.AddTriangle(p00, p11, p10);
                        seamBuilder.AddTriangle(p00, p01, p11);
                    }
                    else
                    {
                        seamBuilder.AddTriangle(p00, p01, p10);
                        seamBuilder.AddTriangle(p01, p11, p10);
                    }
                }
            }

            // Convert to MeshGeometry3D
            var finalMeshes = new Dictionary<string, MeshGeometry3D>();
            foreach (var kvp in labelMeshes)
                finalMeshes[kvp.Key] = kvp.Value.Mesh;
            foreach (var kvp in seamMeshes)
                finalMeshes[kvp.Key] = kvp.Value.Mesh;



            Debug.WriteLine("Unique labels found in mesh: " + string.Join(", ", uniqueLabels.OrderBy(l => l)));


            //trying a new extra step
            return finalMeshes; //old working one            
        }

        private Dictionary<string, MeshGeometry3D> WrapSingleMesh(MeshGeometry3D mesh, string label = "default")
        {
            return new Dictionary<string, MeshGeometry3D>
    {
        { label, mesh }
    };
        }

        private MeshGeometry3D CombineMesh(Dictionary<string, MeshGeometry3D> input)
        {
            var builder = new MeshBuilder();

            foreach (var kvp in input)
            {
                var mesh = kvp.Value;
                var name = kvp.Key;

                if (mesh.Positions.Count == 0 || mesh.TriangleIndices.Count == 0)
                {
                    Debug.WriteLine($"[CombineMesh] Skipping empty mesh: {name}");
                    continue;
                }

                for (int i = 0; i < mesh.TriangleIndices.Count; i += 3)
                {
                    if (i + 2 >= mesh.TriangleIndices.Count)
                    {
                        Debug.WriteLine($"[CombineMesh] Invalid triangle indices at index {i} in mesh {name}");
                        continue;
                    }

                    int i0 = mesh.TriangleIndices[i];
                    int i1 = mesh.TriangleIndices[i + 1];
                    int i2 = mesh.TriangleIndices[i + 2];

                    if (i0 >= mesh.Positions.Count || i1 >= mesh.Positions.Count || i2 >= mesh.Positions.Count)
                    {
                        Debug.WriteLine($"[CombineMesh] Index out of bounds in mesh {name}: {i0}, {i1}, {i2}");
                        continue;
                    }

                    var a = mesh.Positions[i0];
                    var b = mesh.Positions[i1];
                    var c = mesh.Positions[i2];

                    builder.AddTriangle(a, b, c);
                }
            }

            Debug.WriteLine($"[CombineMesh] Combined {input.Count} meshes into {builder.Mesh.TriangleIndices.Count / 3} triangles.");
            return builder.Mesh;
        }

        private Dictionary<string, MeshGeometry3D> CreateMeshesByLabelFirstGood(LabeledValue[,] map)
        {
            int width = map.GetLength(0);
            int height = map.GetLength(1);

            var labelMeshes = new Dictionary<string, MeshBuilder>();
            var seamMeshes = new Dictionary<string, MeshBuilder>();
            var uniqueLabels = new HashSet<string>();

            double angleThreshold = 120.0;

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
            //can use fixed colors for known labels:
            if (label == "Mid") return Colors.Green; //Colors.LightGreen;
            if (label == "Base") return Colors.Green;
            if (label == "ramp") return Colors.Green; //Colors.Aqua;
            if (label == "Top") return Colors.Green; //Colors.ForestGreen;
            if (label == "none") return Colors.Green; //Colors.Blue;
            if (label == "beach") return Colors.Goldenrod;
            if (label == "cliff") return Colors.DarkKhaki;
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

        #region camera helpers

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

        private double EstimateNonZeroRadius(LabeledValue[,] heightMap, Point3D center)
        {
            int width = heightMap.GetLength(0);
            int height = heightMap.GetLength(1);

            double maxDistanceSquared = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float val = heightMap[x, y].Value;
                    if (val > 0f)
                    {
                        double dx = x - center.X;
                        double dz = y - center.Z;

                        double distanceSquared = dx * dx + dz * dz;
                        if (distanceSquared > maxDistanceSquared)
                        {
                            maxDistanceSquared = distanceSquared;
                        }
                    }
                }
            }

            return Math.Sqrt(maxDistanceSquared);
        }

        private void SetCameraToMesh(Viewport3D viewport, Point3D center, float RadiusMath = 200)
        {
            // Pull the camera back and up from the center
            Vector3D offset = new Vector3D(-50, RadiusMath * 2, 90);
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

        private void ExportEachMeshToObj(Model3DGroup modelGroup, string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory); // Make sure output dir exists

            for (int modelIndex = 0; modelIndex < modelGroup.Children.Count; modelIndex++)
            {
                if (!(modelGroup.Children[modelIndex] is GeometryModel3D geom))
                    continue;

                if (!(geom.Geometry is MeshGeometry3D mesh))
                    continue;

                // Try to get label
                string label = geom.GetValue(FrameworkElement.TagProperty) as string ?? $"mesh_{modelIndex}";
                string safeLabel = string.Concat(label.Where(c => char.IsLetterOrDigit(c) || c == '_'));
                string filePath = System.IO.Path.Combine(outputDirectory, $"{safeLabel}.obj");

                using (StreamWriter writer = new StreamWriter(filePath))
                {
                    writer.WriteLine($"# Exported OBJ for mesh: {label}");

                    int vertexOffset = 1;

                    // Write vertices
                    foreach (var pos in mesh.Positions)
                        writer.WriteLine($"v {pos.X:0.######} {pos.Y:0.######} {pos.Z:0.######}");

                    bool hasNormals = mesh.Normals != null && mesh.Normals.Count == mesh.Positions.Count;
                    if (hasNormals)
                    {
                        foreach (var n in mesh.Normals)
                            writer.WriteLine($"vn {n.X:0.######} {n.Y:0.######} {n.Z:0.######}");
                    }

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
                }

                Debug.WriteLine($"Exported OBJ: {filePath}");
            }

            MessageBox.Show("All individual OBJ meshes exported.");
        }


        #endregion

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

        #region subdivide small pieces
        private MeshGeometry3D SubdivideMesh(MeshGeometry3D input)
        {
            var result = new MeshGeometry3D();
            var midpointCache = new Dictionary<(int, int), int>();

            int GetMidpoint(int i1, int i2)
            {
                var key = (Math.Min(i1, i2), Math.Max(i1, i2));
                if (midpointCache.TryGetValue(key, out int index))
                    return index;

                var p1 = input.Positions[i1];
                var p2 = input.Positions[i2];
                var mid = new Point3D(
                    (p1.X + p2.X) * 0.5,
                    (p1.Y + p2.Y) * 0.5,
                    (p1.Z + p2.Z) * 0.5
                );

                result.Positions.Add(mid);
                index = result.Positions.Count - 1;
                midpointCache[key] = index;
                return index;
            }

            // Copy all input vertices
            foreach (var p in input.Positions)
                result.Positions.Add(p);

            for (int i = 0; i < input.TriangleIndices.Count; i += 3)
            {
                int i0 = input.TriangleIndices[i];
                int i1 = input.TriangleIndices[i + 1];
                int i2 = input.TriangleIndices[i + 2];

                int a = GetMidpoint(i0, i1);
                int b = GetMidpoint(i1, i2);
                int c = GetMidpoint(i2, i0);

                // Create 4 subdivided triangles
                result.TriangleIndices.Add(i0);
                result.TriangleIndices.Add(a);
                result.TriangleIndices.Add(c);

                result.TriangleIndices.Add(i1);
                result.TriangleIndices.Add(b);
                result.TriangleIndices.Add(a);

                result.TriangleIndices.Add(i2);
                result.TriangleIndices.Add(c);
                result.TriangleIndices.Add(b);

                result.TriangleIndices.Add(a);
                result.TriangleIndices.Add(b);
                result.TriangleIndices.Add(c);
            }

            return result;
        }


        #endregion

        #region end buttons

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

            PageStateStore.MeshMakerState = null;

            if (this.NavigationService.CanGoBack)
            {
                this.NavigationService.GoBack();
            }

        }
        private void Next_Click(object sender, RoutedEventArgs e)
        {
            var finalMeshes = new Dictionary<string, MeshGeometry3D>();

            foreach (var child in _modelGroup.Children)
            {
                if (child is GeometryModel3D geom && geom.Geometry is MeshGeometry3D mesh)
                {
                    string label = GetLabelFromGeometryModel(geom);
                    Debug.WriteLine($"[EXPORT] Label: {label}");
                    finalMeshes[label] = mesh;
                }
            }



            MeshDataStore.Meshes = finalMeshes;
            MeshDataStore.OriginalMeshPositions = originalMeshPositions;

            //pass the camera along as well
            var camera = _viewport3D.Camera as PerspectiveCamera;

            if (camera != null)
            {
                MeshDataStore.CameraPosition = camera.Position;
                MeshDataStore.CameraLookDirection = camera.LookDirection;
                MeshDataStore.CameraUpDirection = camera.UpDirection;
            }

            SavePageStateBeforeLeaving();

            NavigationService.Navigate(new TexturePage());
        }

        #endregion

        #region page state management

        private void SavePageStateBeforeLeaving()
        {
            var labelMeshes = new Dictionary<string, MeshGeometry3D>();

            foreach (var child in _modelGroup.Children)
            {
                if (child is GeometryModel3D geom && geom.Geometry is MeshGeometry3D mesh)
                {
                    string label = geom.GetValue(FrameworkElement.TagProperty) as string ?? "unknown";
                    labelMeshes[label] = mesh;
                }
            }

            var camera = _viewport3D.Camera as PerspectiveCamera;

            PageStateStore.MeshMakerState = new MeshMakerPageState
            {
                LabelMeshes = labelMeshes,
                OriginalMeshPositions = new Dictionary<string, Point3DCollection>(originalMeshPositions),
                LabeledHeightMap = (LabeledValue[,])labeledHeightMap.Clone(),
                CameraPosition = camera?.Position ?? new Point3D(),
                CameraLookDirection = camera?.LookDirection ?? new Vector3D(),
                CameraUpDirection = camera?.UpDirection ?? new Vector3D(0, 1, 0),
                MeshCreated = meshCreated,
                NoiseStrength = noiseStrength,
                NoiseScale = noiseScale,
                NoiseOctaves = noiseOctaves,
                NoiseLacunarity = noiseLacunarity

            };
        }

        private void UpdateUIFromSaved()
        {
            var NoiseStrengthEntry = HelperExtensions.FindElementByTag<TextBox>(this, "NoiseStrength");
            var NoiseScaleEntry = HelperExtensions.FindElementByTag<TextBox>(this, "NoiseScale");
            var NoiseOctavesEntry = HelperExtensions.FindElementByTag<TextBox>(this, "NoiseOctaves");
            var NoiseLacunarityEntry = HelperExtensions.FindElementByTag<TextBox>(this, "NoiseLacunarity");

            NoiseStrengthEntry.Text = noiseStrength.ToString();
            NoiseScaleEntry.Text = noiseScale.ToString();
            NoiseOctavesEntry.Text = noiseOctaves.ToString();
            NoiseLacunarityEntry.Text = noiseLacunarity.ToString();
        }


        private void RestorePageFromStoredState()
        {
            var state = PageStateStore.MeshMakerState;

            labeledHeightMap = (LabeledValue[,])state.LabeledHeightMap.Clone();
            meshCreated = state.MeshCreated;

            noiseStrength = state.NoiseStrength;
            noiseScale = state.NoiseScale;
            noiseOctaves = state.NoiseOctaves;
            noiseLacunarity = state.NoiseLacunarity;

            UpdateUIFromSaved();

            originalMeshPositions = new Dictionary<string, Point3DCollection>(state.OriginalMeshPositions);

            _modelGroup.Children.Clear();

            foreach (var kvp in state.LabelMeshes)
            {
                var mesh = kvp.Value;
                var color = GetColorForLabel(kvp.Key);
                var material = new DiffuseMaterial(new SolidColorBrush(color));
                var model = new GeometryModel3D(mesh, material)
                {
                    BackMaterial = material
                };

                model.SetValue(FrameworkElement.TagProperty, kvp.Key);
                _modelGroup.Children.Add(model);
            }

            if (state.CameraPosition != null)
            {
                var camera = _viewport3D.Camera as PerspectiveCamera;
                if (camera != null)
                {
                    camera.Position = state.CameraPosition;
                    camera.LookDirection = state.CameraLookDirection;
                    camera.UpDirection = state.CameraUpDirection;
                }
            }

            CreatedInitialMesh(); // Make sure buttons re-enable if mesh was made

            Debug.WriteLine("Restored MeshMakerPage state.");
        }



        #endregion

    }
}