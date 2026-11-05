using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NinjaTraderAutoLogin
{
    internal sealed class OptionsButton : Button
    {
        public bool Expanded
        {
            get; set;
        }
        public OptionsButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Margin = Padding.Empty;
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.ButtonDropDown;
            UseMnemonic = false;
        }
        public void SetCaption(string text, string accessibleName)
        {
            Text = text;
            AccessibleName = accessibleName;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Expanded ? UiAssets.Palette.Hover : BackColor);
            float scale = UiDpi.Get(this) / 96F;
            bool rtl = RightToLeft == RightToLeft.Yes;
            TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | (rtl ? TextFormatFlags.RightToLeft : 0);
            int textWidth = TextRenderer.MeasureText(e.Graphics, Text, Font, Size.Empty, flags).Width + 2;
            float chevronWidth = 10 * scale, chevronHeight = 5 * scale, gap = 7 * scale;
            float left = System.Math.Max(0, (Width - textWidth - gap - chevronWidth) / 2);
            float textLeft = rtl ? left + chevronWidth + gap : left, chevronLeft = rtl ? left : left + textWidth + gap;
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle((int)textLeft, 0, textWidth, Height), ForeColor, flags);
            float top = (Height - chevronHeight) / 2, tip = Expanded ? top : top + chevronHeight, ends = Expanded ? top + chevronHeight : top;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(ForeColor, 1.6F * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                e.Graphics.DrawLines(pen, new[] { new PointF(chevronLeft, ends), new PointF(chevronLeft + chevronWidth / 2, tip), new PointF(chevronLeft + chevronWidth, ends) });
            }
            if (Focused && ShowFocusCues)
            {
                ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -3, -3));
            }
        }
    }
}
