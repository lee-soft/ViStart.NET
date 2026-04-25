using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ViStart.Core;
using ViStart.Data;

namespace ViStart.UI
{
    public class ProgramMenuPanel
    {
        private const int ITEM_HEIGHT = 19;
        private int ICON_SIZE { get { return AppSettings.Instance.UseLargeIcons ? 32 : 16; } }
        // VB6 indents children by one icon width per level (clsTreeview m_cIconSize = 16).
        private int INDENT_SIZE { get { return ICON_SIZE; } }
        // VB6 places text m_cNodeSpace (19) right of the icon's X — i.e. 3px after a 16px icon.
        private const int TEXT_GAP = 3;

        private const int SCROLLBAR_WIDTH = 14;
        private const int MIN_THUMB_HEIGHT = 24;

        public Rectangle Bounds { get; set; }
        public bool Visible { get; set; }

        private List<ProgramNode> displayedNodes;
        private int hoveredIndex = -1;
        private int scrollOffset = 0;
        private int maxScroll = 0;
        private bool isSearchMode = false;

        private bool isDraggingThumb = false;
        private int dragStartMouseY;
        private int dragStartScrollOffset;
        private bool thumbHovered = false;

        public ProgramMenuPanel()
        {
            Visible = false;
            LoadAllPrograms();
        }

        private void LoadAllPrograms()
        {
            var rootNode = StartMenuIndexer.GetRootNode();
            displayedNodes = new List<ProgramNode>();

            // Get all visible nodes (expanded folders show children)
            foreach (var child in rootNode.Children)
            {
                displayedNodes.AddRange(child.GetVisibleNodes());
            }

            isSearchMode = false;
            UpdateScrollBounds();
        }

        public void ShowSearchResults(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                LoadAllPrograms();
                return;
            }

            displayedNodes = StartMenuIndexer.Search(query);
            isSearchMode = true;
            scrollOffset = 0;
            UpdateScrollBounds();
        }

        private void UpdateScrollBounds()
        {
            int totalHeight = displayedNodes.Count * ITEM_HEIGHT;
            maxScroll = Math.Max(0, totalHeight - Bounds.Height);
            scrollOffset = Math.Max(0, Math.Min(scrollOffset, maxScroll));
        }

        private bool ScrollbarVisible { get { return maxScroll > 0; } }

        private Rectangle ContentBounds
        {
            get
            {
                if (!ScrollbarVisible) return Bounds;
                return new Rectangle(Bounds.X, Bounds.Y,
                    Bounds.Width - SCROLLBAR_WIDTH, Bounds.Height);
            }
        }

        private Rectangle ScrollbarTrackBounds
        {
            get
            {
                return new Rectangle(Bounds.Right - SCROLLBAR_WIDTH, Bounds.Y,
                    SCROLLBAR_WIDTH, Bounds.Height);
            }
        }

        private Rectangle ScrollbarThumbBounds
        {
            get
            {
                if (!ScrollbarVisible) return Rectangle.Empty;

                int totalHeight = displayedNodes.Count * ITEM_HEIGHT;
                int thumbHeight = Math.Max(MIN_THUMB_HEIGHT,
                    (int)((long)Bounds.Height * Bounds.Height / totalHeight));
                thumbHeight = Math.Min(thumbHeight, Bounds.Height);

                int travel = Bounds.Height - thumbHeight;
                int thumbY = (maxScroll == 0) ? Bounds.Y
                    : Bounds.Y + (int)((long)scrollOffset * travel / maxScroll);

                return new Rectangle(Bounds.Right - SCROLLBAR_WIDTH + 2, thumbY,
                    SCROLLBAR_WIDTH - 4, thumbHeight);
            }
        }

