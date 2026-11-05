using System;
using System.Drawing;
using System.Windows.Forms;

namespace NinjaTraderAutoLogin
{
    // Native message boxes ignore the theme, font and language direction of this application.
    internal sealed class ConfirmationDialog : Form
    {
        public readonly ActionButton CancelAction, ConfirmAction;
        private readonly string language;
        private readonly bool rtl;
        private readonly Label title, lead, watchNote, untested;
        private readonly Surface watchCard;
        private readonly Panel host;
        private Rectangle band, badge;
        private readonly bool hasWatchNote, live;
        private bool captionTinted;

        public ConfirmationDialog(string language, LiveConfirmation kind, string version = null)
            : this(language, ConfirmationContent.For(language, kind, version))
        {
        }
        public ConfirmationDialog(string language, ConfirmationContent content)
        {
            this.language = language;
            rtl = language == "fa";
            live = content.Live;
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = UiAssets.Canvas;
            ForeColor = UiAssets.Ink;
            Font = UiFonts.Get(language, 9.25F, FontStyle.Regular);
            Text = ProductInfo.Name(language);
            AccessibleRole = AccessibleRole.Dialog;
            AccessibleName = content.Title;
            AccessibleDescription = content.Lead;

            host = new Panel { Margin = Padding.Empty, BackColor = UiAssets.Canvas, RightToLeft = RightToLeft.No };
            host.Paint += PaintHeader;
            Controls.Add(host);
            title = AddText(host, content.Title, 13F, FontStyle.Bold, UiAssets.Title, UiAssets.Canvas);
            lead = AddText(host, content.Lead, 10F, FontStyle.Regular, UiAssets.Ink, UiAssets.Canvas);
            hasWatchNote = content.WatchNote != null;
            watchCard = new Surface { Visible = hasWatchNote };
            host.Controls.Add(watchCard);
            watchNote = AddText(watchCard, content.WatchNote ?? String.Empty, 9.25F, FontStyle.Regular, UiAssets.Ink, UiAssets.Surface);
            untested = AddText(host, content.Untested ?? String.Empty, 9F, FontStyle.Regular, UiAssets.Muted, UiAssets.Canvas);
            CancelAction = AddAction(content.Cancel, DialogResult.No, ActionKind.Secondary);
            ConfirmAction = AddAction(content.Confirm, DialogResult.Yes, live ? ActionKind.Caution : ActionKind.Primary);
            AcceptButton = CancelAction;
            CancelButton = CancelAction;
            ActiveControl = CancelAction;
            Arrange();
        }
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                if (rtl)
                {
                    // Mirror only the title bar; the host keeps its own explicit layout and text direction.
                    parameters.ExStyle |= 0x00400000 | 0x00100000 | 0x00002000; // WS_EX_LAYOUTRTL | WS_EX_NOINHERITLAYOUT | WS_EX_RTLREADING
                }

