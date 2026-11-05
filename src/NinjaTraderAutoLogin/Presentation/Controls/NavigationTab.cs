using System.Drawing;
using System.Windows.Forms;

namespace NinjaTraderAutoLogin
{
    internal sealed class NavigationTab : Button
    {
        public bool Selected
        {
            get; set;
        }
        public NavigationTab()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Margin = Padding.Empty;
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.PageTab;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(0, 0, Width, Height - 4),
                Selected ? UiAssets.Ink : UiAssets.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak |
                (RightToLeft == RightToLeft.Yes ? TextFormatFlags.RightToLeft : 0));
            if (Selected)
            {
                int thickness = System.Math.Max(2, UiDpi.Get(this) / 48);
                using (var brush = new SolidBrush(UiAssets.Accent))
                {
                    e.Graphics.FillRectangle(brush, 8, Height - thickness, System.Math.Max(1, Width - 16), thickness);
                }
            }
            if (Focused && ShowFocusCues)
            {
                ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(3, 3, Width - 6, Height - 6));
            }
        }
        protected override AccessibleObject CreateAccessibilityInstance()
        {
            return new TabAccessibility(this);
        }
        protected override bool IsInputKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            return key == Keys.Left || key == Keys.Right || base.IsInputKey(keyData);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if ((e.KeyCode == Keys.Left || e.KeyCode == Keys.Right) && Parent != null)
            {
                foreach (Control control in Parent.Controls)
                {
                    var peer = control as NavigationTab;
                    if (peer != null && peer != this)
                    {
                        peer.Focus();
                        peer.PerformClick();
                        break;
                    }
                }
                e.Handled = e.SuppressKeyPress = true;
            }
            base.OnKeyDown(e);
        }
        private sealed class TabAccessibility : ControlAccessibleObject
        {
            private readonly NavigationTab tab;
            public TabAccessibility(NavigationTab owner) : base(owner) { tab = owner; }
            public override void DoDefaultAction()
            {
                tab.PerformClick();
            }
            public override AccessibleStates State
            {
                get
                {
                    return base.State | (tab.Selected ? AccessibleStates.Selected : AccessibleStates.None);
                }
            }
        }
    }
}
