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
//using System.Windows.Media.Media3D;
using System.IO;
using Prototyping.Dialogues;
using HelixToolkit.Wpf.SharpDX;
using SharpDX;
using Color = SharpDX.Color;

namespace Prototyping.Pages
{
    /// <summary>
    /// Interaction logic for TextureAndBumpPage.xaml
    /// </summary>
    public partial class TextureAndBumpPage : Page
    {
        private GroupModel3D _sceneGroup = new GroupModel3D();
        private DefaultEffectsManager _effectsManager = new DefaultEffectsManager();      


        public TextureAndBumpPage()
        {
            InitializeComponent();           
            this.Loaded += TextureAndBumpPage_Loaded;
            Debug.WriteLine("made it");
        }

        private void TextureAndBumpPage_Loaded(object sender, RoutedEventArgs e)
        {
            var viewport = FindViewport();
            viewport.EffectsManager = _effectsManager;
            

            viewport.Items.Add(_sceneGroup);

            /*
            foreach (var kvp in MeshDataStore.Meshes)
            {
                var wpfMesh = kvp.Value;
                var label = kvp.Key;

                Debug.WriteLine($"Mesh '{label}' has {wpfMesh.Positions.Count} vertices and {wpfMesh.TriangleIndices.Count / 3} triangles.");

                if (wpfMesh.Positions.Count == 0 || wpfMesh.TriangleIndices.Count == 0)
                {
                    Debug.WriteLine($"kipping mesh '{label}' because it's empty.");
                    continue;
                }


                var helixMesh = ConvertToHelixMesh(wpfMesh);

                var mat = new PhongMaterial
                {
                    DiffuseColor = Color.White,
                    SpecularColor = Color.Gray,
                    SpecularShininess = 20f
                };

                var model = new MeshGeometryModel3D
                {
                    Geometry = helixMesh,
                    Material = mat,
                    CullMode = SharpDX.Direct3D11.CullMode.None
                };

                _sceneGroup.Children.Add(model);
            }
            Debug.WriteLine($"Mesh count: {MeshDataStore.Meshes.Count}");
            */
            /*
            var cubeBuilder = new MeshBuilder();
            cubeBuilder.AddBox(new Vector3(0, 0, 0), 20, 20, 20);
            var cube = new MeshGeometryModel3D
            {
                Geometry = cubeBuilder.ToMeshGeometry3D(),
                Material = PhongMaterials.Red
            };
            _sceneGroup.Children.Add(cube);
            */
            // Grab a single known mesh
            var wpfMesh = MeshDataStore.Meshes["Top"]; // or "Base", etc.
            var helixMesh = ConvertToHelixMesh(wpfMesh);

            var mat = new PhongMaterial
            {
                DiffuseColor = Color.Green,
                SpecularColor = Color.White,
                AmbientColor = Color.Gray,
                EmissiveColor = new Color(0.2f, 0.2f, 0.2f),
                SpecularShininess = 100f
            };

            var model = new MeshGeometryModel3D
            {
                Geometry = helixMesh,
                Material = mat,
                CullMode = SharpDX.Direct3D11.CullMode.None
            };
            Debug.WriteLine($"Positions: {helixMesh.Positions.Count}");
            Debug.WriteLine($"Indices: {helixMesh.Indices.Count}");
            Debug.WriteLine($"Normals: {(helixMesh.Normals != null ? helixMesh.Normals.Count : 0)}");

            _sceneGroup.Children.Add(model);

            var cubeBuilder = new MeshBuilder();
            cubeBuilder.AddBox(new Vector3(0, 0, 0), 20, 20, 20);
            var cube = new MeshGeometryModel3D
            {
                Geometry = cubeBuilder.ToMeshGeometry3D(),
                Material = PhongMaterials.Red
            };
            _sceneGroup.Children.Add(cube);

            //viewport.InvalidateRender();
            //viewport.ShowBoundingBox = true;
            SetCamera(helixMesh);
        }

        private Viewport3DX FindViewport()
        {
            return HelperExtensions.FindElementByTag<Viewport3DX>(this, "HelixViewport");
        }

