using System;
using System.Drawing;
using System.Windows.Forms;
using ViStart.NET;

namespace ViStart.NET
{
    // Static manager for global access
    public static class GlobalManager
    {
        public static IStartMenuManager Current { get; set; }
    }

    // Interface defining start menu operations
    public interface IStartMenuManager
    {
        void ShowStartMenu();

        void ShowStartMenu(Point orbLocation);

        void HideStartMenu();
        IconManager Icons { get; }
        Settings Settings { get; }
    }

    // Concrete implementation of the start menu manager
    public class StartMenuManager : IStartMenuManager, IDisposable
    {
        private readonly Settings settings;
        private readonly IconManager iconManager;
        private StartMenuForm startMenu;
        private bool isDisposed;

        public IconManager Icons => iconManager;
        public Settings Settings => settings;

        public StartMenuManager(Settings settings, IconManager iconManager)
        {
            this.settings = settings;
            this.iconManager = iconManager;
            InitializeStartMenu();
        }

        private void InitializeStartMenu()
        {
            startMenu = new StartMenuForm(settings, iconManager);
        }

        public void ShowStartMenu(Point orbLocation)
        {
            if (startMenu != null && !startMenu.Visible)
            {
                // Calculate menu position based on orb location
                orbLocation.Offset(0, -startMenu.Height); // Position above the orb
                startMenu.Location = orbLocation;
                startMenu.Show();
                startMenu.Activate();
            }
        }

        public void HideStartMenu()
        {
            if (startMenu != null && startMenu.Visible)
            {
                startMenu.Hide();
            }
        }

        public void Dispose()
        {
            if (!isDisposed)
            {
                startMenu?.Dispose();
                isDisposed = true;
            }
        }

        // Keep a parameterless version for the interface
        public void ShowStartMenu()
        {
            ShowStartMenu(new Point(0, Screen.PrimaryScreen.WorkingArea.Height - 100));
        }
    }
}