        public void Render(Graphics g)
        {
            if (!Visible)
                return;

            if (displayedNodes == null || displayedNodes.Count == 0)
            {
                DrawNoResults(g);
                return;
            }

            var content = ContentBounds;
            var previousClip = g.Clip;
            g.SetClip(content);

            int startIndex = Math.Max(0, scrollOffset / ITEM_HEIGHT);
            int endIndex = Math.Min(displayedNodes.Count, startIndex + (content.Height / ITEM_HEIGHT) + 2);

            for (int i = startIndex; i < endIndex; i++)
            {
                int itemY = content.Y + (i * ITEM_HEIGHT) - scrollOffset;

                if (itemY + ITEM_HEIGHT >= content.Y && itemY < content.Bottom)
                {
                    DrawProgramNode(g, displayedNodes[i], itemY, i == hoveredIndex);
                }
            }

            g.Clip = previousClip;

            if (ScrollbarVisible)
                DrawScrollbar(g);
        }

        private void DrawScrollbar(Graphics g)
        {
            var track = ScrollbarTrackBounds;
            var thumb = ScrollbarThumbBounds;

            using (var trackBrush = new SolidBrush(Color.FromArgb(245, 245, 245)))
                g.FillRectangle(trackBrush, track);

            using (var border = new Pen(Color.FromArgb(220, 220, 220)))
                g.DrawLine(border, track.X, track.Y, track.X, track.Bottom - 1);

            Color top, bottom, edge;
            if (isDraggingThumb)
            {
                top = Color.FromArgb(180, 200, 230);
                bottom = Color.FromArgb(150, 175, 215);
                edge = Color.FromArgb(120, 145, 185);
            }
            else if (thumbHovered)
            {
                top = Color.FromArgb(210, 225, 245);
                bottom = Color.FromArgb(180, 205, 235);
                edge = Color.FromArgb(150, 175, 205);
            }
            else
            {
                top = Color.FromArgb(225, 230, 240);
                bottom = Color.FromArgb(195, 205, 220);
                edge = Color.FromArgb(170, 180, 200);
            }

            using (var thumbBrush = new System.Drawing.Drawing2D.LinearGradientBrush(
                thumb, top, bottom, 90f))
            {
                g.FillRectangle(thumbBrush, thumb);
            }

            using (var thumbPen = new Pen(edge))
                g.DrawRectangle(thumbPen, thumb.X, thumb.Y, thumb.Width - 1, thumb.Height - 1);
        }

        private void DrawProgramNode(Graphics g, ProgramNode node, int y, bool isHovered)
        {
            var content = ContentBounds;
            var itemRect = new Rectangle(content.X, y, content.Width, ITEM_HEIGHT);

            // Background - Vista style selection
            if (isHovered)
            {
                using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                    itemRect,
                    Color.FromArgb(242, 249, 252),
                    Color.FromArgb(229, 241, 251),
                    90f))
                {
                    g.FillRectangle(brush, itemRect);
                }

