using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using ViStart.Native;

namespace ViStart.UI
{
    public class LayeredWindow : Form
    {
        protected Bitmap bitmap;
        
        public LayeredWindow()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= User32.WS_EX_LAYERED | User32.WS_EX_TOOLWINDOW | User32.WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected void UpdateLayeredWindow(Bitmap bitmap, byte opacity = 255)
        {
            if (bitmap == null)
                return;

            IntPtr screenDc = User32.GetDC(IntPtr.Zero);
            IntPtr memDc = Gdi32.CreateCompatibleDC(screenDc);
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr hOldBitmap = IntPtr.Zero;

            try
            {
                hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
                hOldBitmap = Gdi32.SelectObject(memDc, hBitmap);

                User32.SIZE size = new User32.SIZE(bitmap.Width, bitmap.Height);
                User32.POINT pointSource = new User32.POINT(0, 0);
                User32.POINT topPos = new User32.POINT(Left, Top);
                
                User32.BLENDFUNCTION blend = new User32.BLENDFUNCTION();
                blend.BlendOp = User32.AC_SRC_OVER;
                blend.BlendFlags = 0;
                blend.SourceConstantAlpha = opacity;
                blend.AlphaFormat = User32.AC_SRC_ALPHA;

                User32.UpdateLayeredWindow(Handle, screenDc, ref topPos, ref size, 
                    memDc, ref pointSource, 0, ref blend, User32.ULW_ALPHA);
            }
            finally
            {
                User32.ReleaseDC(IntPtr.Zero, screenDc);
                if (hBitmap != IntPtr.Zero)
                {
                    Gdi32.SelectObject(memDc, hOldBitmap);
                    Gdi32.DeleteObject(hBitmap);
                }
                Gdi32.DeleteDC(memDc);
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Don't paint background
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                bitmap?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
