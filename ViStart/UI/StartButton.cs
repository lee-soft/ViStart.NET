using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
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
            // Source of truth for the orb's visual state is the menu's actual visibility.
            // Subscribing to VisibleChanged keeps the orb in sync regardless of who
            // shows/hides the menu (toggle, outside-click, jumplist file launch).
            startMenu.VisibleChanged += (s, e) => SyncToMenuVisibility();
        }

        // Picks the correct snap rect for the orb given (a) whether the menu is open
        // and (b) whether the cursor is over the orb. Called whenever either changes.
        private void SyncToMenuVisibility()
        {
            bool open = startMenu != null && startMenu.Visible;
            isPressed = open;
            if (open)
                currentSnap = 2;
            else
                currentSnap = ClientRectangle.Contains(PointToClient(MousePosition)) ? 1 : 0;
            DrawCurrentState();
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

        // Read the menu visibility directly, not the local isPressed flag — that flag
        // gets toggled mid-click sequence (MouseUp clears it before Click runs) and
        // would briefly flicker the orb out of pressed state while the menu is open.
        private bool MenuOpen { get { return startMenu != null && startMenu.Visible; } }

        private void StartButton_MouseEnter(object sender, EventArgs e)
        {
            currentSnap = MenuOpen ? 2 : 1;
            DrawCurrentState();
        }

        private void StartButton_MouseLeave(object sender, EventArgs e)
        {
            currentSnap = MenuOpen ? 2 : 0;
            DrawCurrentState();
        }

        private void StartButton_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                currentSnap = 2;
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
                currentSnap = MenuOpen ? 2 :
                    (ClientRectangle.Contains(PointToClient(MousePosition)) ? 1 : 0);
                DrawCurrentState();
            }
        }

        private void StartButton_Click(object sender, EventArgs e)
        {
            ToggleStartMenu();
        }

        public void ToggleStartMenu()
        {
            // Just flip the menu — VisibleChanged repaints the orb to match.
            if (startMenu.Visible)
                startMenu.Hide();
            else
                startMenu.Show();
        }

        private void ShowExitMenu(Point screenPoint)
        {
            var menu = new ContextMenuStrip();

            var skinsMenu = new ToolStripMenuItem(LanguageManager.T("menu.skins", "Skins"));
            skinsMenu.DropDownItems.Add(LanguageManager.T("menu.default", "Default"), null, (s, a) =>
            {
                AppSettings.Instance.CurrentSkin = string.Empty;
                ApplyAppearanceChanges();
            });

            foreach (string skinName in GetAvailableSkins())
            {
                skinsMenu.DropDownItems.Add(skinName, null, (s, a) =>
                {
                    AppSettings.Instance.CurrentSkin = skinName;
                    ApplyAppearanceChanges();
                });
            }

            var orbsMenu = new ToolStripMenuItem(LanguageManager.T("menu.orbs", "Orbs"));
            orbsMenu.DropDownItems.Add(LanguageManager.T("menu.default", "Default"), null, (s, a) =>
            {
                AppSettings.Instance.CurrentOrb = string.Empty;
                ApplyAppearanceChanges();
            });

            foreach (string orbFile in GetAvailableOrbs())
            {
                orbsMenu.DropDownItems.Add(orbFile, null, (s, a) =>
                {
                    AppSettings.Instance.CurrentOrb = orbFile;
                    ApplyAppearanceChanges();
                });
            }

            menu.Items.Add(skinsMenu);
            menu.Items.Add(orbsMenu);
            var languageMenu = new ToolStripMenuItem(LanguageManager.T("menu.languages", "Language"));
            foreach (string lang in LanguageManager.GetAvailableLanguages())
            {
                languageMenu.DropDownItems.Add(lang, null, (s, a) =>
                {
                    AppSettings.Instance.CurrentLanguage = lang;
                    ApplyLanguageChanges();
                });
            }

            menu.Items.Add(languageMenu);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(LanguageManager.T("menu.exit", "Exit ViStart"), null, (s, a) => Program.Exit());
            menu.Show(screenPoint);
        }

        private IEnumerable<string> GetAvailableSkins()
        {
            var skins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string appDataSkins = Path.Combine(AppSettings.AppDataPath, "_skins");
            string localSkins = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Skins");
            string localSkinsLower = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "skins");

            foreach (string path in new[] { appDataSkins, localSkins, localSkinsLower })
            {
                if (!Directory.Exists(path))
                    continue;

                foreach (string dir in Directory.GetDirectories(path))
                {
                    skins.Add(Path.GetFileName(dir));
                }
            }

            return skins.OrderBy(s => s);
        }

        private IEnumerable<string> GetAvailableOrbs()
        {
            var orbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string appDataOrbs = Path.Combine(AppSettings.AppDataPath, "_orbs");
            string localOrbs = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Orbs");
            string localOrbsLower = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "orbs");

            foreach (string path in new[] { appDataOrbs, localOrbs, localOrbsLower })
            {
                if (!Directory.Exists(path))
                    continue;

                foreach (string file in Directory.GetFiles(path, "*.png", SearchOption.TopDirectoryOnly))
                {
                    orbs.Add(Path.GetFileName(file));
                }
            }

            return orbs.OrderBy(o => o);
        }

        private void ApplyLanguageChanges()
        {
            AppSettings.Save();
            LanguageManager.Initialize();
            ApplyAppearanceChanges();
        }

        private void ApplyAppearanceChanges()
        {
            AppSettings.Save();
            ThemeManager.Reload();
            LoadOrb();

            if (startMenu != null)
            {
                bool wasVisible = startMenu.Visible;
                startMenu.Hide();
                startMenu.Dispose();
                startMenu = new StartMenu(this);
                if (wasVisible)
                {
                    startMenu.Show();
                }
            }
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
