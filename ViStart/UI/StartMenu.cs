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
        private RecentFilesPanel recentFilesPanel;

        // Jumplist "morph" mode: when true, the program list area is replaced with a
        // recent-files list for the program the user clicked the chevron next to.
        // Search box is hidden in this mode and the menu uses startmenu_expanded.png
        // as background if the skin ships one.
        private bool jumpListMode;
        private Image expandedBackgroundImage;

        // Pixels above the bg image reserved for the user picture (which can sit above the menu).
        private int topOffset;

        // The image currently shown in the user-picture frame: either userframe.png
        // (no nav item hovered) or one of the Rollover\*.png variants for the hovered item.
        // userFrameImage is the userframe.png with the user's account picture composited
        // underneath at (11, 11) sized 48x48 — matches VB6 MakeUserRollover positioning.
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
                Image rawFrame = ThemeManager.GetImage("userframe.png");
                // Optional skin asset; null if skin doesn't support jumplist mode (we
                // then fall back to the regular startmenu.png so the feature still works).
                expandedBackgroundImage = ThemeManager.GetImage("startmenu_expanded.png");
                userFrameImage = ComposeUserFrame(rawFrame);
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

        // Build a userframe with the user's account picture composited into the centre
        // hole of the frame, at (11, 11) sized 48x48 — same offset/size VB6 uses in
        // MainHelper.MakeUserRollover. Returns the raw frame unchanged if no user
        // picture is available.
        private Image ComposeUserFrame(Image rawFrame)
        {
            if (rawFrame == null) return null;

            Image userPic = UserPictureLoader.Load();
            if (userPic == null) return rawFrame;

            try
            {
                var composed = new Bitmap(rawFrame.Width, rawFrame.Height,
                    PixelFormat.Format32bppArgb);
                using (Graphics gg = Graphics.FromImage(composed))
                {
                    gg.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    gg.DrawImage(userPic, new Rectangle(11, 11, 48, 48));
                    gg.DrawImage(rawFrame, 0, 0, rawFrame.Width, rawFrame.Height);
                }
                return composed;
            }
            catch
            {
                return rawFrame;
            }
            finally
            {
                userPic.Dispose();
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
            frequentPanel.ProgramsChanged += OnPinnedProgramsChanged;
            frequentPanel.JumpListRequested += EnterJumpListMode;

            theme = ThemeManager.ProgramMenu;
            programPanel = new ProgramMenuPanel();
            programPanel.Bounds = new Rectangle(theme.X, theme.Y + topOffset, theme.Width, theme.Height);
            programPanel.Visible = AppSettings.Instance.ShowProgramsFirst;
            programPanel.ProgramsChanged += OnPinnedProgramsChanged;

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

            // Recent files (jumplist) panel — sized/positioned per the skin's
            // jumplist_viewer schema. Hidden until the user clicks a chevron.
            theme = ThemeManager.JumpListViewer;
            recentFilesPanel = new RecentFilesPanel();
            recentFilesPanel.Bounds = new Rectangle(theme.X, theme.Y + topOffset, theme.Width, theme.Height);
            recentFilesPanel.Visible = false;
            recentFilesPanel.BackRequested += ExitJumpListMode;
            recentFilesPanel.FileSelected += OnJumpListFileSelected;
        }

        private void EnterJumpListMode(Data.ProgramItem program)
        {
            recentFilesPanel.SetProgram(program);
            recentFilesPanel.Visible = true;
            jumpListMode = true;
            // Pinned/frequent panel stays on the left; only the nav pane is replaced
            // by the jumplist (handled in RenderMenu by gating its draw on !jumpListMode).
            // Resize the layered window if the skin ships a wider startmenu_expanded.png.
            if (expandedBackgroundImage != null)
                ApplyBackgroundDimensions(expandedBackgroundImage);
            RenderMenu();
        }

        private void ExitJumpListMode()
        {
            jumpListMode = false;
            recentFilesPanel.Visible = false;
            recentFilesPanel.SetProgram(null);
            if (expandedBackgroundImage != null && backgroundImage != null)
                ApplyBackgroundDimensions(backgroundImage);
            RenderMenu();
        }

        // Resize the layered window to fit the given background image, keeping the
        // menu's bottom edge anchored above the start orb. Also reposition the search
        // box host form, since its bounds are computed off this.Top/this.Left.
        private void ApplyBackgroundDimensions(Image bg)
        {
            if (bg == null) return;

            int newWidth = bg.Width;
            int newHeight = bg.Height + topOffset;

            if (this.Width == newWidth && this.Height == newHeight)
                return;

            this.Width = newWidth;
            this.Height = newHeight;
            this.Top = Math.Max(0, parentButton.Top - newHeight);

            if (searchBox != null)
            {
                var sbTheme = ThemeManager.SearchBox;
                searchBox.Bounds = new Rectangle(
                    this.Left + sbTheme.X,
                    this.Top + sbTheme.Y + topOffset,
                    sbTheme.Width,
                    sbTheme.Height);
                searchBox.UpdatePosition();
            }
        }

        private void OnJumpListFileSelected(string filePath)
        {
            try
            {
                System.Diagnostics.Process.Start(filePath);
            }
            catch { }
            ExitJumpListMode();
            Hide();
        }

        private void OnPinnedProgramsChanged()
        {
            // A pin/unpin/remove from either panel's context menu changed the data.
            // Reload the frequent panel (which renders the pinned list) and repaint.
            frequentPanel?.LoadPrograms();
            RenderMenu();
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

            // Position, show, AND focus the search box NOW — synchronously inside the
            // click handler chain. Windows grants foreground/SetFocus privilege only to
            // the process that just received user input, and only briefly; if we wait
            // until the fade-timer tick (~200ms+ later) the grant has expired and
            // SetForegroundWindow silently fails, leaving the user unable to type.
            // Hide the host visually with Opacity=0 so it doesn't pop in before the
            // start menu finishes fading in; ShowSearchBox restores it afterward.
            try
            {
                var theme = ThemeManager.SearchBox;
                searchBox.Visible = true;
                searchBox.Bounds = new Rectangle(
                    this.Left + theme.X,
                    this.Top + theme.Y + topOffset,
                    theme.Width,
                    theme.Height);
                searchBox.SetOpacity(0);
                searchBox.UpdatePosition();
                searchBox.Focus();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("SearchBox error: " + ex.Message);
            }
        }

        private void ShowSearchBox()
        {
            // Fade-in just completed: reveal the search box that's been sitting
            // invisibly focused since Show() was called.
            searchBox.SetOpacity(1.0);
        }

        public new void Hide()
        {
            mouseHook.Uninstall();
            fadeTimer.Stop();
            // Drop jumplist mode here so the next open starts on the normal view —
            // outside-click dismissal goes through Hide(), bypassing ExitJumpListMode.
            if (jumpListMode)
            {
                jumpListMode = false;
                recentFilesPanel.Visible = false;
                recentFilesPanel.SetProgram(null);
                if (expandedBackgroundImage != null && backgroundImage != null)
                    ApplyBackgroundDimensions(backgroundImage);
            }
            searchBox.Visible = false;
            searchBox.UpdatePosition(); // This will hide it
            fadeOpacity = 0;
            // Render the (now back-to-normal-size) menu at opacity 0 before hiding,
            // so the next Show() doesn't flash the previous frame and the bitmap matches
            // the layered window's current dimensions.
            RenderMenu();
            base.Hide();
            searchBox.Clear();
            // base.Hide() fires VisibleChanged on the form, and StartButton subscribes
            // to that to keep the orb's visual state in sync — no manual sync needed.
        }

        private void RenderMenu()
        {
            bitmap?.Dispose();
            bitmap = new Bitmap(this.Width, this.Height, PixelFormat.Format32bppArgb);

            using (Graphics g = Graphics.FromImage(bitmap))
            {
                // VB6's "morph" jumplist mode swaps the background to startmenu_expanded.png.
                // Skins that don't ship that asset still get the feature with the regular bg.
                Image bg = (jumpListMode && expandedBackgroundImage != null)
                    ? expandedBackgroundImage
                    : backgroundImage;
                if (bg != null)
                    g.DrawImage(bg, 0, topOffset);

                DrawUserPicture(g);

                if (allProgramsButton != null)
                    allProgramsButton.Render(g);

                if (frequentPanel != null && frequentPanel.Visible)
                    frequentPanel.Render(g);

                if (programPanel != null && programPanel.Visible)
                    programPanel.Render(g);

                if (recentFilesPanel != null && recentFilesPanel.Visible)
                    recentFilesPanel.Render(g);

                if (searchBox != null)
                    searchBox.Render(g);

                if (shutdownButton != null)
                {
                    shutdownButton.Render(g);
                    DrawShutdownText(g);
                }

                if (arrowButton != null)
                    arrowButton.Render(g);

                // Suppress the nav pane (Documents/Pictures/…) while the jumplist is
                // showing — it would otherwise paint through underneath the jumplist.
                if (jumpListPanel != null && !jumpListMode)
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

            // Skins ship a separate caption colour for jumplist mode (the expanded
            // background often has a lighter strip near the power button); fall back
            // to white in normal mode where the regular bg is dark.
            Color color = jumpListMode
                ? ThemeManager.ShutdownTextJumpListColor
                : Color.White;

            using (var font = new Font("Segoe UI", 9f))
            using (var brush = new SolidBrush(color))
            {
                g.DrawString(LanguageManager.T("shutdown_text", "Shut down"), font, brush, theme.X, theme.Y + topOffset);
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
                    ShowSearchBox();
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

            if (recentFilesPanel != null && recentFilesPanel.Visible)
            {
                recentFilesPanel.OnMouseMove(e.Location);
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
            recentFilesPanel?.OnMouseLeave();
            jumpListPanel?.OnMouseLeave();
            shutdownButton?.OnMouseLeave();
            arrowButton?.OnMouseLeave();
            RenderMenu();
        }

        private void StartMenu_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                // Recent files panel sits on top of the nav pane area when jumplist
                // mode is on; route clicks to it first so its back-chevron and rows win
                // over anything else that happens to share screen space.
                if (recentFilesPanel != null && recentFilesPanel.Visible
                    && recentFilesPanel.HitTest(e.Location)
                    && recentFilesPanel.OnMouseClick(e.Location, e.Button))
                {
                    return;
                }

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
                        AppSettings.Instance.Programs.UpdateProgramUsage(program.Path, program.Caption);
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
                            if (node.Path.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
                                System.Diagnostics.Process.Start("explorer.exe", node.Path);
                            else
                                System.Diagnostics.Process.Start(node.Path);
                            AppSettings.Instance.Programs.UpdateProgramUsage(node.Path, node.Caption);
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
            if (MessageBox.Show(LanguageManager.T("confirm_shutdown", "Are you sure you want to shut down?"), LanguageManager.T("shutdown_title", "Shut Down Windows"),
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
            menu.Items.Add(LanguageManager.T("log_off", "Log Off"), null, (s, args) => LogOff());
            menu.Items.Add(LanguageManager.T("restart", "Restart"), null, (s, args) => Restart());
            menu.Items.Add(LanguageManager.T("shut_down", "Shut Down"), null, (s, args) => ShutdownButton_Click());
            menu.Show(this.PointToScreen(location));
        }

        private void LogOff()
        {
            NativeMethods.ExitWindowsEx(NativeMethods.EWX_LOGOFF, 0);
        }

        private void Restart()
        {
            if (MessageBox.Show(LanguageManager.T("confirm_restart", "Are you sure you want to restart?"), LanguageManager.T("restart_title", "Restart Windows"),
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
