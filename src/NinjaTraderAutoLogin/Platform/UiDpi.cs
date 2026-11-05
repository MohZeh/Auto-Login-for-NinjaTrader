using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace NinjaTraderAutoLogin
{
    internal static class UiDpi
    {
        [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
        [DllImport("user32.dll")] private static extern int GetSystemMetricsForDpi(int index, uint dpi);
        public static int Native(Control control)
        {
            if (!control.IsHandleCreated)
            {
                return 0;
            }

            try
            {
                return (int)GetDpiForWindow(control.Handle);
            }
            catch (EntryPointNotFoundException) { return 0; }
        }
        public static int Select(int native, int managed)
        {
            if (native >= 72 && native <= 768)
            {
                return native;
            }

            return managed >= 72 && managed <= 768 ? managed : 96;
        }
        public static int Get(Control control)
        {
            return Select(Native(control), control.DeviceDpi);
        }
        public static int Scrollbar(int dpi)
        {
            try
            {
                return Math.Max(1, GetSystemMetricsForDpi(2, (uint)dpi));
            }
            catch (EntryPointNotFoundException) { return SystemInformation.VerticalScrollBarWidth; }
        }
        // Framework 4.8's AutoScroll uses SystemInformation's system metric even
        // when the native bar on this window has another per-monitor DPI width.
        public static int ScrollbarReserve(int dpi)
        {
            return ScrollbarReserve(Scrollbar(dpi), SystemInformation.VerticalScrollBarWidth);
        }
        public static int ScrollbarReserve(int nativeWidth, int frameworkWidth)
        {
            return Math.Max(1, Math.Max(nativeWidth, frameworkWidth));
        }
    }
}
