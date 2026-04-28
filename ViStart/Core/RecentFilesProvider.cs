using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Win32;

namespace ViStart.Core
{
    /// <summary>
    /// Builds a map from program executables to their recent-files lists, the data
    /// driving Win7-style jumplists shown in the Frequent Programs panel.
    ///
    /// Strategy ports the VB6 ViStart approach: collect every recent file the system
    /// remembers from a few well-known sources, then assign each to a program by
    /// looking up the registered handler for the file's extension. Files where the
    /// handler matches the program land in that program's jumplist.
    ///
    /// Sources, in order of preference:
    ///   * Win7+: %APPDATA%\Microsoft\Windows\Recent\AutomaticDestinations\*.automaticDestinations-ms
    ///     Each is an OLE compound file containing the system's per-app jumplist.
    ///     We don't actually parse the OLE structure — like the VB6 version, we
    ///     scan the raw bytes for null-terminated path-like strings ("X:\..."),
    ///     verifying each candidate via File.Exists. Way simpler than SSCF parsing
    ///     and works fine because the embedded .lnk streams contain plain paths.
    ///
    ///   * XP: HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\RecentDocs\&lt;ext&gt;
    ///     Each subkey is an extension; values reference .lnk shortcut filenames in
    ///     %USERPROFILE%\Recent that we resolve to their targets.
    /// </summary>
    public static class RecentFilesProvider
    {
        private static Dictionary<string, List<string>> _filesByExe;
        private static readonly object _initLock = new object();

        public static IList<string> GetRecentFiles(string exePath)
        {
            EnsureInitialized();
            if (string.IsNullOrEmpty(exePath))
                return EmptyList;

            string key = NormalizeExePath(exePath);
            List<string> list;
            return _filesByExe.TryGetValue(key, out list) ? (IList<string>)list : EmptyList;
        }

        public static bool HasRecentFiles(string exePath)
        {
            return GetRecentFiles(exePath).Count > 0;
        }

        /// <summary>
        /// Returns the system-wide list of recently opened files. Combines three
        /// sources to stay reliable across Windows versions and across the Win11
        /// shift toward Activity-History-driven Recent (which leaves the classic
        /// .lnk folder sparsely populated for modern apps):
        ///
        ///   * CSIDL_RECENT (.lnk shortcuts) — primary on XP/Vista/7/8.
        ///   * AutomaticDestinations jumplist blobs — primary on Win11 because
        ///     Explorer + most modern Win32 apps update these even when they no
        ///     longer write .lnks. Same scrape used by the per-program jumplists.
        ///   * RecentDocs registry — XP fallback when neither folder exists.
        ///
        /// Each candidate carries a timestamp (.lnk mtime or blob mtime) used to
        /// merge-sort the combined list most-recent first. Caller passes a soft cap.
        /// </summary>
        public static IList<string> GetSystemRecentFiles(int max = 30)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var candidates = new List<KeyValuePair<DateTime, string>>();

            // Source A — CSIDL_RECENT. Resolving through SpecialFolder.Recent follows
            // the OS's canonical mapping (it's a junction on modern Windows), so this
            // works whether the real folder is in %USERPROFILE% or %APPDATA%.
            string recentFolder = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
            if (!string.IsNullOrEmpty(recentFolder) && Directory.Exists(recentFolder))
            {
                FileInfo[] lnks;
                try
                {
                    // Resolve a healthy multiple of `max` so we still hit the cap when
                    // the most-recent .lnks point at deleted/virtual targets.
                    lnks = new DirectoryInfo(recentFolder)
                        .GetFiles("*.lnk")
                        .OrderByDescending(f => f.LastWriteTimeUtc)
                        .Take(Math.Max(max * 3, 60))
                        .ToArray();
                }
                catch { lnks = new FileInfo[0]; }

                foreach (var lnk in lnks)
                {
                    string target = ShortcutResolver.ResolveTarget(lnk.FullName);
                    if (string.IsNullOrEmpty(target)) continue;
                    // Files only — Recent also stores folder shortcuts which aren't
                    // useful in this list. File.Exists also rejects stale .lnks.
                    if (!File.Exists(target)) continue;
                    if (!seen.Add(target)) continue;

                    candidates.Add(new KeyValuePair<DateTime, string>(
                        lnk.LastWriteTimeUtc, target));
                }
            }

