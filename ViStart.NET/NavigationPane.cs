using System;
using System.Drawing;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Xml;
using System.IO;

namespace ViStart.NET
{
    public class NavigationPaneItem
    {
        public string Text { get; set; }
        public string Command { get; set; }
        public string IconPath { get; set; }
        public string RolloverPath { get; set; }
        public bool IsHovered { get; set; }
        public Rectangle Bounds { get; set; }
        public bool IsVisible { get; set; } = true;
        public bool IsCustom { get; set; }
        public string DisplayMode { get; set; } = "link"; // link or menu
        public ContextMenu ContextMenu { get; set; }
    }

    public class NavigationPane : IDisposable
    {
        private List<NavigationPaneItem> items;
        private readonly Settings settings;
        private readonly LayoutManager layoutManager;

        private Point lastMousePos;
        private int visibilityLimit;
        private bool enableVisibilityLimit;

        private Image buttonImage;
        private Font navFont;
        private Color textColor;
        private Image backgroundImage;

        public Rectangle Bounds { get; set; }
        public bool NeedsRedraw { get; set; }

        private NavigationContextMenu contextMenu;

        /*        public event EventHandler<string> OnRolloverChanged;
                public event EventHandler<NavigationPaneItem> OnItemClicked;
        */
        public NavigationPane(Settings settings, LayoutManager layoutManager, Form form)
        {
            this.settings = settings;
            this.layoutManager = layoutManager;
            items = new List<NavigationPaneItem>();

            contextMenu = new NavigationContextMenu(settings, form);

            LoadResources();
            LoadNavigationItems();
        }

        public void HandleRightClick(Point clickPos, Form form)
        {
            foreach (var item in items)
            {
                if (!item.IsVisible) continue;

                if (item.Bounds.Contains(clickPos))
                {
                    // Show context menu for this item at the click position
                    contextMenu.Show(item, clickPos, form);
                    break;
                }
            }
        }

