using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ViStart.Core
{
    /// <summary>
    /// Late-binds to WScript.Shell to read a .lnk file's TargetPath. Same pattern the
    /// program indexer originally used — extracted so both the indexer and the recent-
    /// files provider can share it.
    /// </summary>
    public static class ShortcutResolver
    {
        public static string ResolveTarget(string lnkPath)
        {
            if (string.IsNullOrEmpty(lnkPath)) return null;
            if (!File.Exists(lnkPath)) return null;

            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return null;

                object shell = Activator.CreateInstance(shellType);
                try
                {
                    object shortcut = shellType.InvokeMember("CreateShortcut",
                        BindingFlags.InvokeMethod, null, shell, new object[] { lnkPath });
                    try
                    {
                        return shortcut.GetType().InvokeMember("TargetPath",
                            BindingFlags.GetProperty, null, shortcut, null) as string;
                    }
                    finally
                    {
                        if (shortcut != null && Marshal.IsComObject(shortcut))
                            Marshal.ReleaseComObject(shortcut);
                    }
                }
                finally
                {
                    if (shell != null && Marshal.IsComObject(shell))
                        Marshal.ReleaseComObject(shell);
                }
            }
            catch
            {
                return null;
            }
        }

        // Returns the executable a ViStart program path ultimately runs. For .lnk files
        // that's the link target; for an .exe it's itself; for shell:AppsFolder items we
        // can't reach a Win32 exe, so return null.
        public static string ResolveProgramExe(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (path.StartsWith("shell:", StringComparison.OrdinalIgnoreCase)) return null;
            if (!File.Exists(path)) return null;

            if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                return ResolveTarget(path);

            return path;
        }
    }
}
