using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using ViStart.Core;
using ViStart.Data;

namespace ViStart.UI
{
    /// <summary>
    /// Renders the per-program jumplist (recent files) shown when the user clicks the
    /// chevron next to a frequent program. Drawn into the existing layered window at
    /// the position defined by the skin's "jumplist_viewer" layout element — VB6 ViStart
    /// calls this the "morph" mode: the start menu replaces its program list area
    /// with a list of recent files for the chosen program.
    /// </summary>
    public class RecentFilesPanel
    {
        private const int ITEM_HEIGHT = 22;
        private const int ICON_SIZE = 16;
        private const int HEADER_HEIGHT = 28;
        private const int BACK_HOTZONE_HEIGHT = HEADER_HEIGHT;

        public Rectangle Bounds { get; set; }
        public bool Visible { get; set; }

        // Fired when the user clicks the back chevron at the top of the panel.
        public event Action BackRequested;

        // Fired when the user clicks a recent-file row.
        public event Action<string> FileSelected;

        private ProgramItem program;
        private List<string> files = new List<string>();
        private int hoveredIndex = -1;
        private bool hoveredBack;

        public ProgramItem CurrentProgram { get { return program; } }

        public void SetProgram(ProgramItem newProgram)
        {
            program = newProgram;
            files.Clear();
            hoveredIndex = -1;
            hoveredBack = false;

            if (newProgram == null)
                return;

            string key = newProgram.GetJumpListKey();
            if (string.IsNullOrEmpty(key))
                return;

            files.AddRange(RecentFilesProvider.GetRecentFiles(key));
        }

        public void Render(Graphics g)
        {
            if (!Visible) return;

            DrawHeader(g);

            int maxRows = Math.Max(0, (Bounds.Height - HEADER_HEIGHT) / ITEM_HEIGHT);
            int rows = Math.Min(maxRows, files.Count);

            for (int i = 0; i < rows; i++)
            {
                int y = Bounds.Y + HEADER_HEIGHT + i * ITEM_HEIGHT;
                DrawRow(g, files[i], y, i == hoveredIndex);
            }

            if (files.Count == 0)
            {
                using (var brush = new SolidBrush(Color.Gray))
                using (var font = new Font("Segoe UI", 9f))
                {
                    var emptyRect = new Rectangle(Bounds.X, Bounds.Y + HEADER_HEIGHT,
                        Bounds.Width, Bounds.Height - HEADER_HEIGHT);
                    var format = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };
                    g.DrawString("No recent files", font, brush, emptyRect, format);
                }
            }
        }

        private void DrawHeader(Graphics g)
        {
            // Header: a back chevron + the program name. Keeps it consistent with how
            // Win7 jump-list popouts identify which program's list you're looking at.
            var headerRect = new Rectangle(Bounds.X, Bounds.Y, Bounds.Width, HEADER_HEIGHT);

            if (hoveredBack)
            {
                using (var bg = new SolidBrush(Color.FromArgb(80, Color.SteelBlue)))
                    g.FillRectangle(bg, headerRect);
            }

            // Back chevron — left-pointing triangle.
            int cx = headerRect.X + 14;
            int cy = headerRect.Y + headerRect.Height / 2;
            const int half = 5;
            using (var path = new GraphicsPath())
            {
                path.AddLine(cx + half, cy - half, cx - half, cy);
                path.AddLine(cx - half, cy, cx + half, cy + half);

                var prevSmoothing = g.SmoothingMode;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(hoveredBack ? Color.White : Color.FromArgb(160, Color.Black), 2f))
                {
                    pen.LineJoin = LineJoin.Round;
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawPath(pen, path);
                }
                g.SmoothingMode = prevSmoothing;
            }

            // Program name to the right of the chevron.
            string title = program != null ? program.Caption : string.Empty;
            using (var font = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            using (var brush = new SolidBrush(hoveredBack ? Color.White : Color.FromArgb(35, 35, 35)))
            {
                var titleRect = new Rectangle(headerRect.X + 30, headerRect.Y,
                    headerRect.Width - 36, headerRect.Height);
                var format = new StringFormat
                {
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter
                };
                g.DrawString(title, font, brush, titleRect, format);
            }
        }

        private void DrawRow(Graphics g, string filePath, int y, bool isHovered)
        {
            var itemRect = new Rectangle(Bounds.X, y, Bounds.Width, ITEM_HEIGHT);

            if (isHovered)
            {
                using (var brush = new SolidBrush(Color.FromArgb(60, Color.SteelBlue)))
                    g.FillRectangle(brush, itemRect);
            }

            var icon = IconCache.GetIcon(filePath, false);
            if (icon != null)
            {
                try
                {
                    g.DrawIcon(icon, new Rectangle(
                        itemRect.X + 4,
                        itemRect.Y + (ITEM_HEIGHT - ICON_SIZE) / 2,
                        ICON_SIZE,
                        ICON_SIZE));
                }
                catch { }
            }

            string caption = Path.GetFileName(filePath);
            using (var font = new Font("Segoe UI", 9f))
            using (var brush = new SolidBrush(Color.FromArgb(45, 45, 45)))
            {
                var textRect = new Rectangle(
                    itemRect.X + ICON_SIZE + 8,
                    itemRect.Y,
                    itemRect.Width - ICON_SIZE - 12,
                    itemRect.Height);
                var format = new StringFormat
                {
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter
                };
                g.DrawString(caption, font, brush, textRect, format);
            }
        }

        public bool HitTest(Point point)
        {
            return Bounds.Contains(point);
        }

        public void OnMouseMove(Point point)
        {
            if (!Bounds.Contains(point))
            {
                hoveredIndex = -1;
                hoveredBack = false;
                return;
            }

            int relativeY = point.Y - Bounds.Y;
            if (relativeY < BACK_HOTZONE_HEIGHT)
            {
                hoveredBack = true;
                hoveredIndex = -1;
                return;
            }

            hoveredBack = false;
            int rowIndex = (relativeY - HEADER_HEIGHT) / ITEM_HEIGHT;
            hoveredIndex = (rowIndex >= 0 && rowIndex < files.Count) ? rowIndex : -1;
        }

        public void OnMouseLeave()
        {
            hoveredIndex = -1;
            hoveredBack = false;
        }

        // Returns true if the panel handled the click (back or row); caller should
        // prevent further hit-test fall-through.
        public bool OnMouseClick(Point point, MouseButtons button)
        {
            if (button != MouseButtons.Left) return false;
            if (!Bounds.Contains(point)) return false;

            int relativeY = point.Y - Bounds.Y;
            if (relativeY < BACK_HOTZONE_HEIGHT)
            {
                BackRequested?.Invoke();
                return true;
            }

            int rowIndex = (relativeY - HEADER_HEIGHT) / ITEM_HEIGHT;
            if (rowIndex >= 0 && rowIndex < files.Count)
            {
                FileSelected?.Invoke(files[rowIndex]);
                return true;
            }

            return false;
        }
    }
}