                return parameters;
            }
        }
        private Label AddText(Control parent, string text, float size, FontStyle style, Color ink, Color background)
        {
            var label = new Label
            {
                Text = text,
                AutoSize = false,
                UseMnemonic = false,
                UseCompatibleTextRendering = false,
                Margin = Padding.Empty,
                Font = UiFonts.Get(language, size, style),
                ForeColor = ink,
                BackColor = background,
                RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No,
                TextAlign = ContentAlignment.TopLeft
            };
            parent.Controls.Add(label);
            return label;
        }
        private ActionButton AddAction(string text, DialogResult result, ActionKind kind)
        {
            var button = new ActionButton
            {
                Kind = kind,
                Text = text,
                DialogResult = result,
                Font = UiFonts.Get(language, 9.25F, FontStyle.Regular),
                RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No,
                TabIndex = host.Controls.Count
            };
            host.Controls.Add(button);
            return button;
        }
        private int TextHeight(Label label, int width)
        {
            TextFormatFlags flags = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | (rtl ? TextFormatFlags.RightToLeft : 0);
            // Labels wrap inside their own client area, so reserve the same padding when measuring.
            return TextRenderer.MeasureText(label.Text, label.Font, new Size(Math.Max(1, width - 6), Int32.MaxValue), flags).Height + 4;
        }
        internal void Arrange()
        {
            float scale = UiDpi.Get(this) / 96F;
            Func<int, int> px = value => (int)Math.Round(value * scale);
            int stripe = captionTinted ? 0 : px(4), pad = px(24), width = px(440), content = width - 2 * pad, top = stripe + px(20);
            band = new Rectangle(0, 0, width, stripe);
            Size badgeSize = Size.Empty;
            if (live)
            {
                using (Graphics graphics = IsHandleCreated ? CreateGraphics() : Graphics.FromHwnd(IntPtr.Zero))
                {
                    badgeSize = LiveBadge.Measure(graphics, scale);
                }
            }

            int badgeRoom = live ? badgeSize.Width + px(10) : 0;
            int titleWidth = content - badgeRoom, titleHeight = Math.Max(TextHeight(title, titleWidth), badgeSize.Height);
            int titleLeft = rtl ? pad : pad + badgeRoom;
            badge = new Rectangle(rtl ? width - pad - badgeSize.Width : pad, top + (titleHeight - badgeSize.Height) / 2, badgeSize.Width, badgeSize.Height);
            // Labels inset their text; this aligns it with the badge, card and button edges.
            int edge = px(3);
            title.Bounds = live ? new Rectangle(titleLeft, top, titleWidth, titleHeight) : new Rectangle(pad - edge, top, content + 2 * edge, titleHeight);
            top += titleHeight + px(12);
            lead.Bounds = new Rectangle(pad - edge, top, content + 2 * edge, TextHeight(lead, content));
            top = lead.Bottom + px(12);
            if (hasWatchNote)
            {
                int inset = px(12), noteWidth = content - 2 * inset, noteHeight = TextHeight(watchNote, noteWidth);
                watchCard.Bounds = new Rectangle(pad, top, content, noteHeight + 2 * inset);
                watchNote.Bounds = new Rectangle(inset, inset, noteWidth, noteHeight);
                top = watchCard.Bottom + px(12);
            }
            untested.Visible = untested.Text.Length > 0;
            if (untested.Text.Length > 0)
            {
                untested.Bounds = new Rectangle(pad - edge, top, content + 2 * edge, TextHeight(untested, content));
                top = untested.Bottom;
            }
            top += px(untested.Text.Length > 0 ? 20 : 8);
            int height = px(34), gap = px(8);
            int cancelWidth = Math.Max(px(96), TextRenderer.MeasureText(CancelAction.Text, CancelAction.Font).Width + px(36));
            int confirmWidth = Math.Max(px(96), TextRenderer.MeasureText(ConfirmAction.Text, ConfirmAction.Font).Width + px(36));
            int confirmLeft = rtl ? pad : width - pad - confirmWidth;
            int cancelLeft = rtl ? confirmLeft + confirmWidth + gap : confirmLeft - gap - cancelWidth;
            ConfirmAction.Bounds = new Rectangle(confirmLeft, top, confirmWidth, height);
            CancelAction.Bounds = new Rectangle(cancelLeft, top, cancelWidth, height);
            ClientSize = new Size(width, top + height + pad);
            host.Bounds = new Rectangle(Point.Empty, ClientSize);
            host.Invalidate();
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            NativeAppearance.ThemeCaption(Handle);
            // Older Windows builds cannot color the title bar, so they keep the stripe instead.
            captionTinted = live ? NativeAppearance.TintCaption(Handle, UiAssets.Palette.CaptionLive, UiAssets.Ink, UiAssets.Palette.FrameLive)
                : NativeAppearance.TintCaption(Handle, UiAssets.Surface, UiAssets.Ink, UiAssets.Border);
            NativeAppearance.HideCaptionText(Handle);
            Arrange();
        }
        protected override void OnLoad(EventArgs e)
        {
            Arrange();
            base.OnLoad(e);
        }
        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            Arrange();
        }
        private void PaintHeader(object sender, PaintEventArgs e)
        {
            float scale = UiDpi.Get(this) / 96F;
            if (!live)
            {
                UiAssets.DrawWindowFrame(e.Graphics, host.ClientSize, UiAssets.Border, scale);
                return;
            }

            using (var fill = new SolidBrush(UiAssets.Live))
            {
                e.Graphics.FillRectangle(fill, band);
            }

            UiAssets.DrawWindowFrame(e.Graphics, host.ClientSize, UiAssets.Palette.FrameLive, scale);
            LiveBadge.Draw(e.Graphics, badge, scale);
        }
    }
}
