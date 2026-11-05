using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NinjaTraderAutoLogin
{
    // A small text action on a card. Only the area around its text reacts to the mouse.
    internal sealed class LinkAction : Button
    {
        private bool hovered, ready;

        public LinkAction()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseMnemonic = false;
            UseVisualStyleBackColor = false;
            Cursor = Cursors.Hand;
            Margin = Padding.Empty;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }
        // A completed state reads as a confirmation, not as an unavailable command.
        public bool Ready
        {
            get
            {
                return ready;
            }
            set
            {
                ready = value;
                Enabled = !value;
                Invalidate();
            }
        }
        private Rectangle Pill(IDeviceContext context)
        {
            float scale = UiDpi.Get(this) / 96F;
            Size text = TextRenderer.MeasureText(context, Text, Font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            int width = Math.Min(Width, text.Width + (int)Math.Round(16 * scale));
            int height = Math.Min(Height, text.Height + (int)Math.Round(5 * scale));
            return new Rectangle(RightToLeft == RightToLeft.Yes ? 0 : Width - width, (Height - height) / 2, width, height);
        }
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x0084 && IsHandleCreated) // WM_NCHITTEST
            {
                long position = message.LParam.ToInt64();
                Point point = PointToClient(new Point((short)(position & 0xFFFF), (short)((position >> 16) & 0xFFFF)));
                using (Graphics graphics = CreateGraphics())
                {
                    if (!Pill(graphics).Contains(point))
                    {
                        message.Result = new IntPtr(-1); // HTTRANSPARENT
                        return;
                    }
                }
            }
            base.WndProc(ref message);
        }
        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            hovered = true;
            Invalidate();
        }
        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hovered = false;
            Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(UiAssets.Surface);
            float scale = UiDpi.Get(this) / 96F;
            Rectangle pill = Pill(e.Graphics);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (hovered && !ready)
            {
                using (GraphicsPath shape = UiAssets.Rounded(new RectangleF(pill.X + 0.5F, pill.Y + 0.5F, pill.Width - 1, pill.Height - 1), 4 * scale))
                using (var brush = new SolidBrush(UiAssets.Palette.Hover))
                {
                    e.Graphics.FillPath(brush, shape);
                }
            }

            TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding |
                (RightToLeft == RightToLeft.Yes ? TextFormatFlags.RightToLeft : 0);
            TextRenderer.DrawText(e.Graphics, Text, Font, pill, ready ? UiAssets.Simulation : UiAssets.Accent, flags);
            if (Focused && ShowFocusCues)
            {
                using (GraphicsPath ring = UiAssets.Rounded(new RectangleF(pill.X + 0.5F, pill.Y + 0.5F, pill.Width - 1, pill.Height - 1), 4 * scale))
                using (var pen = new Pen(UiAssets.Accent, 1.2F * scale))
                {
                    e.Graphics.DrawPath(pen, ring);
                }
            }
        }
    }
}