            // Source B — AutomaticDestinations. On Win11 these are the most reliable
            // signal: Explorer rewrites a blob whenever its app's jumplist updates.
            // ExtractPathsFromBlob already verifies File.Exists per candidate.
            string adFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Microsoft\\Windows\\Recent\\AutomaticDestinations");
            if (Directory.Exists(adFolder))
            {
                FileInfo[] blobs;
                try
                {
                    blobs = new DirectoryInfo(adFolder)
                        .GetFiles("*.automaticDestinations-ms")
                        .OrderByDescending(f => f.LastWriteTimeUtc)
                        .ToArray();
                }
                catch { blobs = new FileInfo[0]; }

                foreach (var blob in blobs)
                {
                    // Stop scanning blobs once we have plenty of candidates — extracting
                    // paths from each blob touches disk and the top-K sort below
                    // narrows down anyway.
                    if (candidates.Count >= max * 3) break;

                    List<string> paths;
                    try { paths = ExtractPathsFromBlob(blob.FullName); }
                    catch { continue; }

                    foreach (var p in paths)
                    {
                        if (!seen.Add(p)) continue;
                        candidates.Add(new KeyValuePair<DateTime, string>(
                            blob.LastWriteTimeUtc, p));
                    }
                }
            }

            // Source C — XP fallback. Only useful when the modern paths above turn up
            // nothing (true XP install, no AutomaticDestinations folder).
            if (candidates.Count == 0)
            {
                string profileRecent = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Recent");
                foreach (var path in CollectFromRecentDocsRegistry())
                {
                    if (!seen.Add(path)) continue;
                    DateTime ts;
                    try { ts = File.GetLastWriteTimeUtc(path); }
                    catch { ts = DateTime.MinValue; }
                    candidates.Add(new KeyValuePair<DateTime, string>(ts, path));
                }
            }

            return candidates
                .OrderByDescending(kv => kv.Key)
                .Take(max)
                .Select(kv => kv.Value)
                .ToList();
        }

        public static void Refresh()
        {
            lock (_initLock)
            {
                _filesByExe = null;
            }
        }

        private static readonly string[] EmptyArray = new string[0];
        private static readonly IList<string> EmptyList = EmptyArray;

        private static void EnsureInitialized()
        {
            if (_filesByExe != null) return;

            lock (_initLock)
            {
                if (_filesByExe != null) return;
                _filesByExe = BuildIndex();
            }
        }

        private static Dictionary<string, List<string>> BuildIndex()
        {
            var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var seenPerExe = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var path in CollectRecentPaths())
            {
                string handlerExe = ExtensionHandlerCache.GetHandlerExe(Path.GetExtension(path));
                if (string.IsNullOrEmpty(handlerExe))
                    continue;

                string key = NormalizeExePath(handlerExe);

                List<string> list;
                if (!result.TryGetValue(key, out list))
                {
                    list = new List<string>();
                    result[key] = list;
                    seenPerExe[key] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }

                if (seenPerExe[key].Add(path))
                    list.Add(path);
            }

