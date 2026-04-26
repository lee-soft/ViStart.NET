using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace ViStart.Core
{
    public class ProgramDatabase
    {
        private static readonly string dbPath;

        static ProgramDatabase()
        {
            dbPath = Path.Combine(AppSettings.AppDataPath, "programs.json");
        }

        public List<Data.ProgramItem> PinnedPrograms { get; set; }
        public List<Data.ProgramItem> FrequentPrograms { get; set; }

        public ProgramDatabase()
        {
            PinnedPrograms = new List<Data.ProgramItem>();
            FrequentPrograms = new List<Data.ProgramItem>();
        }

        public static ProgramDatabase Load()
        {
            try
            {
                if (File.Exists(dbPath))
                {
                    string json = File.ReadAllText(dbPath);
                    var serializer = new JavaScriptSerializer();
                    return serializer.Deserialize<ProgramDatabase>(json);
                }
            }
            catch
            {
                // Fall through to return new instance
            }

            return new ProgramDatabase();
        }

        public void Save()
        {
            try
            {
                var serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(this);
                File.WriteAllText(dbPath, json);
            }
            catch
            {
                // Log error
            }
        }

        public Data.ProgramItem GetOrCreateProgram(string path, string caption = null)
        {
            // Check if already exists
            var existing = PinnedPrograms.FirstOrDefault(p => p.Path == path) ??
                          FrequentPrograms.FirstOrDefault(p => p.Path == path);

            if (existing != null)
                return existing;

            // Caption matters for shell:AppsFolder paths, where the path itself is an
            // opaque AppUserModelID — without it pinned/frequent Store apps render as AUMIDs.
            var program = new Data.ProgramItem(path, caption);
            FrequentPrograms.Add(program);
            return program;
        }

        public void TogglePin(string path, string caption = null)
        {
            var program = PinnedPrograms.FirstOrDefault(p => p.Path == path);

            if (program != null)
            {
                // Unpin
                PinnedPrograms.Remove(program);
                program.IsPinned = false;

                if (program.OpenCount > 0)
                {
                    FrequentPrograms.Add(program);
                    SortFrequentPrograms();
                }
            }
            else
            {
                // Pin
                program = FrequentPrograms.FirstOrDefault(p => p.Path == path);

                if (program != null)
                {
                    FrequentPrograms.Remove(program);
                }
                else
                {
                    program = new Data.ProgramItem(path, caption);
                }

                program.IsPinned = true;
                PinnedPrograms.Add(program);
            }

            Save();
        }

        public void UpdateProgramUsage(string path, string caption = null)
        {
            var program = GetOrCreateProgram(path, caption);
            program.IncrementOpenCount();
            
            if (!program.IsPinned)
            {
                SortFrequentPrograms();
            }

            Save();
        }

        public void RemoveProgram(string path)
        {
            PinnedPrograms.RemoveAll(p => p.Path == path);
            FrequentPrograms.RemoveAll(p => p.Path == path);
            Save();
        }

        private void SortFrequentPrograms()
        {
            FrequentPrograms = FrequentPrograms
                .OrderByDescending(p => p.OpenCount)
                .ToList();
        }

        public IEnumerable<Data.ProgramItem> GetTopFrequentPrograms(int count)
        {
            return FrequentPrograms.Take(count);
        }
    }
}
