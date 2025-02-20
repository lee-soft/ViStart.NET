using System;
using System.Drawing;
using System.Xml;
using System.Collections.Generic;

namespace ViStart.NET
{
    public class LayoutElement
    {
        public string Id { get; set; }
        public Point Location { get; set; }
        public Size Size { get; set; }
        public bool Visible { get; set; } = true;
        public string BackColor { get; set; }
        public string FontId { get; set; }
    }
}
