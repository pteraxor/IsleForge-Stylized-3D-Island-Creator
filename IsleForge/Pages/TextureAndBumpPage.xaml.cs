using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using HelixToolkit.Wpf.SharpDX;
using SharpDX;
using MediaColor = System.Windows.Media.Color;


namespace IsleForge.Pages
{
    /// <summary>
    /// Interaction logic for TextureAndBumpPage.xaml
    /// </summary>
    public partial class TextureAndBumpPage : Page
    {


        public TextureAndBumpPage()
        {
            InitializeComponent();
            this.Loaded += TextureAndBumpPage_Loaded;
        }

        private void TextureAndBumpPage_Loaded(object sender, RoutedEventArgs e)
        {

            Viewport.EffectsManager = new DefaultEffectsManager();
            // 1. Build the cube geometry
            var builder = new MeshBuilder(true, true); // enable normals and UVs
            builder.AddBox(new Vector3(0, 0, 0), 1f, 1f, 1f);
            var mesh = builder.ToMeshGeometry3D();
            

            // 2. Load normal map from resources
            TextureModel normalMap = null;
            var uri = new Uri("pack://application:,,,/Resources/Textures/GrassNormal.png", UriKind.Absolute);

            var resourceInfo = Application.GetResourceStream(uri);
            if (resourceInfo != null)
            {
                normalMap = new TextureModel(resourceInfo.Stream);
            }
            else
            {
                MessageBox.Show("Could not find embedded resource: GrassNormal.png");
            }

            TextureModel diffuseMap = null;
            uri = new Uri("pack://application:,,,/Resources/Textures/Checker.png", UriKind.Absolute);

            resourceInfo = Application.GetResourceStream(uri);
            if (resourceInfo != null)
            {
                diffuseMap = new TextureModel(resourceInfo.Stream);
            }
            else
            {
                MessageBox.Show("Could not find embedded resource: Checker.png");
            }


            // 3. Define material
            var material = new PhongMaterial
            {
                DiffuseColor = MediaColor.FromRgb(128, 128, 128).ToColor4(),
                AmbientColor = MediaColor.FromRgb(64, 64, 64).ToColor4(),
                //SpecularColor = Color.White,
                SpecularColor = MediaColor.FromRgb(64, 64, 64).ToColor4(),
                SpecularShininess = 100f,
                DiffuseMap = diffuseMap,
                NormalMap = normalMap
            };

            // 4. Create the model and add to viewport
            var model = new MeshGeometryModel3D
            {
                Geometry = mesh,
                Material = material
            };

            Viewport.Items.Add(model);
        }

    }
}