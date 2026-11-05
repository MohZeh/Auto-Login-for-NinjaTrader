using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NinjaTraderAutoLogin
{
    internal sealed class GlyphButton : Button
    {
        public bool Slash, Folder, Sun, Moon, Gear;
        public GlyphButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = UiAssets.Input;
            Cursor = Cursors.Hand;
            Text = String.Empty;
            AccessibleRole = AccessibleRole.PushButton;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float scale = Math.Min(Width, Height) / 28F;
            e.Graphics.TranslateTransform(Width / 2F, Height / 2F);
            e.Graphics.ScaleTransform(scale, scale);
            using (var pen = new Pen(Enabled ? UiAssets.Ink : UiAssets.Muted, Sun || Moon ? 1.5F : 1.7F))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                if (Gear)
                {
                    e.Graphics.DrawEllipse(pen, -6, -6, 12, 12);
                    e.Graphics.DrawEllipse(pen, -2, -2, 4, 4);
                    for (int i = 0; i < 8; i++)
                    {
                        double angle = i * Math.PI / 4;
                        e.Graphics.DrawLine(pen, (float)Math.Cos(angle) * 6, (float)Math.Sin(angle) * 6,
                            (float)Math.Cos(angle) * 9, (float)Math.Sin(angle) * 9);
                    }
                }
                else if (Sun)
                {
                    e.Graphics.DrawEllipse(pen, -3.3F, -3.3F, 6.6F, 6.6F);
                    for (int i = 0; i < 8; i++)
                    {
                        double angle = i * Math.PI / 4;
                        e.Graphics.DrawLine(pen, (float)Math.Cos(angle) * 5.5F, (float)Math.Sin(angle) * 5.5F,
                            (float)Math.Cos(angle) * 7.8F, (float)Math.Sin(angle) * 7.8F);
                    }
                }
                else if (Moon)
                {
                    using (var moon = new GraphicsPath())
                    {
                        moon.AddBezier(3, -7, -2, -8, -8, -3, -7, 3);
                        moon.AddBezier(-7, 3, -5, 10, 4, 10, 7, 4);
                        moon.AddBezier(7, 4, 1, 6, -4, 0, 3, -7);
                        moon.CloseFigure();
                        e.Graphics.DrawPath(pen, moon);
                    }
                }
                else if (Folder)
                {
                    e.Graphics.DrawLines(pen, new PointF[] { new PointF(-8, 6), new PointF(-8, -6), new PointF(-2, -6), new PointF(0, -3), new PointF(8, -3), new PointF(8, 6), new PointF(-8, 6) });
                    e.Graphics.DrawLine(pen, -8, -1, 8, -1);
                }
                else
                {
                    using (var eye = new GraphicsPath())
                    {
                        eye.AddBezier(-9, 0, -4, -7, 4, -7, 9, 0);
                        eye.AddBezier(9, 0, 4, 7, -4, 7, -9, 0);
                        e.Graphics.DrawPath(pen, eye);
                    }
                    e.Graphics.DrawEllipse(pen, -2.2F, -2.2F, 4.4F, 4.4F);
                    if (Slash)
                    {
                        e.Graphics.DrawLine(pen, -8, -7, 8, 7);
                    }
                }
            }
            e.Graphics.ResetTransform();
            if (Focused)
            {
                ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(2, 2, Width - 4, Height - 4), UiAssets.Accent, BackColor);
            }
        }
    }
}
