using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ViStart.Core;
using ViStart.Data;

namespace ViStart.UI
{
    public class FrequentProgramsPanel
    {
        private const int ITEM_HEIGHT = 40;
        private const int ICON_SIZE = 32;
        private const int MAX_ITEMS = 10;

        // Reserved width on the right of each row for the jumplist arrow chevron.
        // The row's hit-test splits at this boundary: clicks left of it launch the
        // program, clicks inside it open the jumplist instead.
        private const int JUMPLIST_AREA_WIDTH = 24;

        public Rectangle Bounds { get; set; }
        public bool Visible { get; set; }

        // Fired when this panel's context menu mutates the program database, so the
        // StartMenu can repaint the layered window (this panel can't repaint itself).
        public event Action ProgramsChanged;

        // Fired when the user clicks the jumplist arrow on a program's row. The
        // StartMenu enters jumplist mode and shows recent files for this program.
        public event Action<ProgramItem> JumpListRequested;

        private List<ProgramItem> displayedPrograms;
        private int hoveredIndex = -1;
        private bool hoveredArrow; // true => mouse is over the chevron of the hovered row

        public FrequentProgramsPanel()
        {
            Visible = true;
            LoadPrograms();
        }

        public void LoadPrograms()
        {
            displayedPrograms = new List<ProgramItem>();
            displayedPrograms.AddRange(AppSettings.Instance.Programs.PinnedPrograms);

            var frequentCount = MAX_ITEMS - displayedPrograms.Count;
            if (frequentCount > 0)
            {
                displayedPrograms.AddRange(
                    AppSettings.Instance.Programs.GetTopFrequentPrograms(frequentCount));
            }
        }

        public void Render(Graphics g)
        {
            if (!Visible)
                return;

            if (displayedPrograms == null || displayedPrograms.Count == 0)
            {
                using (var brush = new SolidBrush(Color.Gray))
                using (var font = new Font("Segoe UI", 9f))
                {
                    var format = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };

                    g.DrawString("No pinned programs\n\nRight-click programs in All Programs to pin them here",
                        font, brush, Bounds, format);
                }
                return;
            }

            int y = Bounds.Y;
            for (int i = 0; i < displayedPrograms.Count && i < MAX_ITEMS; i++)
            {
                DrawProgramItem(g, displayedPrograms[i], y, i == hoveredIndex,
                    displayedPrograms[i].IsPinned);
                y += ITEM_HEIGHT;
            }
        }

        private void DrawProgramItem(Graphics g, ProgramItem program, int y, bool isHovered, bool isPinned)
        {
            var itemRect = new Rectangle(Bounds.X, y, Bounds.Width, ITEM_HEIGHT);

            if (isHovered)
            {
                using (var brush = new SolidBrush(Color.FromArgb(50, Color.LightBlue)))
                {
                    g.FillRectangle(brush, itemRect);
                }
            }

            var icon = program.GetIcon();
            if (icon != null)
            {
                g.DrawIcon(icon, new Rectangle(
                    itemRect.X + 4,
                    itemRect.Y + (ITEM_HEIGHT - ICON_SIZE) / 2,
                    ICON_SIZE,
                    ICON_SIZE));
            }

            bool hasJumpList = program.HasJumpList();

            // Caption — shrink the text rect when there's a jumplist arrow to the right
            // so the caption ellipsizes before colliding with the chevron.
            int captionRight = hasJumpList
                ? itemRect.Right - JUMPLIST_AREA_WIDTH
                : itemRect.Right - 4;

            using (var brush = new SolidBrush(Color.Black))
            using (var font = new Font("Segoe UI", 9f))
            {
                int captionLeft = itemRect.X + ICON_SIZE + 8;
                var textRect = new Rectangle(
                    captionLeft,
                    itemRect.Y,
                    captionRight - captionLeft,
                    itemRect.Height);

                var format = new StringFormat
                {
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter
                };

                g.DrawString(program.Caption, font, brush, textRect, format);
            }

            if (hasJumpList)
            {
                bool arrowHot = isHovered && hoveredArrow;
                DrawJumpListChevron(g, itemRect, arrowHot);
            }
        }

        // Right-pointing chevron in the right-side gutter of the row. Drawn with a
        // GraphicsPath so it doesn't depend on a glyph being available in any font.
        private void DrawJumpListChevron(Graphics g, Rectangle itemRect, bool isHot)
        {
            int areaLeft = itemRect.Right - JUMPLIST_AREA_WIDTH;
            var areaRect = new Rectangle(areaLeft, itemRect.Y,
                JUMPLIST_AREA_WIDTH, itemRect.Height);

            if (isHot)
            {
                using (var bg = new SolidBrush(Color.FromArgb(80, Color.SteelBlue)))
                    g.FillRectangle(bg, areaRect);
            }

            int cx = areaRect.X + areaRect.Width / 2;
            int cy = areaRect.Y + areaRect.Height / 2;
            const int half = 4;

            using (var path = new GraphicsPath())
            {
                path.AddLine(cx - half, cy - half, cx + half, cy);
                path.AddLine(cx + half, cy, cx - half, cy + half);

                using (var pen = new Pen(isHot ? Color.White : Color.FromArgb(120, Color.Black), 2f))
                {
                    pen.LineJoin = LineJoin.Round;
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    var prevSmoothing = g.SmoothingMode;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.DrawPath(pen, path);
                    g.SmoothingMode = prevSmoothing;
                }
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
                hoveredArrow = false;
                return;
            }

            int relativeY = point.Y - Bounds.Y;
            int newIndex = relativeY / ITEM_HEIGHT;

            if (newIndex >= 0 && newIndex < displayedPrograms.Count)
                hoveredIndex = newIndex;
            else
                hoveredIndex = -1;

            hoveredArrow = hoveredIndex >= 0
                && displayedPrograms[hoveredIndex].HasJumpList()
                && point.X >= Bounds.Right - JUMPLIST_AREA_WIDTH;
        }

        public void OnMouseLeave()
        {
            hoveredIndex = -1;
            hoveredArrow = false;
        }

        public ProgramItem OnMouseClick(Point point, MouseButtons button)
        {
            if (!Bounds.Contains(point))
                return null;

            int relativeY = point.Y - Bounds.Y;
            int index = relativeY / ITEM_HEIGHT;

            if (index < 0 || index >= displayedPrograms.Count)
                return null;

            var program = displayedPrograms[index];

            // Left-click on the chevron opens the jumplist instead of launching.
            if (button == MouseButtons.Left
                && program.HasJumpList()
                && point.X >= Bounds.Right - JUMPLIST_AREA_WIDTH)
            {
                JumpListRequested?.Invoke(program);
                return null;
            }

            return program;
        }

        public ContextMenuStrip GetContextMenu(ProgramItem program)
        {
            var menu = new ContextMenuStrip();

            if (program.IsPinned)
            {
                menu.Items.Add("Unpin from Start Menu", null,
                    (s, args) => TogglePin(program));
            }
            else
            {
                menu.Items.Add("Pin to Start Menu", null,
                    (s, args) => TogglePin(program));
            }

            menu.Items.Add("Remove from list", null,
                (s, args) => RemoveProgram(program));

            return menu;
        }

        private void TogglePin(ProgramItem program)
        {
            AppSettings.Instance.Programs.TogglePin(program.Path);
            LoadPrograms();
            ProgramsChanged?.Invoke();
        }

        private void RemoveProgram(ProgramItem program)
        {
            AppSettings.Instance.Programs.RemoveProgram(program.Path);
            LoadPrograms();
            ProgramsChanged?.Invoke();
        }
    }
}
