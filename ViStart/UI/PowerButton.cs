using System;
using System.Drawing;
using ViStart.Core;

namespace ViStart.UI
{
    public class PowerButton
    {
        public enum PowerAction
        {
            Shutdown,
            Logoff,
            ShowMenu
        }

        public Rectangle Bounds { get; set; }
        public bool Visible { get; set; }

        private PowerAction action;
        private Image buttonImage;
        private bool isHovered = false;
        private bool isPressed = false;
        private int frameHeight;
        private int frameCount;

        public PowerButton(PowerAction buttonAction)
        {
            action = buttonAction;
            Visible = true;
            LoadButtonImage();
        }

        private void LoadButtonImage()
        {
            string imageName = "";

            switch (action)
            {
                case PowerAction.Shutdown:
                    imageName = "bottombuttons_shutdown.png";
                    break;
                case PowerAction.Logoff:
                    imageName = "bottombuttons_logoff.png";
                    break;
                case PowerAction.ShowMenu:
                    imageName = "bottombuttons_arrow.png";
                    break;
            }

            buttonImage = ThemeManager.GetImage(imageName);

            if (buttonImage != null)
            {
                // These images have 3 frames stacked vertically: normal, hover, pressed
                frameCount = 3;
                frameHeight = buttonImage.Height / frameCount;
            }
            else
            {
                CreateDefaultButton();
            }
        }

        private void CreateDefaultButton()
        {
            int buttonSize = 24;
            buttonImage = new Bitmap(buttonSize, buttonSize * 3); // 3 frames
            frameHeight = buttonSize;
            frameCount = 3;

            using (Graphics g = Graphics.FromImage(buttonImage))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                Color baseColor = action == PowerAction.Shutdown
                    ? Color.FromArgb(220, 50, 50)
                    : Color.FromArgb(50, 120, 200);

                // Draw 3 frames: normal, hover, pressed
                for (int i = 0; i < 3; i++)
                {
                    int brightness = i == 0 ? 0 : (i == 1 ? 30 : -30);
                    Color frameColor = Color.FromArgb(
                        Math.Max(0, Math.Min(255, baseColor.R + brightness)),
                        Math.Max(0, Math.Min(255, baseColor.G + brightness)),
                        Math.Max(0, Math.Min(255, baseColor.B + brightness))
                    );

                    using (var brush = new SolidBrush(frameColor))
                    {
                        g.FillEllipse(brush, 2, i * buttonSize + 2, buttonSize - 4, buttonSize - 4);
                    }
                }

                // Draw icons on all frames
                using (var pen = new Pen(Color.White, 2))
                using (var font = new Font("Segoe UI", 8f, FontStyle.Bold))
                using (var textBrush = new SolidBrush(Color.White))
                {
                    for (int i = 0; i < 3; i++)
                    {
                        if (action == PowerAction.Shutdown)
                        {
                            g.DrawArc(pen, 8, i * buttonSize + 8, 8, 8, 135, 270);
                            g.DrawLine(pen, 12, i * buttonSize + 6, 12, i * buttonSize + 12);
                        }
                        else
                        {
                            var format = new StringFormat
                            {
                                Alignment = StringAlignment.Center,
                                LineAlignment = StringAlignment.Center
                            };
                            g.DrawString("▶", font, textBrush,
                                new RectangleF(0, i * buttonSize, buttonSize, buttonSize), format);
                        }
                    }
                }
            }
        }

        public void Render(Graphics g)
        {
            if (!Visible || buttonImage == null)
                return;

            // Determine which frame to draw: 0=normal, 1=hover, 2=pressed
            int frame = isPressed ? 2 : (isHovered ? 1 : 0);
            int frameY = frame * frameHeight;

            Rectangle sourceRect = new Rectangle(0, frameY, buttonImage.Width, frameHeight);

            g.DrawImage(buttonImage, Bounds, sourceRect, GraphicsUnit.Pixel);
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
            isPressed = false;
        }

        public void OnMouseDown()
        {
            isPressed = true;
        }

        public void OnMouseUp()
        {
            isPressed = false;
        }
    }
}