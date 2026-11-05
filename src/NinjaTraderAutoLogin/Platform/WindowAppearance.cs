using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace NinjaTraderAutoLogin
{
    internal static class NativeAppearance
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct NonClientOptions
        {
            public uint Flags, Mask;
        }
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
        [DllImport("uxtheme.dll")] private static extern int SetWindowThemeAttribute(IntPtr window, int attribute, ref NonClientOptions options, uint size);
        public static void ThemeCaption(IntPtr window)
        {
            try
            {
                int enabled = UiAssets.Palette.Dark ? 1 : 0, caption = Ref(UiAssets.Canvas), text = Ref(UiAssets.Title), border = Ref(UiAssets.Border);
                DwmSetWindowAttribute(window, 20, ref enabled, 4);
                DwmSetWindowAttribute(window, 35, ref caption, 4);
                DwmSetWindowAttribute(window, 36, ref text, 4);
                DwmSetWindowAttribute(window, 34, ref border, 4);
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }
        public static void RoundCorners(IntPtr window)
        {
            try
            {
                int round = 2; // DWMWCP_ROUND
                DwmSetWindowAttribute(window, 33, ref round, 4);
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }
        // Windows draws the title with its system font, which cannot be replaced per window.
        // The text stays set for the taskbar, Alt+Tab and screen readers; the page shows the name.
        public static void HideCaptionText(IntPtr window)
        {
            try
            {
                var options = new NonClientOptions { Flags = 3, Mask = 3 }; // WTNCA_NODRAWCAPTION | WTNCA_NODRAWICON
                SetWindowThemeAttribute(window, 1, ref options, (uint)Marshal.SizeOf(typeof(NonClientOptions)));
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }
        public static bool TintCaption(IntPtr window, Color caption, Color text, Color border)
        {
            try
            {
                int captionValue = Ref(caption), textValue = Ref(text), borderValue = Ref(border);
                DwmSetWindowAttribute(window, 34, ref borderValue, 4);
                return DwmSetWindowAttribute(window, 35, ref captionValue, 4) == 0 && DwmSetWindowAttribute(window, 36, ref textValue, 4) == 0;
            }
            catch (DllNotFoundException) { return false; }
            catch (EntryPointNotFoundException) { return false; }
        }
        private static int Ref(Color color)
        {
            return color.R | (color.G << 8) | (color.B << 16);
        }
    }
}
