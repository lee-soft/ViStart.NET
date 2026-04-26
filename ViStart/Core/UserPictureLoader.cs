using System;
using System.Drawing;
using System.IO;
using System.Text;
using ViStart.Native;

namespace ViStart.Core
{
    /// <summary>
    /// Locates the current user's account picture. Mirrors VB6 ViStart's fallback
    /// chain: prefer shell32 ordinal #261 (Vista+), then the legacy %ProgramData%
    /// "User Account Pictures" location used on XP, then a couple of generic
    /// fallbacks. Returns null if no usable image is found.
    /// </summary>
    public static class UserPictureLoader
    {
        public static Image Load()
        {
            string path = FindPath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return null;

            try { return Image.FromFile(path); }
            catch { return null; }
        }

        private static string FindPath()
        {
            // 1. shell32 #261 — canonical Vista+ entry point. Returns the path the system
            //    currently uses as the user tile (handles Microsoft-account avatars and
            //    group-policy overrides). VB6 ViStart ignores the return code entirely
            //    and just trusts the buffer if the file exists, so do the same — the
            //    HRESULT semantics aren't fully consistent across Windows versions.
            try
            {
                var sb = new StringBuilder(1024);
                Shell32.GetUserTilePath(null, 0x80000000, sb, sb.Capacity);
                string p = sb.ToString();
                if (!string.IsNullOrEmpty(p) && File.Exists(p)) return p;
            }
            catch { /* missing/changed export — fall through */ }

            // 2. Win10/11 ship copies of the active tile in the user's roaming AppData.
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string accountPicturesDir = Path.Combine(roaming,
                @"Microsoft\Windows\AccountPictures");
            if (Directory.Exists(accountPicturesDir))
            {
                // The largest .accountpicture-ms file is typically the highest-resolution
                // tile. Skip the .dat metadata files. Falls back to plain PNGs/JPGs.
                try
                {
                    foreach (var pattern in new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp" })
                    {
                        var matches = Directory.GetFiles(accountPicturesDir, pattern);
                        if (matches.Length == 0) continue;
                        Array.Sort(matches, (a, b) => new FileInfo(b).Length.CompareTo(new FileInfo(a).Length));
                        return matches[0];
                    }
                }
                catch { }
            }

            // 3. Pre-Vista cache: ProgramData\Microsoft\User Account Pictures\<USERNAME>.bmp.
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string userBmp = Path.Combine(programData,
                @"Microsoft\User Account Pictures\" + Environment.UserName + ".bmp");
            if (File.Exists(userBmp)) return userBmp;

            // 4. Generic placeholders shipped with the OS, in order of preference.
            string[] generics =
            {
                Path.Combine(programData, @"Microsoft\User Account Pictures\user.png"),
                Path.Combine(programData, @"Microsoft\User Account Pictures\guest.bmp"),
                Path.Combine(programData, @"Microsoft\User Account Pictures\Guest.bmp"),
            };
            foreach (var g in generics)
                if (File.Exists(g)) return g;

            return null;
        }
    }
}
