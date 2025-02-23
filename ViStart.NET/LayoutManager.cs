using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Xml;

namespace ViStart.NET
{
    public class LayoutManager
    {
        private Dictionary<string, LayoutElement> elements;
        public int XOffset { get; private set; }
        public int YOffset { get; private set; }

        // Quick access properties for common elements
        public LayoutElement SearchBox => GetElement("searchbox");
        public LayoutElement ProgramMenu => GetElement("programmenu");
        public LayoutElement FrequentProgramsMenu => GetElement("frequentprogramsmenu");
        public LayoutElement AllProgramsRollover => GetElement("allprograms_rollover");
        public LayoutElement AllProgramsArrow => GetElement("allprograms_arrow");
        public LayoutElement AllProgramsText => GetElement("allprograms_text");
        public LayoutElement GroupOptions => GetElement("groupoptions");
        public LayoutElement RolloverPlaceholder => GetElement("rolloverplaceholder");
        public LayoutElement ShutdownButton => GetElement("shutdown_button");
        public LayoutElement LogoffButton => GetElement("logoff_button");
        public LayoutElement ArrowButton => GetElement("arrow_button");
        public LayoutElement JumpListViewer => GetElement("jumplist_viewer");
        public LayoutElement ShutdownText => GetElement("shutdown_text");

        public bool ViOrb_FullHeight { get; private set; }
        public int GroupOptionsSeparator { get; private set; } = 35;
        public int GroupOptionsLimit { get; private set; } = 20;
        public bool EnableVisibilityLimit { get; private set; }
        public bool ForceClearType { get; private set; }

        public string FrequentProgramsSeparatorColor { get; private set; }

        public LayoutManager()
        {
            elements = new Dictionary<string, LayoutElement>();
        }

        public void LoadFromFile(string path)
        {
            var doc = new XmlDocument();
            doc.Load(path);
            ParseLayout(doc);
        }

        public void LoadFromXml(string xml)
        {
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            ParseLayout(doc);
        }

        private void ParseLayout(XmlDocument doc)
        {
            elements.Clear();

            // Get base element and offsets
            var baseElement = doc.SelectSingleNode("//startmenu_base");
            if (baseElement == null) throw new Exception("Invalid layout: missing startmenu_base element");

            XOffset = GetAttributeInt(baseElement, "x_offset", 0);
            YOffset = GetAttributeInt(baseElement, "y_offset", 0);

            // Parse all UI elements
            var uiElements = baseElement.SelectNodes(".//vielement");
            foreach (XmlNode node in uiElements)
            {
                var element = ParseElement(node);
                if (element != null)
                {
                    elements[element.Id] = element;
                }
            }

            // Parse special elements like frequentprogramsmenu
            var freqProgMenu = baseElement.SelectSingleNode(".//frequentprogramsmenu");
            if (freqProgMenu != null)
            {
                FrequentProgramsSeparatorColor = GetAttribute(freqProgMenu, "separatorcolour", "#ffffff");
            }
        }

        private LayoutElement ParseElement(XmlNode node)
        {
            var id = GetAttribute(node, "id");
            if (string.IsNullOrEmpty(id)) return null;

            var element = new LayoutElement
            {
                Id = id,
                Location = new Point(
                    GetAttributeInt(node, "x", 0) + XOffset,
                    GetAttributeInt(node, "y", 0) + YOffset
                ),
                Size = new Size(
                    GetAttributeInt(node, "width", 0),
                    GetAttributeInt(node, "height", 0)
                ),
                Visible = GetAttributeBool(node, "visible", true),
                BackColor = GetAttribute(node, "backcolour"),
                FontId = GetAttribute(node, "font")
            };

            return element;
        }

        public LayoutElement GetElement(string id)
        {
            return elements.TryGetValue(id, out var element) ? element : null;
        }

        private static string GetAttribute(XmlNode node, string name, string defaultValue = null)
        {
            var attr = node.Attributes?[name];
            return attr?.Value ?? defaultValue;
        }

        private static int GetAttributeInt(XmlNode node, string name, int defaultValue = 0)
        {
            var value = GetAttribute(node, name);
            return int.TryParse(value, out var result) ? result : defaultValue;
        }

        private static bool GetAttributeBool(XmlNode node, string name, bool defaultValue = false)
        {
            var value = GetAttribute(node, name);
            return bool.TryParse(value, out var result) ? result : defaultValue;
        }

        public Rectangle GetElementBounds(string elementId)
        {
            var element = GetElement(elementId);
            if (element == null) return Rectangle.Empty;

            return new Rectangle(element.Location, element.Size);
        }

        public bool IsPointInElement(Point point, string elementId)
        {
            return GetElementBounds(elementId).Contains(point);
        }
    }
}
