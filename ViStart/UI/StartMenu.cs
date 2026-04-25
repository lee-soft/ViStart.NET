using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using ViStart.Core;
using ViStart.Native;
using ViStart.Data;

namespace ViStart.UI
{
    public class StartMenu : LayeredWindow
    {
        private StartButton parentButton;
        private Image backgroundImage;
        private Timer fadeTimer;
        private int fadeOpacity = 0;
        private bool isFadingIn = false;

        private FrequentProgramsPanel frequentPanel;
        private ProgramMenuPanel programPanel;
        private SearchBox searchBox;
        private PowerButton shutdownButton;
        private PowerButton arrowButton;
        private JumpListPanel jumpListPanel;
        private AllProgramsButton allProgramsButton;

        // Pixels above the bg image reserved for the user picture (which can sit above the menu).
        private int topOffset;

        // The image currently shown in the user-picture frame: either userframe.png
        // (no nav item hovered) or one of the Rollover\*.png variants for the hovered item.
        private Image currentUserPictureImage;
        private Image userFrameImage;

        private MouseHook mouseHook;

        public StartMenu(StartButton parent)
        {
            parentButton = parent;
            InitializeComponent();
            LoadTheme();
            CreateControls();
            PositionMenu();

            mouseHook = new MouseHook();
            mouseHook.LeftButtonDown += MouseHook_OutsideClick;
            mouseHook.RightButtonDown += MouseHook_OutsideClick;
        }

        private void MouseHook_OutsideClick(object sender, MouseHookEventArgs e)
        {
            if (!Visible) return;

            // WH_MOUSE_LL's lParam coords are physical pixels. For a DPI-Unaware app on
            // a scaled display those don't match Form.Top/Left (which are logical).
            // Cursor.Position calls GetCursorPos, which Windows virtualises into logical
            // coords for unaware apps — so it always matches Form rects regardless of DPI.
            var clickPoint = Cursor.Position;

            var menuRect = new Rectangle(this.Left, this.Top, this.Width, this.Height);
            if (menuRect.Contains(clickPoint)) return;

            var orbRect = new Rectangle(parentButton.Left, parentButton.Top,
                parentButton.Width, parentButton.Height);
            if (orbRect.Contains(clickPoint)) return;

            Hide();
        }

        private void InitializeComponent()
        {
            TopMost = true;
            
            fadeTimer = new Timer();
            fadeTimer.Interval = AppSettings.Instance.FadeAnimationSpeed;
            fadeTimer.Tick += FadeTimer_Tick;

            this.Deactivate += StartMenu_Deactivate;
            this.MouseMove += StartMenu_MouseMove;
            this.MouseLeave += StartMenu_MouseLeave;
            this.MouseDown += StartMenu_MouseDown;
            this.MouseUp += StartMenu_MouseUp;
            this.MouseWheel += StartMenu_MouseWheel;
        }





        private void LoadTheme()
        {
            try
            {
                backgroundImage = ThemeManager.GetImage("startmenu.png");
                userFrameImage = ThemeManager.GetImage("userframe.png");
                currentUserPictureImage = userFrameImage;

                int bgWidth = backgroundImage?.Width ?? 510;
                int bgHeight = backgroundImage?.Height ?? 520;

                // The user picture can sit above the bg (rolloverplaceholder.Y is negative
                // in most VB6 skins). Pad the layered window upward so it fits within our bitmap.
                var userPicTheme = ThemeManager.RolloverPlaceholder;
                topOffset = Math.Max(0, -userPicTheme.Y);

                this.Width = bgWidth;
                this.Height = bgHeight + topOffset;

                if (backgroundImage == null)
                    CreateDefaultBackground();
            }
            catch
            {
                this.Width = 510;
                this.Height = 520;
                topOffset = 0;
                CreateDefaultBackground();
            }
        }

        private void CreateDefaultBackground()
        {
            backgroundImage = new Bitmap(this.Width, this.Height, PixelFormat.Format32bppArgb);
            
            using (Graphics g = Graphics.FromImage(backgroundImage))
            {
                using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                    new Rectangle(0, 0, this.Width, this.Height),
                    Color.FromArgb(240, 240, 255),
                    Color.FromArgb(200, 210, 230),
                    90f))
                {
                    g.FillRectangle(brush, 0, 0, this.Width, this.Height);
                }

