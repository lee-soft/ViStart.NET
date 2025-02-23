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

/*        public event EventHandler<string> OnRolloverChanged;
        public event EventHandler<NavigationPaneItem> OnItemClicked;
*/
        public NavigationPane(Settings settings, LayoutManager layoutManager)
        {
            this.settings = settings;
            this.layoutManager = layoutManager;
            items = new List<NavigationPaneItem>();

            LoadResources();
            LoadNavigationItems();
        }

        private void LoadResources()
        {
            try
            {
                // Load visual resources
                buttonImage = Image.FromFile(Path.Combine(settings.ResourcePath, "button.png"));

                // Get font/colors from layout

                    navFont = new Font("Segoe UI", 11);
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
            // First load standard items
            LoadDefaultItems();

            // Then load custom items from XML
            if (!string.IsNullOrEmpty(settings.NavigationPaneXml))
            {
                try
                {
                    var doc = new XmlDocument();
                    doc.LoadXml(settings.NavigationPaneXml);

                    foreach (XmlNode node in doc.SelectNodes("//item"))
                    {
                        var item = new NavigationPaneItem
                        {
                            Text = node.Attributes["text"]?.Value,
                            Command = node.Attributes["command"]?.Value,
                            IconPath = node.Attributes["icon"]?.Value,
                            RolloverPath = node.Attributes["rollover"]?.Value,
                            IsCustom = true,
                            DisplayMode = node.Attributes["display"]?.Value ?? "link",
                            IsVisible = bool.Parse(node.Attributes["visible"]?.Value ?? "true")
                        };

                        if (!string.IsNullOrEmpty(item.Text))
                        {
                            items.Add(item);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading navigation XML: {ex.Message}");
                }
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

            foreach (var item in items)
            {
                if (!item.IsVisible) continue;

                // Update item bounds
                item.Bounds = new Rectangle(Bounds.X, y, Bounds.Width, itemHeight);

                // Draw hover state
                if (item.IsHovered && buttonImage != null)
                {
                    g.DrawImage(buttonImage,
                        item.Bounds,
                        new Rectangle(0, 0, buttonImage.Width, buttonImage.Height / 2),
                        GraphicsUnit.Pixel);
                }

                // Draw text
                using (var brush = new SolidBrush(textColor))
                {
                    var textRect = new Rectangle(
                        item.Bounds.X + layoutManager.XOffset,
                        y + (itemHeight - (int)navFont.GetHeight()) / 2,
                        item.Bounds.Width - (layoutManager.XOffset * 2),
                        itemHeight);

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