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

        private readonly Dictionary<string, TextureModel> _diffuseTextures = new Dictionary<string, TextureModel>();



        public TextureAndBumpPage()
        {
            InitializeComponent();
            this.Loaded += TextureAndBumpPage_Loaded;
            Debug.WriteLine("made it");
        }

        private void TextureAndBumpPage_Loaded(object sender, RoutedEventArgs e)
        {
            //load textures
            _diffuseTextures["Top"] = LoadTexture("GrassAlbedo.png");
            _diffuseTextures["Base"] = LoadTexture("GrassAlbedo.png");
            _diffuseTextures["Mid"] = LoadTexture("GrassAlbedo.png");
            _diffuseTextures["ramp"] = LoadTexture("GrassAlbedo.png");
            _diffuseTextures["beach"] = LoadTexture("SandAlbedo.png");
            _diffuseTextures["cliff"] = LoadTexture("RockAlbedo.png");



            var viewport = FindViewport();
            viewport.EffectsManager = _effectsManager;


            viewport.Items.Add(_sceneGroup);
            //HelixToolkit.Wpf.SharpDX.MeshGeometry3D helixMesh;

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

                var mat2 = new PhongMaterial
                {
                    DiffuseColor = Color.White,
                    SpecularColor = Color.Gray,
                    SpecularShininess = 20f
                };
                var mat = new PhongMaterial
                {
                    DiffuseColor = Color.White,
                    DiffuseMap = LoadTexture("GrassAlbedo.png"),
                    SpecularColor = Color.Gray,
                    SpecularShininess = 0.1f
                };

                var model = new MeshGeometryModel3D
                {
                    Geometry = helixMesh,
                    Material = mat,
                    CullMode = SharpDX.Direct3D11.CullMode.None
                };

                _sceneGroup.Children.Add(model);

                SetCamera(helixMesh);
            }
            */
            PopulateMeshData();
        }

        private void PopulateMeshData()
        {
            foreach (var kvp in MeshDataStore.Meshes)
            {
                string label = kvp.Key;
                var wpfMesh = kvp.Value;

                if (wpfMesh.Positions.Count == 0 || wpfMesh.TriangleIndices.Count == 0)
                {
                    Debug.WriteLine($"Skipping empty mesh: {label}");
                    continue;
                }

                var helixMesh = ConvertToHelixMesh(wpfMesh);
                var texture = GetTextureForLabel(label);

                var mat = new PhongMaterial
                {
                    //DiffuseColor = Color.White,
                    DiffuseMap = texture,
                    //SpecularColor = Color.Gray,
                    SpecularShininess = 1f
                };

                var model = new MeshGeometryModel3D
                {
                    Geometry = helixMesh,
                    Material = mat,
                    CullMode = SharpDX.Direct3D11.CullMode.None
                };

                _sceneGroup.Children.Add(model);

                // Optionally set camera once
                if (label == "Top")
                    SetCamera(helixMesh);
            }

        }

        private Viewport3DX FindViewport()
        {
            return HelperExtensions.FindElementByTag<Viewport3DX>(this, "HelixViewport");
        }

        #region texturing

        private TextureModel GetTextureForLabel(string label)
        {
            if (_diffuseTextures.TryGetValue(label, out var texture))
                return texture;

            // fallback if not matched
            return _diffuseTextures["cliff"];
        }


        private TextureModel LoadTexture(string relativePath)
        {
            var uri = new Uri($"pack://application:,,,/Prototyping;component/Resources/Textures/{relativePath}", UriKind.Absolute);
            var bitmap = new BitmapImage(uri);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            var stream = new MemoryStream();
            encoder.Save(stream);
            stream.Position = 0;

            return new TextureModel(stream);
        }


        #endregion

        #region camera

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

        #endregion

        #region helix conversion

        private HelixToolkit.Wpf.SharpDX.MeshGeometry3D ConvertToHelixMesh(System.Windows.Media.Media3D.MeshGeometry3D wpfMesh)
        {
            var positions = wpfMesh.Positions.Select(p => new Vector3((float)p.X, (float)p.Y, (float)p.Z)).ToList();
            var indices = wpfMesh.TriangleIndices.Select(i => (int)i).ToList();

            var mesh = new HelixToolkit.Wpf.SharpDX.MeshGeometry3D
            {
                Positions = new Vector3Collection(positions),
                Indices = new IntCollection(indices),
                Normals = new Vector3Collection(MeshGeometryHelper.CalculateNormals(new MeshGeometry3D
                {
                    Positions = new Vector3Collection(positions),
                    Indices = new IntCollection(indices)
                })),
                TextureCoordinates = GenerateUVs(positions)
            };

            //Calculate normals
            var normals = MeshGeometryHelper.CalculateNormals(mesh);
            mesh.Normals = new Vector3Collection(normals);

            return mesh;
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

        //new UV projection for helix conversion
        private Vector2Collection GenerateUVs(List<Vector3> positions, float scale = 0.1f)
        {
            var uvs = new Vector2Collection();
            foreach (var pos in positions)
            {
                // Simple top-down projection
                float u = pos.X * scale;
                float v = pos.Z * scale;
                uvs.Add(new Vector2(u, v));
            }
            return uvs;
        }

        #endregion
    }

}