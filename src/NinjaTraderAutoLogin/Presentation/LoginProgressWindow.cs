using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NinjaTraderAutoLogin
{
    // A small notification-style window for sign-ins started without the settings window.
    // It stays near the notification area and never takes focus.
    internal sealed class LoginProgressWindow : Form
    {
        public event Action CancelRequested;
        public readonly ActionButton CancelAction;
        private readonly bool rtl;
        private readonly Color accent, frame;
        private readonly string chipText, cancelling;
        private string stage, notice;
        private readonly Font chipFont;
        private readonly Label title, detail;
        private readonly Bitmap mark;
        private readonly Timer animation = new Timer { Interval = 40 };
        private readonly Timer closing = new Timer();
        private Rectangle icon, chip, bar;
        private int barOffset;
        private bool finished;
        private Action closed;

        public LoginProgressWindow(string language, TradingMode mode)
        {
            rtl = language == "fa";
            accent = mode == TradingMode.Live ? UiAssets.Live : UiAssets.Simulation;
            frame = mode == TradingMode.Live ? UiAssets.Palette.FrameLive : UiAssets.Palette.FrameSimulation;
            chipText = UiText.ModeName(mode, language);
            chipFont = UiFonts.Get(language, 8F, FontStyle.Bold);
            cancelling = UiText.Status("cancelling", language);
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            DoubleBuffered = true;
            BackColor = UiAssets.Canvas;
            Font = UiFonts.Get(language, 9.25F, FontStyle.Regular);
            Text = ProductInfo.Name(language);
            AccessibleRole = AccessibleRole.Dialog;
            mark = UiAssets.LoadBitmap("launch.png");
            title = AddText(UiText.ProgressTitle(language), UiFonts.Get(language, 10F, FontStyle.Bold), UiAssets.Title);
            stage = UiText.ProgressStage(language, null, mode);
            detail = AddText(stage, UiFonts.Get(language, 9F, FontStyle.Regular), UiAssets.Muted);
            CancelAction = new ActionButton
            {
                Kind = ActionKind.Secondary,
                Text = UiText.Choose(language, "Cancel", "لغو"),
                Font = Font,
                RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No
            };
            CancelAction.Click += delegate
            {
                CancelAction.Enabled = false;
                SetStage(cancelling);
                CancelRequested?.Invoke();
            };
            Controls.Add(CancelAction);
            animation.Tick += delegate
            {
                barOffset = (barOffset + 2) % 100;
                Invalidate(bar);
            };
            closing.Tick += delegate
            {
                closing.Stop();
                closed?.Invoke();
            };
            Arrange();
            animation.Start();
        }
        protected override bool ShowWithoutActivation
        {
            get
            {
                return true;
            }
        }
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= 0x08000000 | 0x00000008 | 0x00000080; // WS_EX_NOACTIVATE | WS_EX_TOPMOST | WS_EX_TOOLWINDOW
                parameters.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                return parameters;
            }
        }
        private Label AddText(string text, Font font, Color ink)
        {
            var label = new Label
            {
                Text = text,
                AutoSize = false,
                UseMnemonic = false,
                UseCompatibleTextRendering = false,
                Margin = Padding.Empty,
                Font = font,
                ForeColor = ink,
                BackColor = UiAssets.Canvas,
                RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No,
                TextAlign = ContentAlignment.TopLeft
            };
            Controls.Add(label);
            return label;
        }
        private int TextHeight(Label label, int width)
        {
            TextFormatFlags flags = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | (rtl ? TextFormatFlags.RightToLeft : 0);
            return TextRenderer.MeasureText(label.Text, label.Font, new Size(Math.Max(1, width - 6), Int32.MaxValue), flags).Height + 4;
        }
        internal void Arrange()
        {
            float scale = UiDpi.Get(this) / 96F;
            Func<int, int> px = value => (int)Math.Round(value * scale);
            int width = px(390), pad = px(14), iconSize = px(32), gap = px(12), edge = px(3);
            int cancelWidth = finished ? 0 : Math.Max(px(68), TextRenderer.MeasureText(CancelAction.Text, CancelAction.Font).Width + px(24));
            int textWidth = width - 2 * pad - iconSize - gap - (finished ? 0 : cancelWidth + gap);
            int textLeft = rtl ? pad + (finished ? 0 : cancelWidth + gap) : pad + iconSize + gap;
            icon = new Rectangle(rtl ? width - pad - iconSize : pad, px(14), iconSize, iconSize);
            Size titleSize = TextRenderer.MeasureText(title.Text, title.Font);
            int titleWidth = Math.Min(textWidth, titleSize.Width + edge);
            title.Bounds = new Rectangle(rtl ? textLeft + textWidth - titleWidth + edge : textLeft - edge, px(12), titleWidth, TextHeight(title, textWidth));
            Size chipSize = TextRenderer.MeasureText(chipText, chipFont, Size.Empty, TextFormatFlags.NoPadding);
            int chipWidth = chipSize.Width + px(12), chipHeight = chipSize.Height + px(4);
            chip = new Rectangle(rtl ? title.Left - px(6) - chipWidth : title.Right + px(6), title.Top + (title.Height - chipHeight) / 2, chipWidth, chipHeight);
            detail.Bounds = new Rectangle(textLeft - edge, title.Bottom + px(2), textWidth + 2 * edge, TextHeight(detail, textWidth));
            int contentBottom = Math.Max(detail.Bottom, icon.Bottom);
            CancelAction.Visible = !finished;
            if (!finished)
            {
                int cancelHeight = px(30);
                CancelAction.Bounds = new Rectangle(rtl ? pad : width - pad - cancelWidth, px(14) + Math.Max(0, (contentBottom - px(14) - cancelHeight) / 2), cancelWidth, cancelHeight);
                contentBottom = Math.Max(contentBottom, CancelAction.Bottom);
            }
            int height = contentBottom + px(14) + (finished ? 0 : px(3));
            bar = finished ? Rectangle.Empty : new Rectangle(0, height - px(3), width, px(3));
            ClientSize = new Size(width, height);
            Rectangle work = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(work.Right - Width - px(16), work.Bottom - Height - px(16));
            Invalidate();
        }
        public void SetStage(string text)
        {
            if (!finished && stage != text && (CancelAction.Enabled || text == cancelling))
            {
                stage = text;
                ShowDetail();
            }
        }
        public void SetNotice(string text)
        {
            if (!finished && notice != text)
            {
                notice = text;
                ShowDetail();
            }
        }
        private void ShowDetail()
        {
            detail.Text = notice == null ? stage : stage + Environment.NewLine + notice;
            Arrange();
        }
        public void Finish(string message, int visibleMilliseconds, Action whenClosed)
        {
            finished = true;
            closed = whenClosed;
            animation.Stop();
            detail.Text = message;
            Arrange();
            closing.Interval = visibleMilliseconds;
            closing.Start();
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            NativeAppearance.RoundCorners(Handle);
            NativeAppearance.TintCaption(Handle, UiAssets.Canvas, UiAssets.Ink, frame);
            Arrange();
        }
        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            Arrange();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            float scale = UiDpi.Get(this) / 96F;
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.DrawImage(mark, icon);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath shape = UiAssets.Rounded(chip, 3 * scale))
            using (var fill = new SolidBrush(accent))
            {
                e.Graphics.FillPath(fill, shape);
            }

            TextRenderer.DrawText(e.Graphics, chipText, chipFont, chip, UiAssets.LiveBadgeText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | (rtl ? TextFormatFlags.RightToLeft : 0));
            e.Graphics.SmoothingMode = SmoothingMode.None;
            using (var line = new Pen(frame))
            {
                e.Graphics.DrawRectangle(line, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
            }

            if (bar.Width > 0)
            {
                using (var track = new SolidBrush(UiAssets.Surface))
                using (var fill = new SolidBrush(accent))
                {
                    e.Graphics.FillRectangle(track, bar);
                    int runner = bar.Width / 3, position = (bar.Width + runner) * barOffset / 100 - runner;
                    if (rtl)
                    {
                        position = bar.Width - position - runner;
                    }

                    e.Graphics.FillRectangle(fill, Rectangle.Intersect(bar, new Rectangle(position, bar.Top, runner, bar.Height)));
                }
            }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                animation.Dispose();
                closing.Dispose();
                mark.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
