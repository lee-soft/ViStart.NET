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
            // Without Manual the very first Show ignores Location and lets Windows pick a
            // default spot, so the search box appears in the wrong place on first menu open.
            this.StartPosition = FormStartPosition.Manual;

            textBox = new TextBox();
            textBox.BorderStyle = BorderStyle.None;
            textBox.Font = new Font("Segoe UI", 9f);
            textBox.ForeColor = Color.Black;
            textBox.BackColor = Color.White;
            textBox.Dock = DockStyle.Fill;

            // EM_SETCUEBANNER renders the placeholder in gray whenever the text is empty,
            // including while focused — matches the original VB6 ViStart behaviour.
            textBox.HandleCreated += TextBox_HandleCreated;
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

        private void TextBox_HandleCreated(object sender, EventArgs e)
        {
            User32.SendMessage(textBox.Handle, User32.EM_SETCUEBANNER, (IntPtr)1, placeholderText);
        }

        private void TextBox_TextChanged(object sender, EventArgs e)
        {
            changeTimer.Stop();
            changeTimer.Start();
        }

        private void ChangeTimer_Tick(object sender, EventArgs e)
        {
            changeTimer.Stop();
            SearchTextChanged?.Invoke(this, new SearchTextChangedEventArgs(textBox.Text));
        }

        public void FocusTextBox()
        {
            this.Show();
            // Activate brings this form to the foreground so SetFocus on the child
            // textbox actually directs keystrokes here. Without it, calling Focus()
            // on a non-active form gives the textbox a caret but no keyboard input.
            this.Activate();
            textBox.Focus();
        }

        public void ClearText()
        {
            // Cue banner takes care of the placeholder rendering; we just clear real text.
            textBox.Text = string.Empty;
        }
    }
}