        private void LoadResources()
        {
            try
            {
                // Load visual resources
                buttonImage = Image.FromFile(Path.Combine(settings.ResourcePath, "button.png"));

                // Get font/colors from layout

                    navFont = new Font("Segoe UI", 10);
                    textColor = Color.White;

                // Get layout settings
                visibilityLimit = layoutManager.GroupOptionsLimit;
                enableVisibilityLimit = layoutManager.EnableVisibilityLimit;

                // Load background if specified
                string bgPath = Path.Combine(settings.ResourcePath, "navigation_bg.png");
                if (File.Exists(bgPath))
                {
                    backgroundImage = Image.FromFile(bgPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load navigation resources: {ex.Message}");
            }
        }

        private void LoadNavigationItems()
        {
            items.Clear();

            // Load items from XML
            if (!string.IsNullOrEmpty(settings.NavigationPaneXml))
            {
                try
                {
                    var doc = new XmlDocument();
                    doc.LoadXml(settings.NavigationPaneXml);

                    foreach (XmlNode xmlNode in doc.DocumentElement.ChildNodes)
                    {
                        // Skip non-element nodes (text, comments, etc.)
                        if (xmlNode.NodeType != XmlNodeType.Element)
                            continue;

                        // Skip if not a folder element
                        if (xmlNode.Name != "folder")
                            continue;

                        var item = CreateNavigationItem(xmlNode);
                        if (item != null && IsWindowsVersionSupported(xmlNode))
                        {
                            items.Add(item);
                        }
                    }

                    // If no items were loaded, fall back to defaults
                    if (items.Count == 0)
                    {
                        LoadDefaultItems();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading navigation items: {ex.Message}");
                    LoadDefaultItems();
                }
            }
            else
            {
                LoadDefaultItems();
            }

            // Apply visibility limit if enabled
            if (enableVisibilityLimit && visibilityLimit > 0)
            {
                for (int i = visibilityLimit; i < items.Count; i++)
                {
                    items[i].IsVisible = false;
                }
            }
        }

        private NavigationPaneItem CreateNavigationItem(XmlNode folderNode)
        {
            string caption = GetAttribute(folderNode, "caption");
            string path = GetAttribute(folderNode, "path");
            string rollover = GetAttribute(folderNode, "rollover");
            bool displayAsMenu = GetAttributeBool(folderNode, "display_as_menu", false);
            bool visible = GetAttributeBool(folderNode, "visible", true);

            if (string.IsNullOrEmpty(caption) || string.IsNullOrEmpty(path))
                return null;

            // Expand environment variables and string resources
            caption = ExpandVariables(caption);
            path = ExpandVariables(path);

            // Build rollover path
            string rolloverPath = null;
            if (!string.IsNullOrEmpty(rollover))
            {
                rolloverPath = Path.Combine(settings.ResourcePath, "rollover", rollover);
            }

            return new NavigationPaneItem
            {
                Text = caption,
                Command = path,
                RolloverPath = rolloverPath,
                IsVisible = visible,
                DisplayMode = displayAsMenu ? "menu" : "link",
                IsCustom = false
            };
        }

        private bool IsWindowsVersionSupported(XmlNode folderNode)
        {
            string minVersion = GetAttribute(folderNode, "minwinversion");
            string maxVersion = GetAttribute(folderNode, "maxwinversion");

            if (string.IsNullOrEmpty(minVersion) && string.IsNullOrEmpty(maxVersion))
                return true;

            Version currentVersion = Environment.OSVersion.Version;

            // Convert Windows version numbers (5.1 = XP, 6.0 = Vista, 6.1 = Win7, etc.)
            if (!string.IsNullOrEmpty(minVersion))
            {
                if (Version.TryParse(minVersion, out Version min))
                {
                    if (currentVersion < min) return false;
                }
            }

            if (!string.IsNullOrEmpty(maxVersion))
            {
                if (Version.TryParse(maxVersion, out Version max))
                {
                    if (currentVersion > max) return false;
                }
            }

            return true;
        }

        private string ExpandVariables(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            // Handle environment variables
            string result = Environment.ExpandEnvironmentVariables(input);

            // Handle CSIDL folder constants
            result = ExpandCSIDLPaths(result);

            // Handle string resources
            result = ExpandStringResources(result);

            return result;
        }

        private string ExpandCSIDLPaths(string input)
        {
            var csidlMappings = new Dictionary<string, Environment.SpecialFolder>
            {
                { "%CSIDL_PERSONAL%", Environment.SpecialFolder.Personal },
                { "%CSIDL_MYPICTURES%", Environment.SpecialFolder.MyPictures },
                { "%CSIDL_MYMUSIC%", Environment.SpecialFolder.MyMusic },
                { "%CSIDL_MYVIDEO%", Environment.SpecialFolder.MyVideos },
                { "%CSIDL_DESKTOP%", Environment.SpecialFolder.Desktop },
                { "%CSIDL_RECENT%", Environment.SpecialFolder.Recent }
            };

            foreach (var mapping in csidlMappings)
            {
                if (input.Contains(mapping.Key))
                {
                    string folderPath = Environment.GetFolderPath(mapping.Value);
                    input = input.Replace(mapping.Key, folderPath);
                }
            }

            return input;
        }

        private string ExpandStringResources(string input)
        {
            var stringMappings = new Dictionary<string, string>
            {
                { "%strDocuments%", "Documents" },
                { "%strPictures%", "Pictures" },
                { "%strMusic%", "Music" },
                { "%strVideos%", "Videos" },
                { "%strGames%", "Games" },
                { "%strComputer%", "Computer" },
                { "%strControlPanel%", "Control Panel" },
                { "%strLibraries%", "Libraries" },
                { "%strNetwork%", "Network" },
                { "%strRecent%", "Recent Items" }
            };

            foreach (var mapping in stringMappings)
            {
                if (input.Contains(mapping.Key))
                {
                    input = input.Replace(mapping.Key, mapping.Value);
                }
            }

            return input;
        }

        private string GetAttribute(XmlNode node, string name, string defaultValue = null)
        {
            return node.Attributes?[name]?.Value ?? defaultValue;
        }

        private bool GetAttributeBool(XmlNode node, string name, bool defaultValue = false)
        {
            string value = GetAttribute(node, name);
            return bool.TryParse(value, out bool result) ? result : defaultValue;
        }

        private struct DefaultNavItem
        {
            public string Text;
            public string Command;
            public string Icon;

            public DefaultNavItem(string text, string command, string icon)
            {
                Text = text;
                Command = command;
                Icon = icon;
            }
        }

        private void LoadDefaultItems()
        {
            // These correspond to the VB6 version's default items
            var defaultItems = new[]
            {
                new DefaultNavItem("Documents", "shell:Personal", "documents.png"),
                new DefaultNavItem("Pictures", "shell:My Pictures", "pictures.png"),
                new DefaultNavItem("Music", "shell:My Music", "music.png"),
                new DefaultNavItem("Videos", "shell:My Video", "videos.png"),
                new DefaultNavItem("Games", "shell:Games", "games.png"),
                new DefaultNavItem("Computer", "shell:MyComputerFolder", "computer.png"),
                new DefaultNavItem("Control Panel", "shell:ControlPanelFolder", "control-panel.png"),
                new DefaultNavItem("Devices and Printers", "shell:PrintersFolder", "printers.png"),
                new DefaultNavItem("Network", "shell:NetworkPlacesFolder", "network.png")
            };

            items = new List<NavigationPaneItem>
            {
                new NavigationPaneItem() { Text = "Documents", Command = "shell:Personal", RolloverPath = "documents.png" },
                new NavigationPaneItem() { Text = "Pictures", Command = "shell:My Pictures", RolloverPath = "pictures.png" }
            };
        }

        public void Draw(Graphics g)
        {
            // Draw background if available
            if (backgroundImage != null)
            {
                g.DrawImage(backgroundImage, Bounds);
            }

            int y = Bounds.Y;
            int itemHeight = layoutManager.GroupOptionsSeparator;
            int rolloverHeight = buttonImage.Height / 2;

            foreach (var item in items)
            {
                if (!item.IsVisible) continue;

                // Update item bounds
                item.Bounds = new Rectangle(Bounds.X, y, Bounds.Width, itemHeight);
                Rectangle rolloverPos = new Rectangle(Bounds.X - 3, y, buttonImage.Width, rolloverHeight);
                // Draw hover state
                if (item.IsHovered && buttonImage != null)
                {
                    g.DrawImage(buttonImage,
                        rolloverPos,
                        new Rectangle(0, 0, buttonImage.Width, buttonImage.Height / 2),
                        GraphicsUnit.Pixel);
                }

                // Draw text
                using (var brush = new SolidBrush(textColor))
                {
                    var textRect = new Rectangle(
                        item.Bounds.X + layoutManager.XOffset,
                        y + (rolloverHeight - (int)navFont.GetHeight()) / 2,
                        item.Bounds.Width - (layoutManager.XOffset * 2),
                        rolloverHeight);

                    g.DrawString(item.Text, navFont, brush, textRect);
                }

                y += itemHeight;
            }
        }

        public void HandleMouseMove(Point mousePos)
        {
            lastMousePos = mousePos;
            bool changed = false;
            string newRollover = null;

            if (Bounds.Contains(mousePos))
            {
                foreach (var item in items)
                {
                    if (!item.IsVisible) continue;

                    bool wasHovered = item.IsHovered;
                    item.IsHovered = item.Bounds.Contains(mousePos);

                    if (wasHovered != item.IsHovered)
                    {
                        changed = true;
                        if (item.IsHovered && !string.IsNullOrEmpty(item.RolloverPath))
                        {
                            newRollover = item.RolloverPath;
                        }
                    }
                }
            }
            else
            {
                foreach (var item in items)
                {
                    if (item.IsHovered)
                    {
                        item.IsHovered = false;
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                NeedsRedraw = true;
                // OnRolloverChanged?.Invoke(this, newRollover);
            }
        }

        public void HandleMouseLeave()
        {
            bool changed = false;
            foreach (var item in items)
            {
                if (item.IsHovered)
                {
                    item.IsHovered = false;
                    changed = true;
                }
            }

            if (changed)
            {
                NeedsRedraw = true;
                // OnRolloverChanged?.Invoke(this, null);
            }
        }

        public void HandleClick(Point clickPos, Form parentForm)
        {
            foreach (var item in items)
            {
                if (!item.IsVisible) continue;

                if (item.Bounds.Contains(clickPos))
                {
                    // OnItemClicked?.Invoke(this, item);

                    if (item.DisplayMode == "menu" && item.ContextMenu != null)
                    {
                        item.ContextMenu.Show(parentForm, clickPos);
                    }
                    else
                    {
                        try
                        {
                            System.Diagnostics.Process.Start("explorer.exe", item.Command);
                            parentForm?.Hide();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error opening {item.Text}: {ex.Message}");
                        }
                    }
                    break;
                }
            }
        }

        public void Dispose()
        {
            buttonImage?.Dispose();
            navFont?.Dispose();
            backgroundImage?.Dispose();

            foreach (var item in items)
            {
                item.ContextMenu?.Dispose();
            }
        }
    }
}