            return result;
        }

        private static IEnumerable<string> CollectRecentPaths()
        {
            string adFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Microsoft\\Windows\\Recent\\AutomaticDestinations");

            if (Directory.Exists(adFolder))
            {
                foreach (var path in CollectFromAutomaticDestinations(adFolder))
                    yield return path;
            }
            else
            {
                foreach (var path in CollectFromRecentDocsRegistry())
                    yield return path;
            }
        }

        private static IEnumerable<string> CollectFromAutomaticDestinations(string folder)
        {
            string[] files;
            try
            {
                files = Directory.GetFiles(folder, "*.automaticDestinations-ms");
            }
            catch
            {
                yield break;
            }

            foreach (var file in files)
            {
                List<string> paths;
                try
                {
                    paths = ExtractPathsFromBlob(file);
                }
                catch
                {
                    continue; // skip unreadable / corrupt files
                }

                foreach (var p in paths)
                    yield return p;
            }
        }

        // Scans the binary for null-terminated path-like strings in both ASCII and
        // UTF-16 encodings. Verifies each candidate exists on disk before yielding it.
        private static List<string> ExtractPathsFromBlob(string filePath)
        {
            byte[] data = File.ReadAllBytes(filePath);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var results = new List<string>();

            // ASCII: <drive letter><':'><'\'>
            for (int i = 0; i + 3 <= data.Length; i++)
            {
                if (!IsAsciiDriveLetter(data[i])) continue;
                if (data[i + 1] != (byte)':' || data[i + 2] != (byte)'\\') continue;

                int end = i;
                while (end < data.Length && data[end] != 0)
                    end++;

                int len = end - i;
                if (len > 3 && len < 1024)
                {
                    string path = Encoding.ASCII.GetString(data, i, len);
                    if (LooksLikePath(path) && File.Exists(path) && seen.Add(path))
                        results.Add(path);
                }

                i = end; // skip past this candidate
            }

            // UTF-16 LE: <drive letter><0><':'><0><'\'><0>
            for (int i = 0; i + 6 <= data.Length; i++)
            {
                if (!IsAsciiDriveLetter(data[i]) || data[i + 1] != 0) continue;
                if (data[i + 2] != (byte)':' || data[i + 3] != 0) continue;
                if (data[i + 4] != (byte)'\\' || data[i + 5] != 0) continue;

                int end = i;
                while (end + 1 < data.Length && !(data[end] == 0 && data[end + 1] == 0))
                    end += 2;

                int len = end - i;
                if (len > 6 && len < 2048)
                {
                    string path = Encoding.Unicode.GetString(data, i, len);
                    if (LooksLikePath(path) && File.Exists(path) && seen.Add(path))
                        results.Add(path);
                }

                i = end;
            }

            return results;
        }

        private static bool IsAsciiDriveLetter(byte b)
        {
            return (b >= (byte)'A' && b <= (byte)'Z') || (b >= (byte)'a' && b <= (byte)'z');
        }

        private static bool LooksLikePath(string s)
        {
            // Reject anything with null bytes, control chars, or wildcards. Real paths
            // from .lnk streams are clean strings ending in a filename + extension.
            if (s.Length < 4) return false;
            if (s.IndexOfAny(InvalidPathChars) >= 0) return false;
            return s.IndexOf('.') > 0; // require an extension
        }

        private static readonly char[] InvalidPathChars =
            { '\0', '<', '>', '"', '|', '?', '*' };

        // XP path: walk RecentDocs subkeys, resolve referenced .lnk files in
        // %USERPROFILE%\Recent to their targets. Best-effort; OpenSaveMRU is intentionally
        // skipped — its XP layout is plain strings (handled here as the same shape) but
        // the Vista+ OpenSavePidlMRU stores PIDLs we can't resolve without IShellFolder.
        private static IEnumerable<string> CollectFromRecentDocsRegistry()
        {
            string recentFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Recent");

            using (var rootKey = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\RecentDocs"))
            {
                if (rootKey == null) yield break;

                foreach (var subName in rootKey.GetSubKeyNames())
                {
                    using (var sub = rootKey.OpenSubKey(subName))
                    {
                        if (sub == null) continue;

                        foreach (var lnkName in EnumerateMruValues(sub))
                        {
                            string lnkPath = Path.Combine(recentFolder, lnkName);
                            if (!File.Exists(lnkPath)) continue;

                            string target = ShortcutResolver.ResolveTarget(lnkPath);
                            if (!string.IsNullOrEmpty(target) && File.Exists(target))
                                yield return target;
                        }
                    }
                }
            }
        }

        // Each RecentDocs subkey has either an MRUListEx (DWORD-indexed, with values
        // whose names are the indexes) or the older MRUList (byte-indexed). The values
        // contain UTF-16 .lnk filenames (terminated by a double-null), followed by
        // implementation-specific binary tail we don't need.
        private static IEnumerable<string> EnumerateMruValues(RegistryKey key)
        {
            byte[] mruEx = key.GetValue("MRUListEx") as byte[];
            if (mruEx != null)
            {
                for (int i = 0; i + 4 <= mruEx.Length; i += 4)
                {
                    int idx = BitConverter.ToInt32(mruEx, i);
                    if (idx < 0) break;

                    var raw = key.GetValue(idx.ToString()) as byte[];
                    if (raw == null) continue;

                    string lnk = ExtractLeadingUtf16String(raw);
                    if (!string.IsNullOrEmpty(lnk))
                        yield return lnk;
                }
            }
        }

        private static string ExtractLeadingUtf16String(byte[] raw)
        {
            // Find the first UTF-16 LE null terminator (two zero bytes on an even index).
            int end = 0;
            while (end + 1 < raw.Length && !(raw[end] == 0 && raw[end + 1] == 0))
                end += 2;

            if (end <= 0) return null;
            return Encoding.Unicode.GetString(raw, 0, end);
        }

        private static string NormalizeExePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            try { return Path.GetFullPath(path).TrimEnd('\\'); }
            catch { return path; }
        }
    }
}
