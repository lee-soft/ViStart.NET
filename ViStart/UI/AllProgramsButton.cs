using System;
using System.Drawing;
using ViStart.Core;

namespace ViStart.UI
{
    public class AllProgramsButton
    {
        public Rectangle Bounds { get; set; }
        public bool Visible { get; set; }
        public bool ShowingAllPrograms { get; set; }

        // Pixels above the bg image; layout positions are bg-relative so we need to
        // add this when reading raw theme values from the singleton ThemeManager.
        public int TopOffset { get; set; }

        private bool isHovered = false;
        private Image arrowImage;
        private Image rolloverImage;
        private int frameHeight;

        public AllProgramsButton()
        {
            Visible = true;
            ShowingAllPrograms = false;
            arrowImage = ThemeManager.GetImage("programs_arrow.png");
            // VB6 ships allprograms.png as the rollover background drawn at the
            // allprograms_rollover position. Skins without it fall back to no rollover.
            rolloverImage = ThemeManager.GetImage("allprograms.png");

            if (arrowImage != null)
            {
                frameHeight = arrowImage.Height / 2; // Two frames stacked vertically
            }
        }

        public void Render(Graphics g)
        {
            if (!Visible)
                return;

            if (isHovered)
            {
                var rolloverTheme = ThemeManager.AllProgramsRollover;
                if (rolloverImage != null)
                {
                    g.DrawImage(rolloverImage,
                        rolloverTheme.X, rolloverTheme.Y + TopOffset,
                        rolloverImage.Width, rolloverImage.Height);
                }
                else
                {
                    using (var brush = new SolidBrush(Color.FromArgb(30, Color.LightBlue)))
                    {
                        g.FillRectangle(brush, rolloverTheme.X, rolloverTheme.Y + TopOffset,
                            Bounds.Width, Bounds.Height);
                    }
                }
            }

            if (arrowImage != null)
            {
                var arrowTheme = ThemeManager.AllProgramsArrow;
                int frameY = ShowingAllPrograms ? frameHeight : 0;
                var sourceRect = new Rectangle(0, frameY, arrowImage.Width, frameHeight);
                var destRect = new Rectangle(arrowTheme.X, arrowTheme.Y + TopOffset,
                    arrowImage.Width, frameHeight);
                g.DrawImage(arrowImage, destRect, sourceRect, GraphicsUnit.Pixel);
            }

            using (var brush = new SolidBrush(Color.Black))
            using (var font = new Font("Segoe UI", 9f, FontStyle.Bold))
            {
                string text = ShowingAllPrograms ? LanguageManager.T("back", "Back") : LanguageManager.T("all_programs", "All Programs");
                var format = new StringFormat { LineAlignment = StringAlignment.Center };
                g.DrawString(text, font, brush, Bounds, format);
            }
        }

        public bool HitTest(Point point)
        {
            return Bounds.Contains(point);
        }

        public void OnMouseEnter()
        {
            isHovered = true;
        }

        public void OnMouseLeave()
        {
            isHovered = false;
        }

        public void Toggle()
        {
            ShowingAllPrograms = !ShowingAllPrograms;
        }
    }
}