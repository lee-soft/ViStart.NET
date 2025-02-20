using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ViStart.NET
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                var settings = new Settings();
                var mainContext = new StartMenuContext(settings);
                Application.Run(mainContext);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing ViStart: {ex.Message}", "ViStart Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
