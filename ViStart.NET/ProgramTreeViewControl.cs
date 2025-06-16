using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ViStart.NET.Helpers;

namespace ViStart.NET
{
    /// <summary>
    /// Program TreeView that shows programs and supports filtering
    /// Implemented as a UserControl instead of a separate Form
    /// </summary>
    public partial class ProgramTreeViewControl : UserControl
    {
        private readonly Settings settings;
        private readonly IconManager iconManager;

        private readonly Font itemFont;
        private readonly Font folderFont;
        private readonly SolidBrush textBrush;
        private readonly SolidBrush hoverBrush;  // Added brush for hover state
        private readonly SolidBrush selectedBrush;
        private readonly SolidBrush selectedTextBrush;
        private readonly SolidBrush separatorBrush;
        private ProgramNode rootNode;
        private readonly List<ProgramNode> visibleNodes = new List<ProgramNode>();
        private string filter = string.Empty;
        private ProgramNode selectedNode;
        private ProgramNode hoveredNode;
        private int firstVisibleIndex;
        private int visibleItemsCount;
        private int itemHeight = 22;
        private int indentWidth = 20;
        private bool strictSearch = false;

        private Point lastMousePosition;
        private bool isScrolling;
        private int scrollBarWidth = 16;
        private Rectangle scrollBarBounds;
        private Rectangle thumbBounds;
        private bool isThumbDragging;
        private int thumbDragStartY;
        private int maxScrollPosition;

        // For text centering
        private readonly StringFormat textFormat;

        public Action<object, ProgramNode> ProgramClicked { get; internal set; }
        public event EventHandler RequestCloseStartMenu;

        public string Filter
        {
            get => filter;
            set
            {
                filter = value ?? string.Empty;
                UpdateVisibleNodes();
                Invalidate();
            }
        }

        public ProgramTreeViewControl(Settings settings, IconManager iconManager)
        {
            this.settings = settings;
            this.iconManager = iconManager;

            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable,
                true);

            BackColor = Color.Beige;
            itemFont = new Font("Segoe UI", 9F);
            folderFont = new Font("Segoe UI", 9F, FontStyle.Bold);
            textBrush = new SolidBrush(Color.Black);
            hoverBrush = new SolidBrush(Color.FromArgb(224, 238, 251));  // Light blue for hover
            selectedBrush = new SolidBrush(Color.FromArgb(51, 153, 255));
            selectedTextBrush = new SolidBrush(Color.White);
            separatorBrush = new SolidBrush(Color.FromArgb(210, 210, 210));

            // Setup text format for vertical centering and proper truncation
            textFormat = new StringFormat();
            textFormat.LineAlignment = StringAlignment.Center;
            textFormat.Alignment = StringAlignment.Near;
            textFormat.Trimming = StringTrimming.EllipsisCharacter;
            textFormat.FormatFlags = StringFormatFlags.NoWrap; // Prevent text wrapping

            InitializeContextMenu();
            LoadPrograms();
        }

        private void InitializeContextMenu()
        {
            var contextMenu = new ContextMenuStrip();

            var openItem = new ToolStripMenuItem("Open");
            openItem.Click += (s, e) => LaunchSelectedProgram();

            var runAsAdminItem = new ToolStripMenuItem("Run as administrator");
            runAsAdminItem.Click += (s, e) => LaunchSelectedProgram(true);

            var separator1 = new ToolStripSeparator();

            var pinItem = new ToolStripMenuItem("Pin to Start Menu");
            pinItem.Click += (s, e) => PinSelectedProgram();

            var separator2 = new ToolStripSeparator();

            var propertiesItem = new ToolStripMenuItem("Properties");
            propertiesItem.Click += (s, e) => ShowProgramProperties();

            contextMenu.Items.AddRange(new ToolStripItem[] {
                openItem, runAsAdminItem, separator1, pinItem, separator2, propertiesItem
            });

            ContextMenuStrip = contextMenu;
        }