                using (var pen = new Pen(Color.FromArgb(100, 120, 150), 2))
                {
                    g.DrawRectangle(pen, 0, 0, this.Width - 1, this.Height - 1);
                }
            }
        }

        private void CreateControls()
        {
            var theme = ThemeManager.FrequentProgramsMenu;
            frequentPanel = new FrequentProgramsPanel();
            frequentPanel.Bounds = new Rectangle(theme.X, theme.Y + topOffset, theme.Width, theme.Height);
            frequentPanel.Visible = !AppSettings.Instance.ShowProgramsFirst;

            theme = ThemeManager.ProgramMenu;
            programPanel = new ProgramMenuPanel();
            programPanel.Bounds = new Rectangle(theme.X, theme.Y + topOffset, theme.Width, theme.Height);
            programPanel.Visible = AppSettings.Instance.ShowProgramsFirst;

            // SearchBox is hosted in a separate Form that uses screen coordinates.
            // Its Bounds are populated in Show() once we know our final screen position.
            searchBox = new SearchBox();
            searchBox.SearchTextChanged += SearchBox_SearchTextChanged;

            theme = ThemeManager.ShutdownButton;
            if (theme.Visible)
            {
                shutdownButton = new PowerButton(PowerButton.PowerAction.Shutdown);
                shutdownButton.Bounds = new Rectangle(theme.X, theme.Y + topOffset, 71, 24);
            }

            theme = ThemeManager.ArrowButton;
            if (theme.Visible)
            {
                arrowButton = new PowerButton(PowerButton.PowerAction.ShowMenu);
                arrowButton.Bounds = new Rectangle(theme.X, theme.Y + topOffset, 24, 24);
            }

            // The nav pane (VB6 NavigationPane) is positioned by `groupoptions`, not
            // `jumplist_viewer` (jumplist_viewer is a separate expanded-mode element).
            theme = ThemeManager.GroupOptions;
            jumpListPanel = new JumpListPanel();
            jumpListPanel.Bounds = new Rectangle(theme.X, theme.Y + topOffset, theme.Width, theme.Height);
            jumpListPanel.HoveredItemChanged += JumpListPanel_HoveredItemChanged;

            // Align text Bounds.Y/Height to the arrow image so the text vertically
            // centres on the same midline as the arrow.
            var textTheme = ThemeManager.AllProgramsText;
            var arrowTheme = ThemeManager.AllProgramsArrow;
            int arrowH = ThemeManager.GetImage("programs_arrow.png")?.Height / 2 ?? 18;
            allProgramsButton = new AllProgramsButton();
            allProgramsButton.TopOffset = topOffset;
            allProgramsButton.Bounds = new Rectangle(textTheme.X, arrowTheme.Y + topOffset, 200, arrowH);
        }

        private void JumpListPanel_HoveredItemChanged(object sender, HoveredNavigationItemChangedEventArgs e)
        {
            Image newImage = userFrameImage;
            if (e.Item != null && !string.IsNullOrEmpty(e.Item.Rollover))
            {
                var rollover = ThemeManager.GetImage("Rollover\\" + e.Item.Rollover);
                if (rollover != null)
                    newImage = rollover;
            }

            if (!ReferenceEquals(newImage, currentUserPictureImage))
            {
                currentUserPictureImage = newImage;
                RenderMenu();
            }
        }

        private void PositionMenu()
        {
            this.Left = parentButton.Left;
            this.Top = parentButton.Top - this.Height;

            if (this.Top < 0)
                this.Top = 0;
        }

        public new void Show()
        {
            if (Visible)
                return;

            base.Show();

            isFadingIn = true;
            fadeOpacity = 0;
            RenderMenu();
            fadeTimer.Start();
            mouseHook.Install();

            try
            {
                var theme = ThemeManager.SearchBox;
                searchBox.Visible = true;
                searchBox.Bounds = new Rectangle(
                    this.Left + theme.X,
                    this.Top + theme.Y + topOffset,
                    theme.Width,
                    theme.Height);
                searchBox.UpdatePosition();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("SearchBox error: " + ex.Message);
            }
        }

        public new void Hide()
        {
            mouseHook.Uninstall();
            fadeTimer.Stop();
            searchBox.Visible = false;
            searchBox.UpdatePosition(); // This will hide it
            base.Hide();
            fadeOpacity = 0;
            searchBox.Clear();
        }

        private void RenderMenu()
        {
            bitmap?.Dispose();
            bitmap = new Bitmap(this.Width, this.Height, PixelFormat.Format32bppArgb);

            using (Graphics g = Graphics.FromImage(bitmap))
            {
                if (backgroundImage != null)
                    g.DrawImage(backgroundImage, 0, topOffset);

                DrawUserPicture(g);

                if (allProgramsButton != null)
                    allProgramsButton.Render(g);

                if (frequentPanel != null && frequentPanel.Visible)
                    frequentPanel.Render(g);

                if (programPanel != null && programPanel.Visible)
                    programPanel.Render(g);

                if (searchBox != null)
                    searchBox.Render(g);

                if (shutdownButton != null)
                {
                    shutdownButton.Render(g);
                    DrawShutdownText(g);
                }

                if (arrowButton != null)
                    arrowButton.Render(g);

                if (jumpListPanel != null)
                    jumpListPanel.Render(g);
            }

            UpdateLayeredWindow(bitmap, (byte)fadeOpacity);
        }

        private void DrawUserPicture(Graphics g)
        {
            if (currentUserPictureImage == null)
                return;

            var theme = ThemeManager.RolloverPlaceholder;
            int x = theme.X;
            int y = theme.Y + topOffset; // y becomes 0 when rolloverplaceholder.Y == -topOffset

            g.DrawImage(currentUserPictureImage, x, y,
                currentUserPictureImage.Width, currentUserPictureImage.Height);
        }

        private void DrawShutdownText(Graphics g)
        {
            var theme = ThemeManager.ShutdownText;
            if (!theme.Visible) return;

            using (var font = new Font("Segoe UI", 9f))
            using (var brush = new SolidBrush(Color.White))
            {
                g.DrawString("Shut down", font, brush, theme.X, theme.Y + topOffset);
            }
        }

        private void FadeTimer_Tick(object sender, EventArgs e)
        {
            if (isFadingIn)
            {
                fadeOpacity += 25;
                if (fadeOpacity >= 255)
                {
                    fadeOpacity = 255;
                    fadeTimer.Stop();
                }
            }
            else
            {
                fadeOpacity -= 25;
                if (fadeOpacity <= 0)
                {
                    fadeOpacity = 0;
                    fadeTimer.Stop();
                    base.Hide();
                }
            }

            UpdateLayeredWindow(bitmap, (byte)fadeOpacity);
        }

        private void StartMenu_MouseMove(object sender, MouseEventArgs e)
        {
            bool needsRedraw = false;

            if (allProgramsButton != null)
            {
                if (allProgramsButton.HitTest(e.Location))
                    allProgramsButton.OnMouseEnter();
                else
                    allProgramsButton.OnMouseLeave();
                needsRedraw = true;
            }

            if (frequentPanel != null && frequentPanel.Visible)
            {
                frequentPanel.OnMouseMove(e.Location);
                needsRedraw = true;
            }

            if (programPanel != null && programPanel.Visible)
            {
                programPanel.OnMouseMove(e.Location);
                needsRedraw = true;
            }

            if (jumpListPanel != null)
            {
                jumpListPanel.OnMouseMove(e.Location);
                needsRedraw = true;
            }

            if (shutdownButton != null)
            {
                if (shutdownButton.HitTest(e.Location))
                    shutdownButton.OnMouseEnter();
                else
                    shutdownButton.OnMouseLeave();
                needsRedraw = true;
            }

            if (arrowButton != null)
            {
                if (arrowButton.HitTest(e.Location))
                    arrowButton.OnMouseEnter();
                else
                    arrowButton.OnMouseLeave();
                needsRedraw = true;
            }

            if (needsRedraw)
                RenderMenu();
        }

        private void StartMenu_MouseLeave(object sender, EventArgs e)
        {
            allProgramsButton?.OnMouseLeave();
            frequentPanel?.OnMouseLeave();
            programPanel?.OnMouseLeave();
            jumpListPanel?.OnMouseLeave();
            shutdownButton?.OnMouseLeave();
            arrowButton?.OnMouseLeave();
            RenderMenu();
        }

        private void StartMenu_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (searchBox != null && searchBox.HitTest(e.Location))
                {
                    searchBox.Focus();
                    return;
                }

                if (allProgramsButton != null && allProgramsButton.HitTest(e.Location))
                {
                    allProgramsButton.Toggle();

                    if (allProgramsButton.ShowingAllPrograms)
                    {
                        frequentPanel.Visible = false;
                        programPanel.Visible = true;
                        searchBox.Clear();
                    }
                    else
                    {
                        frequentPanel.Visible = true;
                        programPanel.Visible = false;
                    }

                    RenderMenu();
                    return;
                }

                if (shutdownButton != null && shutdownButton.HitTest(e.Location))
                {
                    shutdownButton.OnMouseDown();
                    RenderMenu();
                    return;
                }

                if (arrowButton != null && arrowButton.HitTest(e.Location))
                {
                    arrowButton.OnMouseDown();
                    RenderMenu();
                    return;
                }

                if (frequentPanel != null && frequentPanel.Visible)
                {
                    var program = frequentPanel.OnMouseClick(e.Location, e.Button);
                    if (program != null)
                    {
                        program.Launch();
                        AppSettings.Instance.Programs.UpdateProgramUsage(program.Path);
                        Hide();
                        return;
                    }
                }

                // Gate on HitTest — otherwise OnMouseClick swallows clicks that
                // fell outside its bounds (e.g. on the jumplist) and the early
                // return below prevents downstream panels from seeing them.
                if (programPanel != null && programPanel.Visible && programPanel.HitTest(e.Location))
                {
                    var node = programPanel.OnMouseClick(e.Location, e.Button);

                    if (node == null)
                    {
                        // Either a folder was toggled or scrollbar consumed the click.
                        RenderMenu();
                        return;
                    }

                    if (!node.IsFolder)
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(node.Path);
                            AppSettings.Instance.Programs.UpdateProgramUsage(node.Path);
                            Hide();
                        }
                        catch { }
                        return;
                    }
                }

                if (jumpListPanel != null && jumpListPanel.OnMouseDown(e.Location))
                {
                    RenderMenu();
                    return;
                }
            }
            else if (e.Button == MouseButtons.Right)
            {
                if (frequentPanel != null && frequentPanel.Visible)
                {
                    var program = frequentPanel.OnMouseClick(e.Location, e.Button);
                    if (program != null)
                    {
                        var menu = frequentPanel.GetContextMenu(program);
                        menu.Show(this.PointToScreen(e.Location));
                        return;
                    }
                }

                if (programPanel != null && programPanel.Visible && programPanel.HitTest(e.Location))
                {
                    var program = programPanel.OnMouseClick(e.Location, e.Button);
                    if (program != null)
                    {
                        var menu = programPanel.GetContextMenu(program);
                        menu.Show(this.PointToScreen(e.Location));
                        return;
                    }
                }
            }
        }

        private void StartMenu_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (programPanel != null && programPanel.Visible)
                {
                    programPanel.OnMouseUp();
                    RenderMenu();
                }

                if (jumpListPanel != null)
                {
                    var navItem = jumpListPanel.OnMouseUp(e.Location);
                    if (navItem != null)
                    {
                        RenderMenu(); // clear the pressed frame before launch/hide
                        navItem.Execute();
                        Hide();
                        return;
                    }
                    RenderMenu();
                }

                if (shutdownButton != null && shutdownButton.HitTest(e.Location))
                {
                    shutdownButton.OnMouseUp();
                    ShutdownButton_Click();
                    return;
                }

                if (arrowButton != null && arrowButton.HitTest(e.Location))
                {
                    arrowButton.OnMouseUp();
                    ArrowButton_Click(e.Location);
                    return;
                }
            }
        }

        private void StartMenu_MouseWheel(object sender, MouseEventArgs e)
        {
            if (programPanel != null && programPanel.Visible && programPanel.HitTest(e.Location))
            {
                programPanel.OnMouseWheel(e.Delta);
                RenderMenu();
            }
        }

        private void SearchBox_SearchTextChanged(object sender, SearchTextChangedEventArgs e)
        {
            bool inAllPrograms = allProgramsButton != null && allProgramsButton.ShowingAllPrograms;

            if (string.IsNullOrWhiteSpace(e.SearchText))
            {
                // Empty search box: if we're in All Programs view we stay there (with
                // the unfiltered list). Otherwise show the frequent-programs panel.
                // Without this guard, clicking All Programs flips back immediately
                // because searchBox.Clear() fires this handler 200ms later.
                if (inAllPrograms)
                {
                    programPanel.ShowSearchResults(string.Empty);
                }
                else
                {
                    frequentPanel.Visible = true;
                    programPanel.Visible = false;
                }
            }
            else
            {
                frequentPanel.Visible = false;
                programPanel.Visible = true;
                programPanel.ShowSearchResults(e.SearchText);
            }
            RenderMenu();
        }

        private void ShutdownButton_Click()
        {
            if (MessageBox.Show("Are you sure you want to shut down?", "Shut Down Windows",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                NativeMethods.EnableShutdownPrivilege();
                NativeMethods.ExitWindowsEx(
                    NativeMethods.EWX_SHUTDOWN | NativeMethods.EWX_POWEROFF, 0);
            }
        }

        private void ArrowButton_Click(Point location)
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Log Off", null, (s, args) => LogOff());
            menu.Items.Add("Restart", null, (s, args) => Restart());
            menu.Items.Add("Shut Down", null, (s, args) => ShutdownButton_Click());
            menu.Show(this.PointToScreen(location));
        }

        private void LogOff()
        {
            NativeMethods.ExitWindowsEx(NativeMethods.EWX_LOGOFF, 0);
        }

        private void Restart()
        {
            if (MessageBox.Show("Are you sure you want to restart?", "Restart Windows",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                NativeMethods.EnableShutdownPrivilege();
                NativeMethods.ExitWindowsEx(NativeMethods.EWX_REBOOT, 0);
            }
        }

        private void StartMenu_Deactivate(object sender, EventArgs e)
        {
            // Intentionally empty: WS_EX_NOACTIVATE makes Deactivate fire spuriously
            // right after Show. Outside-click dismissal is handled by MouseHook instead.
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
