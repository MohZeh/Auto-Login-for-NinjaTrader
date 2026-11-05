using System.Drawing;
using System.Windows.Forms;

namespace NinjaTraderAutoLogin
{
    internal sealed class SettingsViewport : Panel
    {
        public SettingsViewport()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }
        public void ResetScrollbarsForLayout()
        {
            // Called with layout suspended. Old native bars must not reduce the
            // client area while the next page extent and child bounds are applied.
            AdjustFormScrollbars(false);
            AutoScrollPosition = Point.Empty;
        }
    }
}
