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

        private void MeshMakerPage_Loaded(object sender, RoutedEventArgs e)
        {
            _MapCanvas = HelperExtensions.FindElementByTag<Canvas>(this, "MapCanvas");

            _viewport3D = FindVisualChild<Viewport3D>(this);
            _modelGroup = FindSceneModelGroup(_viewport3D);

            MIDVALUE = MapDataStore.MidHeightShare;
            LOWVALUE = MapDataStore.LowHeightShare;

            LoadDataFromHeightMap();

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

            float strength = HelperExtensions.GetFloatFromTag(this, "NoiseStrength", .1f);
            float scale = HelperExtensions.GetFloatFromTag(this, "NoiseScale", 0.1f);
            int octaves = (int)HelperExtensions.GetFloatFromTag(this, "NoiseOctaves", 4f);
            float lacunarity = HelperExtensions.GetFloatFromTag(this, "NoiseLacunarity", 2f);

            ApplyNoiseToMeshes(strength, scale, octaves, 0.5f, lacunarity);

            return;
        }

        private void ExportMesh_Click(object sender, RoutedEventArgs e)
        {
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "heightmapGroup.obj");

            ExportModelGroupToObj(_modelGroup, path);
            return;

        }

        #endregion

        #region loading

        private void LoadDataFromHeightMap()
        {
            //heightMap = MapDataStore.FinalHeightMap;
            labeledHeightMap = MapDataStore.AnnotatedHeightMap;
            MAXVALUE = MapDataStore.MaxHeightShare;//GetMaxValue(labeledHeightMap);
            Debug.WriteLine("Test data loaded.");
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
            float heightThreshold = 1f;

            var labelMeshes = new Dictionary<string, MeshBuilder>();

            for (int y = 0; y < height - 1; y++)
            {
                for (int x = 0; x < width - 1; x++)
                {
                    var v00 = map[x, y];
                    var v10 = map[x + 1, y];
                    var v01 = map[x, y + 1];
                    var v11 = map[x + 1, y + 1];

                    // === Height-based classification ===
                    float h00 = v00.Value;
                    float h10 = v10.Value;
                    float h01 = v01.Value;
                    float h11 = v11.Value;

                    float heightAverage = (h00 + h10 + h01 + h11) / 4;
                    //moving this here so I can use it anywhere

                    //getting height spread to find overly stretched triangles
                    float[] heights = new[] { h00, h10, h01, h11 };
                    float maxH = heights.Max();
                    float minH = heights.Min();
                    float heightSpread = maxH - minH;


                    if (v00.Value <= 0 || v10.Value <= 0 || v01.Value <= 0 || v11.Value <= 0)
                        continue;

                    var labels = new[] { v00.Label, v10.Label, v01.Label, v11.Label };
                    var labelSet = new HashSet<string>(labels);

                    Point3D p00 = new Point3D(x, v00.Value, y);
                    Point3D p10 = new Point3D(x + 1, v10.Value, y);
                    Point3D p01 = new Point3D(x, v01.Value, y + 1);
                    Point3D p11 = new Point3D(x + 1, v11.Value, y + 1);

                    // Allow ramps and beaches by label

                    if (labelSet.Contains("beach"))
                    {
                        string fallbackLabel = labels.First(l => l == "ramp" || l == "beach");
                        if (!labelMeshes.ContainsKey(fallbackLabel))
                            labelMeshes[fallbackLabel] = new MeshBuilder();

                        var builder = labelMeshes[fallbackLabel];
                        builder.AddTriangle(p00, p11, p10);
                        builder.AddTriangle(p00, p01, p11);
                        continue;
                    }

                    float maxSpreadThreshold = 3.0f; //for the ramp, some need to be stretched
                    if (heightSpread > maxSpreadThreshold)
                    {
                        continue;
                    }

                    if (labelSet.Contains("ramp"))
                    {
                        if (Math.Abs(heightAverage - LOWVALUE) > heightThreshold)
                        {
                            string fallbackLabel = labels.First(l => l == "ramp");
                            if (!labelMeshes.ContainsKey(fallbackLabel))
                                labelMeshes[fallbackLabel] = new MeshBuilder();

                            var builder = labelMeshes[fallbackLabel];
                            builder.AddTriangle(p00, p11, p10);
                            builder.AddTriangle(p00, p01, p11);
                            continue;
                        }
                    }

                    //skipping very stretched triangles----------------


                    // Skip triangle if has large vertical difference
                    maxSpreadThreshold = 2.0f; //finer for the non ramped portions
                    if (heightSpread > maxSpreadThreshold)
                    {
                        continue;
                    }
                    //skipping very stretched triangles----------------  


                    //Debug.WriteLine($"Avg: {heightAverage:F2}  LOW: {LOWVALUE}, MID: {MIDVALUE}, MAX: {MAXVALUE}");


                    // BASE
                    if (Math.Abs(heightAverage - LOWVALUE) <= heightThreshold)
                    {
                        if (!labelMeshes.ContainsKey("Base"))
                            labelMeshes["Base"] = new MeshBuilder();

                        var builder = labelMeshes["Base"];
                        builder.AddTriangle(p00, p11, p10);
                        builder.AddTriangle(p00, p01, p11);
                        continue;
                    }

                    // MID
                    else if (Math.Abs(heightAverage - MIDVALUE) <= heightThreshold)
                    {
                        if (!labelMeshes.ContainsKey("Mid"))
                            labelMeshes["Mid"] = new MeshBuilder();

                        var builder = labelMeshes["Mid"];
                        builder.AddTriangle(p00, p11, p10);
                        builder.AddTriangle(p00, p01, p11);
                        continue;
                    }
                    // TOP
                    else if (Math.Abs(heightAverage - MAXVALUE) <= heightThreshold)
                    {
                        if (!labelMeshes.ContainsKey("Top"))
                            labelMeshes["Top"] = new MeshBuilder();

                        var builder = labelMeshes["Top"];
                        builder.AddTriangle(p00, p11, p10);
                        builder.AddTriangle(p00, p01, p11);
                        continue;
                    }

                    // Skip everything else (likely transitions)
                }
            }

            var finalMeshes = new Dictionary<string, MeshGeometry3D>();
            foreach (var kvp in labelMeshes)
                finalMeshes[kvp.Key] = kvp.Value.Mesh;

            Debug.WriteLine("Generated height-based layers: " + string.Join(", ", finalMeshes.Keys));

            return finalMeshes;
        }



        #region skirting
        private Dictionary<string, MeshGeometry3D> AddSkirtsToMeshesRemeshed(Dictionary<string, MeshGeometry3D> input)
        {
            var result = new Dictionary<string, MeshGeometry3D>(input);
            int segments = 4; // vertical resolution — tweak this for more detail

            foreach (var kvp in input)
            {
                string label = kvp.Key;
                var mesh = kvp.Value;
                var skirtBuilder = new MeshBuilder();

                var positions = mesh.Positions;
                var triangles = mesh.TriangleIndices;

                var edgeSet = new HashSet<(Point3D, Point3D)>();

                for (int i = 0; i < triangles.Count; i += 3)
                {
                    var a = positions[triangles[i]];
                    var b = positions[triangles[i + 1]];
                    var c = positions[triangles[i + 2]];

                    AddOrRemoveEdge(edgeSet, a, b);
                    AddOrRemoveEdge(edgeSet, b, c);
                    AddOrRemoveEdge(edgeSet, c, a);
                }

                foreach (var (top1, top2) in edgeSet)
                {
                    for (int i = 0; i < segments; i++)
                    {
                        double t1 = (double)i / segments;
                        double t2 = (double)(i + 1) / segments;

                        // interpolate along edge
                        Point3D p1Top = Interpolate(top1, top2, t1);
                        Point3D p2Top = Interpolate(top1, top2, t2);

                        Point3D p1Bottom = new Point3D(p1Top.X, 0, p1Top.Z);
                        Point3D p2Bottom = new Point3D(p2Top.X, 0, p2Top.Z);

                        // Add two triangles per vertical quad
                        skirtBuilder.AddTriangle(p1Top, p2Top, p1Bottom);
                        skirtBuilder.AddTriangle(p1Bottom, p2Top, p2Bottom);
                    }
                }

                result[label + "_skirt"] = skirtBuilder.Mesh;
            }

            return result;
        }

        private Point3D Interpolate(Point3D a, Point3D b, double t)
        {
            return new Point3D(
                a.X + (b.X - a.X) * t,
                a.Y + (b.Y - a.Y) * t,
                a.Z + (b.Z - a.Z) * t
            );
        }

        private Dictionary<string, MeshGeometry3D> AddSkirtsToMeshes(Dictionary<string, MeshGeometry3D> input)
        {
            var result = new Dictionary<string, MeshGeometry3D>(input);

            foreach (var kvp in input)
            {
                string label = kvp.Key;
                var mesh = kvp.Value;
                var skirtBuilder = new MeshBuilder();

                var positions = mesh.Positions;
                var triangles = mesh.TriangleIndices;

                var edgeSet = new HashSet<(Point3D, Point3D)>();

                // Identify edges used only once (border edges)
                for (int i = 0; i < triangles.Count; i += 3)
                {
                    var a = positions[triangles[i]];
                    var b = positions[triangles[i + 1]];
                    var c = positions[triangles[i + 2]];

                    AddOrRemoveEdge(edgeSet, a, b);
                    AddOrRemoveEdge(edgeSet, b, c);
                    AddOrRemoveEdge(edgeSet, c, a);
                }

                // Build skirt faces from those edges
                foreach (var (p1, p2) in edgeSet)
                {
                    Point3D g1 = new Point3D(p1.X, 0, p1.Z);
                    Point3D g2 = new Point3D(p2.X, 0, p2.Z);

                    skirtBuilder.AddTriangle(p1, p2, g1);
                    skirtBuilder.AddTriangle(g1, p2, g2);
                }

                result[label + "_skirt"] = skirtBuilder.Mesh;
            }

            return result;
        }

        private void AddOrRemoveEdge(HashSet<(Point3D, Point3D)> edgeSet, Point3D a, Point3D b)
        {
            var edge = (a, b);
            var reverse = (b, a);
            if (edgeSet.Contains(reverse))
                edgeSet.Remove(reverse);
            else if (!edgeSet.Add(edge))
                edgeSet.Remove(edge); // prevent duplicates
        }


        #endregion

        private Dictionary<string, MeshGeometry3D> CreateMeshesByLabelKindaSkirt(LabeledValue[,] map)
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

                    // Same label for all 4 corners, but we need to check angle before blindly assigning
                    if (labelSet.Count == 1)
                    {
                        string label = labels[0];

                        // Compute the normal
                        Vector3D normal = CalculateAverageNormal(p00, p11, p10, p01);
                        double angle = Vector3D.AngleBetween(normal, new Vector3D(0, 1, 0));

                        bool isTooSteep = angle > 80; // threshold to define "too vertical" for base/mid/top

                        if (label == "Base" || label == "Mid" || label == "Top")
                        {
                            if (isTooSteep)
                            {
                                // Skip this quad for now — let the skirt handle it
                                continue;
                            }
                        }

                        // safe to assign normally
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
            return AddSkirtsToMeshesRemeshed(finalMeshes);
            //return finalMeshes; //old working one
        }

        private Dictionary<string, MeshGeometry3D> CreateMeshesByLabelPreskirt(LabeledValue[,] map)
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
            if (label == "Mid") return Colors.LightGreen;
            if (label == "Base") return Colors.Green;
            if (label == "ramp") return Colors.Aqua;
            if (label == "Top") return Colors.ForestGreen;
            if (label == "none") return Colors.Blue;
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
            Vector3D offset = new Vector3D(-50, RadiusMath*2, 90);
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

        #region end buttons

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            //might add the option to go back
        }
        private void Next_Click(object sender, RoutedEventArgs e)
        {
            var finalMeshes = new Dictionary<string, MeshGeometry3D>();

            foreach (var child in _modelGroup.Children)
            {
                if (child is GeometryModel3D geom && geom.Geometry is MeshGeometry3D mesh)
                {
                    string label = GetLabelFromGeometryModel(geom);
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

            NavigationService.Navigate(new TexturePage());
        }

        #endregion

    }
}
