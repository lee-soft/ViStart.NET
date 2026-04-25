using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Xml.Linq;
using System.Linq;

namespace ViStart.Core
{
    public class ThemeElement
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool Visible { get; set; }

        public ThemeElement()
        {
            Width = -1;
            Height = -1;
            Visible = true;
        }
    }

    public static class ThemeManager
    {
        private static XDocument layoutDoc;
        private static Dictionary<string, ThemeElement> elements;
        private static Dictionary<string, Image> images;

        public static string ResourcesPath { get; private set; }
        
        // Layout elements
        public static ThemeElement SearchBox { get; private set; }
        public static ThemeElement ProgramMenu { get; private set; }
        public static ThemeElement FrequentProgramsMenu { get; private set; }
        public static ThemeElement AllProgramsRollover { get; private set; }
        public static ThemeElement AllProgramsArrow { get; private set; }
        public static ThemeElement AllProgramsText { get; private set; }
        public static ThemeElement GroupOptions { get; private set; }
        public static ThemeElement RolloverPlaceholder { get; private set; }
        public static ThemeElement ShutdownButton { get; private set; }
        public static ThemeElement LogoffButton { get; private set; }
        public static ThemeElement ArrowButton { get; private set; }
        public static ThemeElement JumpListViewer { get; private set; }
        public static ThemeElement ShutdownText { get; private set; }

        // Colors
        public static Color FrequentProgramsSeparatorColor { get; private set; }
        public static Color ProgramMenuBackColor { get; private set; }
        public static Color FrequentProgramsBackColor { get; private set; }

        static ThemeManager()
        {
            elements = new Dictionary<string, ThemeElement>();
            images = new Dictionary<string, Image>();
        }

        public static void Initialize()
        {
            ResourcesPath = AppSettings.Instance.GetResourcesPath();
            LoadLayout();
            LoadImages();
        }

        private static void LoadLayout()
        {
            try
            {
                string layoutPath = Path.Combine(ResourcesPath, "layout.xml");
                
                if (File.Exists(layoutPath))
                {
                    layoutDoc = XDocument.Load(layoutPath);
                }
                else
                {
                    // Load from embedded resources
                    layoutDoc = XDocument.Parse(GetDefaultLayout());
                }

                ParseLayout();
            }
            catch (Exception ex)
            {
                // Use defaults if layout fails to load
                System.Diagnostics.Debug.WriteLine("Failed to load layout: " + ex.Message);
                CreateDefaultLayout();
            }
        }

        private static void ParseLayout()
        {
            var root = layoutDoc.Root;
            
            // Parse all vielement entries
            foreach (var element in root.Descendants("vielement"))
            {
                string id = (string)element.Attribute("id");
                if (string.IsNullOrEmpty(id))
                    continue;

                var themeElement = new ThemeElement
                {
                    X = (int?)element.Attribute("x") ?? 0,
                    Y = (int?)element.Attribute("y") ?? 0,
                    Width = (int?)element.Attribute("width") ?? -1,
                    Height = (int?)element.Attribute("height") ?? -1,
                    Visible = (bool?)element.Attribute("visible") ?? true
                };

                elements[id] = themeElement;
            }

            // Map to properties
            SearchBox = GetElement("searchbox");
            ProgramMenu = GetElement("programmenu");
            FrequentProgramsMenu = GetElement("frequentprogramsmenu");
            AllProgramsRollover = GetElement("allprograms_rollover");
            AllProgramsArrow = GetElement("allprograms_arrow");
            AllProgramsText = GetElement("allprograms_text");
            GroupOptions = GetElement("groupoptions");
            RolloverPlaceholder = GetElement("rolloverplaceholder");
            ShutdownButton = GetElement("shutdown_button");
            LogoffButton = GetElement("logoff_button");
            ArrowButton = GetElement("arrow_button");
            JumpListViewer = GetElement("jumplist_viewer");
            ShutdownText = GetElement("shutdown_text");

            // VB6 LayoutParser.cls:192-197 — when groupoptions has no explicit width
            // it defaults to 140 (button.png native width) and shifts left/top by -3.
            if (GroupOptions.Width == -1)
            {
                GroupOptions.X -= 3;
                GroupOptions.Y -= 3;
                GroupOptions.Width = 140;
                GroupOptions.Height = 420;
            }

            // Parse colors
            var freqMenu = root.Descendants("frequentprogramsmenu").FirstOrDefault();
            if (freqMenu != null)
            {
                string sepColor = (string)freqMenu.Attribute("separatorcolour");
                FrequentProgramsSeparatorColor = ParseColor(sepColor, Color.White);
            }

            ProgramMenuBackColor = Color.White;
            FrequentProgramsBackColor = Color.White;
        }

        private static ThemeElement GetElement(string id)
        {
            return elements.ContainsKey(id) ? elements[id] : new ThemeElement();
        }

        private static Color ParseColor(string hex, Color defaultColor)
        {
            if (string.IsNullOrEmpty(hex))
                return defaultColor;

            try
            {
                hex = hex.TrimStart('#');
                int rgb = Convert.ToInt32(hex, 16);
                return Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
            }
            catch
            {
                return defaultColor;
            }
        }

        private static void CreateDefaultLayout()
        {
            SearchBox = new ThemeElement { X = 24, Y = 488, Width = 200, Height = 15 };
            ProgramMenu = new ThemeElement { X = 10, Y = 11, Width = 248, Height = 426 };
            FrequentProgramsMenu = new ThemeElement { X = 10, Y = 11, Width = 248, Height = 426 };
            AllProgramsRollover = new ThemeElement { X = 11, Y = 446 };
            AllProgramsArrow = new ThemeElement { X = 13, Y = 448 };
            AllProgramsText = new ThemeElement { X = 46, Y = 449 };
            GroupOptions = new ThemeElement { X = 268, Y = 53 };
            RolloverPlaceholder = new ThemeElement { X = 300, Y = -31 };
            ShutdownButton = new ThemeElement { X = 266, Y = 482 };
            LogoffButton = new ThemeElement { X = 319, Y = 482, Visible = false };
            ArrowButton = new ThemeElement { X = 337, Y = 483 };
            JumpListViewer = new ThemeElement { X = 275, Y = 35, Width = 205, Height = 418 };
            ShutdownText = new ThemeElement { X = 271, Y = 486 };

            FrequentProgramsSeparatorColor = Color.White;
            ProgramMenuBackColor = Color.White;
            FrequentProgramsBackColor = Color.White;
        }

        private static void LoadImages()
        {
            // Pre-load common images
            LoadImage("startmenu.png");
            LoadImage("userframe.png");
            LoadImage("allprograms.png");
            LoadImage("button.png");
            LoadImage("programs_arrow.png");
            LoadImage("bottombuttons_shutdown.png");
            LoadImage("bottombuttons_arrow.png");
        }

        public static Image GetImage(string filename)
        {
            if (images.ContainsKey(filename))
                return images[filename];

            return LoadImage(filename);
        }

        private static Image LoadImage(string filename)
        {
            try
            {
                string path = Path.Combine(ResourcesPath, filename);
                if (File.Exists(path))
                {
                    var image = Image.FromFile(path);
                    images[filename] = image;
                    return image;
                }
            }
            catch
            {
                // Return null if image can't be loaded
            }

            return null;
        }

        private static string GetDefaultLayout()
        {
            // Return embedded default layout
            return @"<startmenu_base x_offset=""0"" y_offset=""0"">
     <vielement id=""searchbox"" x=""24"" y=""488"" width=""200"" height=""15"" />
     <vielement id=""programmenu"" x=""10"" y=""11"" width=""248"" height=""426"" />
     <vielement id=""frequentprogramsmenu"" x=""10"" y=""11"" width=""248"" height=""426"" />
     <vielement id=""allprograms_rollover"" x=""11"" y=""446"" />
     <vielement id=""allprograms_arrow"" x=""13"" y=""448"" />
     <vielement id=""allprograms_text"" x=""46"" y=""449"" />
     <vielement id=""groupoptions"" x=""268"" y=""53"" />
     <vielement id=""rolloverplaceholder"" x=""300"" y=""-31"" />
     <vielement id=""shutdown_button"" x=""266"" y=""482"" visible=""true"" />
     <vielement id=""logoff_button"" x=""319"" y=""482"" visible=""false"" />
     <vielement id=""arrow_button"" x=""337"" y=""483"" visible=""true"" />
     <vielement id=""jumplist_viewer"" x=""275"" y=""35"" width=""205"" height=""418"" />
     <vielement id=""shutdown_text"" x=""271"" y=""486"" visible=""true"" />
     <frequentprogramsmenu separatorcolour=""#ffffff"" />
</startmenu_base>";
        }

        public static void Reload()
        {
            ResourcesPath = AppSettings.Instance.GetResourcesPath();
            
            // Clear image cache
            foreach (var img in images.Values)
            {
                img?.Dispose();
            }
            images.Clear();

            LoadLayout();
            LoadImages();
        }
    }
}
