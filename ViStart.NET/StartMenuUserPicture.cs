using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ViStart.NET
{
    public partial class StartMenuUserPicture : UserControl
    {
        private Image userImage;
        private bool isHovered;

        public StartMenuUserPicture()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer, true);

            LoadUserPicture();
        }

        private void LoadUserPicture()
        {
            // Try to load from various possible locations
            string[] possiblePaths = {
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    @"Microsoft\User Account Pictures",
                    $"{Environment.UserName}.bmp"),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    @"Microsoft\User Account Pictures",
                    "user.bmp"),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    @"Microsoft\User Account Pictures",
                    "guest.bmp")
            };

            foreach (string path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        userImage?.Dispose();
                        userImage = Image.FromFile(path);
                        break;
                    }
                    catch
                    {
                        // Skip if image can't be loaded
                        continue;
                    }
                }
            }
        }

        public void Draw(Graphics g, Rectangle bounds)
        {
            if (userImage == null) return;

            // Draw the user picture
            g.DrawImage(userImage,
                bounds.X + 11, bounds.Y + 11,  // Offset to match frame
                48, 48);  // Standard size for user picture
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Draw(e.Graphics, ClientRectangle);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            isHovered = false;
            Invalidate();
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            // Launch user account control panel
            try
            {
                System.Diagnostics.Process.Start("control", "nusrmgr.cpl");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to launch user settings: {ex.Message}");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                userImage?.Dispose();
            }
            base.Dispose(disposing);
        }

        private void StartMenuUserPicture_Load(object sender, EventArgs e)
        {

        }
    }
}
