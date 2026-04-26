using System;
using System.Drawing;

namespace ViStart.UI
{
    public class SearchTextChangedEventArgs : EventArgs
    {
        public string SearchText { get; set; }

        public SearchTextChangedEventArgs(string searchText)
        {
            SearchText = searchText;
        }
    }

    public class SearchBox
    {
        private SearchBoxHost hostForm;

        public Rectangle Bounds { get; set; }
        public bool Visible { get; set; }

        public event EventHandler<SearchTextChangedEventArgs> SearchTextChanged;

        public SearchBox()
        {
            Visible = true;
            hostForm = new SearchBoxHost();
            hostForm.SearchTextChanged += (s, e) => SearchTextChanged?.Invoke(s, e);
        }

        public IntPtr GetHandle()
        {
            return hostForm.Handle;
        }

        public void UpdatePosition()
        {
            if (Visible)
            {
                hostForm.Location = new Point(Bounds.X, Bounds.Y);
                hostForm.Size = new Size(Bounds.Width, Bounds.Height);
                hostForm.Show();
                hostForm.BringToFront();
            }
            else
            {
                hostForm.Hide();
            }
        }

        public void Render(Graphics g)
        {
            if (!Visible)
                return;

            // Draw border around the textbox
            using (var pen = new Pen(Color.FromArgb(180, 180, 180)))
            {
                g.DrawRectangle(pen, Bounds.X, Bounds.Y, Bounds.Width - 1, Bounds.Height - 1);
            }
        }

        public bool HitTest(Point point)
        {
            return Bounds.Contains(point);
        }

        public void Focus()
        {
            hostForm.FocusTextBox();
        }

        public void SetOpacity(double opacity)
        {
            hostForm.Opacity = opacity;
        }

        public void Clear()
        {
            hostForm.ClearText();
        }

        public bool HasFocus()
        {
            return hostForm != null && (hostForm.Focused || hostForm.ContainsFocus);
        }
    }
}