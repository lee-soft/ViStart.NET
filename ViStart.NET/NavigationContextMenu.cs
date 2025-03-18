using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.IO;

namespace ViStart.NET
{
    public class NavigationContextMenu : IDisposable
    {
        private readonly ContextMenuStrip menu;
        private NavigationPaneItem currentItem;
        private readonly Settings settings;
        private readonly Form parentForm;
        // Fix for first click issue
        private bool isInitialized = false;

        public NavigationContextMenu(Settings settings, Form parentForm)
        {
            this.settings = settings;
            this.parentForm = parentForm;
            menu = new ContextMenuStrip();
            menu.Renderer = new CustomMenuRenderer();
            menu.Opening += Menu_Opening;
        }

        public void Show(NavigationPaneItem item, Point location, Form owner)
        {
            currentItem = item;

            // Convert the form's client coordinates to screen coordinates
            Point screenPoint = owner.PointToScreen(location);

            // Show menu at screen position
            menu.Show(screenPoint);
        }

        private void Menu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            menu.Items.Clear();

            if (currentItem == null)
            {
                e.Cancel = true;
                return;
            }

            // Add menu items based on navigation item type
            if (currentItem.IsCustom)
            {
                // Custom items get a Rename and Remove option
                AddMenuItem("Rename", OnRenameItem);
                AddMenuItem("Remove from Navigation Pane", OnRemoveItem);
                AddSeparator();
            }

            // Standard options for all items
            AddMenuItem("Open", OnOpenItem);
            AddMenuItem("Explore", OnExploreItem);

            // Display mode options
            AddSeparator();
            AddMenuItem("Don't show option in navigation pane", OnHideItem,
                !currentItem.IsVisible);

            if (IsFolder(currentItem.Command))
            {
                string popText = currentItem.DisplayMode == "menu"
                    ? "Pop out folder contents"
                    : "Don't pop out folder contents";

                AddMenuItem(popText, OnTogglePopMode);
            }

            // Additional options for folders
            if (IsFolder(currentItem.Command))
            {
                AddSeparator();
                AddMenuItem("Show on Desktop", OnAddToDesktop);
                AddMenuItem("Copy to ViPad", OnCopyToViPad);
            }

            AddSeparator();
            AddMenuItem("Properties", OnShowProperties);
        }

        private void AddMenuItem(string text, EventHandler clickHandler, bool isChecked = false)
        {
            var item = new ToolStripMenuItem(text);
            item.Click += clickHandler;
            item.Checked = isChecked;
            menu.Items.Add(item);
        }

        private void AddSeparator()
        {
            menu.Items.Add(new ToolStripSeparator());
        }

        private bool IsFolder(string command)
        {
            // Check if the command is a folder path
            if (string.IsNullOrEmpty(command)) return false;

            // Handle shell: commands
            if (command.StartsWith("shell:"))
            {
                // Most shell: commands point to folders
                string[] nonFolders = { "shell:games" };
                return !Array.Exists(nonFolders, x => x.Equals(command, StringComparison.OrdinalIgnoreCase));
            }

            // Check if it's a regular file path
            try
            {
                return Directory.Exists(command);
            }
            catch
            {
                return false;
            }
        }

        private void OnOpenItem(object sender, EventArgs e)
        {
            ExecuteCommand(currentItem.Command);
        }

        private void OnExploreItem(object sender, EventArgs e)
        {
            // For folders, open in explorer
            ExecuteCommand("explorer.exe", currentItem.Command);
        }

        private void OnRenameItem(object sender, EventArgs e)
        {
            // Show rename dialog
            using (var dialog = new RenameDialog(currentItem.Text))
            {
                if (dialog.ShowDialog(parentForm) == DialogResult.OK)
                {
                    currentItem.Text = dialog.ItemName;
                    SaveNavigationChanges();
                }
            }
        }

        private void OnRemoveItem(object sender, EventArgs e)
        {
            if (MessageBox.Show(
                $"Remove '{currentItem.Text}' from navigation pane?",
                "Confirm Removal",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) == DialogResult.Yes)
            {
                // Remove item and save
                RemoveNavigationItem(currentItem);
                SaveNavigationChanges();
            }
        }

        private void OnHideItem(object sender, EventArgs e)
        {
            currentItem.IsVisible = !currentItem.IsVisible;
            SaveNavigationChanges();
        }

        private void OnTogglePopMode(object sender, EventArgs e)
        {
            // Toggle between menu and link mode
            currentItem.DisplayMode = (currentItem.DisplayMode == "menu") ? "link" : "menu";
            SaveNavigationChanges();
        }

