using System;
using System.Collections.Generic;
using System.Drawing;

namespace NinjaTraderAutoLogin
{
    // One production owner defines geometry and hierarchy for both the Form and audit.
    // Header/status scroll with the page; commands pin only when a useful viewport fits.
    internal sealed class SettingsLayoutPlan
    {
        public const int DesignWidth = 480, DesignHeight = 562;
        public readonly Dictionary<string, Rectangle> Bounds = new Dictionary<string, Rectangle>();
        public readonly Dictionary<string, string> Parents = new Dictionary<string, string>();
        public readonly int Dpi, PageContentHeight, PageContentWidth;
        public readonly bool Compact, FooterInScroll;
        public int BodyContentHeight
        {
            get
            {
                return PageContentHeight;
            }
        }
        public SettingsLayoutPlan(int width, int height, int dpi, bool rtl, int scrollWidth)
        {
            Dpi = dpi;
            int pad = Math.Min(Px(12), Math.Max(1, Math.Min(width, height) / 8));
            int inner = Math.Max(1, width - 2 * pad), pageWidth = Math.Max(1, inner - Math.Max(0, scrollWidth));
            PageContentWidth = pageWidth;
            Compact = pageWidth < Px(420);
            int footerHeight = Px(Compact ? 114 : 34), statusHeight = Px(Compact ? 84 : 42), gap = Px(6);
            FooterInScroll = height - 2 * pad - footerHeight - statusHeight - 2 * gap < Px(120);
            int viewportHeight = Math.Max(1, height - 2 * pad - (FooterInScroll ? 0 : footerHeight + statusHeight + 2 * gap));
            Add("bodyViewport", "settings", pad, pad, inner, viewportHeight);

            int titleHeight = Compact && pageWidth < Px(280) ? 64 : 32;
            int navTop = Compact ? titleHeight + 42 : 38;
            bool stackedNavigation = pageWidth < Px(360);
            int tabLogicalHeight = stackedNavigation ? 42 : 30;
            int navigationHeight = stackedNavigation ? tabLogicalHeight + 36 : tabLogicalHeight;
            int headerLogicalHeight = navTop + navigationHeight;
            Row("brandMark", "header", 0, 1, Px(28), 28);
            Row("heading", "header", Px(38), 0, Math.Max(1, pageWidth - Px(Compact ? 38 : 164)), titleHeight);
            Row("language", "header", Compact ? 0 : pageWidth - Px(110), Compact ? titleHeight + 6 : 1, Px(74), 30);
            Add("languageFa", "language", 0, 0, Px(34), Bounds["language"].Height);
            Add("languageEn", "language", Px(40), 0, Px(34), Bounds["language"].Height);
            Row("theme", "header", Compact ? Px(82) : pageWidth - Px(28), Compact ? titleHeight + 6 : 1, Px(28), 30);
            Row("navigation", "header", 0, navTop, pageWidth, navigationHeight);
            int tabWidth = Math.Max(1, Math.Min(Px(112), (pageWidth - Px(stackedNavigation ? 6 : 108)) / 2));
            int tabHeight = Px(navTop + tabLogicalHeight) - Px(navTop);
            Add("settingsTab", "navigation", 0, 0, tabWidth, tabHeight);
            Add("aboutTab", "navigation", tabWidth + Px(6), 0, tabWidth, tabHeight);
            int optionsTop = stackedNavigation ? Px(tabLogicalHeight + 6) : 0;
            Add("options", "navigation", pageWidth - Px(96), optionsTop, Px(96), Bounds["navigation"].Height - optionsTop);
            if (rtl)
            {
                foreach (string role in new[] { "brandMark", "heading", "language", "theme" })
                {
                    Mirror(role, pageWidth);
                }
                Mirror("settingsTab", pageWidth);
                Mirror("aboutTab", pageWidth);
                Mirror("options", pageWidth);
            }
            Add("header", "bodyViewport", 0, 0, pageWidth, Px(headerLogicalHeight));
            int cardLogicalTop = headerLogicalHeight + 6, inset = Px(16), fieldWidth = Math.Max(1, pageWidth - 2 * inset);
            int labelWidth = Px(96), control = inset + labelWidth + Px(8), controlWidth = Math.Max(1, fieldWidth - labelWidth - Px(8));
            int half = (controlWidth - Px(12)) / 2, actionWidth = Px(170);
            int accountLogicalHeight = Compact ? 254 : 126;
            Row("account", "bodyViewport", 0, cardLogicalTop, pageWidth, accountLogicalHeight);
            Row("accountHeading", "account", inset, 6, Compact ? fieldWidth : fieldWidth - actionWidth - Px(8), 22);
            int behaviorLogicalTop = cardLogicalTop + accountLogicalHeight + 6, behaviorLogicalHeight = Compact ? 340 : 116;
            Row("startMethod", "bodyViewport", 0, behaviorLogicalTop, pageWidth, behaviorLogicalHeight);
            Row("startMethodHeading", "startMethod", inset, 6, Compact ? fieldWidth : fieldWidth - actionWidth - Px(8), 22);
            int applicationLogicalTop = behaviorLogicalTop + behaviorLogicalHeight + 6, applicationLogicalHeight = Compact ? 156 : 92;
            Row("application", "bodyViewport", 0, applicationLogicalTop, pageWidth, applicationLogicalHeight);
            Row("pathLabel", "application", inset, 6, fieldWidth, Compact ? 44 : 22);
            Row("pathInput", "application", inset, Compact ? 52 : 32, fieldWidth, 30);
            Row("targetStatus", "application", inset, Compact ? 84 : 64, fieldWidth, Compact ? 66 : 22);
            if (Compact)
            {
                Row("usernameLabel", "account", inset, 30, fieldWidth, 22);
                Row("usernameInput", "account", inset, 54, fieldWidth, 30);
                Row("passwordLabel", "account", inset, 88, fieldWidth, 22);
                Row("passwordInput", "account", inset, 112, fieldWidth, 30);
                Row("credentialHint", "account", inset, 146, fieldWidth, 72);
                Row("removeRejected", "account", inset, 220, fieldWidth, 30);
                Row("environmentLabel", "startMethod", inset, 30, fieldWidth, 22);
                Row("tradingModes", "startMethod", inset, 54, fieldWidth, 56);
                Row("simulationOption", "tradingModes", 0, 0, fieldWidth, 26);
                Row("liveOption", "tradingModes", 0, 30, fieldWidth, 26);
                Row("startMethodLabel", "startMethod", inset, 112, fieldWidth, 22);
                Row("shortcutOption", "startMethod", inset, 136, fieldWidth, 48);
                Row("watchOption", "startMethod", inset, 186, fieldWidth, 48);
                Row("startMethodHint", "startMethod", inset, 236, fieldWidth, 66);
                Row("createShortcut", "startMethod", inset, 304, fieldWidth, 30);
            }
            else
            {
                Row("usernameLabel", "account", inset, 32, labelWidth, 30);
                Row("usernameInput", "account", control, 32, controlWidth, 30);
                Row("passwordLabel", "account", inset, 66, labelWidth, 30);
                Row("passwordInput", "account", control, 66, controlWidth, 30);
                Row("credentialHint", "account", inset, 98, fieldWidth, 22);
                Row("removeRejected", "account", inset + fieldWidth - actionWidth, 6, actionWidth, 22);
                Row("createShortcut", "startMethod", inset + fieldWidth - actionWidth, 6, actionWidth, 22);
                Row("environmentLabel", "startMethod", inset, 32, labelWidth, 26);
                Row("tradingModes", "startMethod", control, 32, controlWidth, 26);
                Row("simulationOption", "tradingModes", 0, 0, half, 26);
                Row("liveOption", "tradingModes", half + Px(12), 0, half, 26);
                Row("startMethodLabel", "startMethod", inset, 60, labelWidth, 26);
                Row("shortcutOption", "startMethod", control, 60, half, 26);
                Row("watchOption", "startMethod", control + half + Px(12), 60, half, 26);
                Row("startMethodHint", "startMethod", inset, 88, fieldWidth, 22);
                if (rtl)
                {
                    foreach (string role in new[] { "accountHeading", "removeRejected", "usernameLabel", "usernameInput", "passwordLabel", "passwordInput", "startMethodHeading", "createShortcut", "environmentLabel", "tradingModes", "startMethodLabel", "shortcutOption", "watchOption" })
                    {
                        Mirror(role, pageWidth);
                    }
                    Mirror("simulationOption", controlWidth);
                    Mirror("liveOption", controlWidth);
                }
            }
            int statusLogicalTop = applicationLogicalTop + applicationLogicalHeight + 6, statusLogicalHeight = Compact ? 84 : 42;
            int pageBottom = Px(applicationLogicalTop + applicationLogicalHeight);
            Add("statusRow", FooterInScroll ? "bodyViewport" : "settings", FooterInScroll ? 0 : pad, FooterInScroll ? Px(statusLogicalTop) : height - pad - footerHeight - gap - statusHeight, FooterInScroll ? pageWidth : inner, statusHeight);
            Add("status", "statusRow", Px(10), 0, Math.Max(1, Bounds["statusRow"].Width - Px(110)), statusHeight);
            Add("cancelLogin", "statusRow", Bounds["statusRow"].Width - Px(92), Px(4), Px(82), Px(30));
            if (rtl)
            {
                Mirror("status", Bounds["statusRow"].Width);
                Mirror("cancelLogin", Bounds["statusRow"].Width);
            }
            int footerWidth = FooterInScroll ? pageWidth : inner;
            Add("footer", FooterInScroll ? "bodyViewport" : "settings", FooterInScroll ? 0 : pad, FooterInScroll ? Px(statusLogicalTop + statusLogicalHeight + 6) : height - pad - footerHeight, footerWidth, footerHeight);
            if (Compact)
            {
                Row("saveSettings", "footer", 0, 0, footerWidth, 34);
                Row("saveAndLaunch", "footer", 0, 40, footerWidth, 34);
                Row("closeSettings", "footer", 0, 80, footerWidth, 34);
            }
            else
            {
                int right = footerWidth;
                Footer("saveSettings", 124, ref right);
                Footer("saveAndLaunch", 124, ref right);
                Footer("closeSettings", 84, ref right);
                if (rtl)
                {
                    foreach (string role in new[] { "saveSettings", "saveAndLaunch", "closeSettings" })
                    {
                        Mirror(role, footerWidth);
                    }
                }
            }
            PageContentHeight = FooterInScroll ? Bounds["footer"].Bottom : pageBottom;
            if (!FooterInScroll)
            {
                // DPI transitions may round the window down by a pixel while
                // individual rows round up. Absorb at most two logical pixels
                // from the existing gap; preserve text rows and a four-pixel gap.
                Rectangle viewport = Bounds["bodyViewport"];
                int overflow = PageContentHeight - viewport.Height;
                int spareGap = Math.Max(0, Bounds["statusRow"].Top - viewport.Bottom - Px(4));
                if (overflow > 0 && overflow <= Math.Min(Px(2), spareGap))
                {
                    viewport.Height += overflow;
                    Bounds["bodyViewport"] = viewport;
                }
            }
        }
        public int Px(int logical)
        {
            return (int)Math.Round(logical * Dpi / 96.0);
        }
        private void Add(string role, string parent, int x, int y, int width, int height)
        {
            Bounds.Add(role, new Rectangle(x, y, width, height));
            Parents.Add(role, parent);
        }
        private void Row(string role, string parent, int x, int y, int width, int height)
        {
            Add(role, parent, x, Px(y), width, Px(y + height) - Px(y));
        }
        private void Mirror(string role, int parentWidth)
        {
            Rectangle bounds = Bounds[role];
            bounds.X = parentWidth - bounds.Right;
            Bounds[role] = bounds;
        }
        private void Footer(string role, int width, ref int right)
        {
            right -= Px(width);
            Add(role, "footer", right, 0, Px(width), Px(34));
            right -= Px(8);
        }
    }
}
