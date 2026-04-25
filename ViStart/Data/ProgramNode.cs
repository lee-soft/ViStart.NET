using System;
using System.Collections.Generic;
using System.Drawing;

namespace ViStart.Data
{
    public class ProgramNode
    {
        public string Caption { get; set; }
        public string Path { get; set; }
        public bool IsFolder { get; set; }
        public bool IsExpanded { get; set; }
        public int Level { get; set; }
        public List<ProgramNode> Children { get; set; }
        public ProgramNode Parent { get; set; }

        public ProgramNode()
        {
            Children = new List<ProgramNode>();
            IsExpanded = false;
            Level = 0;
        }

        public ProgramNode(string caption, string path, bool isFolder, int level = 0)
        {
            Caption = caption;
            Path = path;
            IsFolder = isFolder;
            Level = level;
            IsExpanded = false;
            Children = new List<ProgramNode>();
        }

        public Icon GetIcon(bool largeIcon = false)
        {
            if (IsFolder)
            {
                return Core.IconCache.GetFolderIcon(largeIcon);
            }
            else
            {
                return Core.IconCache.GetIcon(Path, largeIcon);
            }
        }

        public void Toggle()
        {
            if (IsFolder)
            {
                IsExpanded = !IsExpanded;
            }
        }

        public List<ProgramNode> GetVisibleNodes()
        {
            var visible = new List<ProgramNode>();
            visible.Add(this);

            if (IsFolder && IsExpanded)
            {
                foreach (var child in Children)
                {
                    visible.AddRange(child.GetVisibleNodes());
                }
            }

            return visible;
        }

        public void AddChild(ProgramNode child)
        {
            child.Parent = this;
            child.Level = this.Level + 1;
            Children.Add(child);
        }
    }
}