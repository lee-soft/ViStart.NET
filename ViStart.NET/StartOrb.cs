using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using ViStart.NET;

namespace ViStart.NET
{
    public partial class StartOrb : Form
    {
        private const int WS_EX_LAYERED = 0x80000;
        private const int WS_EX_TOOLWINDOW = 0x80;

        private readonly IStartMenuManager startMenuManager;
        private Image orbImage;
        private bool mouseDown;
        private int currentState; // 0=normal, 1=hover, 2=pressed
        private string imagePath;

        public StartOrb(IStartMenuManager manager)
        {
            startMenuManager = manager;
            InitializeComponent();
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED | WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        public void LoadOrbImage(string path)
        {
            imagePath = path;
            orbImage?.Dispose();
            orbImage = Image.FromFile(path);

            // Orb image is vertical strip with 3 states (normal/hover/pressed)
            Size = new Size(orbImage.Width, orbImage.Height / 3);
            UpdateOrbDisplay();
        }

        private void UpdateOrbDisplay()
        {
            if (orbImage == null) return;

            using (Bitmap bmp = new Bitmap(Width, Height))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                // Draw the current state from the image strip
                Rectangle sourceRect = new Rectangle(
                    0,
                    currentState * (orbImage.Height / 3),
                    orbImage.Width,
                    orbImage.Height / 3
                );

                g.DrawImage(orbImage, ClientRectangle, sourceRect, GraphicsUnit.Pixel);

                // Update the form with the new image
                SetBitmap(bmp);
            }
        }

        private void SetBitmap(Bitmap bitmap)
        {
            if (bitmap.PixelFormat != PixelFormat.Format32bppArgb)
            {
                throw new ApplicationException("The bitmap must be 32bpp with alpha channel.");
            }

            IntPtr screenDc = Win32.GetDC(IntPtr.Zero);
            IntPtr memDc = Win32.CreateCompatibleDC(screenDc);
            IntPtr hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
            IntPtr oldBitmap = Win32.SelectObject(memDc, hBitmap);

            Win32.Size size = new Win32.Size(bitmap.Width, bitmap.Height);
            Win32.Point pointSource = new Win32.Point(0, 0);
            Win32.Point topPos = new Win32.Point(Left, Top);
            Win32.BLENDFUNCTION blend = new Win32.BLENDFUNCTION
            {
                BlendOp = Win32.AC_SRC_OVER,
                BlendFlags = 0,
                SourceConstantAlpha = 255,
                AlphaFormat = Win32.AC_SRC_ALPHA
            };

            Win32.UpdateLayeredWindow(
                Handle, screenDc, ref topPos, ref size,
                memDc, ref pointSource, 0, ref blend, Win32.ULW_ALPHA);

            Win32.SelectObject(memDc, oldBitmap);
            Win32.DeleteObject(hBitmap);
            Win32.DeleteDC(memDc);
            Win32.ReleaseDC(IntPtr.Zero, screenDc);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                mouseDown = true;
                currentState = 2; // Pressed
                UpdateOrbDisplay();

                var pos = this.Location;
                pos.Offset(this.Width / 2, 0); // Center horizontally
                startMenuManager.ShowStartMenu(pos);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left)
            {
                mouseDown = false;
                currentState = 0;
                UpdateOrbDisplay();
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            if (!mouseDown)
            {
                currentState = 1; // Hover
                UpdateOrbDisplay();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (!mouseDown)
            {
                currentState = 0; // Normal
                UpdateOrbDisplay();
            }
        }

        private void StartOrb_Load(object sender, EventArgs e)
        {

        }

        private void StartOrb_Click(object sender, EventArgs e)
        {

        }
    }

    // Win32 APIs needed for layered window
    internal static class Win32
    {
        public const byte AC_SRC_OVER = 0;
        public const byte AC_SRC_ALPHA = 1;
        public const int ULW_ALPHA = 2;

        [StructLayout(LayoutKind.Sequential)]
        public struct Point
        {
            public int x;
            public int y;
            public Point(int x, int y) { this.x = x; this.y = y; }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Size
        {
            public int cx;
            public int cy;
            public Size(int cx, int cy) { this.cx = cx; this.cy = cy; }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr CreateCompatibleDC(IntPtr hDC);

        [DllImport("gdi32.dll", ExactSpelling = true)]
        public static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        public static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst,
            ref Point pptDst, ref Size psize, IntPtr hdcSrc, ref Point pptSrc,
            int crKey, ref BLENDFUNCTION pblend, int dwFlags);
    }
}