        private void SetCamera(HelixToolkit.Wpf.SharpDX.MeshGeometry3D helixMesh)
        {
            Debug.WriteLine("called set camera");
            var center = BoundingBoxExtensions.FromPoints(helixMesh.Positions).Center;

            var camera = new HelixToolkit.Wpf.SharpDX.PerspectiveCamera
            {
                /*
                 Position = new SharpDX.Vector3(200, 200, 200),
        LookDirection = new SharpDX.Vector3(-200, -200, -200),
        UpDirection = new SharpDX.Vector3(0, 1, 0), 
                */
                Position = new System.Windows.Media.Media3D.Point3D(center.X + 300, center.Y + 300, center.Z + 300),
                LookDirection = new System.Windows.Media.Media3D.Vector3D(-300, -300, -300),
                UpDirection = new System.Windows.Media.Media3D.Vector3D(0, 1, 0),
                FieldOfView = 60f,
                FarPlaneDistance = 1000,
                NearPlaneDistance = 0.1
            };
            var viewport = FindViewport();
            viewport.Camera = camera;
        }

        private void SetCamera2()
        {
            Debug.WriteLine("called set camera");


            var camera = new HelixToolkit.Wpf.SharpDX.PerspectiveCamera
            {
                /*
                 Position = new SharpDX.Vector3(200, 200, 200),
        LookDirection = new SharpDX.Vector3(-200, -200, -200),
        UpDirection = new SharpDX.Vector3(0, 1, 0), 
                */
                Position = new System.Windows.Media.Media3D.Point3D(200, 200, 200),
                LookDirection = new System.Windows.Media.Media3D.Vector3D(-200, -200, -200),
                UpDirection = new System.Windows.Media.Media3D.Vector3D(0, 1, 0),
                FieldOfView = 60f,
                FarPlaneDistance = 1000,
                NearPlaneDistance = 0.1
            };
            var viewport = FindViewport();
            viewport.Camera = camera;
        }

        private HelixToolkit.Wpf.SharpDX.MeshGeometry3D ConvertToHelixMesh(System.Windows.Media.Media3D.MeshGeometry3D wpfMesh)
        {
            var positions = wpfMesh.Positions.Select(p => new Vector3((float)p.X, (float)p.Y, (float)p.Z)).ToList();
            var indices = wpfMesh.TriangleIndices.Select(i => (int)i).ToList();

            var mesh = new HelixToolkit.Wpf.SharpDX.MeshGeometry3D
            {
                Positions = new Vector3Collection(positions),
                Indices = new IntCollection(indices)
            };

            //Calculate normals
            var normals = MeshGeometryHelper.CalculateNormals(mesh);
            mesh.Normals = new Vector3Collection(normals);

            return mesh;
        }


        private HelixToolkit.Wpf.SharpDX.MeshGeometry3D ConvertToHelixMesh123(System.Windows.Media.Media3D.MeshGeometry3D wpfMesh)
        {
            var positions = wpfMesh.Positions.Select(p => new Vector3((float)p.X, (float)p.Y, (float)p.Z)).ToList();
            var indices = wpfMesh.TriangleIndices.Select(i => (int)i).ToList();

            var mesh = new HelixToolkit.Wpf.SharpDX.MeshGeometry3D
            {
                Positions = new Vector3Collection(positions),
                Indices = new IntCollection(indices),
                Normals = null // Let the material handle this for now
            };

            return mesh;
        }


        private HelixToolkit.Wpf.SharpDX.MeshGeometry3D ConvertToHelixMesh2(System.Windows.Media.Media3D.MeshGeometry3D wpfMesh)
        {
            var builder = new MeshBuilder(true, true);

            for (int i = 0; i < wpfMesh.TriangleIndices.Count; i += 3)
            {
                var p0 = wpfMesh.Positions[wpfMesh.TriangleIndices[i]];
                var p1 = wpfMesh.Positions[wpfMesh.TriangleIndices[i + 1]];
                var p2 = wpfMesh.Positions[wpfMesh.TriangleIndices[i + 2]];

                builder.AddTriangle(
                    p0.ToVector3(),
                    p1.ToVector3(),
                    p2.ToVector3());
            }

            return builder.ToMeshGeometry3D();
        }

        private Vector3 ToVector3(System.Windows.Media.Media3D.Point3D point)
        {
            return new Vector3((float)point.X, (float)point.Y, (float)point.Z);
        }

        // Converts WPF Vector3D to SharpDX Vector3
        private Vector3 ToVector3(System.Windows.Media.Media3D.Vector3D vector)
        {
            return new Vector3((float)vector.X, (float)vector.Y, (float)vector.Z);
        }
    }

}