using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace ViStart.Core
{
    /// <summary>
    /// Builds a reverse map from "app identity" → "extensions that app handles" by
    /// walking the registry the same way Windows does to decide what opens what.
    /// Driven entirely off the user's actual configuration so a pinned app gets the
    /// jumplist it should — no hardcoded list of "this exe handles those extensions."
    ///
    /// For each registered file extension we collect every ProgID that's associated
    /// with it across three sources:
    ///
    ///   * HKCU\...\FileExts\&lt;ext&gt;\UserChoice\ProgId  — current default handler
    ///   * HKCU\...\FileExts\&lt;ext&gt;\OpenWithList         — apps the user has
    ///                                                          actually opened files with
    ///   * HKCU\...\FileExts\&lt;ext&gt;\OpenWithProgids      — registered alternates
    ///
    /// Each ProgID resolves to one of two identities:
    ///   * AppX (UWP) ProgID → AppUserModelID at HKCR\&lt;ProgId&gt;\Application
    ///                         (e.g. "Microsoft.WindowsNotepad_8wekyb3d8bbwe!App")
    ///   * Classic ProgID    → exe path under \shell\open\command
    ///
    /// The lookup tries multiple identity forms (full path / basename / AppX AUMID)
    /// because a pinned program can be referenced any of these ways.
    /// </summary>
    internal static class UserChoiceIndex
    {
        private static Dictionary<string, List<string>> _identityToExts;
        private static readonly object _initLock = new object();

        public static IList<string> GetExtensionsFor(string identity)
        {
            EnsureInitialized();
            if (string.IsNullOrEmpty(identity)) return EmptyList;

            List<string> exts;
            return _identityToExts.TryGetValue(identity.ToLowerInvariant(), out exts)
                ? (IList<string>)exts
                : EmptyList;
        }

        public static void Refresh()
        {
            lock (_initLock) { _identityToExts = null; }
        }

        private static readonly IList<string> EmptyList = new string[0];

        private static void EnsureInitialized()
        {
            if (_identityToExts != null) return;
            lock (_initLock)
            {
                if (_identityToExts != null) return;
                _identityToExts = BuildIndex();
            }
        }

        private static Dictionary<string, List<string>> BuildIndex()
        {
            var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            using (var fileExts = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts"))
            {
                if (fileExts == null) return result;

                foreach (var ext in fileExts.GetSubKeyNames())
                {
                    if (string.IsNullOrEmpty(ext) || ext[0] != '.') continue;

                    using (var extKey = fileExts.OpenSubKey(ext))
                    {
                        if (extKey == null) continue;

                        // 1) Current default handler
                        AddProgIdValue(extKey, "UserChoice", "ProgId", ext, result);

                        // 2) Apps the user has explicitly opened files with — values
                        //    are exe basenames or AppX AUMIDs (PFN!AppID form).
                        using (var owl = extKey.OpenSubKey("OpenWithList"))
                        {
                            if (owl != null)
                            {
                                foreach (var name in owl.GetValueNames())
                                {
                                    if (name == null || name.Length != 1) continue;
                                    char c = name[0];
                                    if (c < 'a' || c > 'z') continue;
                                    string val = owl.GetValue(name) as string;
                                    AddIdentity(val, ext, result);
                                }
                            }
                        }

                        // 3) Registered alternate ProgIDs.
                        using (var owp = extKey.OpenSubKey("OpenWithProgids"))
                        {
                            if (owp != null)
                            {
                                foreach (var progId in owp.GetValueNames())
                                {
                                    AddIdentity(ResolveProgId(progId), ext, result);
                                }
                            }
                        }
                    }
                }
            }

            return result;
        }

        private static void AddProgIdValue(RegistryKey parent, string subKey,
            string valueName, string ext, Dictionary<string, List<string>> dest)
        {
            using (var k = parent.OpenSubKey(subKey))
            {
                if (k == null) return;
                string progId = k.GetValue(valueName) as string;
                AddIdentity(ResolveProgId(progId), ext, dest);
            }
        }

        private static void AddIdentity(string identity, string ext,
            Dictionary<string, List<string>> dest)
        {
            if (string.IsNullOrEmpty(identity) || string.IsNullOrEmpty(ext)) return;

            string key = identity.ToLowerInvariant();
            List<string> list;
            if (!dest.TryGetValue(key, out list))
            {
                list = new List<string>();
                dest[key] = list;
            }
            if (!list.Contains(ext, StringComparer.OrdinalIgnoreCase))
                list.Add(ext);
        }

        // ProgID → either an AUMID (for AppX/UWP) or an exe path (for classic).
        // Tries HKCU first, then HKLM — same precedence Windows applies.
        private static string ResolveProgId(string progId)
        {
            if (string.IsNullOrEmpty(progId)) return null;

            foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                using (var key = hive.OpenSubKey("SOFTWARE\\Classes\\" + progId))
                {
                    if (key == null) continue;

                    // AppX: AppUserModelID is published under \Application
                    using (var app = key.OpenSubKey("Application"))
                    {
                        if (app != null)
                        {
                            string aumid = app.GetValue("AppUserModelID") as string;
                            if (!string.IsNullOrEmpty(aumid)) return aumid;
                        }
                    }

                    // Classic: pull the exe out of the open verb's command line.
                    using (var cmd = key.OpenSubKey("shell\\open\\command"))
                    {
                        if (cmd != null)
                        {
                            string command = cmd.GetValue("") as string;
                            if (!string.IsNullOrEmpty(command))
                            {
                                string exe = ExtractExe(command);
                                if (!string.IsNullOrEmpty(exe)) return exe;
                            }
                        }
                    }
                }
            }
            return null;
        }

        // Mirrors ExtensionHandlerCache.ExtractExePath — kept private here so the
        // index doesn't take a dependency on that internal class's behaviour.
        private static string ExtractExe(string command)
        {
            command = Environment.ExpandEnvironmentVariables(command).Trim();
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
                int space = command.IndexOf(' ');
                exe = space < 0 ? command : command.Substring(0, space);
            }

            try { return Path.GetFullPath(exe); }
            catch { return null; }
        }
    }
}
