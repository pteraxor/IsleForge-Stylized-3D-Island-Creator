using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using IsleForge.Helpers;

namespace IsleForge.Pages
{
    /// <summary>
    /// Interaction logic for TestLauncherPage.xaml
    /// </summary>
    public partial class TestLauncherPage : Page
    {
        public TestLauncherPage()
        {
            InitializeComponent();
            this.Loaded += TestLauncherPage_Loaded;
        }

        private void TestLauncherPage_Loaded(object sender, RoutedEventArgs e)
        {

            LoadMeshMakerPage();
        }


        #region mesh maker
        //this is for cleaner testing, so that every page would open like it is meant to
        private void LoadMeshMakerPage()
        {
            string heightMapPath = @"../../../Resources/solvedMap.txt";
            string labelMapPath = @"../../../Resources/solvedMapWithLabels.txt";

            MapDataStore.MaxHeightShare = 50;
            MapDataStore.MidHeightShare = 40;
            MapDataStore.LowHeightShare = 30;

            MapDataStore.FinalHeightMap = LoadFloatArrayFromFile(System.IO.Path.GetFullPath(heightMapPath));
            MapDataStore.AnnotatedHeightMap = LoadLabeledMapFromText(System.IO.Path.GetFullPath(labelMapPath));


            Debug.WriteLine("Test data loaded.");

            NavigationService.Navigate(new MeshMakerPage());

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

        #endregion
    }
}
