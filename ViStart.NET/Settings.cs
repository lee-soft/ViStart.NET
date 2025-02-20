using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace ViStart.NET
{
    public class Settings
    {
        private string appDataPath;
        private string currentSkin;
        private string currentOrb;

        public string AppDataPath => appDataPath;
        public string ResourcePath { get; private set; }
        public string OrbPath { get; private set; }
        public string NavigationPaneXml { get; set; }

        // UI Settings
        public bool ShowUserPicture { get; set; }
        public bool ShowProgramsFirst { get; set; }
        public bool AutoClick { get; set; }
        public bool ShowSplashScreen { get; set; }
        public bool ShowTrayIcon { get; set; }

        // Current skin/theme
        public string CurrentSkin
        {
            get => currentSkin;
            set
            {
                currentSkin = value;
                UpdateResourcePaths();
            }
        }

        public string CurrentOrb
        {
            get => currentOrb;
            set
            {
                currentOrb = value;
                UpdateOrbPath();
            }
        }

        public Settings()
        {
            InitializePaths();
            LoadSettings();
        }

        private void InitializePaths()
        {
            // Get ViStart AppData path
            appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ViStart"
            );

            // Create directories if they don't exist
            Directory.CreateDirectory(appDataPath);
            Directory.CreateDirectory(Path.Combine(appDataPath, "_skins"));
            Directory.CreateDirectory(Path.Combine(appDataPath, "_orbs"));

            // Initialize default paths
            CurrentSkin = "Default";
            UpdateResourcePaths();
        }

        private void LoadSettings()
        {
            string settingsPath = Path.Combine(appDataPath, "settings.xml");

            if (!File.Exists(settingsPath))
            {
                SetDefaults();
                return;
            }

            try
            {
                var doc = new XmlDocument();
                doc.Load(settingsPath);

                var root = doc.SelectSingleNode("//settings");
                if (root != null)
                {
                    CurrentSkin = GetXmlValue(root, "current_skin", "Default");
                    CurrentOrb = GetXmlValue(root, "current_orb", "");
                    ShowUserPicture = GetXmlBool(root, "show_user_picture", true);
                    ShowProgramsFirst = GetXmlBool(root, "show_programs_first", false);
                    AutoClick = GetXmlBool(root, "auto_click", true);
                    ShowSplashScreen = GetXmlBool(root, "show_splash", true);
                    ShowTrayIcon = GetXmlBool(root, "show_tray_icon", true);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
                SetDefaults();
            }
        }

        private void SetDefaults()
        {
            CurrentSkin = "Default";
            CurrentOrb = "";
            ShowUserPicture = true;
            ShowProgramsFirst = false;
            AutoClick = true;
            ShowSplashScreen = true;
            ShowTrayIcon = true;
        }

        private void UpdateResourcePaths()
        {
            // Check if skin exists in appdata first
            string skinPath = Path.Combine(appDataPath, "_skins", CurrentSkin);

            // If not in appdata, check exe directory
            if (!Directory.Exists(skinPath))
            {
                string exePath = AppDomain.CurrentDomain.BaseDirectory;
                skinPath = Path.Combine(exePath, "_skins", CurrentSkin);
            }

            ResourcePath = skinPath;
        }

        private void UpdateOrbPath()
        {
            if (string.IsNullOrEmpty(CurrentOrb))
            {
                // Look for default orbs
                string[] defaultOrbs = new[]
                {
                    Path.Combine(ResourcePath, "start_button.png"),
                    Path.Combine(appDataPath, "_orbs", "default.png"),
                    Path.Combine(appDataPath, "_orbs", "Windows 7.png"),
                    Path.Combine(appDataPath, "_orbs", "start_button.png"),
                    Path.Combine(appDataPath, "start_button.png")
                };

                foreach (string path in defaultOrbs)
                {
                    if (File.Exists(path))
                    {
                        OrbPath = path;
                        return;
                    }
                }
            }
            else
            {
                OrbPath = Path.Combine(appDataPath, "_orbs", CurrentOrb);
            }
        }

        public void Save()
        {
            string settingsPath = Path.Combine(appDataPath, "settings.xml");

            var doc = new XmlDocument();
            var root = doc.CreateElement("settings");
            doc.AppendChild(root);

            AddElement(doc, root, "current_skin", CurrentSkin);
            AddElement(doc, root, "current_orb", CurrentOrb);
            AddElement(doc, root, "show_user_picture", ShowUserPicture);
            AddElement(doc, root, "show_programs_first", ShowProgramsFirst);
            AddElement(doc, root, "auto_click", AutoClick);
            AddElement(doc, root, "show_splash", ShowSplashScreen);
            AddElement(doc, root, "show_tray_icon", ShowTrayIcon);

            doc.Save(settingsPath);
        }

        private void AddElement(XmlDocument doc, XmlElement parent, string name, object value)
        {
            var element = doc.CreateElement(name);
            element.InnerText = value.ToString();
            parent.AppendChild(element);
        }

        private string GetXmlValue(XmlNode node, string xpath, string defaultValue)
        {
            var element = node.SelectSingleNode(xpath);
            return element?.InnerText ?? defaultValue;
        }

        private bool GetXmlBool(XmlNode node, string xpath, bool defaultValue)
        {
            var value = GetXmlValue(node, xpath, defaultValue.ToString());
            return bool.TryParse(value, out bool result) ? result : defaultValue;
        }
    }
}
