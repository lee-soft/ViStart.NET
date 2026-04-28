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

        // Recent files (jumplist) attribution is keyed off the resolved exe — for a
        // .lnk that's the link target, for an .exe it's itself. Cached because the
        // resolution touches WScript.Shell COM and we hit this on every render.
        private string _resolvedExePath;
        private bool _resolvedExeChecked;

        public string GetResolvedExePath()
        {
            if (!_resolvedExeChecked)
            {
                _resolvedExePath = Core.ShortcutResolver.ResolveProgramExe(Path);
                _resolvedExeChecked = true;
            }
            return _resolvedExePath;
        }

        public bool HasJumpList()
        {
            string key = GetJumpListKey();
            return !string.IsNullOrEmpty(key) && Core.RecentFilesProvider.HasRecentFiles(key);
        }

        // Identifier passed to RecentFilesProvider. For Win32 exes / .lnk shortcuts
        // it's the resolved target exe (so handler-exe attribution can match). For
        // AppX pins (shell:AppsFolder\X!App) we can't resolve a Win32 exe, but the
        // provider's fallback can match the package family name out of the path,
        // so pass it through unchanged.
        public string GetJumpListKey()
        {
            string resolved = GetResolvedExePath();
            if (!string.IsNullOrEmpty(resolved)) return resolved;
            if (!string.IsNullOrEmpty(Path)
                && Path.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
                return Path;
            return null;
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
                else if (Path.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
                {
                    // Store apps and other virtual shell items can't be Process.Start'd
                    // directly — explorer.exe is the canonical launcher for shell paths.
                    System.Diagnostics.Process.Start("explorer.exe", Path);
                    IncrementOpenCount();
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
