using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ViStart.Core
{
    /// <summary>
    /// Enumerates the Win10/11 "All Applications" virtual shell folder (shell:AppsFolder),
    /// which is where Microsoft Store apps and modern packaged apps like Paint and Notepad
    /// live. The classic %ProgramData%\Microsoft\Windows\Start Menu\Programs tree no longer
    /// contains entries for these on modern Windows, so without this enumeration they are
    /// invisible to ViStart.
    /// </summary>
    public static class AppsFolderIndexer
    {
        public class AppEntry
        {
            public string Name;
            // Parsing path of the form "shell:AppsFolder\<AppUserModelID>". Both the launch
            // logic (Process.Start via explorer.exe) and the icon extractor recognise this.
            public string Path;
        }

        // PIDLs captured from the live FolderItems during enumeration, keyed by Path.
        // Re-binding through SHCreateItemFromIDList gives us the same shell object the
        // user sees in Explorer, which yields proper Win11 UWP icons (Calculator, Notepad,
        // Camera, ...) — the parsing-name path doesn't reconstruct that binding.
        // PIDLs live for the process lifetime; they're allocated from the shell allocator
        // and reclaimed automatically on exit.
        private static readonly Dictionary<string, IntPtr> _pidlCache =
            new Dictionary<string, IntPtr>(StringComparer.OrdinalIgnoreCase);

        public static IntPtr GetCachedPidl(string path)
        {
            IntPtr pidl;
            return _pidlCache.TryGetValue(path, out pidl) ? pidl : IntPtr.Zero;
        }

        public static List<AppEntry> Enumerate()
        {
            var entries = new List<AppEntry>();

            object shell = null;
            object folder = null;
            object items = null;

            try
            {
                Type shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType == null)
                    return entries;

                shell = Activator.CreateInstance(shellType);

                // GUID is the well-known KNOWNFOLDERID of AppsFolder.
                folder = shellType.InvokeMember("NameSpace",
                    BindingFlags.InvokeMethod, null, shell,
                    new object[] { "shell:::{4234d49b-0245-4df3-b780-3893943456e1}" });

                if (folder == null)
                    return entries;

                items = folder.GetType().InvokeMember("Items",
                    BindingFlags.InvokeMethod, null, folder, null);

                int count = (int)items.GetType().InvokeMember("Count",
                    BindingFlags.GetProperty, null, items, null);

                for (int i = 0; i < count; i++)
                {
                    object item = null;
                    try
                    {
                        item = items.GetType().InvokeMember("Item",
                            BindingFlags.InvokeMethod, null, items, new object[] { i });

                        if (item == null)
                            continue;

                        string name = item.GetType().InvokeMember("Name",
                            BindingFlags.GetProperty, null, item, null) as string;

                        // FolderItem.Path on a virtual AppsFolder entry returns the
                        // AppUserModelID (e.g. "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App").
                        // For classic Win32 entries it returns a real .lnk/.exe path.
                        string path = item.GetType().InvokeMember("Path",
                            BindingFlags.GetProperty, null, item, null) as string;

                        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(path))
                            continue;

                        // Normalise: turn an AUMID into the parsing-name form so launch
                        // and icon extraction can route it through the shell uniformly.
                        if (!path.StartsWith("shell:", StringComparison.OrdinalIgnoreCase)
                            && !System.IO.File.Exists(path))
                        {
                            path = "shell:AppsFolder\\" + path;
                        }

                        // Capture the live FolderItem's absolute PIDL so the icon path can
                        // re-bind to the same shell object (vs. recreating one from text).
                        try
                        {
                            IntPtr pidl;
                            Native.Shell32.SHGetIDListFromObject(item, out pidl);
                            if (pidl != IntPtr.Zero && !_pidlCache.ContainsKey(path))
                                _pidlCache[path] = pidl;
                        }
                        catch
                        {
                            // PIDL capture is best-effort — icon extraction has a parsing-name
                            // fallback, so an item without a cached PIDL still works (just with
                            // the lower-fidelity icon for things like Win11 first-party UWP apps).
                        }

                        entries.Add(new AppEntry { Name = name, Path = path });
                    }
                    catch
                    {
                        // Skip individual items that fail to enumerate; the rest are still useful.
                    }
                    finally
                    {
                        if (item != null && Marshal.IsComObject(item))
                            Marshal.ReleaseComObject(item);
                    }
                }
            }
            catch
            {
                // Whole enumeration failed (e.g. on XP, where shell:AppsFolder doesn't exist).
                // Caller falls back to filesystem-only indexing — that's the correct behaviour.
            }
            finally
            {
                if (items != null && Marshal.IsComObject(items))
                    Marshal.ReleaseComObject(items);
                if (folder != null && Marshal.IsComObject(folder))
                    Marshal.ReleaseComObject(folder);
                if (shell != null && Marshal.IsComObject(shell))
                    Marshal.ReleaseComObject(shell);
            }

            return entries;
        }
    }
}
