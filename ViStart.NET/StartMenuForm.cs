using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ScrollBar;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;
using ViStart.NET.Properties;
using System.IO;

namespace ViStart.NET
{
    public partial class StartMenuForm : Form
    {
        private const int WS_EX_LAYERED = 0x80000;
        private const int WS_EX_TOOLWINDOW = 0x80;

        // Dependencies
        private readonly Settings settings;
        private readonly IconManager iconManager;
        private readonly LayoutManager layoutManager;

        // Child controls
/*        private SearchBox searchBox;
        private ProgramList programList;
        private NavigationPane navigationPane;*/
        private StartMenuUserPicture userPicture;

        // UI Resources
        private Image backgroundImage;
        private Image backgroundMask;
        private Image userFrame;
        private Image shutdownButton;
        private Image arrowButton;
        private Image allProgramsImage;
        private Image programsArrow;

        // State tracking
        private bool isProgramListVisible;
        private bool isSearchActive;
        private bool isJumpListMode;
        private bool mouseButtonDown;
        private Point lastMousePosition;

        public StartMenuForm(Settings settings, IconManager iconManager)
        {
            this.settings = settings;
            this.iconManager = iconManager;
            this.layoutManager = new LayoutManager();

            // Set window styles
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;

            InitializeComponent();
            InitializeStartMenu();
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

        private void InitializeStartMenu()
        {
            // Load layout
            LoadLayout();

            // Create child controls
            CreateProgramList();
            CreateSearchBox();
            CreateNavigationPane();
            CreateUserPicture();

            // Load resources
            LoadResources();
        }

        private void LoadLayout()
        {
            string layoutPath = Path.Combine(
                settings.ResourcePath,
                "layout.xml");

            if (!File.Exists(layoutPath))
            {
                throw new FileNotFoundException(
                    "Start menu layout file not found",
                    layoutPath);
            }

            layoutManager.LoadFromFile(layoutPath);
        }

        private void LoadResources()
        {
            string resourcePath = settings.ResourcePath;

            // Load all required images
            backgroundImage = Image.FromFile(
                Path.Combine(resourcePath, "startmenu.png"));
            backgroundMask = Image.FromFile(
                Path.Combine(resourcePath, "startmenu_mask.bmp"));
            userFrame = Image.FromFile(
                Path.Combine(resourcePath, "userframe.png"));
            shutdownButton = Image.FromFile(
                Path.Combine(resourcePath, "bottombuttons_shutdown.png"));
            arrowButton = Image.FromFile(
                Path.Combine(resourcePath, "bottombuttons_arrow.png"));
            allProgramsImage = Image.FromFile(
                Path.Combine(resourcePath, "allprograms.png"));
            programsArrow = Image.FromFile(
                Path.Combine(resourcePath, "programs_arrow.png"));

            // Set form size based on background
            Size = backgroundImage.Size;
        }

        private void CreateProgramList()
        {
            var element = layoutManager.ProgramMenu;
            if (element == null) return;

/*            programList = new ProgramList(settings, iconManager);
            programList.Location = element.Location;
            programList.Size = element.Size;
            Controls.Add(programList);*/
        }

        private void CreateSearchBox()
        {
            var element = layoutManager.SearchBox;
            if (element == null) return;
/*
            searchBox = new SearchBox();
            searchBox.Location = element.Location;
            searchBox.Size = element.Size;
            searchBox.TextChanged += SearchBox_TextChanged;
            Controls.Add(searchBox);*/
        }

        private void CreateNavigationPane()
        {
            var element = layoutManager.GroupOptions;
            if (element == null) return;

/*            navigationPane = new NavigationPane(settings);
            navigationPane.Location = element.Location;
            navigationPane.Size = element.Size;
            Controls.Add(navigationPane);*/
        }

        private void CreateUserPicture()
        {
            if (!settings.ShowUserPicture) return;

            var element = layoutManager.RolloverPlaceholder;
            if (element == null) return;

            userPicture = new StartMenuUserPicture();
            userPicture.Location = element.Location;
            userPicture.Size = element.Size;
            Controls.Add(userPicture);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            mouseButtonDown = true;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            mouseButtonDown = false;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            lastMousePosition = e.Location;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            lastMousePosition = Point.Empty;
            Invalidate();
        }

        private void SearchBox_TextChanged(object sender, EventArgs e)
        {
/*            if (programList != null)
            {
                programList.Filter = searchBox.Text;
            }
            isSearchActive = !string.IsNullOrEmpty(searchBox.Text);*/
            Invalidate();
        }

        private void StartMenuForm_Load(object sender, EventArgs e)
        {

        }

        public void Show(Point orbLocation)
        {
            // Calculate position relative to orb
            int x = orbLocation.X - (Width / 2); // Center horizontally on orb
            int y = orbLocation.Y - Height; // Position above orb

            // Ensure menu stays within screen bounds
            x = Math.Max(0, Math.Min(x, Screen.PrimaryScreen.WorkingArea.Width - Width));
            y = Math.Max(0, Math.Min(y, Screen.PrimaryScreen.WorkingArea.Height - Height));

            this.Location = new Point(x, y);
            base.Show();
            this.Activate();
        }

        // Add this method to properly draw the layered window
        private void UpdateLayeredWindow()
        {
            if (backgroundImage == null) return;

            using (Bitmap bmp = new Bitmap(Width, Height))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                // Draw background
                g.DrawImage(backgroundImage, 0, 0, Width, Height);

                // Draw shutdown button if exists
                if (shutdownButton != null)
                {
                    var element = layoutManager.ShutdownButton;
                    if (element?.Visible == true)
                    {
                        g.DrawImage(shutdownButton, element.Location);
                    }
                }

                // Other UI elements...

                // Update the layered window
                IntPtr screenDc = Win32.GetDC(IntPtr.Zero);
                IntPtr memDc = Win32.CreateCompatibleDC(screenDc);
                IntPtr hBitmap = bmp.GetHbitmap(Color.FromArgb(0));
                IntPtr oldBitmap = Win32.SelectObject(memDc, hBitmap);

                Win32.Size size = new Win32.Size(bmp.Width, bmp.Height);
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
        }

        private void OnPaint(object sender, PaintEventArgs e)
        {
            UpdateLayeredWindow();
        }

        private void OnShown(object sender, EventArgs e)
        {
            UpdateLayeredWindow();
        }
    }
}
