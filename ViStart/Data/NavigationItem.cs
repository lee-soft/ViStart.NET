using System;
using System.Diagnostics;
using System.Drawing;

namespace ViStart.Data
{
    public class NavigationItem
    {
        public string Caption { get; set; }
        public string Command { get; set; }
        public string Arguments { get; set; }
        public string IconPath { get; set; }
        public string Rollover { get; set; }
        public bool HasSubmenu { get; set; }
        public bool Visible { get; set; }

        public NavigationItem()
        {
            Visible = true;
        }

        public NavigationItem(string caption, string command, string arguments = null, string rollover = null, bool hasSubmenu = false)
        {
            Caption = caption;
            Command = command;
            Arguments = arguments;
            Rollover = rollover;
            HasSubmenu = hasSubmenu;
            Visible = true;
        }

        public Icon GetIcon()
        {
            if (!string.IsNullOrEmpty(IconPath))
                return Core.IconCache.GetIcon(IconPath);
            return null;
        }

        public void Execute()
        {
            try
            {
                if (Command.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
                {
                    Process.Start("explorer.exe", Command);
                    return;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = Command,
                    Arguments = Arguments ?? string.Empty,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch
            {
                // Swallow launch failures (e.g. helpctr.exe missing on Win7+);
                // there's no good way to report from a click handler.
            }
        }
    }
}
