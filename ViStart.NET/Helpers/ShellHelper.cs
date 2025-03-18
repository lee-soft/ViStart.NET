using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ViStart.NET.Helpers
{
    // Helper class for shell operations (placeholder - implement actual functions)
    public static class ShellHelper
    {
        public static string GetFileDescription(string filePath)
        {
            // Implement file description extraction
            return Path.GetFileNameWithoutExtension(filePath);
        }

        public static void ShowFileProperties(string filePath)
        {
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
        }
    }
}
