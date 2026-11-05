using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NinjaTraderAutoLogin
{
    internal sealed class Surface : Panel
    {
        public Surface()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Width < 4 || Height < 4)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = UiAssets.Rounded(new RectangleF(1, 1, Width - 3, Height - 3), 6 * UiDpi.Get(this) / 96F))
            using (var fill = new SolidBrush(UiAssets.Surface))
            using (var pen = new Pen(UiAssets.Border))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(pen, path);
            }
        }
    }
}
