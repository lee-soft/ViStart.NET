using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.IO;

namespace ViStart.NET
{
    public class IconManager : IDisposable
    {
        private Dictionary<string, Icon> iconCache;
        private bool isDisposed;

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr ExtractIconEx(string szFileName, int nIconIndex,
            out IntPtr phiconLarge, out IntPtr phiconSmall, int nIcons);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public IconManager()
        {
            iconCache = new Dictionary<string, Icon>(StringComparer.OrdinalIgnoreCase);
        }

        public Icon GetIcon(string path, bool large = true)
        {
            if (string.IsNullOrEmpty(path)) return null;

            string cacheKey = $"{path}|{large}";
            if (iconCache.TryGetValue(cacheKey, out Icon cachedIcon))
            {
                return cachedIcon;
            }

            Icon icon = ExtractIconFromFile(path, large);
            if (icon != null)
            {
                iconCache[cacheKey] = icon;
            }
            return icon;
        }

        private Icon ExtractIconFromFile(string path, bool large)
        {
            if (!File.Exists(path)) return null;

            IntPtr large_icons, small_icons;
            try
            {
                // Extract the icon
                int readIcons = (int)ExtractIconEx(path, 0, out large_icons, out small_icons, 1);

                if (readIcons > 0)
                {
                    IntPtr handle = large ? large_icons : small_icons;
                    Icon icon = Icon.FromHandle(handle);

                    // Create a copy we can safely cache
                    Icon clone = (Icon)icon.Clone();

                    // Clean up
                    icon.Dispose();
                    if (large_icons != IntPtr.Zero) DestroyIcon(large_icons);
                    if (small_icons != IntPtr.Zero) DestroyIcon(small_icons);

                    return clone;
                }
            }
            catch
            {
                // Fallback: try to get icon from file info
                try
                {
                    Icon icon = Icon.ExtractAssociatedIcon(path);
                    return (Icon)icon?.Clone();
                }
                catch
                {
                    // Return null if all methods fail
                    return null;
                }
            }
            return null;
        }

        public void ClearCache()
        {
            foreach (var icon in iconCache.Values)
            {
                icon.Dispose();
            }
            iconCache.Clear();
        }

        public void Dispose()
        {
            if (!isDisposed)
            {
                ClearCache();
                isDisposed = true;
            }
        }
    }
}