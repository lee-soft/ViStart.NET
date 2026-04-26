using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ViStart.Data;

namespace ViStart.Core
{
    public static class StartMenuIndexer
    {
        private static ProgramNode rootNode;
        private static bool isIndexed = false;

        public static void Index()
        {
            if (isIndexed)
                return;

            rootNode = new ProgramNode(LanguageManager.T("all_programs", "All Programs"), "", true, 0);
            rootNode.IsExpanded = true;

            // Index common start menu locations
            string commonStartMenu = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);
            string userStartMenu = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);

            // Merge both into the tree
            if (Directory.Exists(Path.Combine(commonStartMenu, "Programs")))
                IndexFolder(Path.Combine(commonStartMenu, "Programs"), rootNode);

            if (Directory.Exists(Path.Combine(userStartMenu, "Programs")))
                IndexFolder(Path.Combine(userStartMenu, "Programs"), rootNode);

            // Sort children alphabetically
            SortNode(rootNode);

            isIndexed = true;
        }

        private static void IndexFolder(string folderPath, ProgramNode parentNode)
        {
            if (!Directory.Exists(folderPath))
                return;

            try
            {
                // Get all subdirectories
                var directories = Directory.GetDirectories(folderPath);
                foreach (var dir in directories)
                {
                    string folderName = Path.GetFileName(dir);

                    // Skip hidden/system folders
                    var dirInfo = new DirectoryInfo(dir);
                    if ((dirInfo.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                        continue;

                    var folderNode = new ProgramNode(folderName, dir, true);
                    parentNode.AddChild(folderNode);

                    System.Diagnostics.Debug.WriteLine($"Added folder: {folderName} with {Directory.GetFiles(dir, "*.lnk").Length} shortcuts");

                    // Recursively index subfolder
                    IndexFolder(dir, folderNode);
                }

                // Get all .lnk files in this folder. Store the .lnk path as Path
                // (not the resolved target) so Icon.ExtractAssociatedIcon honours the
                // shortcut's IconLocation, and Process.Start on the .lnk works for
                // launching. We still resolve once just to filter out broken shortcuts.
                var files = Directory.GetFiles(folderPath, "*.lnk");
                foreach (var file in files)
                {
                    try
                    {
                        string caption = Path.GetFileNameWithoutExtension(file);
                        string target = ResolveShortcut(file);

                        if (!string.IsNullOrEmpty(target) && File.Exists(target))
                        {
                            var programNode = new ProgramNode(caption, file, false);
                            parentNode.AddChild(programNode);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static void SortNode(ProgramNode node)
        {
            if (node.Children.Count == 0)
                return;

            // Sort: folders first, then programs, both alphabetically
            node.Children = node.Children
                .OrderByDescending(n => n.IsFolder)
                .ThenBy(n => n.Caption)
                .ToList();

            // Recursively sort children
            foreach (var child in node.Children)
            {
                SortNode(child);
            }
        }

        private static string ResolveShortcut(string shortcutPath)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                object shell = Activator.CreateInstance(shellType);

                object shortcut = shellType.InvokeMember("CreateShortcut",
                    System.Reflection.BindingFlags.InvokeMethod,
                    null, shell, new object[] { shortcutPath });

                object targetPath = shortcut.GetType().InvokeMember("TargetPath",
                    System.Reflection.BindingFlags.GetProperty,
                    null, shortcut, null);

                string result = targetPath as string;

                System.Runtime.InteropServices.Marshal.ReleaseComObject(shortcut);
                System.Runtime.InteropServices.Marshal.ReleaseComObject(shell);

                return result;
            }
            catch
            {
                return null;
            }
        }

        public static ProgramNode GetRootNode()
        {
            if (!isIndexed)
                Index();

            return rootNode;
        }

        public static List<ProgramNode> Search(string query)
        {
            if (!isIndexed)
                Index();

            if (string.IsNullOrWhiteSpace(query))
                return new List<ProgramNode>();

            query = query.ToLowerInvariant();
            var results = new List<ProgramNode>();

            SearchNode(rootNode, query, results);

            return results.Take(20).ToList();
        }

        private static void SearchNode(ProgramNode node, string query, List<ProgramNode> results)
        {
            if (!node.IsFolder && node.Caption.ToLowerInvariant().Contains(query))
            {
                results.Add(node);
            }

            foreach (var child in node.Children)
            {
                SearchNode(child, query, results);
            }
        }

        public static void Refresh()
        {
            isIndexed = false;
            rootNode = null;
            Index();
        }
    }
}