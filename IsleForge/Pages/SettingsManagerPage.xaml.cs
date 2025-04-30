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
using IsleForge.PageStates;

namespace IsleForge.Pages
{
    /// <summary>
    /// Interaction logic for SettingsManagerPage.xaml
    /// </summary>
    public partial class SettingsManagerPage : Page
    {

        private bool CurrentSettingsSaved = true;

        public SettingsManagerPage()
        {
            InitializeComponent();
            this.Loaded += SettingsManagerPage_Loaded;
        }


        private void SettingsManagerPage_Loaded(object sender, RoutedEventArgs e)
        {
            //do this before adding listener thing
            LoadSettingsIntoUI();

            var allTextBoxes = FindVisualChildren<TextBox>(this);
            foreach (var textBox in allTextBoxes)
            {
                textBox.TextChanged += Setting_TextChanged;
            }
        }

        private void Setting_TextChanged(object sender, TextChangedEventArgs e)
        {
            CurrentSettingsSaved = false;
            Debug.WriteLine("changed to false");
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            CurrentSettingsSaved = true;
            var settings = new AppSettings();

            // Texture tiling
            var tilingBox = HelperExtensions.FindElementByTag<TextBox>(this, "TextureTiling");
            if (tilingBox != null && double.TryParse(tilingBox.Text, out var tiling))
                settings.TextureTiling = tiling;

            // Tool size
            var toolSizeBox = HelperExtensions.FindElementByTag<TextBox>(this, "DefaultToolSize");
            if (toolSizeBox != null && int.TryParse(toolSizeBox.Text, out var toolSize))
                settings.DefaultToolSize = toolSize;

            // Heights
            var baseBox = HelperExtensions.FindElementByTag<TextBox>(this, "BaseHeight");
            if (baseBox != null && float.TryParse(baseBox.Text, out var baseHeight))
                settings.BaseHeight = baseHeight;

            var midBox = HelperExtensions.FindElementByTag<TextBox>(this, "MidHeight");
            if (midBox != null && float.TryParse(midBox.Text, out var midHeight))
                settings.MidHeight = midHeight;

            var topBox = HelperExtensions.FindElementByTag<TextBox>(this, "TopHeight");
            if (topBox != null && float.TryParse(topBox.Text, out var topHeight))
                settings.TopHeight = topHeight;

            // Noise settings
            var strengthBox = HelperExtensions.FindElementByTag<TextBox>(this, "NoiseStrength");
            if (strengthBox != null && float.TryParse(strengthBox.Text, out var strength))
                settings.NoiseStrength = strength;

            var scaleBox = HelperExtensions.FindElementByTag<TextBox>(this, "NoiseScale");
            if (scaleBox != null && float.TryParse(scaleBox.Text, out var scale))
                settings.NoiseScale = scale;

            var octavesBox = HelperExtensions.FindElementByTag<TextBox>(this, "NoiseOctaves");
            if (octavesBox != null && int.TryParse(octavesBox.Text, out var octaves))
                settings.NoiseOctaves = octaves;

            var lacunarityBox = HelperExtensions.FindElementByTag<TextBox>(this, "NoiseLacunarity");
            if (lacunarityBox != null && float.TryParse(lacunarityBox.Text, out var lacunarity))
                settings.NoiseLacunarity = lacunarity;

            SettingsManager.Save(settings);

            MessageBox.Show("Settings saved.");
        }

        private void LoadSettingsIntoUI()
        {
            var settings = SettingsManager.Load();

            SetTextBoxByTag("TextureTiling", settings.TextureTiling.ToString());
            SetTextBoxByTag("DefaultToolSize", settings.DefaultToolSize.ToString());
            SetTextBoxByTag("BaseHeight", settings.BaseHeight.ToString());
            SetTextBoxByTag("MidHeight", settings.MidHeight.ToString());
            SetTextBoxByTag("TopHeight", settings.TopHeight.ToString());
            SetTextBoxByTag("NoiseStrength", settings.NoiseStrength.ToString());
            SetTextBoxByTag("NoiseScale", settings.NoiseScale.ToString());
            SetTextBoxByTag("NoiseOctaves", settings.NoiseOctaves.ToString());
            SetTextBoxByTag("NoiseLacunarity", settings.NoiseLacunarity.ToString());



            CurrentSettingsSaved = true;
        }

        private void ResetDefaults_Click(object sender, RoutedEventArgs e)
        {
            var defaults = new AppSettings(); // uses default values via property initializers

            SetTextBoxByTag("TextureTiling", defaults.TextureTiling.ToString());
            SetTextBoxByTag("DefaultToolSize", defaults.DefaultToolSize.ToString());
            SetTextBoxByTag("BaseHeight", defaults.BaseHeight.ToString());
            SetTextBoxByTag("MidHeight", defaults.MidHeight.ToString());
            SetTextBoxByTag("TopHeight", defaults.TopHeight.ToString());
            SetTextBoxByTag("NoiseStrength", defaults.NoiseStrength.ToString());
            SetTextBoxByTag("NoiseScale", defaults.NoiseScale.ToString());
            SetTextBoxByTag("NoiseOctaves", defaults.NoiseOctaves.ToString());
            SetTextBoxByTag("NoiseLacunarity", defaults.NoiseLacunarity.ToString());

            CurrentSettingsSaved = false;
        }



        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentSettingsSaved == false)
            {
                var result = MessageBox.Show(
                    $"Your settings are not saved, are you sure you want to go back?",
                    "Return to Menu?",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    return; // User said NO — cancel
                }
            }

            if (this.NavigationService.CanGoBack)
                this.NavigationService.GoBack();

        }

        #region helpers for UI
        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null)
                yield break;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T t)
                    yield return t;

                foreach (var childOfChild in FindVisualChildren<T>(child))
                    yield return childOfChild;
            }
        }

        private void SetTextBoxByTag(string tag, string value)
        {
            var textBox = HelperExtensions.FindElementByTag<TextBox>(this, tag);
            if (textBox != null)
                textBox.Text = value;
        }


        #endregion

    }
}
