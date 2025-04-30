using System.Configuration;
using System.Data;
using System.Windows;
using IsleForge.Helpers;

namespace IsleForge
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static AppSettings CurrentSettings { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            CurrentSettings = SettingsManager.Load();
        }
    }

}
