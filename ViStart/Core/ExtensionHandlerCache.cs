using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace ViStart.Core
{
    /// <summary>
    /// Resolves a file extension to the executable that "open"s it. Used to map every
    /// recent file the system remembers to the program ViStart should attribute it to
    /// in the jumplist.
    ///
    /// Walks the standard chain:
    ///   HKCR\&lt;.ext&gt;\(default)            -> ProgID
    ///   HKCR\&lt;ProgID&gt;\shell\(default)    -> primary verb (defaults to "open")
    ///   HKCR\&lt;ProgID&gt;\shell\&lt;verb&gt;\command\(default) -> command line; first
    ///     argument is the exe path. Environment variables expanded.
    /// </summary>
    internal static class ExtensionHandlerCache
    {
        private static readonly Dictionary<string, string> _cache =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static string GetHandlerExe(string extension)
        {
            if (string.IsNullOrEmpty(extension)) return null;
            if (extension[0] != '.') extension = "." + extension;

            string cached;
            if (_cache.TryGetValue(extension, out cached))
                return cached;

            string exe = ResolveHandlerExe(extension);
            _cache[extension] = exe; // cache nulls too — failed lookups stay failed
            return exe;
        }

        private static string ResolveHandlerExe(string extension)
        {
            string progId = ReadDefault(Registry.ClassesRoot, extension);
            if (string.IsNullOrEmpty(progId)) return null;

            string verb = ReadDefault(Registry.ClassesRoot, progId + "\\shell");
            if (string.IsNullOrEmpty(verb)) verb = "open";

            string command = ReadDefault(Registry.ClassesRoot,
                progId + "\\shell\\" + verb + "\\command");
            if (string.IsNullOrEmpty(command)) return null;

            command = Environment.ExpandEnvironmentVariables(command);
            return ExtractExePath(command);
        }

        private static string ReadDefault(RegistryKey root, string subKey)
        {
            using (var key = root.OpenSubKey(subKey))
            {
                return key == null ? null : key.GetValue("") as string;
            }
        }

        // Pulls the executable path out of a Windows shell command line. Handles the
        // typical forms:   "C:\Path\app.exe" "%1"
        //                   C:\Path\app.exe %1
        //                   C:\Path\app.exe -arg "%1"
        private static string ExtractExePath(string command)
        {
            command = command.Trim();
            if (command.Length == 0) return null;

            string exe;
            if (command[0] == '"')
            {
                int closing = command.IndexOf('"', 1);
                if (closing <= 1) return null;
                exe = command.Substring(1, closing - 1);
            }
            else
            {
                // First whitespace ends the exe path.
                int space = command.IndexOf(' ');
                exe = space < 0 ? command : command.Substring(0, space);
            }

            try { return Path.GetFullPath(exe); }
            catch { return null; }
        }
    }
}
