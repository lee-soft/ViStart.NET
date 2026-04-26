using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace ViStart.Core
{
    public class AppSettings
    {
        private static AppSettings instance;
        private static readonly string settingsPath;
        private static readonly string appDataPath;

        static AppSettings()
        {
            appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Lee-Soft.com",
                "ViStart");

            if (!Directory.Exists(appDataPath))
                Directory.CreateDirectory(appDataPath);

            settingsPath = Path.Combine(appDataPath, "settings.json");
        }

        public static AppSettings Instance
        {
            get { return instance ?? (instance = new AppSettings()); }
        }

        public static string AppDataPath
        {
            get { return appDataPath; }
        }

        // General Settings
        public string CurrentSkin { get; set; }
        public string CurrentOrb { get; set; }
        public string CurrentLanguage { get; set; }
        public bool ShowUserPicture { get; set; }
        public bool ShowProgramsFirst { get; set; }
        public bool CatchLeftWindowsKey { get; set; }
        public bool CatchRightWindowsKey { get; set; }
        public bool ShowSplashScreen { get; set; }
        public int FadeAnimationSpeed { get; set; }

        public bool UseLargeIcons { get; set; }

        // Program Database (stored separately)
        public ProgramDatabase Programs { get; set; }

        private AppSettings()
        {
            // Set defaults
            CurrentSkin = "Windows 7 Start Menu";
            CurrentOrb = "Orb Windows 7.png";
            CurrentLanguage = "english";
            ShowUserPicture = true;
            ShowProgramsFirst = false;
            CatchLeftWindowsKey = true;
            CatchRightWindowsKey = true;
            ShowSplashScreen = false;
            UseLargeIcons = false;
            FadeAnimationSpeed = 15;
            Programs = new ProgramDatabase();
        }

        public static void Load()
        {
            try
            {
                if (File.Exists(settingsPath))
                {
                    string json = File.ReadAllText(settingsPath);
                    var serializer = new JavaScriptSerializer();
                    instance = serializer.Deserialize<AppSettings>(json);
                }
                else
                {
                    instance = new AppSettings();
                }

                // Load programs separately
                instance.Programs = ProgramDatabase.Load();
            }
            catch
            {
                instance = new AppSettings();
                instance.Programs = new ProgramDatabase();
            }
        }

        public static void Save()
        {
            try
            {
                var serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(instance);
                File.WriteAllText(settingsPath, json);

                // Save programs separately
                instance.Programs.Save();
            }
            catch
            {
                // Log error
            }
        }

        public string GetResourcesPath()
        {
            if (!string.IsNullOrEmpty(CurrentSkin))
            {
                string appDataSkinPath = Path.Combine(appDataPath, "_skins", CurrentSkin);
                if (Directory.Exists(appDataSkinPath))
                    return appDataSkinPath;

                string localSkinsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Skins", CurrentSkin);
                if (Directory.Exists(localSkinsPath))
                    return localSkinsPath;

                string localSkinsLowerPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "skins", CurrentSkin);
                if (Directory.Exists(localSkinsLowerPath))
                    return localSkinsLowerPath;
            }

            // Default resources in application directory
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");
        }

        public string GetOrbPath()
        {
            if (!string.IsNullOrEmpty(CurrentOrb))
            {
                string appDataOrbPath = Path.Combine(appDataPath, "_orbs", CurrentOrb);
                if (File.Exists(appDataOrbPath))
                    return appDataOrbPath;

                string localOrbsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Orbs", CurrentOrb);
                if (File.Exists(localOrbsPath))
                    return localOrbsPath;

                string localOrbsLowerPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "orbs", CurrentOrb);
                if (File.Exists(localOrbsLowerPath))
                    return localOrbsLowerPath;
            }

            // Default orb
            string defaultOrb = Path.Combine(GetResourcesPath(), "start_button.png");
            if (File.Exists(defaultOrb))
                return defaultOrb;

            return null;
        }
    }
}