                using (var pen = new Pen(Color.FromArgb(185, 215, 252)))
                {
                    g.DrawRectangle(pen, itemRect.X, itemRect.Y, itemRect.Width - 1, itemRect.Height - 1);
                }
            }

            // VB6 layout: icon at X = level * 16, text at X = iconX + 19. No extra
            // padding — the only horizontal slack is whatever the container provides.
            int indent = isSearchMode ? 0 : (node.Level * INDENT_SIZE);
            int iconX = itemRect.X + indent;
            int textX = iconX + ICON_SIZE + TEXT_GAP;

            // Icon — draw at native size (DrawIconUnstretched picks the matching frame
            // and skips the bilinear stretch that made icons look "squished").
            var icon = node.GetIcon(AppSettings.Instance.UseLargeIcons);
            if (icon != null)
            {
                try
                {
                    g.DrawIconUnstretched(icon, new Rectangle(
                        iconX,
                        itemRect.Y + (ITEM_HEIGHT - ICON_SIZE) / 2,
                        ICON_SIZE,
                        ICON_SIZE));
                }
                catch { }
            }

            // Caption
            using (var brush = new SolidBrush(Color.Black))
            using (var font = new Font("Segoe UI", 9f))
            {
                var textRect = new Rectangle(
                    textX,
                    itemRect.Y,
                    itemRect.Right - textX,
                    itemRect.Height);

                var format = new StringFormat
                {
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter
                };

                g.DrawString(node.Caption, font, brush, textRect, format);
            }
        }

        private void DrawNoResults(Graphics g)
        {
            using (var brush = new SolidBrush(Color.Gray))
            using (var font = new Font("Segoe UI", 10f))
            {
                var format = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };

                g.DrawString("No programs found", font, brush, Bounds, format);
            }
        }

        public bool HitTest(Point point)
        {
            return Bounds.Contains(point);
        }

        public void OnMouseMove(Point point)
        {
            if (isDraggingThumb)
            {
                int travel = Bounds.Height - ScrollbarThumbBounds.Height;
                if (travel > 0)
                {
                    int delta = point.Y - dragStartMouseY;
                    scrollOffset = dragStartScrollOffset + (int)((long)delta * maxScroll / travel);
                    scrollOffset = Math.Max(0, Math.Min(scrollOffset, maxScroll));
                }
                return;
            }

            bool wasThumbHovered = thumbHovered;
            thumbHovered = ScrollbarVisible && ScrollbarThumbBounds.Contains(point);

            if (!ContentBounds.Contains(point))
            {
                hoveredIndex = -1;
                return;
            }

            int relativeY = point.Y - Bounds.Y + scrollOffset;
            int newIndex = relativeY / ITEM_HEIGHT;

            if (newIndex != hoveredIndex && newIndex < displayedNodes.Count)
            {
                hoveredIndex = newIndex;
            }
        }

        public void OnMouseLeave()
        {
            hoveredIndex = -1;
            thumbHovered = false;
        }

        public void OnMouseUp()
        {
            isDraggingThumb = false;
        }

        public void OnMouseWheel(int delta)
        {
            scrollOffset -= delta / 3;
            scrollOffset = Math.Max(0, Math.Min(scrollOffset, maxScroll));
        }

        public ProgramNode OnMouseClick(Point point, MouseButtons button)
        {
            if (!Bounds.Contains(point))
                return null;

            // Scrollbar takes priority over node hits.
            if (ScrollbarVisible && button == MouseButtons.Left)
            {
                if (ScrollbarThumbBounds.Contains(point))
                {
                    isDraggingThumb = true;
                    dragStartMouseY = point.Y;
                    dragStartScrollOffset = scrollOffset;
                    return null;
                }

                if (ScrollbarTrackBounds.Contains(point))
                {
                    // Page up/down depending on which side of the thumb was clicked.
                    var thumb = ScrollbarThumbBounds;
                    int page = Math.Max(ITEM_HEIGHT, Bounds.Height - ITEM_HEIGHT);
                    scrollOffset += (point.Y < thumb.Y) ? -page : page;
                    scrollOffset = Math.Max(0, Math.Min(scrollOffset, maxScroll));
                    return null;
                }
            }

            if (!ContentBounds.Contains(point))
                return null;

            int relativeY = point.Y - Bounds.Y + scrollOffset;
            int index = relativeY / ITEM_HEIGHT;

            if (index >= 0 && index < displayedNodes.Count)
            {
                var node = displayedNodes[index];

                if (button == MouseButtons.Left && node.IsFolder && !isSearchMode)
                {
                    node.Toggle();

                    var rootNode = StartMenuIndexer.GetRootNode();
                    displayedNodes = new List<ProgramNode>();
                    foreach (var child in rootNode.Children)
                    {
                        displayedNodes.AddRange(child.GetVisibleNodes());
                    }
                    UpdateScrollBounds();

                    return null;
                }

                return node;
            }

            return null;
        }

        public ContextMenuStrip GetContextMenu(ProgramNode node)
        {
            var menu = new ContextMenuStrip();

            if (!node.IsFolder)
            {
                menu.Items.Add("Pin to Start Menu", null,
                    (s, args) => PinProgram(node));
            }

            return menu;
        }

        private void PinProgram(ProgramNode node)
        {
            if (!node.IsFolder)
            {
                AppSettings.Instance.Programs.TogglePin(node.Path);
            }
        }
    }
}