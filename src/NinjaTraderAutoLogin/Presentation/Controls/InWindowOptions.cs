using System;
using System.Drawing;
using System.Windows.Forms;

namespace NinjaTraderAutoLogin
{
    // A child of the Form is clipped by the Form; no floating native menu exists.
    internal sealed class InWindowOptions : Panel, IMessageFilter
    {
        public readonly Button Pause, Remove, Exit;
        public event EventHandler Opening;
        private readonly Form owner;
        private readonly OptionsButton trigger;
        private bool filtering, arranging;
        public InWindowOptions(Form owner, OptionsButton trigger)
        {
            this.owner = owner;
            this.trigger = trigger;
            Name = "optionsMenu";
            AutoScroll = true;
            Visible = false;
            AccessibleRole = AccessibleRole.MenuPopup;
            Pause = AddAction("pauseMonitoring");
            Remove = AddAction("removeCredentials");
            Exit = AddAction("exitHelper");
            owner.Controls.Add(this);
            owner.Deactivate += OwnerDeactivated;
            trigger.Click += Toggle;
        }
        private Button AddAction(string name)
        {
            var button = new Button
            {
                Name = name,
                FlatStyle = FlatStyle.Flat,
                UseMnemonic = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Cursor = Cursors.Hand,
                TabIndex = Controls.Count,
                Margin = Padding.Empty,
                AccessibleRole = AccessibleRole.MenuItem
            };
            button.FlatAppearance.BorderSize = 0;
            button.Click += delegate
            {
                CloseMenu(false);
            };
            Controls.Add(button);
            return button;
        }
        private void Toggle(object sender, EventArgs e)
        {
            if (Visible)
            {
                CloseMenu(true);
                return;
            }
            Opening?.Invoke(this, EventArgs.Empty);
            Reposition();
            Visible = true;
            BringToFront();
            trigger.Expanded = true;
            trigger.Invalidate();
            Application.AddMessageFilter(this);
            filtering = true;
            foreach (Control row in Controls)
            {
                if (row.Enabled)
                {
                    row.Focus();
                    break;
                }
            }
            SettingsLayoutEvidence.RecordSurface(owner, RightToLeft == RightToLeft.Yes ? "fa" : "en", "OptionsOpened");
        }
        public void CloseMenu(bool restoreFocus)
        {
            if (filtering)
            {
                Application.RemoveMessageFilter(this);
                filtering = false;
            }
            Visible = false;
            trigger.Expanded = false;
            trigger.Invalidate();
            if (restoreFocus && trigger.CanFocus)
            {
                trigger.Focus();
            }
        }
        public void ApplyPalette()
        {
            BackColor = UiAssets.Surface;
            ForeColor = UiAssets.Ink;
            foreach (Button row in Controls)
            {
                row.BackColor = UiAssets.Surface;
                row.ForeColor = UiAssets.Ink;
                row.FlatAppearance.MouseOverBackColor = UiAssets.Palette.Hover;
                row.FlatAppearance.MouseDownBackColor = UiAssets.Palette.Hover;
            }
            Invalidate(true);
        }
        public void Reposition()
        {
            if (arranging || !owner.IsHandleCreated)
            {
                return;
            }
            arranging = true;
            try
            {
                int dpi = UiDpi.Get(owner), inset = Math.Max(1, (int)Math.Round(8 * dpi / 96.0));
                int rowHeight = (int)Math.Round(32 * dpi / 96.0), gap = (int)Math.Round(6 * dpi / 96.0);
                int padding = Math.Max(2, (int)Math.Round(4 * dpi / 96.0));
                int width = (int)Math.Round(180 * dpi / 96.0);
                foreach (Button row in Controls)
                {
                    width = Math.Max(width, TextRenderer.MeasureText(row.Text, row.Font).Width + inset * 3);
                }
                Rectangle anchor = owner.RectangleToClient(trigger.RectangleToScreen(trigger.ClientRectangle));
                Bounds = PopupPlacement.InWindow(owner.ClientSize, anchor, new Size(width, rowHeight * 3 + gap * 2 + padding * 2), inset, RightToLeft == RightToLeft.Yes);
                int contentHeight = rowHeight * 3 + gap * 2 + padding * 2;
                int reserve = contentHeight > Height ? UiDpi.ScrollbarReserve(dpi) : 0;
                int top = padding;
                AutoScrollPosition = Point.Empty;
                foreach (Button row in Controls)
                {
                    row.Bounds = new Rectangle(padding, top, Math.Max(1, Width - padding * 2 - reserve), rowHeight);
                    row.Padding = new Padding(inset, 0, inset, 0);
                    top += rowHeight + gap;
                }
                AutoScrollMinSize = new Size(0, contentHeight);
            }
            finally { arranging = false; }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(UiAssets.Border))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
            }
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                CloseMenu(true);
                return true;
            }
            if (keyData == Keys.Down || keyData == Keys.Up)
            {
                SelectNextControl(owner.ActiveControl, keyData == Keys.Down, true, true, true);
                return true;
            }
            if ((keyData & Keys.KeyCode) == Keys.Tab)
            {
                CloseMenu(true);
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
        public bool PreFilterMessage(ref Message message)
        {
            if (Visible && (message.Msg == 0x0201 || message.Msg == 0x0204 || message.Msg == 0x00A1 || message.Msg == 0x00A4))
            {
                Point point = Control.MousePosition;
                if (!RectangleToScreen(ClientRectangle).Contains(point) && !trigger.RectangleToScreen(trigger.ClientRectangle).Contains(point))
                {
                    CloseMenu(false);
                }
            }
            return false;
        }
        private void OwnerDeactivated(object sender, EventArgs e)
        {
            CloseMenu(false);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (filtering)
                {
                    Application.RemoveMessageFilter(this);
                    filtering = false;
                }
                owner.Deactivate -= OwnerDeactivated;
                trigger.Click -= Toggle;
            }
            base.Dispose(disposing);
        }
    }
}
