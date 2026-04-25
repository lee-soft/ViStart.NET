using System;
using System.Drawing;

namespace ViStart.Data
{
    public class ProgramItem
    {
        public string Caption { get; set; }
        public string Path { get; set; }
        public string IconPath { get; set; }
        public int OpenCount { get; set; }
        public bool IsPinned { get; set; }
        
        private Icon cachedIcon;

        public ProgramItem()
        {
            Caption = string.Empty;
            Path = string.Empty;
            IconPath = string.Empty;
            OpenCount = 0;
            IsPinned = false;
        }

        public ProgramItem(string path, string caption = null)
        {
            Path = path;
            Caption = caption ?? System.IO.Path.GetFileNameWithoutExtension(path);
            IconPath = path;
            OpenCount = 0;
            IsPinned = false;
        }

        public void IncrementOpenCount()
        {
            OpenCount++;
        }

        public Icon GetIcon()
        {
            if (cachedIcon == null)
            {
                cachedIcon = Core.IconCache.GetIcon(IconPath ?? Path);
            }
            return cachedIcon;
        }

        public void Launch()
        {
            try
            {
                if (Path.StartsWith("!"))
                {
                    // Special command
                    HandleSpecialCommand();
                }
                else
                {
                    System.Diagnostics.Process.Start(Path);
                    IncrementOpenCount();
                }
            }
            catch
            {
                // Handle error
            }
        }

        private void HandleSpecialCommand()
        {
            switch (Path)
            {
                case "!default_menu":
                    // Show Windows default start menu
                    Native.User32.SendMessage(
                        Native.User32.FindWindow("Shell_TrayWnd", null),
                        0x5555, // WM_SYSCOMMAND equivalent for start menu
                        IntPtr.Zero,
                        IntPtr.Zero);
                    break;
            }
        }
    }
}
