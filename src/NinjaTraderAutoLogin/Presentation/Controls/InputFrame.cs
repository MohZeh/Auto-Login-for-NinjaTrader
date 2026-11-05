using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NinjaTraderAutoLogin
{
    internal sealed class InputFrame : Panel
    {
        public readonly TextBox Field = new TextBox(); public readonly GlyphButton ActionButton;
        private Label savedPasswordIndicator;

        public void SetInputToolTip(ToolTip toolTip, string text)
        {
            toolTip.SetToolTip(Field, text);
            if (savedPasswordIndicator != null)
            {
                toolTip.SetToolTip(savedPasswordIndicator, text);
            }
        }

        public void ShowSavedPassword(bool visible, string description)
        {
            if (savedPasswordIndicator == null && visible)
            {
                // A decorative placeholder, never a textbox value or a password length.
                savedPasswordIndicator = new Label
                {
                    Name = "savedPasswordIndicator",
                    Text = "\u25CF\u25CF\u25CF\u25CF\u25CF\u25CF\u25CF\u25CF",
                    AutoSize = false,
                    TabStop = false,
                    UseMnemonic = false,
                    TextAlign = ContentAlignment.MiddleLeft,
                    RightToLeft = RightToLeft.No
                };
                savedPasswordIndicator.Click += delegate
                {
                    Field.Focus();
                };
                Controls.Add(savedPasswordIndicator);
            }
            if (savedPasswordIndicator == null)
            {
                return;
            }

            savedPasswordIndicator.AccessibleName = description;
            savedPasswordIndicator.Font = Field.Font;
            savedPasswordIndicator.BackColor = Field.BackColor;
            savedPasswordIndicator.ForeColor = Field.ForeColor;
            savedPasswordIndicator.Bounds = Field.Bounds;
            bool show = visible && Field.TextLength == 0;
            savedPasswordIndicator.Visible = show;
            if (show)
            {
                savedPasswordIndicator.BringToFront();
            }
        }

        public InputFrame(bool action, bool folder)
        {
            Height = 30;
            MinimumSize = new Size(90, 30);
            Margin = Padding.Empty;
            BackColor = UiAssets.Input;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Field.BorderStyle = BorderStyle.None;
            Field.BackColor = UiAssets.Input;
            Field.ForeColor = UiAssets.Ink;
            Field.RightToLeft = RightToLeft.No;
            Field.Enter += delegate
            {
                Invalidate();
            };
            Field.Leave += delegate
            {
                Invalidate();
            };
            Controls.Add(Field);
            if (action)
            {
                ActionButton = new GlyphButton { Folder = folder };
                Controls.Add(ActionButton);
            }
        }
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (Field == null)
            {
                return;
            }

            float scale = UiDpi.Get(this) / 96F;
            int pad = (int)(9 * scale), inset = Math.Max(2, (int)(4 * scale)), action = ActionButton == null ? 0 : Height - inset * 2;
            Field.SetBounds(pad, Math.Max(2, (Height - Field.PreferredHeight) / 2), Math.Max(20, Width - pad * 2 - action - inset), Field.PreferredHeight);
            if (ActionButton != null)
            {
                ActionButton.SetBounds(Width - action - inset, inset, action, Height - inset * 2);
            }
            if (savedPasswordIndicator != null)
            {
                savedPasswordIndicator.Bounds = Field.Bounds;
            }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Width < 4 || Height < 4)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = UiAssets.Rounded(new RectangleF(1, 1, Width - 3, Height - 3), 4 * UiDpi.Get(this) / 96F))
            using (var pen = new Pen(Field.Focused ? UiAssets.Accent : UiAssets.Border, Field.Focused ? 1.5F : 1F))
            {
                e.Graphics.DrawPath(pen, path);
            }
        }
    }
}
