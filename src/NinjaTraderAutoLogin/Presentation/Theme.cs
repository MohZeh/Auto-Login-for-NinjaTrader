using System.Drawing;

namespace NinjaTraderAutoLogin
{
    internal sealed class UiPalette
    {
        public readonly Color Canvas, Surface, Input, Ink, Title, Muted, Accent, Primary, Border, Error, Hover, Simulation, Live, LiveBadgeText, CaptionSimulation, CaptionLive, FrameSimulation, FrameLive;
        public readonly bool Dark;
        private UiPalette(bool dark)
        {
            Dark = dark;
            Canvas = Color.FromArgb(dark ? 27 : 243, dark ? 27 : 244, dark ? 27 : 246);
            Surface = Color.FromArgb(dark ? 45 : 255, dark ? 45 : 255, dark ? 47 : 255);
            Input = Color.FromArgb(dark ? 30 : 250, dark ? 30 : 251, dark ? 30 : 252);
            Ink = Color.FromArgb(dark ? 220 : 37, dark ? 220 : 39, dark ? 222 : 43);
            Title = Color.FromArgb(dark ? 245 : 27, dark ? 245 : 29, dark ? 245 : 33);
            Muted = Color.FromArgb(dark ? 178 : 96, dark ? 178 : 101, dark ? 183 : 110);
            Accent = Color.FromArgb(dark ? 255 : 180, dark ? 107 : 50, dark ? 53 : 0);
            Primary = Color.FromArgb(220, 57, 0);
            Border = Color.FromArgb(dark ? 78 : 204, dark ? 78 : 209, dark ? 82 : 216);
            Error = Color.FromArgb(dark ? 255 : 180, dark ? 159 : 35, dark ? 137 : 24);
            Hover = Color.FromArgb(dark ? 62 : 235, dark ? 62 : 238, dark ? 65 : 242);
            // NinjaTrader's own mode screen marks Simulation green and Live orange.
            Simulation = Color.FromArgb(dark ? 104 : 27, dark ? 199 : 122, dark ? 119 : 61);
            Live = Color.FromArgb(dark ? 227 : 180, dark ? 88 : 50, dark ? 32 : 0);
            LiveBadgeText = dark ? Color.Black : Color.White;
            // Softer than the selection colors because they cover a whole title bar.
            CaptionSimulation = Color.FromArgb(dark ? 88 : 183, dark ? 160 : 222, dark ? 118 : 194);
            CaptionLive = Color.FromArgb(dark ? 206 : 237, dark ? 120 : 177, dark ? 82 : 143);
            // The window frame must stay visible against the page, so it is stronger than the title bar.
            FrameSimulation = Color.FromArgb(dark ? 104 : 70, dark ? 199 : 150, dark ? 119 : 100);
            FrameLive = Color.FromArgb(dark ? 227 : 200, dark ? 110 : 100, dark ? 60 : 55);
        }
        public static UiPalette For(string theme)
        {
            return new UiPalette(theme != "Light");
        }
    }
}
