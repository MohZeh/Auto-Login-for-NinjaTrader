using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NinjaTraderAutoLogin
{
    internal enum ActionKind
    {
        Primary, Secondary, Caution
    }

    // One command style for the whole application. Buttons sit on the page canvas.
    internal sealed class ActionButton : Button
    {
        private bool hovered, pressed;

        public ActionKind Kind
        {
            get; set;
        }
        // A button on a card clears its rounded corners with the card color instead of the page color.
        public bool OnCard
        {
            get; set;
        }
        public ActionButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseMnemonic = false;
            UseVisualStyleBackColor = false;
            Cursor = Cursors.Hand;
            Margin = Padding.Empty;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }
        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            hovered = true;
            Invalidate();
        }
        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hovered = pressed = false;
            Invalidate();
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            pressed = true;
            Invalidate();
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            pressed = false;
            Invalidate();
        }
        private Color Fill()
        {
            if (!Enabled)
            {
                return UiAssets.Surface;
            }

            switch (Kind)
            {
                case ActionKind.Primary:
                    return pressed ? Color.FromArgb(170, 40, 0) : hovered ? Color.FromArgb(195, 48, 0) : UiAssets.Primary;
                case ActionKind.Caution:
                    return pressed || hovered ? ControlPaint.Dark(UiAssets.Live, 0.08F) : UiAssets.Live;
                default:
                    return hovered || pressed ? UiAssets.Palette.Hover : UiAssets.Surface;
            }
        }
        private Color Ink()
        {
            return !Enabled ? UiAssets.Muted : Kind == ActionKind.Primary ? Color.White : Kind == ActionKind.Caution ? UiAssets.LiveBadgeText : UiAssets.Ink;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(OnCard ? UiAssets.Surface : UiAssets.Canvas);
            float scale = UiDpi.Get(this) / 96F;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath shape = UiAssets.Rounded(new RectangleF(0.5F, 0.5F, Width - 1.5F, Height - 1.5F), 5 * scale))
            {
                using (var brush = new SolidBrush(Fill()))
                {
                    e.Graphics.FillPath(brush, shape);
                }

                if (Kind == ActionKind.Secondary || !Enabled)
                {
                    using (var pen = new Pen(UiAssets.Border))
                    {
                        e.Graphics.DrawPath(pen, shape);
                    }
                }
            }

            TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix |
                (RightToLeft == RightToLeft.Yes ? TextFormatFlags.RightToLeft : 0);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Ink(), flags);
            if (Focused && ShowFocusCues)
            {
                float inset = 2 * scale;
                using (GraphicsPath ring = UiAssets.Rounded(new RectangleF(inset, inset, Width - 1 - 2 * inset, Height - 1 - 2 * inset), 4 * scale))
                using (var pen = new Pen(Kind == ActionKind.Primary || Kind == ActionKind.Caution ? Ink() : UiAssets.Accent, 1.2F * scale))
                {
                    e.Graphics.DrawPath(pen, ring);
                }
            }
        }
    }
}
