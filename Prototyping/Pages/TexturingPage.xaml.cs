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
using System.Windows.Media.Media3D;
using System.IO;

namespace Prototyping.Pages
{
    /// <summary>
    /// Interaction logic for TexturingPage.xaml
    /// </summary>
    public partial class TexturingPage : Page
    {
        private Dictionary<string, MeshGeometry3D> _meshes;
        private Dictionary<string, Point3DCollection> _originalPositions;

        private Canvas _MapCanvas;
        private Viewport3D _viewport3D;
        private Model3DGroup _modelGroup;

        public TexturingPage()
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

            /*
            foreach (var kvp in _meshes)
            {
                string label = kvp.Key;
                var mesh = kvp.Value;

                var color = GetColorForLabel(label);
                var material = new DiffuseMaterial(new SolidColorBrush(color));

                var model = new GeometryModel3D(mesh, material)
                {
                    BackMaterial = material
                };

                _modelGroup.Children.Add(model);
            }
            */
            _modelGroup.Children.Clear();

            foreach (var kvp in _meshes)
            {
                string label = kvp.Key;
                var mesh = kvp.Value;

                ApplyTextureCoordinates(mesh, 0.1); // UVs scaled for tiling

                var material = new DiffuseMaterial(GetTextureForLabel(label));

                var model = new GeometryModel3D(mesh, material)
                {
                    BackMaterial = material
                };

                _modelGroup.Children.Add(model);
            }


            var modelVisual = new ModelVisual3D { Content = _modelGroup };
            //_viewport3D.Children.Clear();
            _viewport3D.Children.Add(modelVisual);

            SetCameraToMesh(GetMeshCenter(_meshes));
        }

        #region texturing images

        private ImageBrush LoadTilingBrush(string relativePath, double tileScale = 0.1)
        {
            var uri = new Uri($"pack://application:,,,/Prototyping;component/Resources/Textures/{relativePath}", UriKind.Absolute);
            var image = new BitmapImage(uri);

            var brush = new ImageBrush(image)
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, tileScale, tileScale),
                ViewportUnits = BrushMappingMode.RelativeToBoundingBox,
                Stretch = Stretch.Fill
            };

            return brush;
        }

        private ImageBrush GetTextureForLabel(string label)
        {
            switch (label)
            {
                case "beach":
                    return LoadTilingBrush("SandAlbedo.png");
                case "cliff":
                    return LoadTilingBrush("RockAlbedo.png");
                case "Top":
                    return LoadTilingBrush("GrassAlbedo.png");
                case "Base":
                    return LoadTilingBrush("GrassAlbedo.png");
                case "Mid":
                    return LoadTilingBrush("GrassAlbedo.png");
                case "ramp":
                    return LoadTilingBrush("GrassAlbedo.png");
                default:
                    return LoadTilingBrush("RockAlbedo.png"); // fallback
            }
        }

        private void ApplyTextureCoordinates(MeshGeometry3D mesh, double scale)
        {
            var coords = new PointCollection();

            foreach (var pos in mesh.Positions)
            {
                coords.Add(new Point(pos.X * scale, pos.Z * scale)); // Top-down projection
            }

            mesh.TextureCoordinates = coords;
        }



        #endregion

        #region absurd helpers

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

        private Point3D GetMeshCenter(Dictionary<string, MeshGeometry3D> meshes)
        {
            double sumX = 0, sumY = 0, sumZ = 0;
            int count = 0;

            foreach (var mesh in meshes.Values)
            {
                foreach (var pos in mesh.Positions)
                {
                    sumX += pos.X;
                    sumY += pos.Y;
                    sumZ += pos.Z;
                    count++;
                }
            }

            if (count == 0) return new Point3D(0, 0, 0);
            return new Point3D(sumX / count, sumY / count, sumZ / count);
        }

        private void SetCameraToMesh(Point3D center)
        {
            var camera = new PerspectiveCamera
            {
                Position = center + new Vector3D(0, 300, 50),
                LookDirection = center - (center + new Vector3D(0, 300, 50)),
                UpDirection = new Vector3D(0, 1, 0),
                FieldOfView = 60
            };

            _viewport3D.Camera = camera;
        }

        private Color GetColorForLabel(string label)
        {
            if (label == "Mid") return Colors.Green;
            if (label == "Base") return Colors.Green;
            if (label == "ramp") return Colors.Green;
            if (label == "Top") return Colors.Green;
            if (label == "none") return Colors.Blue;
            if (label == "beach") return Colors.Goldenrod;
            if (label == "cliff") return Colors.Gray;
            if (label == "WEIRDLOL") return Colors.Magenta;

            return Colors.Gray;
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
    }
}
