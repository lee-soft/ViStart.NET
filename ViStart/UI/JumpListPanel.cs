using System;
using System.Collections.Generic;
using System.Drawing;
using ViStart.Core;
using ViStart.Data;

namespace ViStart.UI
{
    public class HoveredNavigationItemChangedEventArgs : EventArgs
    {
        public NavigationItem Item { get; set; }
    }

    public class JumpListPanel
    {
        private const int ITEM_HEIGHT = 33;
        private const int LEFT_PADDING = 7;   // matches VB6 NavigationPane.cls LEFT_MARGIN
        private const int TOP_MARGIN = 10;    // matches VB6 NavigationPane.cls TOP_MARGIN
        private const int HOVER_SLICE_WIDTH = 8;

        public Rectangle Bounds { get; set; }
        public bool Visible { get; set; }

        public event EventHandler<HoveredNavigationItemChangedEventArgs> HoveredItemChanged;

        private List<NavigationItem> navigationItems;
        private int hoveredIndex = -1;
        private int pressedIndex = -1;
        private Image hoverBackgroundImage;
        private int hoverBackgroundFrameHeight;

        public JumpListPanel()
        {
            Visible = true;
            LoadNavigationItems();
            LoadHoverBackground();
        }

        private void LoadNavigationItems()
        {
            // Default 9-item nav pane matching the VB6 ViStart layout for Win 7-era skins.
            // Recent uses display_as_menu in VB6 — rendered with a ▶ submenu chevron.
            navigationItems = new List<NavigationItem>
            {
                new NavigationItem(LanguageManager.T("documents", "Documents"),     "shell:Personal",         null,              "documents.png"),
                new NavigationItem(LanguageManager.T("pictures", "Pictures"),      "shell:My Pictures",      null,              "pictures.png"),
                new NavigationItem(LanguageManager.T("music", "Music"),         "shell:My Music",         null,              "music.png"),
                new NavigationItem(LanguageManager.T("videos", "Videos"),        "shell:My Video",         null,              "videos.png"),
                new NavigationItem(LanguageManager.T("recent", "Recent"),        "shell:Recent",           null,              "Recent.png", hasSubmenu: true),
                new NavigationItem(LanguageManager.T("computer", "Computer"),      "shell:MyComputerFolder", null,              "computer.png"),
                new NavigationItem(LanguageManager.T("connect_to", "Connect To"),    "ncpa.cpl",               null,              "connect.png"),
                new NavigationItem(LanguageManager.T("control_panel", "Control Panel"), "control.exe",            null,              "control.png"),
                new NavigationItem(LanguageManager.T("run", "Run..."),        "rundll32.exe",           "shell32.dll,#61", "run.png"),
            };
        }

        private void LoadHoverBackground()
        {
            hoverBackgroundImage = ThemeManager.GetImage("button.png");
            if (hoverBackgroundImage != null)
                hoverBackgroundFrameHeight = hoverBackgroundImage.Height / 2;
        }

        public void Render(Graphics g)
        {
            if (!Visible || navigationItems == null || navigationItems.Count == 0)
                return;

            int y = Bounds.Y + TOP_MARGIN;
            for (int i = 0; i < navigationItems.Count; i++)
            {
                DrawNavigationItem(g, navigationItems[i], y, i == hoveredIndex, i == pressedIndex);
                y += ITEM_HEIGHT;
            }
        }

        private void DrawNavigationItem(Graphics g, NavigationItem item, int y, bool isHovered, bool isPressed)
        {
            var itemRect = new Rectangle(Bounds.X, y, Bounds.Width, ITEM_HEIGHT);

            // VB6 NavigationPane.cls:735 — top half = hover, bottom half = pressed.
            if (isPressed)
                DrawHoverBackground(g, itemRect, pressed: true);
            else if (isHovered)
                DrawHoverBackground(g, itemRect, pressed: false);

            // VB6 default nav text colour is #ffffff (LayoutParser.cls:152 +
            // NavigationPane.cls:702 fall-through "ffffff").
            using (var brush = new SolidBrush(Color.White))
            using (var font = new Font("Segoe UI", 9.5f))
            {
                var textRect = new Rectangle(
                    itemRect.X + LEFT_PADDING,
                    itemRect.Y,
                    itemRect.Width - LEFT_PADDING - 16,
                    itemRect.Height);

                var format = new StringFormat
                {
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter
                };

                g.DrawString(item.Caption, font, brush, textRect, format);
            }

            if (item.HasSubmenu)
                DrawSubmenuChevron(g, itemRect);
        }

