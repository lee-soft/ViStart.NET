using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
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
                // IShellItemImageFactory is the modern shell API for icons. It works for
                // filesystem paths, shortcuts (returning the target's icon, no overlay
                // arrow — which is what the original ViStart wants), AND virtual items
                // like Microsoft Store apps under shell:AppsFolder. Try it first for
                // anything that's a real path or a shell parsing name.
                if (File.Exists(path)
                    || path.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
                {
                    Icon shellItemIcon = IconFromShellItem(path, largeIcon);
                    if (shellItemIcon != null)
                        return shellItemIcon;
                }

                if (File.Exists(path))
                {
                    // Fallback: SHGetFileInfo by path, then the always-32x32 framework helper.
                    Icon shellIcon = ShellIconForPath(path, largeIcon);
                    if (shellIcon != null)
                        return shellIcon;

                    return Icon.ExtractAssociatedIcon(path);
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

        // Resolve to an IShellItem and use IShellItemImageFactory. The factory returns a
        // Bitmap at the exact requested size with the alpha channel intact, so the result
        // composites correctly over the layered Start menu background.
        //
        // For shell:AppsFolder\<AUMID> entries we prefer the PIDL captured during the
        // AppsFolder enumeration (SHCreateItemFromIDList): that re-binds the same shell
        // object the user sees in Explorer, which carries the real UWP icon binding for
        // first-party Win11 apps (Calculator, Notepad, Camera). Going through
        // SHCreateItemFromParsingName instead reconstructs a thinner item with only the
        // generic "file" icon for those.
        private static Icon IconFromShellItem(string parsingName, bool largeIcon)
        {
            int size = largeIcon ? 32 : 16;
            Guid IID_IShellItem = new Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe");

            Native.Shell32.IShellItem item = null;

            IntPtr cachedPidl = AppsFolderIndexer.GetCachedPidl(parsingName);
            if (cachedPidl != IntPtr.Zero)
            {
                try
                {
                    Native.Shell32.SHCreateItemFromIDList(cachedPidl, ref IID_IShellItem, out item);
                }
                catch
                {
                    item = null;
                }
            }

            if (item == null)
            {
                try
                {
                    int hr = Native.Shell32.SHCreateItemFromParsingName(
                        parsingName, IntPtr.Zero, ref IID_IShellItem, out item);
                    if (hr != 0)
                        item = null;
                }
                catch
                {
                    item = null;
                }
            }

            if (item == null)
                return null;

            try
            {
                var factory = item as Native.Shell32.IShellItemImageFactory;
                if (factory == null)
                    return null;

                IntPtr hbm;
                try
                {
                    factory.GetImage(
                        new Native.Shell32.SIZE { cx = size, cy = size },
                        Native.Shell32.SIIGBF_ICONONLY,
                        out hbm);
                }
                catch
                {
                    return null;
                }

                if (hbm == IntPtr.Zero)
                    return null;

                try
                {
                    using (Bitmap bmp = BitmapFromHBitmapPreservingAlpha(hbm))
                    {
                        if (bmp == null)
                            return null;

                        IntPtr hIcon = bmp.GetHicon();
                        Icon clone = (Icon)Icon.FromHandle(hIcon).Clone();
                        Native.User32.DestroyIcon(hIcon);
                        return clone;
                    }
                }
                finally
                {
                    Native.Gdi32.DeleteObject(hbm);
                }
            }
            finally
            {
                if (item != null && Marshal.IsComObject(item))
                    Marshal.ReleaseComObject(item);
            }
        }

        // Bitmap.FromHbitmap / Image.FromHbitmap silently drop the alpha channel. The
        // shell hands back a 32bpp ARGB DIB section, so we read its bits directly via
        // GetObject(DIBSECTION) and copy them into a managed Bitmap that owns its memory.
        // Querying DIBSECTION (rather than BITMAP) lets us see the signed biHeight so we
        // can distinguish top-down from bottom-up DIBs — without that, bottom-up icons
        // come out vertically flipped.
        private static Bitmap BitmapFromHBitmapPreservingAlpha(IntPtr hbm)
        {
            var ds = new Native.Gdi32.DIBSECTION();
            if (Native.Gdi32.GetObject(hbm, Marshal.SizeOf(ds), ref ds) == 0)
                return null;

            if (ds.dsBm.bmBitsPixel != 32 || ds.dsBm.bmBits == IntPtr.Zero)
                return null;

            int width = ds.dsBm.bmWidth;
            int height = Math.Abs(ds.dsBm.bmHeight);
            int absStride = ds.dsBm.bmWidthBytes;

            // For a bottom-up DIB (biHeight > 0), bmBits points to the BOTTOM scanline.
            // System.Drawing.Bitmap walks scanlines top-down; we feed it a scan0 that
            // points at the top scanline (last row in memory) plus a negative stride so
            // it walks upward — equivalent to flipping vertically as we copy.
            bool topDown = ds.dsBmih.biHeight < 0;
            IntPtr scan0 = ds.dsBm.bmBits;
            int srcStride = absStride;
            if (!topDown)
            {
                scan0 = new IntPtr(ds.dsBm.bmBits.ToInt64() + (long)(height - 1) * absStride);
                srcStride = -absStride;
            }

            using (var shared = new Bitmap(width, height, srcStride, PixelFormat.Format32bppArgb, scan0))
            {
                var copy = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(copy))
                {
                    g.CompositingMode = CompositingMode.SourceCopy;
                    g.DrawImageUnscaled(shared, 0, 0);
                }
                return copy;
            }
        }

        // Resolve a virtual shell parsing name (e.g. "shell:AppsFolder\<AUMID>") to an
        // icon. SHParseDisplayName produces a PIDL we can pass to SHGetFileInfo with
        // SHGFI_PIDL — the file-path overload can't reach virtual items like Store apps.
        private static Icon ShellIconForParsingName(string parsingName, bool largeIcon)
        {
            // SHParseDisplayName doesn't recognise the "shell:..." prefix syntax (that's a
            // Windows Explorer convenience handled by ShellExecute / SHGetKnownFolderPath).
            // The portable canonical form is the CLSID-rooted parsing name.
            const string AppsFolderClsidRoot = "::{4234d49b-0245-4df3-b780-3893943456e1}\\";
            const string ShellAppsFolderPrefix = "shell:AppsFolder\\";
            if (parsingName.StartsWith(ShellAppsFolderPrefix, StringComparison.OrdinalIgnoreCase))
            {
                parsingName = AppsFolderClsidRoot + parsingName.Substring(ShellAppsFolderPrefix.Length);
            }

            IntPtr pidl;
            uint sfgao;
            int hr = Native.Shell32.SHParseDisplayName(parsingName, IntPtr.Zero, out pidl, 0, out sfgao);
            if (hr != 0 || pidl == IntPtr.Zero)
                return null;

            try
            {
                var info = new Native.Shell32.SHFILEINFO();
                uint flags = Native.Shell32.SHGFI_ICON | Native.Shell32.SHGFI_PIDL
                           | (largeIcon ? Native.Shell32.SHGFI_LARGEICON : Native.Shell32.SHGFI_SMALLICON);

                Native.Shell32.SHGetFileInfo(pidl, 0, ref info, (uint)Marshal.SizeOf(info), flags);

                if (info.hIcon == IntPtr.Zero)
                    return null;

                Icon clone = (Icon)Icon.FromHandle(info.hIcon).Clone();
                Native.User32.DestroyIcon(info.hIcon);
                return clone;
            }
            finally
            {
                Native.Shell32.ILFree(pidl);
            }
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
