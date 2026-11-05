using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NinjaTraderAutoLogin
{
    // Shows exactly the address it opens, so the visible text and the destination cannot differ.
    // Only the new-release link uses a sentence instead, and its target is checked before opening.
    internal sealed class AddressLink : Control
    {
        private Uri target;
        private bool hovered;

        public AddressLink(Uri target)
        {
            Target = target;
            RightToLeft = RightToLeft.No;
            Margin = Padding.Empty;
            Cursor = Cursors.Hand;
            TabStop = true;
            Font = UiFonts.Get("en", 9.25F, FontStyle.Underline);
            AccessibleRole = AccessibleRole.Link;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        }
        public ContentAlignment Alignment
        {
            get; set;
        } = ContentAlignment.MiddleLeft;
        public Uri Target
        {
            get
            {
                return target;
            }
            set
            {
                target = value;
                Text = Display(value);
                Invalidate();
            }
        }
        public void ShowSentence(Uri address, string sentence)
        {
            target = address;
            Text = sentence;
            Invalidate();
        }
        public static string Display(Uri address)
        {
            if (address == null)
            {
                return String.Empty;
            }

            return address.Scheme == Uri.UriSchemeMailto ? address.AbsoluteUri.Substring(Uri.UriSchemeMailto.Length + 1) : address.AbsoluteUri;
        }
        private TextFormatFlags Flags
        {
            get
            {
                TextFormatFlags side = Alignment == ContentAlignment.MiddleCenter ? TextFormatFlags.HorizontalCenter
                    : Alignment == ContentAlignment.MiddleRight ? TextFormatFlags.Right : TextFormatFlags.Left;
                // TextBoxControl also breaks a long address, so it wraps instead of being cut off.
                return side | TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding |
                    (RightToLeft == RightToLeft.Yes ? TextFormatFlags.RightToLeft : 0);
            }
        }
        public int HeightFor(IDeviceContext context, int width)
        {
            return TextRenderer.MeasureText(context, Text, Font, new Size(Math.Max(1, width), Int32.MaxValue), Flags).Height;
        }
        // Only the text reacts to the pointer, not the empty part of the row.
        private Rectangle TextArea(IDeviceContext context)
        {
            Size size = TextRenderer.MeasureText(context, Text, Font, new Size(Math.Max(1, Width), Int32.MaxValue), Flags);
            int width = Math.Min(Width, size.Width), height = Math.Min(Height, size.Height);
            int left = Alignment == ContentAlignment.MiddleCenter ? (Width - width) / 2 : Alignment == ContentAlignment.MiddleRight ? Width - width : 0;
            return new Rectangle(left, (Height - height) / 2, width, height);
        }
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x0084 && IsHandleCreated) // WM_NCHITTEST
            {
                long position = message.LParam.ToInt64();
                Point point = PointToClient(new Point((short)(position & 0xFFFF), (short)((position >> 16) & 0xFFFF)));
                using (Graphics graphics = CreateGraphics())
                {
                    if (!TextArea(graphics).Contains(point))
                    {
                        message.Result = new IntPtr(-1); // HTTRANSPARENT
                        return;
                    }
                }
            }
            base.WndProc(ref message);
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
            hovered = false;
            Invalidate();
        }
        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }
        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }
        protected override bool IsInputKey(Keys keyData)
        {
            return (keyData & Keys.KeyCode) == Keys.Enter || base.IsInputKey(keyData);
        }
        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                e.Handled = true;
                OnClick(EventArgs.Empty);
            }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(UiAssets.Surface);
            Rectangle text = ClientRectangle;
            text.Y = Math.Max(0, (Height - HeightFor(e.Graphics, Width)) / 2);
            text.Height = Height - text.Y;
            TextRenderer.DrawText(e.Graphics, Text, Font, text, hovered ? UiAssets.Primary : UiAssets.Accent, Flags);
            if (Focused && ShowFocusCues)
            {
                float scale = UiDpi.Get(this) / 96F;
                Rectangle area = TextArea(e.Graphics);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath ring = UiAssets.Rounded(new RectangleF(area.X + 0.5F, area.Y + 0.5F, Math.Max(1, area.Width - 1), Math.Max(1, area.Height - 1)), 3 * scale))
                using (var pen = new Pen(UiAssets.Accent, 1.2F * scale))
                {
                    e.Graphics.DrawPath(pen, ring);
                }
            }
        }
        protected override AccessibleObject CreateAccessibilityInstance()
        {
            return new LinkAccessibility(this);
        }
        private sealed class LinkAccessibility : ControlAccessibleObject
        {
            private readonly AddressLink link;
            public LinkAccessibility(AddressLink owner) : base(owner) { link = owner; }
            public override string DefaultAction
            {
                get
                {
                    return "Open";
                }
            }
            public override string Value
            {
                get
                {
                    return link.Text;
                }
            }
            public override void DoDefaultAction()
            {
                link.BeginInvoke((Action)delegate
                {
                    link.OnClick(EventArgs.Empty);
                });
            }
        }
    }
}
