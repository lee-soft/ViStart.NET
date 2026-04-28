using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ViStart.Core;
using ViStart.Native;

namespace ViStart.UI
{
    /// <summary>
    /// Floating popup window listing recently opened files. Mirrors VB6 frmFileMenu —
    /// a separate borderless window that sits next to the start menu rather than
    /// morphing the menu surface in place. Closes on outside click, Esc, or when the
    /// user picks a file (which then also dismisses the start menu).
    /// </summary>
    public class RecentFilesPopup : Form
    {
        private const int ITEM_HEIGHT = 22;
        private const int HEADER_HEIGHT = 28;
        private const int ICON_SIZE = 16;
        private const int POPUP_WIDTH = 320;

        public event Action<string> FileSelected;

        private readonly string title;
        private readonly List<string> files;
        private int hoveredIndex = -1;
        private MouseHook mouseHook;

        public RecentFilesPopup(string title, IList<string> files)
        {
            this.title = title ?? string.Empty;
            this.files = (files ?? new string[0]).ToList();

            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.StartPosition = FormStartPosition.Manual;
            this.BackColor = Color.White;
            this.KeyPreview = true;
            this.SetStyle(
                ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.UserPaint,
                true);

            int rows = Math.Max(1, this.files.Count);
            this.Width = POPUP_WIDTH;
            this.Height = HEADER_HEIGHT + rows * ITEM_HEIGHT + 6;

            mouseHook = new MouseHook();
            mouseHook.LeftButtonDown += OnOutsideClick;
            mouseHook.RightButtonDown += OnOutsideClick;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= User32.WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            mouseHook.Install();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            mouseHook.Uninstall();
            base.OnFormClosed(e);
        }

        private void OnOutsideClick(object sender, MouseHookEventArgs e)
        {
            // The hook fires on screen-wide mouse-down. Cursor.Position is virtualised
            // to logical pixels for unaware apps, matching this.Bounds.
            var rect = new Rectangle(this.Left, this.Top, this.Width, this.Height);
            if (rect.Contains(Cursor.Position)) return;

            // Defer the close so the click is allowed to propagate to whatever the user
            // actually targeted (start menu, taskbar, another app). Closing mid-hook
            // races with that delivery.
            BeginInvoke((MethodInvoker)(() =>
            {
                if (!IsDisposed) Close();
            }));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Color.White);

            using (var pen = new Pen(Color.FromArgb(180, 180, 180)))
                g.DrawRectangle(pen, 0, 0, this.Width - 1, this.Height - 1);

            DrawHeader(g);

            if (files.Count == 0)
            {
                using (var font = new Font("Segoe UI", 9f))
                using (var brush = new SolidBrush(Color.Gray))
                {
                    var rect = new Rectangle(0, HEADER_HEIGHT, this.Width, this.Height - HEADER_HEIGHT);
                    var format = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };
                    g.DrawString("No recent files", font, brush, rect, format);
                }
                return;
            }

            for (int i = 0; i < files.Count; i++)
            {
                int y = HEADER_HEIGHT + i * ITEM_HEIGHT;
                DrawRow(g, files[i], y, i == hoveredIndex);
            }
        }

        private void DrawHeader(Graphics g)
        {
            var headerRect = new Rectangle(0, 0, this.Width, HEADER_HEIGHT);
            using (var bg = new SolidBrush(Color.FromArgb(245, 245, 250)))
                g.FillRectangle(bg, headerRect);
            using (var pen = new Pen(Color.FromArgb(220, 220, 220)))
                g.DrawLine(pen, 0, HEADER_HEIGHT - 1, this.Width, HEADER_HEIGHT - 1);

            using (var font = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            using (var brush = new SolidBrush(Color.FromArgb(35, 35, 35)))
            {
                var titleRect = new Rectangle(10, 0, this.Width - 20, HEADER_HEIGHT);
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
            var rect = new Rectangle(0, y, this.Width, ITEM_HEIGHT);

            if (isHovered)
            {
                using (var brush = new SolidBrush(Color.FromArgb(60, Color.SteelBlue)))
                    g.FillRectangle(brush, rect);
            }

            var icon = IconCache.GetIcon(filePath, false);
            if (icon != null)
            {
                try
                {
                    g.DrawIcon(icon, new Rectangle(
                        8,
                        y + (ITEM_HEIGHT - ICON_SIZE) / 2,
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
                    8 + ICON_SIZE + 6,
                    y,
                    this.Width - 8 - ICON_SIZE - 12,
                    ITEM_HEIGHT);
                var format = new StringFormat
                {
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter
                };
                g.DrawString(caption, font, brush, textRect, format);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int newIndex = -1;
            if (e.Y >= HEADER_HEIGHT && files.Count > 0)
            {
                int row = (e.Y - HEADER_HEIGHT) / ITEM_HEIGHT;
                if (row >= 0 && row < files.Count)
                    newIndex = row;
            }
            if (newIndex != hoveredIndex)
            {
                hoveredIndex = newIndex;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hoveredIndex != -1)
            {
                hoveredIndex = -1;
                Invalidate();
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left) return;
            if (e.Y < HEADER_HEIGHT) return;

            int row = (e.Y - HEADER_HEIGHT) / ITEM_HEIGHT;
            if (row < 0 || row >= files.Count) return;

            string path = files[row];
            FileSelected?.Invoke(path);
            Close();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape)
            {
                Close();
                e.Handled = true;
            }
        }
    }
}
