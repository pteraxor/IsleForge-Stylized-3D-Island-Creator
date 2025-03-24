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


namespace Prototyping
{
    public partial class WelcomePage : Page
    {
        public WelcomePage()
        {
            InitializeComponent();
        }


        private void Start_Project(object sender, RoutedEventArgs e)
        {
            
            NavigationService.Navigate(new BaseMapDrawingPage());
        }
        

        private void Settings_Page(object sender, RoutedEventArgs e)
        {
           
        }
    }
}


