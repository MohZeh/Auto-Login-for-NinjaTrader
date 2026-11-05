using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

namespace NinjaTraderAutoLogin
{
    internal static class UiAssets
    {
        internal static Stream OpenImage(string name)
        {
            Stream resource = typeof(UiAssets).Assembly.GetManifestResourceStream("NinjaTraderAutoLogin.Images." + name);
            if (resource == null)
            {
                throw new InvalidDataException("EmbeddedImageMissing");
            }
            return resource;
        }
        public static Icon LoadIcon(string name)
        {
            using (Stream resource = OpenImage(name))
            using (var icon = new Icon(resource))
            {
                return (Icon)icon.Clone();
            }
        }
        public static Bitmap LoadBitmap(string name)
        {
            // Own the decoded pixels; no dependency on an Icon/HICON or a disposed decoder.
            using (Stream resource = OpenImage(name))
            using (Image source = Image.FromStream(resource))
            {
                return new Bitmap(source);
            }
        }
        public static GraphicsPath Rounded(RectangleF rectangle, float radius)
        {
            float d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(rectangle.X, rectangle.Y, d, d, 180, 90);
            path.AddArc(rectangle.Right - d, rectangle.Y, d, d, 270, 90);
            path.AddArc(rectangle.Right - d, rectangle.Bottom - d, d, d, 0, 90);
            path.AddArc(rectangle.X, rectangle.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
        // Native window borders stay one pixel wide, so the page continues them inside its own margin.
        public static void DrawWindowFrame(Graphics graphics, Size client, Color color, float scale)
        {
            int thickness = System.Math.Max(1, (int)System.Math.Round(1.5 * scale));
            using (var fill = new SolidBrush(color))
            {
                graphics.FillRectangle(fill, 0, 0, thickness, client.Height);
                graphics.FillRectangle(fill, client.Width - thickness, 0, thickness, client.Height);
                graphics.FillRectangle(fill, 0, client.Height - thickness, client.Width, thickness);
            }
        }
        public static UiPalette Palette = UiPalette.For("Dark");
        public static Color Canvas
        {
            get
            {
                return Palette.Canvas;
            }
        }
        public static Color Surface
        {
            get
            {
                return Palette.Surface;
            }
        }
        public static Color Input
        {
            get
            {
                return Palette.Input;
            }
        }
        public static Color Ink
        {
            get
            {
                return Palette.Ink;
            }
        }
        public static Color Title
        {
            get
            {
                return Palette.Title;
            }
        }
        public static Color Muted
        {
            get
            {
                return Palette.Muted;
            }
        }
        public static Color Accent
        {
            get
            {
                return Palette.Accent;
            }
        }
        public static Color Primary
        {
            get
            {
                return Palette.Primary;
            }
        }
        public static Color Border
        {
            get
            {
                return Palette.Border;
            }
        }
        public static Color Error
        {
            get
            {
                return Palette.Error;
            }
        }
        public static Color Simulation
        {
            get
            {
                return Palette.Simulation;
            }
        }
        public static Color Live
        {
            get
            {
                return Palette.Live;
            }
        }
        public static Color LiveBadgeText
        {
            get
            {
                return Palette.LiveBadgeText;
            }
        }
    }
}
