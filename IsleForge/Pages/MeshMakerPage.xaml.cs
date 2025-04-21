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

        private float MAXVALUE = 100f;

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

        #endregion

        #region loading

        private void LoadDataFromHeightMap()
        {
            heightMap = MapDataStore.FinalHeightMap;
            labeledHeightMap = MapDataStore.AnnotatedHeightMap;
            MAXVALUE = GetMaxValue(heightMap);
            Debug.WriteLine("Test data loaded.");
        }

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
            SetCameraToMesh(_viewport3D, center);


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

            //NavigationService.Navigate(new TextureAndBumpPage());
        }

        #endregion

    }
}
