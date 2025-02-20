using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ViStart.NET;

namespace ViStart.NET
{
    public class StartMenuContext : ApplicationContext, IStartMenuManager
    {
        private readonly Settings settings;
        private readonly IconManager iconManager;
        private StartMenuForm startMenu;
        private StartOrb startOrb;

        public IconManager Icons => iconManager;
        public Settings Settings => settings;

        public StartMenuContext(Settings settings)
        {
            this.settings = settings;
            this.iconManager = new IconManager();
            InitializeComponents();

            // Register this instance
            GlobalManager.Current = this;
        }

        private void InitializeComponents()
        {
            // Initialize system integration
            SystemIntegration.Initialize();

            // Create start menu and orb
            startMenu = new StartMenuForm(settings, iconManager);
            startOrb = new StartOrb(this);

            // Load the orb image from settings
            if (!string.IsNullOrEmpty(settings.OrbPath))
            {
                startOrb.LoadOrbImage(settings.OrbPath);
            }
            else
            {
                // Load default orb image from resources
                string defaultOrbPath = Path.Combine(settings.ResourcePath, "start_button.png");
                if (File.Exists(defaultOrbPath))
                {
                    startOrb.LoadOrbImage(defaultOrbPath);
                }
                else
                {
                    throw new FileNotFoundException("Could not find start button image");
                }
            }

            // Position the start orb
            PositionStartOrb();
        }

        private void PositionStartOrb()
        {
            // TODO: Position the start orb based on taskbar location
            startOrb.Location = new System.Drawing.Point(0,
                Screen.PrimaryScreen.WorkingArea.Bottom - startOrb.Height);

            startOrb.Show();
        }

        public void ShowStartMenu()
        {
            if (!startMenu.Visible)
            {
                startMenu.Show();
            }
        }

        public void HideStartMenu()
        {
            if (startMenu.Visible)
            {
                startMenu.Hide();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Cleanup
                SystemIntegration.Cleanup();
                startOrb?.Dispose();
                startMenu?.Dispose();
                GlobalManager.Current = null;
            }
            base.Dispose(disposing);
        }

        public void ShowStartMenu(Point orbLocation)
        {
            if (startMenu != null && !startMenu.Visible)
            {
                startMenu.Show(orbLocation);
                startMenu.Activate();
            }
        }
    }
}