        private void LoadPrograms()
        {
            rootNode = new ProgramNode
            {
                Caption = "Programs",
                IsFolder = true,
                IsExpanded = true // Root should start expanded
            };

            PopulateStartMenuFolders(rootNode);
            UpdateVisibleNodes();
        }

        private void PopulateStartMenuFolders(ProgramNode parentNode)
        {
            string currentUserPrograms = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");
            string commonPrograms = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs");

            // Load directly from Programs folders, not the Start Menu root
            if (Directory.Exists(currentUserPrograms))
            {
                LoadProgramsFromFolder(parentNode, currentUserPrograms);
            }

            if (Directory.Exists(commonPrograms))
            {
                LoadProgramsFromFolder(parentNode, commonPrograms);
            }

            // Sort and merge duplicates if needed
            parentNode.Children.Sort((a, b) => string.Compare(a.Caption, b.Caption, StringComparison.OrdinalIgnoreCase));
        }

        private void LoadProgramsFromFolder(ProgramNode parentNode, string folderPath)
        {
            try
            {
                // Basic validation
                if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath) || parentNode == null)
                    return;

                // Skip certain folders and load their contents directly
                string folderName = Path.GetFileName(folderPath);
                if (folderName == "Programs" || folderName == "Start Menu")
                {
                    // For top-level Programs folder, load directly to parent without creating intermediate node
                    try
                    {
                        foreach (string dir in Directory.GetDirectories(folderPath))
                        {
                            AddFolderNode(parentNode, dir);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error reading directories in {folderPath}: {ex.Message}");
                    }

                    try
                    {
                        foreach (string file in Directory.GetFiles(folderPath, "*.lnk"))
                        {
                            AddFileNode(parentNode, file);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error reading files in {folderPath}: {ex.Message}");
                    }

                    return;
                }

                // Create folder node only for actual program folders
                var folderNode = new ProgramNode
                {
                    Caption = folderName,
                    Path = folderPath,
                    IsFolder = true,
                    IsExpanded = false, // Start collapsed by default
                    Icon = iconManager?.GetFolderIcon(folderPath, false)
                };

                // Add subfolders
                try
                {
                    foreach (string dir in Directory.GetDirectories(folderPath))
                    {
                        AddFolderNode(folderNode, dir);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error reading subdirectories in {folderPath}: {ex.Message}");
                }

                // Add files
                try
                {
                    foreach (string file in Directory.GetFiles(folderPath, "*.lnk"))
                    {
                        AddFileNode(folderNode, file);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error reading files in {folderPath}: {ex.Message}");
                }

                // Only add non-empty folders
                if (folderNode.Children.Count > 0)
                {
                    parentNode.Children.Add(folderNode);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading programs from folder {folderPath}: {ex.Message}");
            }
        }

        private void AddFolderNode(ProgramNode parentNode, string folderPath)
        {
            try
            {
                // Basic validation
                if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
                    return;

                // Create folder node and process its contents
                var folderNode = new ProgramNode
                {
                    Caption = Path.GetFileName(folderPath),
                    Path = folderPath,
                    IsFolder = true,
                    IsExpanded = false, // Start collapsed by default
                    Icon = iconManager?.GetFolderIcon(folderPath, false)
                };

                // Recursively add contents
                try
                {
                    foreach (string dir in Directory.GetDirectories(folderPath))
                    {
                        AddFolderNode(folderNode, dir);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error reading subdirectories in {folderPath}: {ex.Message}");
                }

                try
                {
                    foreach (string file in Directory.GetFiles(folderPath, "*.lnk"))
                    {
                        AddFileNode(folderNode, file);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error reading files in {folderPath}: {ex.Message}");
                }

                // Only add non-empty folders
                if (folderNode.Children.Count > 0)
                {
                    parentNode.Children.Add(folderNode);

                    // Sort children safely
                    try
                    {
                        folderNode.Children.Sort((a, b) =>
                        {
                            if (a?.IsFolder == true && b?.IsFolder == false) return -1;
                            if (a?.IsFolder == false && b?.IsFolder == true) return 1;
                            return string.Compare(a?.Caption ?? "", b?.Caption ?? "", StringComparison.OrdinalIgnoreCase);
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error sorting children: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error adding folder {folderPath}: {ex.Message}");
            }
        }

        private void AddFileNode(ProgramNode parentNode, string filePath)
        {
            try
            {
                // Basic validation first
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                    return;

                string fileName = Path.GetFileNameWithoutExtension(filePath);
                if (string.IsNullOrEmpty(fileName))
                    return;

                // Skip certain file types that shouldn't appear as programs
                string extension = Path.GetExtension(filePath).ToLowerInvariant();
                if (extension != ".lnk")
                {
                    return; // Only process .lnk files
                }

                var fileNode = new ProgramNode
                {
                    Caption = fileName,
                    Path = filePath,
                    IsFolder = false,
                    Icon = GetShortcutIcon(filePath),
                    SearchableText = MakeSearchable(fileName)
                };

                // Try to get description for better search
                try
                {
                    if (!string.IsNullOrEmpty(fileNode.Description))
                    {
                        fileNode.SearchableText += " " + MakeSearchable(fileNode.Description);
                    }
                }
                catch { /* Ignore errors retrieving description */ }

                parentNode.Children.Add(fileNode);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error adding file {filePath}: {ex.Message}");
            }
        }

        private Image GetShortcutIcon(string shortcutPath)
        {
            try
            {
                // First try to get the icon from the shortcut target
                if (iconManager != null)
                {
                    // Try to get the icon from the .lnk file itself
                    var icon = iconManager.GetFileIcon(shortcutPath, false);
                    if (icon != null)
                        return icon;
                }

                // Fallback to a default program icon
                return SystemIcons.Application.ToBitmap();
            }
            catch
            {
                // Ultimate fallback
                return SystemIcons.Application.ToBitmap();
            }
        }

        private string MakeSearchable(string text)
        {
            // Convert to uppercase for case-insensitive search
            return text.ToUpperInvariant();
        }

        private void UpdateVisibleNodes()
        {
            visibleNodes.Clear();
            firstVisibleIndex = 0;

            if (string.IsNullOrEmpty(filter))
            {
                // No filter, show regular hierarchy with expand/collapse
                FlattenVisibleNodes(rootNode, 0, false);
            }
            else
            {
                // Filter nodes by search text - show all matching items regardless of expand state
                string searchText = filter.ToUpperInvariant();
                FlattenVisibleNodes(rootNode, 0, true, searchText);

                // If strict search is enabled, only show items that start with the search text
                if (strictSearch)
                {
                    visibleNodes.RemoveAll(node =>
                        !node.IsFolder &&
                        !node.SearchableText.StartsWith(searchText));
                }
            }

            // Calculate max scroll position - ensure we have a valid height
            int totalHeight = visibleNodes.Count * itemHeight;
            visibleItemsCount = Math.Max(1, Height / Math.Max(1, itemHeight)); // Prevent division by zero
            maxScrollPosition = Math.Max(0, visibleNodes.Count - visibleItemsCount);

            // Ensure firstVisibleIndex is within bounds
            firstVisibleIndex = Math.Max(0, Math.Min(firstVisibleIndex, maxScrollPosition));

            UpdateScrollBars();

            // Reset selection if current selection is no longer visible
            if (selectedNode != null && !visibleNodes.Contains(selectedNode))
            {
                selectedNode = null;
            }
        }

        private void FlattenVisibleNodes(ProgramNode node, int level, bool filtering, string searchText = null)
        {
            // Add the node itself (skip root node entirely to avoid duplication)
            if (node != rootNode)
            {
                node.Level = level;

                if (filtering)
                {
                    // When filtering, only add folders that contain matching items or files that match
                    bool folderHasMatchingItems = false;

                    if (node.IsFolder)
                    {
                        // Check if any child matches
                        folderHasMatchingItems = node.Children.Any(child =>
                            !child.IsFolder &&
                            child.SearchableText.Contains(searchText));

                        // Or if a subfolder contains matches
                        if (!folderHasMatchingItems)
                        {
                            foreach (var child in node.Children.Where(c => c.IsFolder))
                            {
                                if (FolderContainsMatch(child, searchText))
                                {
                                    folderHasMatchingItems = true;
                                    break;
                                }
                            }
                        }
                    }

                    // Add folder if it has matches or is a file that matches
                    if ((node.IsFolder && folderHasMatchingItems) ||
                        (!node.IsFolder && node.SearchableText.Contains(searchText)))
                    {
                        visibleNodes.Add(node);
                    }
                }
                else
                {
                    // Regular mode, add all nodes except root
                    visibleNodes.Add(node);
                }
            }

            // Add children recursively
            foreach (var child in node.Children)
            {
                // In regular mode, only show children if parent is expanded or we're filtering or it's the root
                if (filtering || node.IsExpanded || node == rootNode)
                {
                    FlattenVisibleNodes(child, level + (node == rootNode ? 0 : 1), filtering, searchText);
                }
            }
        }

        private bool FolderContainsMatch(ProgramNode folder, string searchText)
        {
            // Same implementation as original
            // Check direct children
            bool hasMatch = folder.Children.Any(child =>
                !child.IsFolder &&
                child.SearchableText.Contains(searchText));

            // Check subfolders
            if (!hasMatch)
            {
                foreach (var subfolder in folder.Children.Where(c => c.IsFolder))
                {
                    if (FolderContainsMatch(subfolder, searchText))
                    {
                        return true;
                    }
                }
            }

            return hasMatch;
        }

        private void UpdateScrollBars()
        {
            // Calculate scrollbar dimensions - Fix: Always use full height and position at right edge
            scrollBarBounds = new Rectangle(
                Width - scrollBarWidth,
                0,
                scrollBarWidth,
                Height);

            // Calculate thumb size and position
            int totalItems = visibleNodes.Count;
            int viewableItems = Height / itemHeight;

            if (totalItems <= viewableItems)
            {
                // No need for scrollbar
                thumbBounds = Rectangle.Empty;
                return;
            }

            // Calculate thumb height proportional to visible content
            int thumbHeight = Math.Max(20, (int)((float)viewableItems / totalItems * Height));
            int thumbPosition = (int)((float)firstVisibleIndex / Math.Max(1, totalItems - viewableItems) * (Height - thumbHeight));

            thumbBounds = new Rectangle(
                Width - scrollBarWidth,
                thumbPosition,
                scrollBarWidth,
                thumbHeight);
        }

        public void DrawToGraphics(Graphics g)
        {
            if (g == null || !Visible) return;

            // Create a clipping region to ensure we only draw within our bounds
            g.SetClip(new Rectangle(Location, Size));

            // Calculate how many items can fit in the current height
            int currentVisibleItemsCount = Height / itemHeight;

            // Draw visible items - fix the end index calculation
            int yPos = 0;
            int endIndex = Math.Min(firstVisibleIndex + currentVisibleItemsCount + 2, visibleNodes.Count); // +2 for partial items

            // Draw background
            g.FillRectangle(new SolidBrush(BackColor), Location.X, Location.Y, Width, Height);

            for (int i = firstVisibleIndex; i < endIndex && yPos < Height; i++) // Added yPos < Height check
            {
                ProgramNode node = visibleNodes[i];
                bool isSelected = node == selectedNode;
                bool isHovered = node == hoveredNode && !isSelected; // Hover only if not selected

                int availableWidth = Width - (thumbBounds.IsEmpty ? 0 : scrollBarWidth);
                Rectangle itemRect = new Rectangle(Location.X, Location.Y + yPos, availableWidth, itemHeight);

                // Only draw if the item rectangle is within the visible area
                if (itemRect.Bottom > Location.Y && itemRect.Top < Location.Y + Height)
                {
                    // Draw selection/hover background
                    if (isSelected)
                    {
                        g.FillRectangle(selectedBrush, itemRect);
                    }
                    else if (isHovered)
                    {
                        g.FillRectangle(hoverBrush, itemRect);
                    }

                    // Draw expand/collapse indicator for folders (when not filtering)
                    int expanderX = Location.X + node.Level * indentWidth + 2;
                    int expanderY = Location.Y + yPos + (itemHeight / 2) - 4; // Center vertically

                    if (node.IsFolder && string.IsNullOrEmpty(filter) && node.Children.Count > 0)
                    {
                        // Draw triangle indicator
                        Point[] triangle;
                        if (node.IsExpanded)
                        {
                            // Down-pointing triangle (expanded)
                            triangle = new Point[]
                            {
                                new Point(expanderX, expanderY),
                                new Point(expanderX + 8, expanderY),
                                new Point(expanderX + 4, expanderY + 6)
                            };
                        }
                        else
                        {
                            // Right-pointing triangle (collapsed)
                            triangle = new Point[]
                            {
                                new Point(expanderX, expanderY),
                                new Point(expanderX + 6, expanderY + 4),
                                new Point(expanderX, expanderY + 8)
                            };
                        }
                        g.FillPolygon(textBrush, triangle);
                    }

                    // Draw icon - adjust position to account for expander
                    int iconX = expanderX + (node.IsFolder && string.IsNullOrEmpty(filter) && node.Children.Count > 0 ? 12 : 0);
                    int iconY = Location.Y + yPos + 3;

                    if (node.Icon != null)
                    {
                        g.DrawImage(node.Icon, iconX, iconY, 16, 16);
                    }

                    // Draw text with proper truncation
                    Font fontToUse = node.IsFolder ? folderFont : itemFont;
                    int textX = iconX + 20; // Icon width (16) + small padding (4)
                    int textWidth = availableWidth - (textX - Location.X) - 4; // Leave some padding on the right

                    Rectangle textRect = new Rectangle(
                        textX,
                        Location.Y + yPos,
                        Math.Max(0, textWidth), // Ensure width is not negative
                        itemHeight);

                    // Only draw text if we have space for it
                    if (textRect.Width > 0)
                    {
                        g.DrawString(
                            node.Caption,
                            fontToUse,
                            isSelected ? selectedTextBrush : textBrush,
                            textRect,
                            textFormat);
                    }
                }

                yPos += itemHeight;
            }

            // Draw "no results" message if needed
            if (visibleNodes.Count == 0 && !string.IsNullOrEmpty(filter))
            {
                string noResultsText = "No programs match the search criteria";
                SizeF textSize = g.MeasureString(noResultsText, itemFont);

                g.DrawString(
                    noResultsText,
                    itemFont,
                    Brushes.Gray,
                    Location.X + (Width - textSize.Width) / 2,
                    Location.Y + (Height - textSize.Height) / 2);
            }

            // Draw scrollbar if needed - Fixed positioning
            if (!thumbBounds.IsEmpty)
            {
                // Adjust scrollbar position to account for control location
                Rectangle adjustedScrollBarBounds = new Rectangle(
                    Location.X + Width - scrollBarWidth,
                    Location.Y,
                    scrollBarWidth,
                    Height
                );

                // Recalculate thumb position to ensure it's properly sized
                int totalItems = visibleNodes.Count;
                int viewableItems = Math.Max(1, Height / itemHeight); // Ensure we don't divide by zero
                int thumbHeight = Math.Max(20, (int)((float)viewableItems / Math.Max(1, totalItems) * Height));
                int thumbPosition = totalItems > viewableItems ?
                    (int)((float)firstVisibleIndex / Math.Max(1, totalItems - viewableItems) * (Height - thumbHeight)) : 0;

                Rectangle adjustedThumbBounds = new Rectangle(
                    Location.X + Width - scrollBarWidth,
                    Location.Y + thumbPosition,
                    scrollBarWidth,
                    thumbHeight
                );

                // Draw scrollbar background
                g.FillRectangle(SystemBrushes.Control, adjustedScrollBarBounds);

                // Draw scrollbar thumb
                g.FillRectangle(SystemBrushes.ControlDark, adjustedThumbBounds);

                // Optional: Draw scrollbar border
                g.DrawRectangle(SystemPens.ControlDarkDark,
                    adjustedScrollBarBounds.X, adjustedScrollBarBounds.Y,
                    adjustedScrollBarBounds.Width - 1, adjustedScrollBarBounds.Height - 1);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            //base.OnPaint(e);
            //DrawToGraphics(e.Graphics);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            UpdateScrollBars();
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button == MouseButtons.Left)
            {
                if (scrollBarBounds.Contains(e.Location) && !thumbBounds.IsEmpty)
                {
                    // Clicked on scrollbar
                    isScrolling = true;

                    if (thumbBounds.Contains(e.Location))
                    {
                        // Clicked on thumb - start dragging
                        isThumbDragging = true;
                        thumbDragStartY = e.Y - thumbBounds.Y;
                    }
                    else
                    {
                        // Clicked on track - page up/down
                        if (e.Y < thumbBounds.Y)
                        {
                            // Page up
                            ScrollToIndex(Math.Max(0, firstVisibleIndex - visibleItemsCount));
                        }
                        else
                        {
                            // Page down
                            ScrollToIndex(Math.Min(maxScrollPosition, firstVisibleIndex + visibleItemsCount));
                        }
                    }
                }
                else
                {
                    // Clicked on an item
                    int index = firstVisibleIndex + e.Y / itemHeight;
                    if (index >= 0 && index < visibleNodes.Count)
                    {
                        var clickedNode = visibleNodes[index];
                        selectedNode = clickedNode;

                        // Handle folder expand/collapse
                        if (clickedNode.IsFolder && string.IsNullOrEmpty(filter))
                        {
                            // Toggle expansion state
                            clickedNode.IsExpanded = !clickedNode.IsExpanded;
                            UpdateVisibleNodes();
                        }
                        // Double click to launch program
                        else if (e.Clicks == 2 && !clickedNode.IsFolder)
                        {
                            LaunchSelectedProgram();
                        }

                        Invalidate();
                    }
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            isScrolling = false;
            isThumbDragging = false;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            lastMousePosition = e.Location;

            // Update hovered node
            ProgramNode oldHoveredNode = hoveredNode;
            hoveredNode = null;

            if (!isThumbDragging)
            {
                int index = firstVisibleIndex + e.Y / itemHeight;
                if (index >= 0 && index < visibleNodes.Count)
                {
                    hoveredNode = visibleNodes[index];
                }
            }

            // Only redraw if hover state changed
            if (oldHoveredNode != hoveredNode)
            {
                Invalidate();
            }

            // Handle thumb dragging
            if (isThumbDragging)
            {
                // Update thumb position
                int newThumbY = e.Y - thumbDragStartY;
                int maxThumbY = Height - thumbBounds.Height;

                newThumbY = Math.Max(0, Math.Min(newThumbY, maxThumbY));

                // Calculate new scroll position
                int newIndex = (int)((float)newThumbY / maxThumbY * maxScrollPosition);
                ScrollToIndex(newIndex);
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

            // Scroll by wheel
            int linesToScroll = SystemInformation.MouseWheelScrollLines;
            int scrollAmount = e.Delta > 0 ? -linesToScroll : linesToScroll;

            ScrollToIndex(firstVisibleIndex + scrollAmount);
        }

        private void ScrollToIndex(int index)
        {
            int oldIndex = firstVisibleIndex;
            firstVisibleIndex = Math.Max(0, Math.Min(index, maxScrollPosition));

            if (oldIndex != firstVisibleIndex)
            {
                UpdateScrollBars();
                Invalidate();
            }
        }

        public void SelectFirstVisibleItem()
        {
            if (visibleNodes.Count > 0)
            {
                selectedNode = visibleNodes[0];
                Invalidate();
            }
        }

        public void SelectLastItem()
        {
            if (visibleNodes.Count > 0)
            {
                selectedNode = visibleNodes[visibleNodes.Count - 1];
                ScrollToIndex(maxScrollPosition);
                Invalidate();
            }
        }

        public void SelectNextItem()
        {
            if (selectedNode == null || visibleNodes.Count == 0)
            {
                SelectFirstVisibleItem();
                return;
            }

            int index = visibleNodes.IndexOf(selectedNode);
            if (index < visibleNodes.Count - 1)
            {
                index++;
                selectedNode = visibleNodes[index];

                // Auto-scroll if needed
                if (index >= firstVisibleIndex + visibleItemsCount)
                {
                    ScrollToIndex(index - visibleItemsCount + 1);
                }

                Invalidate();
            }
        }

        public void SelectPreviousItem()
        {
            if (selectedNode == null || visibleNodes.Count == 0)
            {
                SelectLastItem();
                return;
            }

            int index = visibleNodes.IndexOf(selectedNode);
            if (index > 0)
            {
                index--;
                selectedNode = visibleNodes[index];

                // Auto-scroll if needed
                if (index < firstVisibleIndex)
                {
                    ScrollToIndex(index);
                }

                Invalidate();
            }
        }

        public void CollapseAllAndResetSearch()
        {
            strictSearch = false;
            filter = string.Empty;
            CollapseAllFolders(rootNode);
            UpdateVisibleNodes();
            Invalidate();
        }

        private void CollapseAllFolders(ProgramNode node)
        {
            if (node.IsFolder && node != rootNode)
            {
                node.IsExpanded = false;
            }

            foreach (var child in node.Children)
            {
                CollapseAllFolders(child);
            }
        }

        private void LaunchSelectedProgram(bool asAdmin = false)
        {
            if (selectedNode == null || selectedNode.IsFolder) return;

            try
            {
                if (asAdmin)
                {
                    var processInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = selectedNode.Path,
                        Verb = "runas" // Run as administrator
                    };
                    System.Diagnostics.Process.Start(processInfo);
                }
                else
                {
                    System.Diagnostics.Process.Start(selectedNode.Path);
                }

                // Notify that program was clicked
                ProgramClicked?.Invoke(this, selectedNode);

                // Close start menu
                RequestCloseStartMenu?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error launching program: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void PinSelectedProgram()
        {
            if (selectedNode == null || selectedNode.IsFolder) return;

            // Add to pinned programs (you'll need to implement this)
            // settings.Programs.TogglePin_ElseAddToPin_ByProgram(selectedNode.Path);
            MessageBox.Show($"Program '{selectedNode.Caption}' pinned to Start Menu",
                "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowProgramProperties()
        {
            if (selectedNode == null) return;

            try
            {
                ShellHelper.ShowFileProperties(selectedNode.Path);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error showing properties: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CleanupNodeIcons(ProgramNode node)
        {
            node.Icon?.Dispose();

            foreach (var child in node.Children)
            {
                CleanupNodeIcons(child);
            }
        }

        public void ProcessMouseDown(Point location, MouseButtons button, int clicks)
        {
            OnMouseDown(new MouseEventArgs(button, clicks, location.X, location.Y, 0));
        }

        public void ProcessMouseUp(Point location, MouseButtons button)
        {
            OnMouseUp(new MouseEventArgs(button, 1, location.X, location.Y, 0));
        }

        public void ProcessMouseMove(Point location)
        {
            OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, location.X, location.Y, 0));
        }

        public void ProcessMouseWheel(Point location, int delta)
        {
            OnMouseWheel(new MouseEventArgs(MouseButtons.None, 0, location.X, location.Y, delta));
        }

        private void ProgramTreeViewControl_Load(object sender, EventArgs e)
        {

        }
    }
}