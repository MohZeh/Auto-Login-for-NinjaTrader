using System;
using System.Drawing;

namespace NinjaTraderAutoLogin
{
    internal sealed class WindowFit
    {
        public Rectangle Bounds;
        public Size MinimumOuter;
    }

    // Pure policy shared by actual monitor-change fitting and numerical audit.
    internal static class WindowFitPolicy
    {
        public static WindowFit Calculate(Rectangle work, Rectangle requested, Size frame, int dpi, bool center)
        {
            int margin = Math.Min(12, Math.Max(0, Math.Min(work.Width, work.Height) / 8));
            int availableWidth = Math.Max(1, work.Width - 2 * margin), availableHeight = Math.Max(1, work.Height - 2 * margin);
            int minWidth = Math.Min(availableWidth, (int)Math.Round(220 * dpi / 96.0) + frame.Width);
            int minHeight = Math.Min(availableHeight, (int)Math.Round(120 * dpi / 96.0) + frame.Height);
            int width = Math.Max(minWidth, Math.Min(Math.Max(1, requested.Width), availableWidth));
            int height = Math.Max(minHeight, Math.Min(Math.Max(1, requested.Height), availableHeight));
            int left = center ? work.Left + (work.Width - width) / 2 : Math.Max(work.Left + margin, Math.Min(requested.Left, work.Right - margin - width));
            int top = center ? work.Top + (work.Height - height) / 2 : Math.Max(work.Top + margin, Math.Min(requested.Top, work.Bottom - margin - height));
            return new WindowFit { Bounds = new Rectangle(left, top, width, height), MinimumOuter = new Size(minWidth, minHeight) };
        }
    }
}
