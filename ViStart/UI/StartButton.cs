using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using ViStart.Core;
using ViStart.Native;

namespace ViStart.UI
{
    public class StartButton : LayeredWindow
    {
        private Image orbImage;
        private Rectangle[] snapRects; // Normal, hover, pressed, pressed-hover states
        private int currentSnap = 0;
        private bool isPressed = false;
        private StartMenu startMenu;
        private IntPtr taskbarHandle;
        private IntPtr startButtonHandle;

        public StartButton()
        {
            InitializeComponent();
            LoadOrb();
            PositionButton();
            
            startMenu = new StartMenu(this);
        }

        private void InitializeComponent()
        {
            TopMost = true;
            
            this.MouseEnter += StartButton_MouseEnter;
            this.MouseLeave += StartButton_MouseLeave;
            this.MouseDown += StartButton_MouseDown;
            this.MouseUp += StartButton_MouseUp;
            this.Click += StartButton_Click;
        }

        private void LoadOrb()
        {
            try
            {
                string orbPath = AppSettings.Instance.GetOrbPath();
                
                if (!string.IsNullOrEmpty(orbPath) && System.IO.File.Exists(orbPath))
                {
                    orbImage = Image.FromFile(orbPath);
                    
                    // Skin orbs ship 3 vertical states: normal, hover, pressed
                    int stateHeight = orbImage.Height / 3;
                    int width = orbImage.Width;

                    snapRects = new Rectangle[3];
                    for (int i = 0; i < 3; i++)
                    {
                        snapRects[i] = new Rectangle(0, i * stateHeight, width, stateHeight);
                    }

                    this.Width = width;
                    this.Height = stateHeight;
                }
                else
                {
                    // Create a simple default button
                    this.Width = 54;
                    this.Height = 54;
                    CreateDefaultOrb();
                }

                DrawCurrentState();
            }
            catch
            {
                // Fallback to default
                this.Width = 54;
                this.Height = 54;
                CreateDefaultOrb();
            }
        }

        private void CreateDefaultOrb()
        {
            // Fallback orb: 3 vertical states (normal, hover, pressed) matching the skin contract
            orbImage = new Bitmap(54, 162);
            using (Graphics g = Graphics.FromImage(orbImage))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                    new Rectangle(0, 0, 54, 54),
                    Color.FromArgb(100, 150, 220),
                    Color.FromArgb(50, 100, 180),
                    90f))
                {
                    g.FillEllipse(brush, 2, 2, 50, 50);
                }

                using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                    new Rectangle(0, 54, 54, 54),
                    Color.FromArgb(120, 170, 240),
                    Color.FromArgb(70, 120, 200),
                    90f))
                {
                    g.FillEllipse(brush, 2, 56, 50, 50);
                }

                using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                    new Rectangle(0, 108, 54, 54),
                    Color.FromArgb(70, 120, 190),
                    Color.FromArgb(40, 80, 150),
                    90f))
                {
                    g.FillEllipse(brush, 2, 110, 50, 50);
                }
            }

            snapRects = new Rectangle[3];
            for (int i = 0; i < 3; i++)
            {
                snapRects[i] = new Rectangle(0, i * 54, 54, 54);
            }
        }

        private void PositionButton()
        {
            taskbarHandle = User32.FindWindow("Shell_TrayWnd", null);
            
            if (taskbarHandle != IntPtr.Zero)
            {
                startButtonHandle = User32.FindWindowEx(taskbarHandle, IntPtr.Zero, "Button", "Start");
                
                if (startButtonHandle != IntPtr.Zero)
                {
                    User32.RECT rect;
                    if (User32.GetWindowRect(startButtonHandle, out rect))
                    {
                        this.Left = rect.Left;
                        this.Top = rect.Top;
                        return;
                    }
                }
            }

            // Fallback position (bottom-left corner)
            this.Left = 0;
            this.Top = Screen.PrimaryScreen.WorkingArea.Bottom - this.Height;
        }

        private void DrawCurrentState()
        {
            if (orbImage == null || snapRects == null)
                return;

            bitmap?.Dispose();
            bitmap = new Bitmap(this.Width, this.Height, PixelFormat.Format32bppArgb);
            
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.DrawImage(orbImage, new Rectangle(0, 0, this.Width, this.Height), 
                    snapRects[currentSnap], GraphicsUnit.Pixel);
            }

            UpdateLayeredWindow(bitmap);
        }

        private void StartButton_MouseEnter(object sender, EventArgs e)
        {
            currentSnap = isPressed ? 2 : 1; // Pressed stays pressed; otherwise hover
            DrawCurrentState();
        }

        private void StartButton_MouseLeave(object sender, EventArgs e)
        {
            currentSnap = isPressed ? 2 : 0; // Pressed stays pressed; otherwise normal
            DrawCurrentState();
        }

        private void StartButton_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isPressed = true;
                currentSnap = 2; // Pressed
                DrawCurrentState();
            }
            else if (e.Button == MouseButtons.Right)
            {
                ShowExitMenu(this.PointToScreen(e.Location));
            }
        }

        private void StartButton_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isPressed = false;
                currentSnap = ClientRectangle.Contains(PointToClient(MousePosition)) ? 1 : 0;
                DrawCurrentState();
            }
        }

        private void StartButton_Click(object sender, EventArgs e)
        {
            ToggleStartMenu();
        }

        public void ToggleStartMenu()
        {
            if (startMenu.Visible)
            {
                startMenu.Hide();
                isPressed = false;
                currentSnap = 0;
            }
            else
            {
                startMenu.Show();
                isPressed = true;
                currentSnap = 2;
            }
            DrawCurrentState();
        }

        private void ShowExitMenu(Point screenPoint)
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Exit ViStart", null, (s, a) => Program.Exit());
            menu.Show(screenPoint);
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_MOUSEACTIVATE = 0x21;
            const int MA_NOACTIVATE = 3;

            if (m.Msg == WM_MOUSEACTIVATE)
            {
                m.Result = (IntPtr)MA_NOACTIVATE;
                return;
            }

            base.WndProc(ref m);
        }
    }
}
