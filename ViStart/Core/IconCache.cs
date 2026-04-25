using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;

namespace ViStart.Core
{
    public static class IconCache
    {
        private static readonly Dictionary<string, Icon> cache = new Dictionary<string, Icon>();
        private const int MAX_CACHE_SIZE = 200;

        private static Icon largeFolderIcon;
        private static Icon smallFolderIcon;

        public static Icon GetIcon(string path, bool largeIcon = true)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            string cacheKey = path + (largeIcon ? "_L" : "_S");

            if (cache.ContainsKey(cacheKey))
                return cache[cacheKey];

            Icon icon = ExtractIcon(path, largeIcon);
            
            if (icon != null)
            {
                // Limit cache size
                if (cache.Count >= MAX_CACHE_SIZE)
                {
                    // Remove oldest entries (simple FIFO for now)
                    var enumerator = cache.GetEnumerator();
                    enumerator.MoveNext();
                    cache.Remove(enumerator.Current.Key);
                }

                cache[cacheKey] = icon;
            }

            return icon;
        }

        private static Icon ExtractIcon(string path, bool largeIcon)
        {
            try
            {
                if (File.Exists(path))
                {
                    // Ask the shell for the size we will actually draw, so the icon
                    // isn't stretched. Icon.ExtractAssociatedIcon always returns 32x32.
                    Icon shellIcon = ShellIconForPath(path, largeIcon);
                    if (shellIcon != null)
                        return shellIcon;

                    return Icon.ExtractAssociatedIcon(path);
                }
                else if (path.StartsWith("shell:"))
                {
                    return GetFolderIcon(largeIcon);
                }
                else
                {
                    int commaIndex = path.LastIndexOf(',');
                    if (commaIndex > 0)
                    {
                        string filePath = path.Substring(0, commaIndex);
                        int index;
                        if (int.TryParse(path.Substring(commaIndex + 1), out index))
                        {
                            return ExtractIconFromFile(filePath, index, largeIcon);
                        }
                    }
                }
            }
            catch
            {
            }

            return GetDefaultIcon();
        }

        private static Icon ShellIconForPath(string path, bool largeIcon)
        {
            var info = new Native.Shell32.SHFILEINFO();
            uint flags = Native.Shell32.SHGFI_ICON
                       | (largeIcon ? Native.Shell32.SHGFI_LARGEICON : Native.Shell32.SHGFI_SMALLICON);

            Native.Shell32.SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf(info), flags);

            if (info.hIcon == IntPtr.Zero)
                return null;

            Icon clone = (Icon)Icon.FromHandle(info.hIcon).Clone();
            Native.User32.DestroyIcon(info.hIcon);
            return clone;
        }

        private static Icon ExtractIconFromFile(string file, int index, bool largeIcon)
        {
            try
            {
                IntPtr hIcon = IntPtr.Zero;
                
                if (largeIcon)
                {
                    Native.Shell32.ExtractIconEx(file, index, out hIcon, IntPtr.Zero, 1);
                }
                else
                {
                    Native.Shell32.ExtractIconEx(file, index, IntPtr.Zero, out hIcon, 1);
                }

                if (hIcon != IntPtr.Zero)
                {
                    Icon icon = Icon.FromHandle(hIcon);
                    return (Icon)icon.Clone(); // Clone to avoid handle issues
                }
            }
            catch
            {
                // Fall through
            }

            return null;
        }

        public static Icon GetFolderIcon(bool largeIcon = true)
        {
            if (largeIcon && largeFolderIcon != null)
                return largeFolderIcon;
            if (!largeIcon && smallFolderIcon != null)
                return smallFolderIcon;

            // Match VB6: query the actual %windir% folder so we get whatever
            // shell icon Windows uses for a real directory (incl. theme overlays).
            Icon icon = ShellIconForPath(Environment.GetFolderPath(Environment.SpecialFolder.Windows), largeIcon);

            if (icon != null)
            {
                if (largeIcon)
                    largeFolderIcon = icon;
                else
                    smallFolderIcon = icon;
            }

            return icon;
        }

        private static Icon GetDefaultIcon()
        {
            // Return a default application icon
            try
            {
                return Icon.ExtractAssociatedIcon(
                    System.Reflection.Assembly.GetExecutingAssembly().Location);
            }
            catch
            {
                return null;
            }
        }

        public static void Clear()
        {
            foreach (var icon in cache.Values)
            {
                icon?.Dispose();
            }
            cache.Clear();
        }
    }
}
