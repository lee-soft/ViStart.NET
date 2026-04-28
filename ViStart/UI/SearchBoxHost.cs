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

            // SetForegroundWindow / Activate need foreground privilege, which Windows
            // only grants to a process that just received user input. When the start
            // menu is summoned via the WH_KEYBOARD_LL hook (Win key), the keypress
            // is intercepted globally and never delivered to our input queue, so we
            // have no privilege and Activate silently fails — the textbox shows a
            // caret but keystrokes go to whatever app was previously focused.
            //
            // Workaround: briefly attach our input queue to the foreground thread's
            // input queue. While attached, the OS treats both threads as one input
            // context, so SetForegroundWindow on our window succeeds.
            IntPtr foreground = User32.GetForegroundWindow();
            uint foregroundThread = (foreground != IntPtr.Zero)
                ? User32.GetWindowThreadProcessId(foreground, IntPtr.Zero)
                : 0;
            uint thisThread = User32.GetCurrentThreadId();

            bool attached = false;
            if (foregroundThread != 0 && foregroundThread != thisThread)
            {
                attached = User32.AttachThreadInput(thisThread, foregroundThread, true);
            }
            try
            {
                User32.SetForegroundWindow(this.Handle);
                this.Activate();
                textBox.Focus();
            }
            finally
            {
                if (attached)
                    User32.AttachThreadInput(thisThread, foregroundThread, false);
            }
        }

        public void ClearText()
        {
            // Cue banner takes care of the placeholder rendering; we just clear real text.
            textBox.Text = string.Empty;
        }
    }
}
