using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.IO;

namespace ViStart.NET
{
    /// <summary>
    /// Manages file and folder icons with caching for efficiency
    /// </summary>
    public class IconManager : IDisposable
    {
        // Cache icons to avoid repeatedly extracting them
        private Dictionary<string, Image> iconCache = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, Image> folderIconCache = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);

        // Default icons
        private Image defaultFileIcon;
        private Image defaultFolderIcon;
        private Image defaultFolderOpenIcon;

        // Shell32 API constants
        private const uint SHGFI_ICON = 0x100;
        private const uint SHGFI_LARGEICON = 0x0;
        private const uint SHGFI_SMALLICON = 0x1;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x10;
        private const uint SHGFI_LINKOVERLAY = 0x8000;
        private const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;

        [StructLayout(LayoutKind.Sequential)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public IntPtr iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("shell32.dll")]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public IconManager()
        {
            // Initialize default icons
            InitializeDefaultIcons();
        }

        private void InitializeDefaultIcons()
        {
            // Get default file icon
            defaultFileIcon = GetIconFromShell("file.txt", false, false);

            // Get default folder icons
            defaultFolderIcon = GetIconFromShell("folder", false, true);
            defaultFolderOpenIcon = GetIconFromShell("folder", true, true);
        }

        /// <summary>
        /// Gets an icon for a file
        /// </summary>
        public Image GetFileIcon(string filePath, bool large)
        {
            string cacheKey = $"{filePath}|{large}";

            if (iconCache.TryGetValue(cacheKey, out Image cachedIcon))
            {
                return cachedIcon;
            }

            Image icon = null;

            try
            {
                // For .lnk files, try to get the target's icon
                if (Path.GetExtension(filePath).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    icon = GetLinkTargetIcon(filePath, large);
                }

                // If we couldn't get the target icon, get the file's icon directly
                if (icon == null)
                {
                    icon = GetIconFromShell(filePath, false, false);
                }

                if (icon == null)
                {
                    icon = defaultFileIcon;
                }

                // Cache the icon
                iconCache[cacheKey] = icon;
                return icon;
            }
            catch
            {
                return defaultFileIcon;
            }
        }

        /// <summary>
        /// Gets an icon for a folder
        /// </summary>
        public Image GetFolderIcon(string folderPath, bool open)
        {
            string cacheKey = $"{folderPath}|{open}";

            if (folderIconCache.TryGetValue(cacheKey, out Image cachedIcon))
            {
                return cachedIcon;
            }

            Image icon;

            try
            {
                icon = GetIconFromShell(folderPath, open, true);

                if (icon == null)
                {
                    icon = open ? defaultFolderOpenIcon : defaultFolderIcon;
                }

                // Cache the icon
                folderIconCache[cacheKey] = icon;
                return icon;
            }
            catch
            {
                return open ? defaultFolderOpenIcon : defaultFolderIcon;
            }
        }

        /// <summary>
        /// Gets an icon for a shortcut's target
        /// </summary>
        private Image GetLinkTargetIcon(string linkPath, bool large)
        {
            try
            {
                // Use Windows Script Host to resolve the shortcut
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                {
                    dynamic shell = Activator.CreateInstance(shellType);
                    dynamic shortcut = shell.CreateShortcut(linkPath);
                    string targetPath = shortcut.TargetPath;

                    if (!string.IsNullOrEmpty(targetPath))
                    {
                        // Get the target's icon
                        if (Directory.Exists(targetPath))
                        {
                            return GetFolderIcon(targetPath, false);
                        }
                        else if (File.Exists(targetPath))
                        {
                            // See if it has a custom icon
                            string iconLocation = shortcut.IconLocation;
                            if (!string.IsNullOrEmpty(iconLocation))
                            {
                                string[] parts = iconLocation.Split(',');
                                string iconPath = parts[0];
                                int iconIndex = parts.Length > 1 ? int.Parse(parts[1]) : 0;

                                return ExtractIconFromFile(iconPath, iconIndex, large);
                            }
                            else
                            {
                                return GetIconFromShell(targetPath, false, false);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting shortcut target icon: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Gets an icon from the shell
        /// </summary>
        private Image GetIconFromShell(string path, bool open, bool isFolder)
        {
            SHFILEINFO info = new SHFILEINFO();
            uint flags = SHGFI_ICON | SHGFI_SMALLICON;

            if (isFolder)
            {
                flags |= SHGFI_USEFILEATTRIBUTES;
                if (open)
                {
                    // For open folders, would use SHGFI_OPENICON but it's not always reliable
                }
            }

            uint fileAttributes = isFolder ? FILE_ATTRIBUTE_DIRECTORY : 0;

            IntPtr result = SHGetFileInfo(path, fileAttributes, ref info, (uint)Marshal.SizeOf(info), flags);

            if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                // Convert the icon handle to an Image
                return Icon.FromHandle(info.hIcon).ToBitmap();
            }
            finally
            {
                // Clean up
                DestroyIcon(info.hIcon);
            }
        }

        /// <summary>
        /// Extracts an icon from a file (exe, dll, ico)
        /// </summary>
        private Image ExtractIconFromFile(string path, int index, bool large)
        {
            try
            {
                if (path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
                {
                    // Direct icon file
                    return new Icon(path).ToBitmap();
                }
                else
                {
                    // Extract from EXE/DLL
                    SHFILEINFO info = new SHFILEINFO();
                    uint flags = SHGFI_ICON | (large ? SHGFI_LARGEICON : SHGFI_SMALLICON);

                    IntPtr result = SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf(info), flags);

                    if (result != IntPtr.Zero && info.hIcon != IntPtr.Zero)
                    {
                        try
                        {
                            return Icon.FromHandle(info.hIcon).ToBitmap();
                        }
                        finally
                        {
                            DestroyIcon(info.hIcon);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error extracting icon: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Clean up resources
        /// </summary>
        public void Dispose()
        {
            // Dispose cached icons
            foreach (var icon in iconCache.Values)
            {
                icon?.Dispose();
            }
            iconCache.Clear();

            foreach (var icon in folderIconCache.Values)
            {
                icon?.Dispose();
            }
            folderIconCache.Clear();

            // Dispose default icons
            defaultFileIcon?.Dispose();
            defaultFolderIcon?.Dispose();
            defaultFolderOpenIcon?.Dispose();
        }
    }
}