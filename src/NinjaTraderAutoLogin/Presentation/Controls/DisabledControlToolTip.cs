using System;
using System.Drawing;
using System.Windows.Forms;

namespace NinjaTraderAutoLogin
{
    // Disabled child windows route mouse input to their parent. Display the hint
    // from that parent without enabling the button or adding an input overlay.
    internal sealed class DisabledControlToolTip : IDisposable
    {
        private readonly Control target, parent;
        private readonly ToolTip tip = new ToolTip();
        private readonly Timer delay = new Timer();
        private string text = String.Empty;
        private bool shown, disposed;

        public DisabledControlToolTip(Control target)
        {
            this.target = target;
            parent = target.Parent ?? throw new ArgumentException("The control needs a parent.", nameof(target));
            delay.Interval = tip.InitialDelay;
            delay.Tick += ShowHint;
            parent.MouseMove += TrackPointer;
            parent.MouseLeave += Reset;
            parent.MouseDown += Dismiss;
            parent.VisibleChanged += Reset;
            parent.LocationChanged += Reset;
            parent.SizeChanged += Reset;
            target.EnabledChanged += Reset;
        }

        public string Text
        {
            set
            {
                if (text == value)
                {
                    return;
                }
                Hide();
                text = value ?? String.Empty;
            }
        }

        public void Hide()
        {
            if (disposed)
            {
                return;
            }
            delay.Stop();
            if (shown && !parent.IsDisposed)
            {
                tip.Hide(parent);
            }
            shown = false;
        }

        private bool CanShow(Point point)
        {
            return !disposed && !parent.IsDisposed && parent.Visible && !target.IsDisposed &&
                target.Visible && !target.Enabled && text.Length > 0 && target.Bounds.Contains(point);
        }

        private void TrackPointer(object sender, MouseEventArgs e)
        {
            if (!CanShow(e.Location))
            {
                Hide();
            }
            else if (!shown && !delay.Enabled)
            {
                delay.Start();
            }
        }

        private void ShowHint(object sender, EventArgs e)
        {
            delay.Stop();
            if (parent.IsDisposed || !parent.IsHandleCreated || !CanShow(parent.PointToClient(Cursor.Position)))
            {
                Hide();
                return;
            }
            Form form = parent.FindForm();
            if (form == null || !form.ContainsFocus)
            {
                return;
            }
            int gap = Math.Max(4, UiDpi.Get(parent) / 24);
            tip.Show(text, parent, target.Left, target.Bottom + gap, tip.AutoPopDelay);
            shown = true;
        }

        private void Reset(object sender, EventArgs e)
        {
            Hide();
        }

        private void Dismiss(object sender, MouseEventArgs e)
        {
            Hide();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            Hide();
            disposed = true;
            parent.MouseMove -= TrackPointer;
            parent.MouseLeave -= Reset;
            parent.MouseDown -= Dismiss;
            parent.VisibleChanged -= Reset;
            parent.LocationChanged -= Reset;
            parent.SizeChanged -= Reset;
            target.EnabledChanged -= Reset;
            delay.Tick -= ShowHint;
            delay.Dispose();
            tip.Dispose();
        }
    }
}
