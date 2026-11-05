using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NinjaTraderAutoLogin
{
    // Keep RadioButton's grouping, keyboard and accessibility semantics; own only
    // the visual so checked has a solid center and unchecked stays an empty ring.
    internal sealed class FilledRadioButton : RadioButton
    {
        private bool wrapText;
        private string badge;
        public string Badge
        {
            get
            {
                return badge;
            }
            set
            {
                if (badge == value)
                {
                    return;
                }

                badge = value;
                Invalidate();
            }
        }
        public bool WrapText
        {
            get
            {
                return wrapText;
            }
            set
            {
                if (wrapText == value)
                {
                    return;
                }

                wrapText = value;
                Invalidate();
            }
        }
        public FilledRadioButton()
        {
            FlatStyle = FlatStyle.Standard;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }
        protected override void OnCheckedChanged(EventArgs e)
        {
            base.OnCheckedChanged(e);
            Invalidate();
        }
        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Invalidate();
        }
        protected override void OnEnter(EventArgs e)
        {
            base.OnEnter(e);
            Invalidate();
        }
        protected override void OnLeave(EventArgs e)
        {
            base.OnLeave(e);
            Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            float scale = UiDpi.Get(this) / 96F, diameter = 13 * scale, margin = 2 * scale, gap = 7 * scale;
            bool rtl = RightToLeft == RightToLeft.Yes;
            var circle = new RectangleF(rtl ? Width - margin - diameter : margin, (Height - diameter) / 2F, diameter, diameter);
            Color ink = Enabled ? ForeColor : UiAssets.Muted;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var border = new Pen(Checked ? ink : UiAssets.Muted, 1.2F * scale))
            {
                e.Graphics.DrawEllipse(border, circle);
            }

            if (Checked)
            {
                RectangleF center = circle;
                center.Inflate(-2 * scale, -2 * scale);
                using (var fill = new SolidBrush(ink))
                {
                    e.Graphics.FillEllipse(fill, center);
                }
            }
            int textLeft = rtl ? (int)Math.Ceiling(margin) : (int)Math.Ceiling(circle.Right + gap);
            int textRight = rtl ? (int)Math.Floor(circle.Left - gap) : Width - (int)Math.Ceiling(margin);
            var textBounds = new Rectangle(textLeft, 0, Math.Max(0, textRight - textLeft), Height);
            var flags = TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;
            flags |= WrapText ? TextFormatFlags.WordBreak : TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter;
            flags |= rtl ? TextFormatFlags.Right | TextFormatFlags.RightToLeft : TextFormatFlags.Left;
            if (WrapText)
            {
                Size measured = TextRenderer.MeasureText(e.Graphics, Text, Font, new Size(textBounds.Width, Height), flags);
                textBounds.Y = Math.Max(0, (Height - measured.Height) / 2);
                textBounds.Height = Height - textBounds.Y;
            }
            TextRenderer.DrawText(e.Graphics, Text, Font, textBounds, ink, flags);
            if (!String.IsNullOrEmpty(badge) && !WrapText)
            {
                DrawBadge(e.Graphics, textBounds, TextRenderer.MeasureText(e.Graphics, Text, Font, textBounds.Size, flags).Width, scale, rtl);
            }
            if (Focused && ShowFocusCues)
            {
                ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(textBounds, -1, -2), ink, BackColor);
            }
        }
        private void DrawBadge(Graphics graphics, Rectangle textBounds, int textWidth, float scale, bool rtl)
        {
            Size size = LiveBadge.Measure(graphics, scale);
            int gap = (int)Math.Round(6 * scale);
            if (textWidth + gap + size.Width > textBounds.Width)
            {
                return;
            }

            LiveBadge.Draw(graphics, new Rectangle(rtl ? textBounds.Right - textWidth - gap - size.Width : textBounds.Left + textWidth + gap, (Height - size.Height) / 2, size.Width, size.Height), scale);
        }
    }
}
