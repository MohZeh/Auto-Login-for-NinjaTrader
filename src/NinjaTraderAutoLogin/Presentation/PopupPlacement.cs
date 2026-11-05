using System;
using System.Drawing;

namespace NinjaTraderAutoLogin
{
    internal static class PopupPlacement
    {
        public static Rectangle InWindow(Size client, Rectangle anchor, Size preferred, int inset, bool rtl)
        {
            int pad = Math.Min(Math.Max(0, inset), Math.Max(0, (Math.Min(client.Width, client.Height) - 1) / 2));
            var container = new Rectangle(pad, pad, Math.Max(1, client.Width - pad * 2), Math.Max(1, client.Height - pad * 2));
            int width = Math.Min(preferred.Width, container.Width), height = Math.Min(preferred.Height, container.Height);
            int top = anchor.Bottom + pad;
            if (top + height > container.Bottom && anchor.Top - pad - height >= container.Top)
            {
                top = anchor.Top - pad - height;
            }
            return Constrain(new Rectangle(rtl ? anchor.Left : anchor.Right - width, top, width, height), container);
        }
        public static Rectangle Constrain(Rectangle requested, Rectangle container)
        {
            int width = Math.Min(Math.Max(1, requested.Width), Math.Max(1, container.Width));
            int height = Math.Min(Math.Max(1, requested.Height), Math.Max(1, container.Height));
            int x = Math.Max(container.Left, Math.Min(requested.X, container.Right - width));
            int y = Math.Max(container.Top, Math.Min(requested.Y, container.Bottom - height));
            return new Rectangle(x, y, width, height);
        }
    }
}
