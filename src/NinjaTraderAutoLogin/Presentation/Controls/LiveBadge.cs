using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NinjaTraderAutoLogin
{
    internal static class LiveBadge
    {
        public const string Text = "LIVE";
        private const TextFormatFlags Flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;

        private static Font Font
        {
            get
            {
                return UiFonts.Get("en", 7.5F, FontStyle.Bold);
            }
        }
        public static Size Measure(IDeviceContext context, float scale)
        {
            Size label = TextRenderer.MeasureText(context, Text, Font, Size.Empty, TextFormatFlags.NoPadding);
            return new Size(label.Width + (int)Math.Round(10 * scale), label.Height + (int)Math.Round(3 * scale));
        }
        public static void Draw(Graphics graphics, Rectangle bounds, float scale)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = UiAssets.Rounded(bounds, 3 * scale))
            using (var fill = new SolidBrush(UiAssets.Live))
            {
                graphics.FillPath(fill, path);
            }

            TextRenderer.DrawText(graphics, Text, Font, bounds, UiAssets.LiveBadgeText, Flags);
        }
    }
}
