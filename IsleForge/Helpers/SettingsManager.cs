using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IsleForge.Helpers
{
    using System.IO;
    using System.Text.Json;

    public static class SettingsManager
    {
        public static AppSettings Current { get; private set; } = new AppSettings();

        private static string SettingsPath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        public static AppSettings Load()
        {
            if (File.Exists(SettingsPath))
            {
                string json = File.ReadAllText(SettingsPath);
                Current = JsonSerializer.Deserialize<AppSettings>(json);
            }
            else
            {
                Current = new AppSettings(); // defaults
            }

            return Current;
        }

        public static void Save(AppSettings settings)
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
            Current = settings;
            App.CurrentSettings = settings;
        }
    }

}