        private void OnAddToDesktop(object sender, EventArgs e)
        {
            // Create shortcut on desktop
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                CreateShortcut(desktopPath, currentItem.Text, currentItem.Command);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating shortcut: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnCopyToViPad(object sender, EventArgs e)
        {
            // Add to ViPad if available
            try
            {
                // This is a placeholder - actual ViPad integration would be here
                MessageBox.Show("ViPad integration not implemented yet.", "Information",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding to ViPad: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnShowProperties(object sender, EventArgs e)
        {
            // Show properties dialog
            try
            {
                if (currentItem.Command.StartsWith("shell:"))
                {
                    // For shell locations, need to resolve to actual path
                    ResolveShellAndShowProperties(currentItem.Command);
                }
                else
                {
                    ShowFileProperties(currentItem.Command);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error showing properties: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExecuteCommand(string command, string args = null)
        {
            try
            {
                if (string.IsNullOrEmpty(args))
                {
                    System.Diagnostics.Process.Start(command);
                }
                else
                {
                    System.Diagnostics.Process.Start(command, args);
                }

                // Close the start menu
                parentForm?.Hide();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error executing command: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveNavigationChanges()
        {
            // Update XML and save settings
            // This would update settings.NavigationPaneXml with the changes
            // and persist to storage

            // For demo purposes, just request redraw
            parentForm?.Invalidate();
        }

        private void RemoveNavigationItem(NavigationPaneItem item)
        {
            // This would remove the item from the navigation pane collection
            // The actual implementation depends on how your NavigationPane stores items
        }

        private void CreateShortcut(string targetDir, string shortcutName, string targetPath)
        {
            // Create a shortcut file
            // This requires Windows Script Host or IWshRuntimeLibrary

            // Simple implementation using .lnk file creation
            string shortcutPath = Path.Combine(targetDir, $"{shortcutName}.lnk");

            // This is a simplified version - a real implementation would use
            // IWshRuntimeLibrary or similar COM interop
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType != null)
            {
                dynamic shell = Activator.CreateInstance(shellType);
                var shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = targetPath;
                shortcut.Save();
            }
        }

        private void ResolveShellAndShowProperties(string shellPath)
        {
            // Convert shell: path to real path and show properties
            // This is a simplified version
            ExecuteCommand("explorer.exe", $"/select,{shellPath}");
        }

        private void ShowFileProperties(string path)
        {
            // Show file/folder properties
            ExecuteCommand("explorer.exe", $"/select,{path}");
        }

        public void Dispose()
        {
            menu?.Dispose();
        }
    }

    // Custom menu renderer for themed appearance
    public class CustomMenuRenderer : ToolStripProfessionalRenderer
    {
        public CustomMenuRenderer() : base(new CustomColorTable())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            // Use Segoe UI for menu text
            if (e.Item.Font.FontFamily.Name != "Segoe UI")
            {
                e.TextFont = new Font("Segoe UI", 9F);
            }
            base.OnRenderItemText(e);
        }
    }

    // Custom colors for the context menu
    public class CustomColorTable : ProfessionalColorTable
    {
        public override Color MenuItemSelected => Color.FromArgb(51, 153, 255);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(51, 153, 255);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(51, 153, 255);
        public override Color MenuItemBorder => Color.FromArgb(51, 153, 255);
        public override Color MenuBorder => Color.FromArgb(204, 206, 219);
    }

    // Dialog for renaming navigation items
    public class RenameDialog : Form
    {
        private TextBox textBox;
        private Button okButton;
        private Button cancelButton;

        public string ItemName { get; private set; }

        public RenameDialog(string currentName)
        {
            ItemName = currentName;
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            this.Text = "Rename Item";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Size = new Size(300, 150);
            this.ShowInTaskbar = false;

            Label label = new Label
            {
                Text = "Enter new name:",
                Location = new Point(10, 20),
                AutoSize = true
            };

            textBox = new TextBox
            {
                Text = ItemName,
                Location = new Point(10, 45),
                Width = 260,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            okButton = new Button
            {
                Text = "OK",
                Location = new Point(110, 80),
                DialogResult = DialogResult.OK
            };

            cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(195, 80),
                DialogResult = DialogResult.Cancel
            };

            okButton.Click += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(textBox.Text))
                {
                    ItemName = textBox.Text;
                    this.DialogResult = DialogResult.OK;
                }
                else
                {
                    MessageBox.Show("Name cannot be empty.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            this.Controls.Add(label);
            this.Controls.Add(textBox);
            this.Controls.Add(okButton);
            this.Controls.Add(cancelButton);

            this.AcceptButton = okButton;
            this.CancelButton = cancelButton;
        }
    }
}