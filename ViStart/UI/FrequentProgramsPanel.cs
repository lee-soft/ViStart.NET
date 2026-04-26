using System;
using System.Collections.Generic;
using System.Drawing;
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

        public Rectangle Bounds { get; set; }
        public bool Visible { get; set; }

        private List<ProgramItem> displayedPrograms;
        private int hoveredIndex = -1;

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
                // Draw empty state message
                using (var brush = new SolidBrush(Color.Gray))
                using (var font = new Font("Segoe UI", 9f))
                {
                    var format = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };

                    g.DrawString(LanguageManager.T("no_pinned_programs", "No pinned programs\n\nRight-click programs in All Programs to pin them here"),
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

            // Background
            if (isHovered)
            {
                using (var brush = new SolidBrush(Color.FromArgb(50, Color.LightBlue)))
                {
                    g.FillRectangle(brush, itemRect);
                }
            }

            // Icon
            var icon = program.GetIcon();
            if (icon != null)
            {
                g.DrawIcon(icon, new Rectangle(
                    itemRect.X + 4,
                    itemRect.Y + (ITEM_HEIGHT - ICON_SIZE) / 2,
                    ICON_SIZE,
                    ICON_SIZE));
            }

            // Caption
            using (var brush = new SolidBrush(Color.Black))
            using (var font = new Font("Segoe UI", 9f))
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

                g.DrawString(program.Caption, font, brush, textRect, format);
            }

            // Pin indicator
            if (isPinned)
            {
                using (var brush = new SolidBrush(Color.FromArgb(100, Color.Gray)))
                using (var font = new Font("Segoe UI", 7f))
                {
                    g.DrawString("📌", font, brush,
                        itemRect.Right - 20,
                        itemRect.Y + (ITEM_HEIGHT - 16) / 2);
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
                return;
            }

            int relativeY = point.Y - Bounds.Y;
            int newIndex = relativeY / ITEM_HEIGHT;

            if (newIndex != hoveredIndex && newIndex < displayedPrograms.Count)
            {
                hoveredIndex = newIndex;
            }
        }

        public void OnMouseLeave()
        {
            hoveredIndex = -1;
        }

        public ProgramItem OnMouseClick(Point point, MouseButtons button)
        {
            if (!Bounds.Contains(point))
                return null;

            int relativeY = point.Y - Bounds.Y;
            int index = relativeY / ITEM_HEIGHT;

            if (index >= 0 && index < displayedPrograms.Count)
            {
                return displayedPrograms[index];
            }

            return null;
        }

        public ContextMenuStrip GetContextMenu(ProgramItem program)
        {
            var menu = new ContextMenuStrip();

            if (program.IsPinned)
            {
                menu.Items.Add(LanguageManager.T("unpin_from_start", "Unpin from Start Menu"), null,
                    (s, args) => TogglePin(program));
            }
            else
            {
                menu.Items.Add(LanguageManager.T("pin_to_start", "Pin to Start Menu"), null,
                    (s, args) => TogglePin(program));
            }

            menu.Items.Add(LanguageManager.T("remove_from_list", "Remove from list"), null,
                (s, args) => RemoveProgram(program));

            return menu;
        }

        private void TogglePin(ProgramItem program)
        {
            AppSettings.Instance.Programs.TogglePin(program.Path);
            LoadPrograms();
        }

        private void RemoveProgram(ProgramItem program)
        {
            AppSettings.Instance.Programs.RemoveProgram(program.Path);
            LoadPrograms();
        }
    }
}
