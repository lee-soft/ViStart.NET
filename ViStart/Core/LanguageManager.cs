using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace ViStart.Core
{
    public static class LanguageManager
    {
        private static Dictionary<string, string> strings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static void Initialize()
        {
            strings = new Dictionary<string, string>(GetDefaultStrings(), StringComparer.OrdinalIgnoreCase);

            string langCode = AppSettings.Instance.CurrentLanguage;
            if (string.IsNullOrEmpty(langCode))
                langCode = "english";

            string langFile = FindLanguageFile(langCode);
            if (string.IsNullOrEmpty(langFile) || !File.Exists(langFile))
                return;

            try
            {
                var serializer = new JavaScriptSerializer();
                var loaded = serializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(langFile));
                if (loaded == null) return;

                foreach (var kv in loaded)
                    strings[kv.Key] = kv.Value;
            }
            catch
            {
                // keep defaults
            }
        }

        public static string T(string key, string fallback)
        {
            if (string.IsNullOrEmpty(key))
                return fallback;

            string value;
            if (strings.TryGetValue(key, out value) && !string.IsNullOrEmpty(value))
                return value;

            return fallback;
        }

        public static IEnumerable<string> GetAvailableLanguages()
        {
            var langs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string dir in GetLanguageDirectories())
            {
                if (!Directory.Exists(dir)) continue;
                foreach (string file in Directory.GetFiles(dir, "*.json", SearchOption.TopDirectoryOnly))
                    langs.Add(Path.GetFileNameWithoutExtension(file));
            }

            if (langs.Count == 0)
                langs.Add("english");

            return langs.OrderBy(l => l);
        }

        private static string FindLanguageFile(string langCode)
        {
            foreach (string dir in GetLanguageDirectories())
            {
                string json = Path.Combine(dir, langCode + ".json");
                if (File.Exists(json))
                    return json;
            }
            return null;
        }

        private static IEnumerable<string> GetLanguageDirectories()
        {
            yield return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Languages");
            yield return Path.Combine(AppSettings.AppDataPath, "_languages");
        }

        private static Dictionary<string, string> GetDefaultStrings()
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["menu.skins"] = "Skins",
                ["menu.orbs"] = "Orbs",
                ["menu.languages"] = "Language",
                ["menu.default"] = "Default",
                ["menu.exit"] = "Exit ViStart",
                ["all_programs"] = "All Programs",
                ["back"] = "Back",
                ["search_placeholder"] = "Search programs and files...",
                ["no_pinned_programs"] = "No pinned programs\n\nRight-click programs in All Programs to pin them here",
                ["pin_to_start"] = "Pin to Start Menu",
                ["unpin_from_start"] = "Unpin from Start Menu",
                ["remove_from_list"] = "Remove from list",
                ["documents"] = "Documents",
                ["pictures"] = "Pictures",
                ["music"] = "Music",
                ["videos"] = "Videos",
                ["recent"] = "Recent",
                ["computer"] = "Computer",
                ["connect_to"] = "Connect To",
                ["control_panel"] = "Control Panel",
                ["run"] = "Run...",
                ["shutdown_text"] = "Shut down",
                ["confirm_shutdown"] = "Are you sure you want to shut down?",
                ["shutdown_title"] = "Shut Down Windows",
                ["log_off"] = "Log Off",
                ["restart"] = "Restart",
                ["shut_down"] = "Shut Down",
                ["confirm_restart"] = "Are you sure you want to restart?",
                ["restart_title"] = "Restart Windows"
            };
        }
    }
}
