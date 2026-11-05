using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NinjaTraderAutoLogin
{
    // A small pill that keeps Latin digits in both languages.
    internal sealed class VersionTag : Control
    {
        public VersionTag(string language, string value)
        {
            Text = value;
            Font = UiFonts.Get("en", 9F, FontStyle.Bold);
            Margin = Padding.Empty;
            TabStop = false;
            AccessibleRole = AccessibleRole.StaticText;
            RightToLeft = language == "fa" ? RightToLeft.Yes : RightToLeft.No;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            float scale = UiDpi.Get(this) / 96F;
            TextFormatFlags flags = TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix;
            Size size = TextRenderer.MeasureText(e.Graphics, Text, Font, Size.Empty, flags);
            int width = Math.Min(Width - 1, size.Width + (int)Math.Round(4 * scale));
            var pill = new Rectangle(RightToLeft == RightToLeft.Yes ? Width - width - 1 : 0, 0, width, Height - 1);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = UiAssets.Rounded(pill, Math.Min(pill.Height / 2F, 6 * scale)))
            using (var fill = new SolidBrush(UiAssets.Surface))
            using (var pen = new Pen(UiAssets.Border))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(pen, path);
            }

            TextRenderer.DrawText(e.Graphics, Text, Font, pill, UiAssets.Ink, flags);
        }
    }
}
