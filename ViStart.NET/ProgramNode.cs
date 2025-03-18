using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace ViStart.NET
{
    /// <summary>
    /// Represents a program or folder in the TreeView
    /// </summary>
    public class ProgramNode
    {
        public string Caption { get; set; }
        public string Path { get; set; }
        public Image Icon { get; set; }
        public bool IsFolder { get; set; }
        public List<ProgramNode> Children { get; } = new List<ProgramNode>();
        public bool IsVisible { get; set; } = true;
        public string SearchableText { get; set; }
        public string Description { get; set; }
        public int Level { get; internal set; }
    }
}
