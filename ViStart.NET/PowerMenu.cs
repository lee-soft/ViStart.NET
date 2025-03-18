using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ViStart.NET
{
    // Power menu implementation
    public class PowerMenu : IDisposable
    {
        private readonly ContextMenuStrip menu;
        private readonly Form parentForm;
        private readonly Settings settings;

        public Action<object, string> CommandSelected { get; internal set; }

        public PowerMenu(Settings settings, Form parentForm)
        {
            this.settings = settings;
            this.parentForm = parentForm;

            menu = new ContextMenuStrip();
            menu.Renderer = new CustomMenuRenderer();

            // Initialize the power menu
            InitializeMenu();

            // Fix first-click issue
            PreInitialize();
        }

        private void InitializeMenu()
        {
            // Add standard power menu items
            AddMenuItem("Log Off", "LOGOFF");
            AddMenuItem("Switch User", "rundll32.exe user32.dll, LockWorkStation");
            AddSeparator();
            AddMenuItem("Restart", "REBOOT");
            AddSeparator();
            AddMenuItem("Shut down", "SHUTDOWN");
            AddSeparator();
            AddMenuItem("Stand By", "STANDBY");
            AddMenuItem("Hibernate", "HIBERNATE");
            AddSeparator();
            AddMenuItem("Options", "OPTIONS");
            AddMenuItem("About", "ABOUT");
            AddMenuItem("Exit", "EXIT");
        }

        private void AddMenuItem(string text, string command)
        {
            var item = new ToolStripMenuItem(text);
            item.Tag = command;
            item.Click += (s, e) =>
            {
                CommandSelected?.Invoke(this, (s as ToolStripMenuItem)?.Tag as string);
                menu.Close();
            };
            menu.Items.Add(item);
        }

        private void AddSeparator()
        {
            menu.Items.Add(new ToolStripSeparator());
        }

        public void Show(Point location, Form owner)
        {
            // Convert to screen coordinates
            Point screenPoint = owner.PointToScreen(location);

            // Adjust position to show above the button (like Windows Start menu)
            screenPoint.Y -= menu.Height;

            // Show menu
            menu.Show(screenPoint);
        }

        private void PreInitialize()
        {
            // Create a dummy menu item to initialize internal structures
            var tempItem = new ToolStripMenuItem("Initializing...");
            menu.Items.Add(tempItem);

            // Show and immediately hide the menu off-screen
            Point offScreen = new Point(-1000, -1000);
            menu.Show(offScreen);
            menu.Close();

            // Remove the temp item
            menu.Items.Remove(tempItem);
        }

        public void Dispose()
        {
            menu?.Dispose();
        }
    }

}