        private void DrawHoverBackground(Graphics g, Rectangle dest, bool pressed)
        {
            if (hoverBackgroundImage == null || hoverBackgroundFrameHeight == 0)
            {
                Color tint = pressed
                    ? Color.FromArgb(110, Color.SteelBlue)
                    : Color.FromArgb(60, Color.LightSteelBlue);
                using (var brush = new SolidBrush(tint))
                    g.FillRectangle(brush, dest);
                return;
            }

            // 3-slice horizontal scaling so we don't squish button.png (140 wide) into a
            // wider pane. VB6 itself draws the rollover at button.png's natural width;
            // 3-slicing lets us cover any pane width without distorting the corners.
            int srcW = hoverBackgroundImage.Width;
            int srcH = hoverBackgroundFrameHeight;
            int srcY = pressed ? srcH : 0; // top half = hover, bottom half = pressed

            int slice = HOVER_SLICE_WIDTH;
            if (slice * 2 >= srcW) slice = Math.Max(1, srcW / 4);

            // Left edge
            g.DrawImage(hoverBackgroundImage,
                new Rectangle(dest.X, dest.Y, slice, dest.Height),
                new Rectangle(0, srcY, slice, srcH),
                GraphicsUnit.Pixel);

            // Middle (stretched)
            int destMidW = dest.Width - 2 * slice;
            if (destMidW > 0)
            {
                g.DrawImage(hoverBackgroundImage,
                    new Rectangle(dest.X + slice, dest.Y, destMidW, dest.Height),
                    new Rectangle(slice, srcY, srcW - 2 * slice, srcH),
                    GraphicsUnit.Pixel);
            }

            // Right edge
            g.DrawImage(hoverBackgroundImage,
                new Rectangle(dest.Right - slice, dest.Y, slice, dest.Height),
                new Rectangle(srcW - slice, srcY, slice, srcH),
                GraphicsUnit.Pixel);
        }

        private void DrawSubmenuChevron(Graphics g, Rectangle itemRect)
        {
            // Right-pointing triangle on the right edge of the row.
            int size = 5;
            int rightPad = 10;
            int cx = itemRect.Right - rightPad;
            int cy = itemRect.Y + itemRect.Height / 2;

            var pts = new[]
            {
                new Point(cx - size + 2, cy - size),
                new Point(cx + 2,        cy),
                new Point(cx - size + 2, cy + size),
            };

            var oldSmoothing = g.SmoothingMode;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var brush = new SolidBrush(Color.FromArgb(80, 80, 80)))
                g.FillPolygon(brush, pts);
            g.SmoothingMode = oldSmoothing;
        }

        public bool HitTest(Point point)
        {
            return Bounds.Contains(point);
        }

        public void OnMouseMove(Point point)
        {
            int newIndex = -1;

            if (Bounds.Contains(point))
            {
                int relativeY = point.Y - Bounds.Y - TOP_MARGIN;
                if (relativeY >= 0)
                {
                    int candidate = relativeY / ITEM_HEIGHT;
                    if (candidate >= 0 && candidate < navigationItems.Count)
                        newIndex = candidate;
                }
            }

            if (newIndex != hoveredIndex)
            {
                hoveredIndex = newIndex;
                var hovered = newIndex >= 0 ? navigationItems[newIndex] : null;
                HoveredItemChanged?.Invoke(this, new HoveredNavigationItemChangedEventArgs { Item = hovered });
            }
        }

        public void OnMouseLeave()
        {
            pressedIndex = -1;
            if (hoveredIndex != -1)
            {
                hoveredIndex = -1;
                HoveredItemChanged?.Invoke(this, new HoveredNavigationItemChangedEventArgs { Item = null });
            }
        }

        // Returns true if a nav item was hit and entered the pressed state — caller
        // should redraw and stop dispatching the click further.
        public bool OnMouseDown(Point point)
        {
            int index = HitIndex(point);
            if (index < 0) return false;

            pressedIndex = index;
            return true;
        }

        // Returns the nav item to launch when the mouse-up lands on the same item
        // that was pressed. Drag-off cancels (matches Win/UI conventions).
        public NavigationItem OnMouseUp(Point point)
        {
            int wasPressed = pressedIndex;
            pressedIndex = -1;

            if (wasPressed < 0) return null;

            int index = HitIndex(point);
            if (index == wasPressed && index < navigationItems.Count)
                return navigationItems[index];

            return null;
        }

        private int HitIndex(Point point)
        {
            if (!Bounds.Contains(point)) return -1;
            int relativeY = point.Y - Bounds.Y - TOP_MARGIN;
            if (relativeY < 0) return -1;
            int index = relativeY / ITEM_HEIGHT;
            if (index < 0 || index >= navigationItems.Count) return -1;
            return index;
        }
    }
}
