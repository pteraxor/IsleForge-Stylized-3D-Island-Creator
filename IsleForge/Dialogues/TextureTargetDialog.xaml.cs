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

namespace IsleForge.Dialogues
{
    /// <summary>
    /// Interaction logic for TextureTargetDialog.xaml
    /// </summary>
    public partial class TextureTargetDialog : Window
    {
        public string SelectedType { get; private set; }

        public TextureTargetDialog()
        {
            InitializeComponent();
        }
        private void Grass_Click(object sender, RoutedEventArgs e)
        {
            SelectedType = "grass";
            DialogResult = true;
        }

        private void Sand_Click(object sender, RoutedEventArgs e)
        {
            SelectedType = "sand";
            DialogResult = true;
        }

        private void Rock_Click(object sender, RoutedEventArgs e)
        {
            SelectedType = "rock";
            DialogResult = true;
        }
    }
}
