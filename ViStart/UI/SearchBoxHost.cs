using System;
using System.Drawing;
using System.Windows.Forms;
using ViStart.Core;
using ViStart.Native;

namespace ViStart.UI
{
    public class SearchBoxHost : Form
    {
        private TextBox textBox;
        private string placeholderText = LanguageManager.T("search_placeholder", "Search programs and files...");
        private Timer changeTimer;

        public event EventHandler<SearchTextChangedEventArgs> SearchTextChanged;

        public SearchBoxHost()
        {
            // Make this a borderless, tool window. Must be TopMost so it sits in the same
            // z-order tier as the (TopMost) StartMenu form — otherwise it renders behind
            // the menu and the textbox is unclickable.
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.BackColor = Color.White;
            this.TopMost = true;

            textBox = new TextBox();
            textBox.BorderStyle = BorderStyle.None;
            textBox.Font = new Font("Segoe UI", 9f);
            textBox.ForeColor = Color.Gray;
            textBox.Text = placeholderText;
            textBox.BackColor = Color.White;
            textBox.Dock = DockStyle.Fill;

            textBox.GotFocus += TextBox_GotFocus;
            textBox.LostFocus += TextBox_LostFocus;
            textBox.TextChanged += TextBox_TextChanged;

            this.Controls.Add(textBox);

            changeTimer = new Timer();
            changeTimer.Interval = 200;
            changeTimer.Tick += ChangeTimer_Tick;
        }

        public void SetOwner(IntPtr ownerHandle)
        {
            // Set the owner window - this prevents the search box from activating separately
            User32.SetWindowLong(this.Handle, User32.GWL_HWNDPARENT, ownerHandle.ToInt32());
        }

        protected override CreateParams CreateParams
        {
            get
            {
                // NOTE: deliberately no WS_EX_NOACTIVATE here. The search box must be
                // able to activate to receive keyboard input — without activation, the
                // textbox shows a focus caret but WM_KEYDOWN never reaches it.
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= User32.WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        private void TextBox_GotFocus(object sender, EventArgs e)
        {
            if (textBox.Text == placeholderText)
            {
                textBox.Text = string.Empty;
                textBox.ForeColor = Color.Black;
            }
        }

        private void TextBox_LostFocus(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(textBox.Text))
            {
                textBox.Text = placeholderText;
                textBox.ForeColor = Color.Gray;
            }
        }

        private void TextBox_TextChanged(object sender, EventArgs e)
        {
            changeTimer.Stop();
            changeTimer.Start();
        }

        private void ChangeTimer_Tick(object sender, EventArgs e)
        {
            changeTimer.Stop();

            string searchText = textBox.ForeColor == Color.Gray ? string.Empty : textBox.Text;
            SearchTextChanged?.Invoke(this, new SearchTextChangedEventArgs(searchText));
        }

        public void FocusTextBox()
        {
            this.Show();
            textBox.Focus();
        }

        public void ClearText()
        {
            textBox.Text = placeholderText;
            textBox.ForeColor = Color.Gray;
        }
    }
}