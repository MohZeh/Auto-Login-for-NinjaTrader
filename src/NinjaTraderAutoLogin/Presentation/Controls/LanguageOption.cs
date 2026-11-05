using System.Drawing;
using System.Windows.Forms;

namespace NinjaTraderAutoLogin
{
    internal sealed class LanguageOption : RadioButton
    {
        public LanguageOption()
        {
            AutoSize = false;
            UseMnemonic = false;
            Margin = Padding.Empty;
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.RadioButton;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle,
                Checked ? UiAssets.Accent : UiAssets.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            if (Focused && ShowFocusCues)
            {
                ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -3, -3));
            }
        }
    }
}
